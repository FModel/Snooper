using CUE4Parse_Conversion.Meshes;
using CUE4Parse_Conversion.Meshes.PSK;
using CUE4Parse.UE4.Assets.Exports.SkeletalMesh;
using CUE4Parse.UE4.Assets.Exports.StaticMesh;
using CUE4Parse.UE4.Objects.Core.Misc;
using Snooper.Rendering.Primitives;

namespace Snooper.Rendering.Components.Descriptors;

public class PrimitiveDescriptor2<TVertex> where TVertex : unmanaged
{
    public string? Path { get; }
    public FGuid Guid { get; }
    public CullingBounds Bounds { get; }
    public LodDescriptor<TVertex>[] Lods { get; }
    
    public PrimitiveDescriptor2(CullingBounds bounds, Func<TPrimitiveData<TVertex>> factory)
    {
        Guid = FGuid.Random();
        Bounds = bounds;
        Lods = [new LodDescriptor<TVertex>(factory())];
    }
    
    public PrimitiveDescriptor2(uint id, CullingBounds bounds, Func<uint, TPrimitiveData<TVertex>> factory)
    {
        Guid = new FGuid(id);
        Bounds = bounds;
        Lods = [new LodDescriptor<TVertex>(factory(id))];
    }
    
    public PrimitiveDescriptor2(UStaticMesh owner, Func<CMeshVertex[], uint[], TPrimitiveData<TVertex>> factory)
    {
        Path = owner.Name;
        Guid = owner.LightingGuid;
        
        if (!owner.TryConvert(out var mesh))
            throw new ArgumentException("Failed to convert static mesh.", nameof(owner));

        using (mesh)
        {
            Bounds = new CullingBounds(mesh.BoundingBox);
            Lods = new LodDescriptor<TVertex>[mesh.LODs.Count];
            for (var i = 0; i < Lods.Length; i++)
            {
                Lods[i] = new LodDescriptor<TVertex>(mesh.LODs[i], factory);
            }
        }
    }
    
    public PrimitiveDescriptor2(USkeletalMesh owner, Func<CMeshVertex[], uint[], TPrimitiveData<TVertex>> factory)
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
    }
}