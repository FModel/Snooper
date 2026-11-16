using System.Numerics;
using System.Runtime.InteropServices;
using CUE4Parse_Conversion.Dto;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.StaticMesh;
using CUE4Parse.UE4.Objects.Core.Math;
using CUE4Parse.UE4.Objects.PhysicsEngine;
using Snooper.Core;
using Snooper.Core.Containers.Resources;
using Snooper.Core.Containers.Textures;
using Snooper.Extensions;
using Snooper.Hosting;
using Snooper.Rendering.Components.Descriptors;
using Snooper.Rendering.Components.Mesh;
using Snooper.Rendering.Components.Primitive;
using Snooper.Rendering.Primitives;
using Snooper.Rendering.Systems;
using Snooper.UI;

namespace Snooper.Rendering.Components.Visualization;

[StructLayout(LayoutKind.Sequential)]
public readonly struct PerMaterialCollisionData : IPerMaterialData
{
    public bool IsReady { get; init; }
    public uint Type { get; init; }
}

[DefaultActorSystem(typeof(CollisionSystem))]
public class CollisionComponentVisualization : PrimitiveComponent<PerMaterialCollisionData>, IVisualization<CollisionComponentVisualization>
{
    private static readonly MaterialDataContainer[] _containers = CreateContainers();

    private readonly MeshComponent _owner;

    private CollisionComponentVisualization(MeshComponent owner, PrimitiveDescriptor<Vector3> descriptor, ECollisionEnabled type, string? bone = null) : base(name: $"{owner.Name}{(bone != null ? $" ({bone})" : "")} (Collision)")
    {
        _owner = owner;

        AttachSocketName = bone;
        Descriptor = descriptor;
        DrawDistance = owner.DrawDistance;
        Materials[0].InlineContainer = _containers[(int) type];
    }

    public static CollisionComponentVisualization[] Create(ActorComponent owner)
    {
        if (owner.Actor is not { EnableCollision: true, ActorManager.FileProvider: { } provider })
            return [];

        return owner switch
        {
            StaticMeshComponent mesh and not SplineMeshComponent => FromStaticMesh(mesh),
            SkinnedMeshComponent mesh => FromSkinnedMesh(mesh),
            _ => []
        };

        CollisionComponentVisualization[] FromStaticMesh(StaticMeshComponent mesh)
        {
            if (mesh is { UseDefaultCollision: false, CollisionEnabled: ECollisionEnabled.NoCollision } ||
                mesh._bodySetup is not { } path || !path.TryLoad<UBodySetup>(provider, out var bodySetup))
                return [];

            var type = mesh.UseDefaultCollision ? bodySetup.DefaultInstance?.CollisionEnabled ?? ECollisionEnabled.QueryAndPhysics : mesh.CollisionEnabled;
            if (type == ECollisionEnabled.NoCollision)
                return [];

            return Shell(mesh, bodySetup, type, () => Load<UStaticMesh>(mesh)) is { } shell ? [shell] : [];
        }

        CollisionComponentVisualization[] FromSkinnedMesh(SkinnedMeshComponent mesh)
        {
            if (mesh.Leader is not null || // a follower has no physics state of its own
                mesh.CollisionEnabled == ECollisionEnabled.NoCollision ||
                mesh._physicsAsset is not { } path || !path.TryLoad<UPhysicsAsset>(provider, out var physicsAsset))
                return [];

            var shells = new List<CollisionComponentVisualization>(physicsAsset.SkeletalBodySetups.Length);
            foreach (var index in physicsAsset.SkeletalBodySetups)
            {
                if (index.TryLoad<USkeletalBodySetup>(out var body) && Shell(mesh, body, mesh.CollisionEnabled, null, body.BoneName?.Text) is { } shell)
                    shells.Add(shell);
            }
            return [.. shells];
        }

        CollisionComponentVisualization? Shell(MeshComponent mesh, UBodySetup body, ECollisionEnabled type, Func<UStaticMesh?>? loadMesh = null, string? bone = null)
        {
            var descriptor = PrimitiveDescriptor<Vector3>.GetOrCreate(body, setup => new Geometry(setup, loadMesh));
            return descriptor.Lods[0].IndexCount > 0 ? new CollisionComponentVisualization(mesh, descriptor, type, bone) : null;
        }

        T? Load<T>(MeshComponent mesh) where T : UObject
        {
            if (mesh.Descriptor is not { Path: { } path, Name: { } name } ||
                !provider.TryLoadPackage(path, out var package))
                return null;

            return package.GetExportOrNull<T>(name);
        }
    }

    public override int InstanceCount => _owner.InstanceCount;
    public override Matrix4x4[] GetWorldMatrices(int index = -1)
    {
        var matrices = _owner.GetWorldMatrices(index);
        if (AttachSocketName is not { } bone) return matrices;

        var socket = _owner.GetSocketModelMatrix(bone);
        for (var i = 0; i < matrices.Length; i++)
        {
            matrices[i] = socket * matrices[i];
        }
        return matrices;
    }

    public override string Icon => Settings.ShapesIcon;

    private static MaterialDataContainer[] CreateContainers()
    {
        var containers = new MaterialDataContainer[Enum.GetValues<ECollisionEnabled>().Length];
        for (var i = 0; i < containers.Length; i++)
        {
            containers[i] = new MaterialDataContainer((ECollisionEnabled) i);
        }
        return containers;
    }

    private sealed class MaterialDataContainer(ECollisionEnabled type) : IMaterialDataContainer
    {
        public string Name => type.GetDescription();
        public bool HasTextures => false;
        public bool IsTranslucent => false;
        public Dictionary<string, Texture> GetTextures() => [];
        public void SetBindlessTexture(string key, BindlessTexture bindless) { }

        public void FinalizeGpuData()
        {
            // if (Raw is not null)
            //     throw new InvalidOperationException("GPU data has already been finalized and sent.");

            Raw = new PerMaterialCollisionData { IsReady = true, Type = (uint) type };
        }

        public IPerMaterialData? Raw { get; private set; }
        public uint Version => 0;

        public void DrawControls() => EditorUI.Text("Type", type.GetDescription());
    }

    private sealed class Geometry : PrimitiveData
    {
        private const int Sides = 24;
        private const int CapRings = 6;

        public Geometry(UBodySetup setup, Func<UStaticMesh?>? loadMesh = null)
        {
            var vertices = new List<Vector3>();
            var indices = new List<uint>();

            if (setup.CollisionTraceFlag == ECollisionTraceFlag.CTF_UseComplexAsSimple && loadMesh?.Invoke() is { } mesh)
            {
                Triangles(mesh);
            }
            else if (setup.AggGeom is { } geometry)
            {
                foreach (var sphere in geometry.SphereElems)
                    Capsule(sphere.Center, FQuat.Identity, sphere.Radius, sphere.Radius, 0f);
                foreach (var box in geometry.BoxElems)
                {
                    var extent = new FVector(box.X, box.Y, box.Z) * 0.5f;
                    Box(new FTransform(box.Rotation.Quaternion(), box.Center, FVector.OneVector), -extent, extent);
                }
                foreach (var sphyl in geometry.SphylElems)
                    Capsule(sphyl.Center, sphyl.Rotation.Quaternion(), sphyl.Radius, sphyl.Radius, sphyl.Length * 0.5f);
                foreach (var capsule in geometry.TaperedCapsuleElems)
                    Capsule(capsule.Center, capsule.Rotation.Quaternion(), capsule.Radius0, capsule.Radius1, capsule.Length * 0.5f); // TODO: Radius0 on +Z is not checked against the engine
                foreach (var convex in geometry.ConvexElems)
                    Convex(convex);
            }

            Vertices = [.. vertices];
            Indices = [.. indices];

            void Triangles(UStaticMesh asset)
            {
                using var dto = new StaticMeshDto(asset, Bridge.Options.ToExportOptions());
                if (dto.LODs.Count == 0) return;

                var lod = dto.LODs[0];
                foreach (var candidate in dto.LODs)
                {
                    if (candidate.SourceLodIndex == asset.LODForCollision) lod = candidate;
                }

                var first = (uint) vertices.Count;
                foreach (var vertex in lod.Vertices)
                {
                    Add(vertex.Position);
                }

                foreach (var section in lod.Sections)
                {
                    if (!section.EnableCollision) continue;

                    var end = Math.Min(section.FirstIndex + section.NumFaces * 3, lod.Indices.Length);
                    for (var i = section.FirstIndex; i < end; i++)
                    {
                        indices.Add(first + lod.Indices[i]);
                    }
                }
            }

            void Box(FTransform transform, FVector min, FVector max)
            {
                var first = (uint) vertices.Count;
                for (var i = 0; i < 8; i++)
                {
                    Add(transform.TransformPosition(new FVector(
                        (i & 1) == 0 ? min.X : max.X,
                        (i & 2) == 0 ? min.Y : max.Y,
                        (i & 4) == 0 ? min.Z : max.Z)));
                }

                Quad(first, first + 2, first + 6, first + 4);
                Quad(first + 1, first + 5, first + 7, first + 3);
                Quad(first, first + 4, first + 5, first + 1);
                Quad(first + 2, first + 3, first + 7, first + 6);
                Quad(first, first + 1, first + 3, first + 2);
                Quad(first + 4, first + 6, first + 7, first + 5);
            }

            // two spheres on the Z axis, the first at +halfLength, joined by the cone tangent to both: a sphere
            // (no length), a capsule (equal radii) or a tapered capsule
            void Capsule(FVector center, FQuat rotation, float radius0, float radius1, float halfLength)
            {
                var slope = halfLength > 0f ? Math.Clamp((radius1 - radius0) / (2f * halfLength), -1f, 1f) : 0f;
                var tangent = MathF.Asin(slope); // the latitude where the cone touches both spheres

                var first = (uint) vertices.Count;
                var rings = 0u;
                for (var i = 0; i <= CapRings; i++)
                {
                    var latitude = MathF.PI * 0.5f - i * (MathF.PI * 0.5f - tangent) / CapRings;
                    Ring(radius0 * MathF.Cos(latitude), halfLength + radius0 * MathF.Sin(latitude));
                }
                for (var i = 0; i <= CapRings; i++)
                {
                    var latitude = tangent - i * (tangent + MathF.PI * 0.5f) / CapRings;
                    Ring(radius1 * MathF.Cos(latitude), -halfLength + radius1 * MathF.Sin(latitude));
                }

                for (var r = 0u; r + 1 < rings; r++)
                {
                    for (var s = 0u; s < Sides; s++)
                    {
                        var upper = first + r * Sides;
                        Quad(upper + s, upper + (s + 1) % Sides, upper + Sides + (s + 1) % Sides, upper + Sides + s);
                    }
                }

                void Ring(float radius, float z)
                {
                    for (var s = 0; s < Sides; s++)
                    {
                        var angle = s * 2f * MathF.PI / Sides;
                        Add(center + rotation.RotateVector(new FVector(radius * MathF.Cos(angle), radius * MathF.Sin(angle), z)));
                    }
                    rings++;
                }
            }

            void Convex(FKConvexElem convex)
            {
                var transform = convex.Transform.Rotation.IsNormalized ? convex.Transform : FTransform.Identity; // absent, it reads as all zeros
                var usable = convex.IndexData.Length >= 3 && convex.IndexData.Length % 3 == 0;
                foreach (var index in convex.IndexData)
                {
                    usable &= index >= 0 && index < convex.VertexData.Length;
                }
                if (!usable)
                {
                    if (convex.ElemBox.IsValid != 0) Box(transform, convex.ElemBox.Min, convex.ElemBox.Max);
                    return;
                }

                var first = (uint) vertices.Count;
                foreach (var vertex in convex.VertexData)
                {
                    Add(transform.TransformPosition(vertex));
                }
                foreach (var index in convex.IndexData)
                {
                    indices.Add(first + (uint) index);
                }
            }

            void Quad(uint a, uint b, uint c, uint d)
            {
                indices.Add(a);
                indices.Add(b);
                indices.Add(c);
                indices.Add(a);
                indices.Add(c);
                indices.Add(d);
            }

            // rounded to half floats like a render vertex, so a shell lying on its mesh is exactly on it
            void Add(Vector3 position)
            {
                var p = new Vector3(position.X, position.Z, position.Y) * Settings.GlobalScale;
                vertices.Add(new Vector3((float) (Half) p.X, (float) (Half) p.Y, (float) (Half) p.Z));
            }
        }
    }
}
