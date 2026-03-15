using CUE4Parse_Conversion.Meshes;
using CUE4Parse_Conversion.Meshes.PSK;
using CUE4Parse.UE4.Assets.Exports.SkeletalMesh;
using CUE4Parse.UE4.Assets.Exports.StaticMesh;
using CUE4Parse.UE4.Objects.Core.Math;
using CUE4Parse.UE4.Objects.Core.Misc;
using CUE4Parse.UE4.Objects.Meshes;
using Snooper.Rendering.Cache;
using Snooper.Rendering.Primitives;

namespace Snooper.Rendering.Components.Descriptors;

public class PrimitiveDescriptor<TVertex> where TVertex : unmanaged
{
    public string? Path { get; }
    public FGuid Guid { get; } // does not have to match the cached GUID (if descriptor is cached), but it's better for debug purposes if it does
    public CullingBounds Bounds { get; }
    public LodDescriptor<TVertex>[] Lods { get; }
    public SkeletonDescriptor? Skeleton { get; }

    public PrimitiveDescriptor(CullingBounds bounds, Func<TPrimitiveData<TVertex>> factory)
    {
        Guid = FGuid.Random();
        Bounds = bounds;
        Lods = [new LodDescriptor<TVertex>(factory())];
    }

    private PrimitiveDescriptor(uint id, CullingBounds bounds, Func<uint, TPrimitiveData<TVertex>> factory)
    {
        Guid = new FGuid(id);
        Bounds = bounds;
        Lods = [new LodDescriptor<TVertex>(factory(id))];
    }

    private PrimitiveDescriptor(UStaticMesh owner, Func<CMeshVertex[], uint[], FColor[]?, FMeshUVFloat[]?, TPrimitiveData<TVertex>> factory)
    {
        Path = owner.Name;
        Guid = owner.LightingGuid;

        if (!owner.TryConvert(out var mesh))
            throw new ArgumentException("Failed to convert static mesh.", nameof(owner));

        using (mesh)
        {
            Bounds = new CullingBounds(mesh.BoundingBox);
            Lods = (from lod in mesh.LODs where lod.NumVerts > 0 select new LodDescriptor<TVertex>(lod, factory)).ToArray();
        }
    }

    private PrimitiveDescriptor(USkeletalMesh owner, Func<CMeshVertex[], uint[], FColor[]?, FMeshUVFloat[]?, TPrimitiveData<TVertex>> factory)
    {
        Path = owner.Name;
        Guid = new FGuid((uint)owner.Name.GetHashCode());

        if (!owner.TryConvert(out var mesh))
            throw new ArgumentException("Failed to convert skeletal mesh.", nameof(owner));

        using (mesh)
        {
            Bounds = new CullingBounds(mesh.BoundingBox);
            Lods = new LodDescriptor<TVertex>[mesh.LODs.Count];
            for (var i = 0; i < Lods.Length; i++)
            {
                Lods[i] = new LodDescriptor<TVertex>(mesh.LODs[i], factory);
            }
        }

        Skeleton = new SkeletonDescriptor(owner.ReferenceSkeleton);
    }

    /// <summary>
    /// Creates or retrieves a cached <see cref="PrimitiveDescriptor{TVertex}"/> based on the provided ID.
    /// The factory function is used to generate the primitive data if it doesn't already exist in the cache.
    /// </summary>
    public static PrimitiveDescriptor<TVertex> GetOrCreate(uint id, CullingBounds bounds, Func<uint, TPrimitiveData<TVertex>> factory)
        => MeshCache.GetOrCreate(new FGuid(id), () => new PrimitiveDescriptor<TVertex>(id, bounds, factory));

    /// <summary>
    /// Creates or retrieves a cached <see cref="PrimitiveDescriptor{TVertex}"/> for the given static mesh.
    /// The factory function is used to generate the primitive data if it doesn't already exist in the cache.
    /// </summary>
    public static PrimitiveDescriptor<TVertex> GetOrCreate(UStaticMesh owner, Func<CMeshVertex[], uint[], FColor[]?, FMeshUVFloat[]?, TPrimitiveData<TVertex>> factory)
        => MeshCache.GetOrCreate(owner.LightingGuid, () => new PrimitiveDescriptor<TVertex>(owner, factory));

    /// <summary>
    /// Creates or retrieves a cached <see cref="PrimitiveDescriptor{TVertex}"/> for the given skeletal mesh.
    /// The factory function is used to generate the primitive data if it doesn't already exist in the
    /// </summary>
    public static PrimitiveDescriptor<TVertex> GetOrCreate(USkeletalMesh owner, Func<CMeshVertex[], uint[], FColor[]?, FMeshUVFloat[]?, TPrimitiveData<TVertex>> factory)
        => MeshCache.GetOrCreate(FGuid.Random(), () => new PrimitiveDescriptor<TVertex>(owner, factory));
}
