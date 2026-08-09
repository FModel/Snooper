using System.Numerics;
using OpenTK.Graphics.OpenGL4;
using Snooper.Core;
using Snooper.Core.Containers;
using Snooper.Core.Containers.Buffers;
using Snooper.Core.Containers.Programs;
using Snooper.Core.Containers.Resources;
using Snooper.Rendering.Components;
using Snooper.Rendering.Components.Camera;
using Snooper.Rendering.Components.Mesh;

namespace Snooper.Rendering.Systems;

/// <summary>
/// TODO: gpu driven animation update + morph targets
/// </summary>
public unsafe struct PerMeshSkinningData
{
    public uint BaseBone; // offset of this mesh's bones in the inverse bind buffer
    public fixed uint LOD_BaseBoneInfluence[Settings.MaxNumberOfLods];
    public readonly uint Pad0, Pad1, Pad2;

    public PerMeshSkinningData()
    {
        for (var i = 0; i < Settings.MaxNumberOfLods; i++)
        {
            LOD_BaseBoneInfluence[i] = uint.MaxValue;
        }
    }
}

public class SkinnedMeshRenderSystem() : MeshRenderSystem<SkinnedMeshComponent>(["SKINNED_MESH_VERTEX", ..SkinnedBindings.OwnDefines])
{
    private abstract class SkinnedBindings : Bindings
    {
        public const uint Poses = BaseMaxBinding + 1;
        public const uint InverseBind = BaseMaxBinding + 2;
        public const uint BoneInfluences = BaseMaxBinding + 3;
        public const uint BoneInfluenceOffsets = BaseMaxBinding + 4;
        public const uint SkinMeshData = BaseMaxBinding + 5;
        public const uint PoseMapping = BaseMaxBinding + 6;
        public const uint MaxBinding = PoseMapping;

        public static readonly string[] OwnDefines =
        [
            Define("SKIN_POSES", Poses),
            Define("SKIN_INVERSE_BIND", InverseBind),
            Define("SKIN_BONE_INFLUENCES", BoneInfluences),
            Define("SKIN_BONE_INFLUENCE_OFFSETS", BoneInfluenceOffsets),
            Define("SKIN_MESH_DATA", SkinMeshData),
            Define("SKIN_POSE_MAPPING", PoseMapping)
        ];
    }

    public override uint Order => 23;
    public override uint? MaxBindingUsed => SkinnedBindings.MaxBinding;
    // protected override bool IsCulled => DirtyComponentsCount == 0; // TODO: cull on the cpu (do not update non visible components + compute the bounds from bones)

    private readonly ShaderStorageBuffer<Matrix4x4> _poseData = new(BufferUsageHint.DynamicDraw);
    private readonly ShaderStorageBuffer<Matrix4x4> _inverseBind = new();
    private readonly ShaderStorageBuffer<uint> _boneInfluences = new();
    private readonly ShaderStorageBuffer<uint> _boneInfluenceOffsets = new();
    private readonly ShaderStorageBuffer<PerMeshSkinningData> _skinMeshData = new();
    private readonly ShaderStorageBuffer<uint> _poseMapping = new();
    protected override IEnumerable<(uint, IIndexedBind)> SystemBuffers =>
    [
        (SkinnedBindings.Poses, _poseData),
        (SkinnedBindings.InverseBind, _inverseBind),
        (SkinnedBindings.BoneInfluences, _boneInfluences),
        (SkinnedBindings.BoneInfluenceOffsets, _boneInfluenceOffsets),
        (SkinnedBindings.SkinMeshData, _skinMeshData),
        (SkinnedBindings.PoseMapping, _poseMapping)
    ];

    private sealed class SkinnedCounts : AllocationCounts
    {
        public uint PoseBones; // total number of bones across every skeleton, one pose per component
        public uint UniqueBones; // number of bones across unique skeletons only
        public uint SkinnedVertices; // total number of skinned vertices across all LODs of all unique meshes
    }
    private readonly SkinnedCounts _counts = new();
    protected override AllocationCounts CreateCounts() => _counts;

    protected override void OnActorComponentEnqueued(SkinnedMeshComponent component)
    {
        base.OnActorComponentEnqueued(component);

        if (component.Descriptor.Skeleton is { } skeleton)
        {
            _counts.PoseBones += (uint)skeleton.BoneCount;
        }

        // the base just took this mesh's refcount up; anything above 1 means another component already counted it
        if (Meshes[component.Descriptor.Guid].RefCount > 1) return;

        if (component.Descriptor.Skeleton is { } uniqueSkeleton)
            _counts.UniqueBones += (uint)uniqueSkeleton.BoneCount;

        foreach (var lod in component.Descriptor.Lods)
        {
            if (lod.HasSkinnedVertices) _counts.SkinnedVertices += lod.VertexCount;
        }
    }

    protected override void OnLoad()
    {
        base.OnLoad();

        _poseData.Generate();
        _inverseBind.Generate();
        _boneInfluences.Generate();
        _boneInfluenceOffsets.Generate();
        _skinMeshData.Generate();
        _poseMapping.Generate();

        if (_counts.PoseBones > 0) _poseData.Allocate(_counts.PoseBones);
        if (_counts.UniqueBones > 0) _inverseBind.Allocate(_counts.UniqueBones);
        if (_counts.SkinnedVertices > 0)
        {
            _boneInfluences.Allocate(_counts.SkinnedVertices * 2);
            _boneInfluenceOffsets.Allocate(_counts.SkinnedVertices);
        }
        if (_counts.UniqueComponents > 0) _skinMeshData.Allocate(_counts.UniqueComponents);
        if (_counts.Instances > 0) _poseMapping.Allocate(_counts.Instances);
    }

    protected override void OnResourcesAdded(SkinnedMeshComponent component, ResourcesMetadata metadata)
    {
        base.OnResourcesAdded(component, metadata);

        var descriptor = component.Descriptor;
        if (descriptor.Skeleton is not { } skeleton) return;

        if (Meshes[descriptor.Guid].UploadedBy == component.Id)
        {
            var inverseBoneMatrices = new Matrix4x4[skeleton.BoneMatrices.Length];
            for (var i = 0; i < inverseBoneMatrices.Length; i++)
            {
                Matrix4x4.Invert(skeleton.BoneMatrices[i], out inverseBoneMatrices[i]);
            }

            var data = new PerMeshSkinningData
            {
                BaseBone = (uint)_inverseBind.AddRange(inverseBoneMatrices).StartIndex
            };

            var lods = descriptor.Lods;
            for (var i = 0; i < lods.Length && i < Settings.MaxNumberOfLods; i++)
            {
                var primitive = lods[i].CreatePrimitive(); // cached, already created by GeometryPool.Add
                if (primitive is not { BoneInfluences: { Length: > 0 } boneInfluences, BoneInfluenceCounts: { Length: > 0 } boneInfluenceCounts })
                    continue;

                var cursor = (uint)_boneInfluences.AddRange(boneInfluences).StartIndex;

                var packedOffsets = new uint[boneInfluenceCounts.Length];
                for (var j = 0; j < packedOffsets.Length; j++)
                {
                    var count = boneInfluenceCounts[j];
                    packedOffsets[j] = (cursor << 8) | count;
                    cursor += count;
                }

                unsafe
                {
                    data.LOD_BaseBoneInfluence[i] = (uint)_boneInfluenceOffsets.AddRange(packedOffsets).StartIndex;
                }
            }

            _skinMeshData.Upsert((int)metadata.GeometryHandle.MeshIndex, data);
        }

        skeleton._poseAllocation = _poseData.AddRange(skeleton.BoneMatrices);

        // one pose per component, so every instance of it points at the same base
        var basePose = (uint)skeleton._poseAllocation.Value.StartIndex;
        for (var i = 0; i < metadata.InstanceAllocation.Length; i++)
        {
            _poseMapping.Upsert(metadata.InstanceAllocation.StartIndex + i, basePose);
        }
    }

    protected override void OnComponentUpdate(SkinnedMeshComponent component, float delta)
    {
        base.OnComponentUpdate(component, delta);
        if (!component.IsDirty(DirtyFlags.Animation) || component is not SkeletalMeshComponent { Descriptor.Skeleton: { } skeleton } meshComponent) return;

        // TODO: do not animate invisible meshes
        if (meshComponent.Playback is { Animation: { Segments.Count: > 0 } animation } playback)
        {
            var time = playback.Time;
            foreach (var (boneName, boneIndex) in skeleton.BoneNameToIndex)
            {
                // for each vertex bone, find its skeleton bone
                if (!animation.Skeleton.BoneNameToIndex.TryGetValue(boneName, out var skeletonIndex) ||
                    !animation.TryGetSegment(skeletonIndex, time, out var segment))
                    continue;

                var scale = !skeleton.BoneDescriptors[boneIndex].IsRoot;
                skeleton.BoneLocalMatrices[boneIndex] = segment.GetBoneMatrix(skeletonIndex, time, scale);
            }
        }
        else
        {
            // manually moved a bone
        }

        skeleton.RecalculateBoneMatrices();

        if (skeleton._poseAllocation is { } poseAllocation)
        {
            _poseData.Update(poseAllocation, skeleton.BoneMatrices);
        }
        component.MarkClean(DirtyFlags.Animation);

        foreach (var child in component.Children)
        {
            child.MarkDirty(DirtyFlags.Transform);
        }
    }

    protected override void OnActorComponentRemoved(SkinnedMeshComponent component, EEndPlayReason reason)
    {
        base.OnActorComponentRemoved(component, reason);

        if (component.Descriptor.Skeleton is not { _poseAllocation: { } poseAllocation } descriptor) return;

        // if we are shutting down, the whole buffer is about to be deleted, no need to remove the allocation
        if (reason != EEndPlayReason.Shutdown)
        {
            _poseData.Remove(poseAllocation);
        }
        descriptor._poseAllocation = null; // cleared either way tho, the descriptor outlives the buffer
    }

    public override void Dispose()
    {
        base.Dispose();

        _poseData.Dispose();
        _inverseBind.Dispose();
        _boneInfluences.Dispose();
        _boneInfluenceOffsets.Dispose();
        _skinMeshData.Dispose();
        _poseMapping.Dispose();
    }

    public override long Allocated => base.Allocated + _poseData.Allocated + _inverseBind.Allocated + _boneInfluences.Allocated + _boneInfluenceOffsets.Allocated + _skinMeshData.Allocated + _poseMapping.Allocated;
    public override long Used => base.Used + _poseData.Used + _inverseBind.Used + _boneInfluences.Used + _boneInfluenceOffsets.Used + _skinMeshData.Used + _poseMapping.Used;

    public override IEnumerable<MemoryDetail> GetMemoryDetails()
    {
        foreach (var detail in base.GetMemoryDetails())
            yield return detail;

        yield return new MemoryDetail("Pose Data", _poseData);
        yield return new MemoryDetail("Inverse Bind Data", _inverseBind);
        yield return new MemoryDetail("Bone Influence Buffer", _boneInfluences);
        yield return new MemoryDetail("Bone Influence Offset Buffer", _boneInfluenceOffsets);
        yield return new MemoryDetail("Skin Mesh Data", _skinMeshData);
        yield return new MemoryDetail("Pose Mapping Buffer", _poseMapping);
    }
}
