using System.Numerics;
using OpenTK.Graphics.OpenGL4;
using Snooper.Core;
using Snooper.Core.Containers;
using Snooper.Core.Containers.Buffers;
using Snooper.Core.Containers.Resources;
using Snooper.Rendering.Components;
using Snooper.Rendering.Components.Descriptors;
using Snooper.Rendering.Components.Mesh;

namespace Snooper.Rendering.Systems;

/// <summary>
/// TODO: gpu driven animation update
/// </summary>
public unsafe struct PerMeshSkinningData
{
    public uint BaseBone; // offset of this mesh's bones in the inverse bind buffer
    public uint MorphCount; // number of morph targets on this mesh, 0 when it has none
    public fixed uint LOD_BaseBoneInfluence[Settings.MaxNumberOfLods];
    public fixed uint LOD_BaseMorphOffset[Settings.MaxNumberOfLods];
    public readonly uint Pad0, Pad1;

    public PerMeshSkinningData()
    {
        for (var i = 0; i < Settings.MaxNumberOfLods; i++)
        {
            LOD_BaseBoneInfluence[i] = uint.MaxValue;
            LOD_BaseMorphOffset[i] = uint.MaxValue;
        }
    }
}

public struct PerInstanceSkinningData
{
    public uint BasePose;
    public uint BaseMorphWeight;
}

public class SkinnedMeshRenderSystem() : MeshRenderSystem<SkinnedMeshComponent>(["SKINNED_MESH_VERTEX", $"MAX_NUMBER_OF_LODS {Settings.MaxNumberOfLods}", ..SkinnedBindings.OwnDefines])
{
    private abstract class SkinnedBindings : Bindings
    {
        public const uint Poses = BaseMaxBinding + 1;
        public const uint InverseBind = BaseMaxBinding + 2;
        public const uint BoneInfluences = BaseMaxBinding + 3;
        public const uint BoneInfluenceOffsets = BaseMaxBinding + 4;
        public const uint SkinMeshData = BaseMaxBinding + 5;
        public const uint SkinInstanceData = BaseMaxBinding + 6;
        public const uint MorphDeltas = BaseMaxBinding + 7;
        public const uint MorphDeltaOffsets = BaseMaxBinding + 8;
        public const uint MorphWeights = BaseMaxBinding + 9;
        public const uint MaxBinding = MorphWeights;

        public static readonly string[] OwnDefines =
        [
            Define("SKIN_POSES", Poses),
            Define("SKIN_INVERSE_BIND", InverseBind),
            Define("SKIN_BONE_INFLUENCES", BoneInfluences),
            Define("SKIN_BONE_INFLUENCE_OFFSETS", BoneInfluenceOffsets),
            Define("SKIN_MESH_DATA", SkinMeshData),
            Define("SKIN_INSTANCE_DATA", SkinInstanceData),
            Define("MORPH_DELTAS", MorphDeltas),
            Define("MORPH_DELTA_OFFSETS", MorphDeltaOffsets),
            Define("MORPH_WEIGHTS", MorphWeights)
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
    private readonly ShaderStorageBuffer<PerInstanceSkinningData> _skinInstanceData = new();
    private readonly ShaderStorageBuffer<MorphDelta> _morphDeltas = new();
    private readonly ShaderStorageBuffer<uint> _morphDeltaOffsets = new();
    private readonly ShaderStorageBuffer<float> _morphWeights = new(BufferUsageHint.DynamicDraw);
    protected override IEnumerable<(uint, IIndexedBind)> SystemBuffers =>
    [
        (SkinnedBindings.Poses, _poseData),
        (SkinnedBindings.InverseBind, _inverseBind),
        (SkinnedBindings.BoneInfluences, _boneInfluences),
        (SkinnedBindings.BoneInfluenceOffsets, _boneInfluenceOffsets),
        (SkinnedBindings.SkinMeshData, _skinMeshData),
        (SkinnedBindings.SkinInstanceData, _skinInstanceData),
        (SkinnedBindings.MorphDeltas, _morphDeltas),
        (SkinnedBindings.MorphDeltaOffsets, _morphDeltaOffsets),
        (SkinnedBindings.MorphWeights, _morphWeights)
    ];

    private sealed class SkinnedCounts : AllocationCounts
    {
        public uint PoseBones; // total number of bones across every skeleton, one pose per component
        public uint UniqueBones; // number of bones across unique skeletons only
        public uint SkinnedVertices; // total number of skinned vertices across all LODs of all unique meshes
        public uint MorphDeltas; // total number of morph deltas across all LODs of all unique meshes
        public uint MorphDeltaOffsets; // one CSR entry per vertex, plus a tail, per morphed LOD of every unique mesh
        public uint MorphWeights; // one weight per morph target, one set per component
    }
    private readonly SkinnedCounts _counts = new();
    protected override AllocationCounts CreateCounts() => _counts;

    protected override void OnActorComponentEnqueued(SkinnedMeshComponent component)
    {
        base.OnActorComponentEnqueued(component);

        var descriptor = component.Descriptor;
        var isUnique = Meshes[descriptor.Guid].RefCount == 1;

        if (isUnique)
        {
            foreach (var lod in descriptor.Lods)
            {
                if (lod.HasSkinnedVertices)
                {
                    _counts.SkinnedVertices += lod.VertexCount;
                }
            }
        }

        if (descriptor.Skeleton is { } skeleton)
        {
            _counts.PoseBones += (uint)skeleton.BoneCount; // one pose per component
            if (isUnique) _counts.UniqueBones += (uint)skeleton.BoneCount;
        }

        if (descriptor.Morphs is { Count: > 0 } morphs)
        {
            _counts.MorphWeights += (uint)morphs.Count; // per component too, each drives its own morphs
            if (isUnique)
            {
                for (var i = 0; i < morphs.Lods.Length && i < Settings.MaxNumberOfLods; i++)
                {
                    _counts.MorphDeltas += (uint)morphs.Lods[i].Deltas.Length;
                    _counts.MorphDeltaOffsets += (uint)morphs.Lods[i].Offsets.Length;
                }
            }
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
        _skinInstanceData.Generate();
        _morphDeltas.Generate();
        _morphDeltaOffsets.Generate();
        _morphWeights.Generate();

        if (_counts.MorphDeltas > 0) _morphDeltas.Allocate(_counts.MorphDeltas);
        if (_counts.MorphDeltaOffsets > 0) _morphDeltaOffsets.Allocate(_counts.MorphDeltaOffsets);
        if (_counts.MorphWeights > 0) _morphWeights.Allocate(_counts.MorphWeights);

        if (_counts.PoseBones > 0) _poseData.Allocate(_counts.PoseBones);
        if (_counts.UniqueBones > 0) _inverseBind.Allocate(_counts.UniqueBones);
        if (_counts.SkinnedVertices > 0)
        {
            _boneInfluences.Allocate(_counts.SkinnedVertices * 2);
            _boneInfluenceOffsets.Allocate(_counts.SkinnedVertices);
        }
        if (_counts.UniqueComponents > 0) _skinMeshData.Allocate(_counts.UniqueComponents);
        if (_counts.Instances > 0) _skinInstanceData.Allocate(_counts.Instances);
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

            if (descriptor.Morphs is { Count: > 0 } morphs)
            {
                data.MorphCount = (uint)morphs.Count;

                for (var i = 0; i < morphs.Lods.Length && i < Settings.MaxNumberOfLods; i++)
                {
                    var morphLod = morphs.Lods[i];
                    if (morphLod.IsEmpty) continue;

                    var baseDelta = (uint)_morphDeltas.AddRange(morphLod.Deltas).StartIndex;
                    var offsets = new uint[morphLod.Offsets.Length];
                    for (var j = 0; j < offsets.Length; j++)
                    {
                        offsets[j] = morphLod.Offsets[j] + baseDelta;
                    }

                    unsafe
                    {
                        data.LOD_BaseMorphOffset[i] = (uint)_morphDeltaOffsets.AddRange(offsets).StartIndex;
                    }
                }
            }

            _skinMeshData.Upsert((int)metadata.GeometryHandle.MeshIndex, data);
        }

        skeleton._poseAllocation = _poseData.AddRange(skeleton.BoneMatrices);

        var instanceData = new PerInstanceSkinningData
        {
            BasePose = (uint)skeleton._poseAllocation.Value.StartIndex,
            BaseMorphWeight = uint.MaxValue
        };

        // a weight set per component as well, so two components sharing a mesh can wear different faces
        if (descriptor.Morphs is { Count: > 0 })
        {
            component._morphWeightAllocation = _morphWeights.AddRange(component.MorphWeights);
            instanceData.BaseMorphWeight = (uint)component._morphWeightAllocation.Value.StartIndex;
        }

        _skinInstanceData.Upsert(metadata.InstanceAllocation.StartIndex, instanceData);
    }

    protected override void OnComponentUpdate(SkinnedMeshComponent component, float delta)
    {
        base.OnComponentUpdate(component, delta);

        if (component.IsDirty(DirtyFlags.Morph))
        {
            if (component._morphWeightAllocation is { } morphWeightAllocation)
            {
                _morphWeights.Update(morphWeightAllocation, component.MorphWeights);
            }
            component.MarkClean(DirtyFlags.Morph);
        }

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

        if (component._morphWeightAllocation is { } morphWeightAllocation)
        {
            if (reason is EEndPlayReason.Destroyed)
            {
                _morphWeights.Remove(morphWeightAllocation);
            }
            component._morphWeightAllocation = null;
        }

        if (component.Descriptor.Skeleton is not { _poseAllocation: { } poseAllocation } descriptor) return;

        // only a component leaving on its own gives its slot back: on a scene swap or a shutdown the
        // whole buffer goes with the system, so freeing slot by slot would be wasted work
        if (reason is EEndPlayReason.Destroyed)
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
        _skinInstanceData.Dispose();
        _morphDeltas.Dispose();
        _morphDeltaOffsets.Dispose();
        _morphWeights.Dispose();
    }

    public override long Allocated => base.Allocated + _poseData.Allocated + _inverseBind.Allocated + _boneInfluences.Allocated + _boneInfluenceOffsets.Allocated + _skinMeshData.Allocated + _skinInstanceData.Allocated + _morphDeltas.Allocated + _morphDeltaOffsets.Allocated + _morphWeights.Allocated;
    public override long Used => base.Used + _poseData.Used + _inverseBind.Used + _boneInfluences.Used + _boneInfluenceOffsets.Used + _skinMeshData.Used + _skinInstanceData.Used + _morphDeltas.Used + _morphDeltaOffsets.Used + _morphWeights.Used;

    public override IEnumerable<MemoryDetail> GetMemoryDetails()
    {
        foreach (var detail in base.GetMemoryDetails())
            yield return detail;

        yield return new MemoryDetail("Pose Data", _poseData);
        yield return new MemoryDetail("Inverse Bind Data", _inverseBind);
        yield return new MemoryDetail("Bone Influence Buffer", _boneInfluences);
        yield return new MemoryDetail("Bone Influence Offset Buffer", _boneInfluenceOffsets);
        yield return new MemoryDetail("Skin Mesh Data", _skinMeshData);
        yield return new MemoryDetail("Instance Skinning Data", _skinInstanceData);
        yield return new MemoryDetail("Morph Delta Buffer", _morphDeltas);
        yield return new MemoryDetail("Morph Delta Offset Buffer", _morphDeltaOffsets);
        yield return new MemoryDetail("Morph Weight Buffer", _morphWeights);
    }
}
