using System.Collections.Concurrent;
using System.Numerics;
using CUE4Parse.UE4.Objects.Core.Misc;
using OpenTK.Graphics.OpenGL4;
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

public class SkinnedMeshRenderSystem() : MeshRenderSystem<SkinnedMeshComponent>(["SKINNED_MESH_VERTEX"])
{
    public override uint Order => 23;
    // protected override bool IsCulled => DirtyComponentsCount == 0; // TODO: meshes can disappear if animated outside of their BB

    private readonly ShaderStorageBuffer<Matrix4x4> _poseData = new(BufferUsageHint.DynamicDraw);
    private readonly ShaderStorageBuffer<Matrix4x4> _inverseBind = new();
    private readonly ShaderStorageBuffer<uint> _boneInfluences = new();
    private readonly ShaderStorageBuffer<uint> _boneInfluenceOffsets = new();
    private readonly ShaderStorageBuffer<PerMeshSkinningData> _skinMeshData = new();
    private readonly ShaderStorageBuffer<uint> _poseMapping = new();

    // TODO: it's annoying to always deduplicate, do it once somewhere
    private readonly ConcurrentDictionary<FGuid, byte> _guids = [];
    private readonly HashSet<FGuid> _uploadedGuids = [];
    private uint _poseBones;
    private uint _uniqueBones;
    private uint _skinnedVertices;
    private int _maxComponentId;

    protected override void OnActorComponentEnqueued(SkinnedMeshComponent component)
    {
        base.OnActorComponentEnqueued(component);

        if (component.Id > _maxComponentId)
        {
            _maxComponentId = component.Id;
        }

        if (component.Descriptor.Skeleton is { } skeleton)
        {
            _poseBones += (uint)skeleton.BoneCount;
        }

        if (_guids.TryAdd(component.Descriptor.Guid, 0))
        {
            if (component.Descriptor.Skeleton is { } uniqueSkeleton)
                _uniqueBones += (uint)uniqueSkeleton.BoneCount;

            foreach (var lod in component.Descriptor.Lods)
            {
                if (lod.HasSkinnedVertices) _skinnedVertices += lod.VertexCount;
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
        _poseMapping.Generate();

        if (_poseBones > 0) _poseData.Allocate(_poseBones);
        if (_uniqueBones > 0) _inverseBind.Allocate(_uniqueBones);
        if (_skinnedVertices > 0)
        {
            _boneInfluences.Allocate(_skinnedVertices * 2);
            _boneInfluenceOffsets.Allocate(_skinnedVertices);
        }
        if (_guids.Count > 0) _skinMeshData.Allocate((uint)_guids.Count);
        _poseMapping.Allocate(_maxComponentId + 1);
    }

    protected override void OnResourcesAdded(SkinnedMeshComponent component, ResourcesMetadata metadata)
    {
        base.OnResourcesAdded(component, metadata);

        var descriptor = component.Descriptor;
        if (descriptor.Skeleton is not { } skeleton) return;

        if (_uploadedGuids.Add(descriptor.Guid))
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
        _poseMapping.Upsert(component.Id, (uint)skeleton._poseAllocation.Value.StartIndex);
    }

    protected override void OnComponentUpdate(SkinnedMeshComponent component, float delta)
    {
        base.OnComponentUpdate(component, delta);

        if (component is SkeletalMeshComponent { IsPlayingAnimation: true, Descriptor.Skeleton: { } skeleton, Animation: { Sequences.Length: > 0 } animation } skeletal)
        {
            float time = ActorManager?.Time ?? delta;
            time = (time * animation.PlayRate + animation.StartTime) % skeletal.MaxAnimationDuration;

            foreach (var (boneName, boneIndex) in skeleton.BoneNameToIndex)
            {
                // for each vertex bone, find its skeleton bone
                if (!animation.Skeleton.BoneNameToIndex.TryGetValue(boneName, out var skeletonIndex))
                    continue;

                foreach (var sequence in animation.Sequences)
                {
                    if (!sequence.IsAnimatingBone(skeletonIndex)) continue;

                    // if this sequence should be played for this frame
                    if (time >= sequence.StartTime && time < sequence.EndTime)
                    {
                        var scale = !skeleton.BoneDescriptors[boneIndex].IsRoot;
                        skeleton.BoneLocalMatrices[boneIndex] = sequence.GetBoneMatrix(skeletonIndex, time, scale);
                        break;
                    }
                }
            }

            skeleton.RecalculateBoneMatrices();
            component.MarkDirty(DirtyFlags.Animation);
        }

        if (component.IsDirty(DirtyFlags.Animation))
        {
            if (component.Descriptor.Skeleton is { _poseAllocation: { } poseAllocation } descriptor)
                _poseData.Update(poseAllocation, descriptor.BoneMatrices);

            component.MarkClean(DirtyFlags.Animation);
            foreach (var child in component.Children)
            {
                child.MarkDirty(DirtyFlags.Transform);
            }
        }
    }

    protected override void OnActorComponentRemoved(SkinnedMeshComponent component)
    {
        base.OnActorComponentRemoved(component);

        if (component.Descriptor.Skeleton is { _poseAllocation: { } poseAllocation } descriptor)
        {
            _poseData.Remove(poseAllocation);
            descriptor._poseAllocation = null;
        }
    }

    protected override void PreRender(CameraComponent camera, ShaderProgram shader)
    {
        base.PreRender(camera, shader);

        _poseData.Bind(BindingPoints.SkinPoses);
        _inverseBind.Bind(BindingPoints.SkinInverseBind);
        _boneInfluences.Bind(BindingPoints.SkinBoneInfluences);
        _boneInfluenceOffsets.Bind(BindingPoints.SkinBoneInfluenceOffsets);
        _skinMeshData.Bind(BindingPoints.SkinMeshData);
        _poseMapping.Bind(BindingPoints.SkinPoseMapping);
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
