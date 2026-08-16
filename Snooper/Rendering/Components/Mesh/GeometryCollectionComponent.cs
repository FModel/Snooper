using CUE4Parse_Conversion.Dto;
using CUE4Parse.UE4.Assets.Exports.Component;
using CUE4Parse.UE4.Assets.Exports.GeometryCollection;
using CUE4Parse.UE4.Assets.Exports.StaticMesh;
using CUE4Parse.UE4.Objects.Core.Math;
using CUE4Parse.UE4.Objects.Meshes;
using CUE4Parse.UE4.Objects.UObject;
using Snooper.Core;
using Snooper.Core.Managers;
using Snooper.Rendering.Components.Descriptors;
using Snooper.Rendering.Components.Transforms;

namespace Snooper.Rendering.Components.Mesh;

public class GeometryCollectionComponent : StaticMeshComponent
{
    private readonly List<SpatialComponent> _components = [];

    public GeometryCollectionComponent(UGeometryCollectionComponent component, UGeometryCollection geometryCollection) : base(component.OverrideMaterials, component)
    {
        Descriptor = Create(geometryCollection);
    }

    public GeometryCollectionComponent(UGeometryCollection geometryCollection, Transform? transform = null) : base(geometryCollection.Materials, transform, geometryCollection.Name)
    {
        Descriptor = Create(geometryCollection);
    }

    private PrimitiveDescriptor<Vertex> Create(UGeometryCollection geometryCollection)
    {
        PrimitiveDescriptor<Vertex> descriptor;
        try
        {
            descriptor = PrimitiveDescriptor<Vertex>.GetOrCreate(geometryCollection, (vertices, indices, colors, extraUvs) => new Geometry(vertices, indices, colors, extraUvs));
        }
        catch
        {
            MeshVertex[] vertices =
            [
                new(new FVector(-0.5f, -0.5f, -0.5f), FVector.ZeroVector, FVector4.ZeroVector, FMeshUVFloat.ZeroVector),
                new(new FVector(0.5f, -0.5f, -0.5f), FVector.ZeroVector, FVector4.ZeroVector, FMeshUVFloat.ZeroVector),
                new(new FVector(0.5f, 0.5f, -0.5f), FVector.ZeroVector, FVector4.ZeroVector, FMeshUVFloat.ZeroVector),
                new(new FVector(-0.5f, 0.5f, -0.5f), FVector.ZeroVector, FVector4.ZeroVector, FMeshUVFloat.ZeroVector),
            ];

            uint[] indices =
            [
                0, 1, 2,
                0, 2, 3
            ];

            FBox? bounds = null;
            if (geometryCollection.RenderData?.PreSkinnedBounds is { } preSkinnedBounds)
                bounds = preSkinnedBounds.GetBox();
            else if (geometryCollection.RenderData?.MeshDescription?.PreSkinnedBounds  is { } meshDescriptionBounds)
                bounds = meshDescriptionBounds.GetBox();

            descriptor = new PrimitiveDescriptor<Vertex>(bounds ?? new FBox(FVector.ZeroVector, FVector.OneVector), () => new Geometry(vertices, indices, null, null));
        }

        CreateInstances(geometryCollection);
        return descriptor;
    }

    private void CreateInstances(UGeometryCollection geometryCollection)
    {
        if (geometryCollection is { GeometryCollection: { } collection, AutoInstanceMeshes: { Length: > 0 } meshes })
        {
            // TODO: use vertices group to create this component descriptor, in the meantime, this component is not a mesh
            // we need an asset with actual vertices as an example, could not find any yet
            // Descriptor = PrimitiveDescriptor<Vertex>.GetOrCreate(staticMesh, (vertices, indices, colors, extraUvs) => new Geometry(vertices, indices, colors, extraUvs));

            var group = new FName("Transform");

            var meshIndices = collection.GetAttributeValue<int>("AutoInstanceMeshIndex", group);
            if (meshIndices is not { Length: > 0 }) return;

            var transforms = collection.GetAttributeValue<FTransform>("Transform", group);
            if (transforms is not { Length: > 0 }) return;

            const int rigid = 1;
            var simulationTypes = collection.GetAttributeValue<int>("SimulationType", group);
            var parents = collection.GetAttributeValue<int>("Parent", group);
            var hides = collection.GetAttributeValue<bool>("Hide", group); // TODO: maybe we should support hiding instances

            var placements = new List<Transform>?[meshes.Length];
            for (var i = 0; i < transforms.Length; i++)
            {
                if (GetIndex(simulationTypes, i, rigid) != rigid || GetIndex(hides, i, false)) continue;

                var meshIndex = GetIndex(meshIndices, i, -1);
                if (meshIndex < 0 || meshIndex >= meshes.Length) continue;

                (placements[meshIndex] ??= new List<Transform>(meshes[meshIndex].NumInstances)).Add(ResolveTransform(i));
            }

            for (var i = 0; i < meshes.Length; i++)
            {
                if (placements[i] is not { Count: > 0 } instances) continue;

                if (meshes[i].Mesh?.TryLoad<UStaticMesh>(out var sm) == true)
                {
                    _components.Add(new InstancedStaticMeshComponent(sm, instances) { Relation = this });
                }
            }

            FTransform ResolveTransform(int index)
            {
                var local = transforms[index];
                var parent = GetIndex(parents, index, -1);
                return parent >= 0 && parent < transforms.Length ? local * ResolveTransform(parent) : local;
            }
            T GetIndex<T>(T[]? data, int index, T fallback) => data is not null && index < data.Length ? data[index] : fallback;
        }
    }

    protected override void BeginPlay(ActorManager scene)
    {
        base.BeginPlay(scene);
        if (Actor is not { } actor) return;

        foreach (var component in _components)
        {
            actor.Components.Add(component);
        }
    }

    protected override void EndPlay(EEndPlayReason reason)
    {
        base.EndPlay(reason);
        if (Actor is not { } actor) return;

        foreach (var component in _components)
        {
            actor.Components.Remove(component);
        }
    }
}
