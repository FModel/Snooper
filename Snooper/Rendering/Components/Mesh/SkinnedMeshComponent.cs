using System.Numerics;
using CUE4Parse.UE4.Assets.Exports.Animation;
using CUE4Parse.UE4.Assets.Exports.Component.SkeletalMesh;
using CUE4Parse.UE4.Assets.Exports.SkeletalMesh;
using Snooper.Core;
using Snooper.Core.Containers.Buffers;
using Snooper.Rendering.Components.Descriptors;
using Snooper.Rendering.Components.Transforms;
using Snooper.Rendering.Systems;

namespace Snooper.Rendering.Components.Mesh;

[DefaultActorSystem(typeof(SkinnedMeshRenderSystem))]
public abstract class SkinnedMeshComponent : MeshComponent
{
    protected override DirtyFlags SupportedDirtyFlags => base.SupportedDirtyFlags | DirtyFlags.Morph;

    private float[]? _morphWeights;
    public float[] MorphWeights => _morphWeights ??= new float[Descriptor.Morphs?.Count ?? 0];
    internal BufferAllocation? _morphWeightAllocation;

    protected SkinnedMeshComponent(SkinnedMeshComponent other) : base(other)
    {
        _morphWeights = (float[]?) other._morphWeights?.Clone();
    }

    protected SkinnedMeshComponent(USkeletalMesh skeletalMesh, Transform? transform = null) : base(skeletalMesh.Materials, transform, skeletalMesh.Name)
    {
        Descriptor = PrimitiveDescriptor<Vertex>.GetOrCreate(skeletalMesh, (vertices, indices, colors, extraUvs) => new Geometry(vertices, indices, colors, extraUvs));
    }

    protected SkinnedMeshComponent(USkeleton skeleton, Transform? transform = null) : base([], transform, skeleton.Name)
    {
        Descriptor = PrimitiveDescriptor<Vertex>.GetOrCreate(skeleton, descriptor => new SkeletonGeometry(descriptor, ESkeletonShape.Adaptive));
    }

    protected SkinnedMeshComponent(USkeletalMesh skeletalMesh, USkinnedMeshComponent component) : base(skeletalMesh.Materials, component)
    {
        Descriptor = PrimitiveDescriptor<Vertex>.GetOrCreate(skeletalMesh, (vertices, indices, colors, extraUvs) => new Geometry(vertices, indices, colors, extraUvs));
    }

    private class SkeletonGeometry : Geometry
    {
        public SkeletonGeometry(SkeletonDescriptor descriptor, ESkeletonShape shape) : base(descriptor)
        {
            var count = descriptor.BoneCount;
            var matrices = descriptor.BoneMatrices;

            var vertices = new List<Vertex>(count * 48);
            var indices = new List<uint>(count * 132);
            var influences = new List<uint>(count * 72); // bone vertices carry two each
            var influenceCounts = new List<byte>(count * 48);

            const float inset = 0.0f;      // gap left at each end, small enough for the joint marker to cover it
            const float shoulder = 0.1f;    // where along an octahedron the widest cross-section sits
            const float boneGirth = 0.1f;   // octahedron half-width, as a fraction of the bone's own span
            const float stickGirth = 0.02f; // stick half-width, as a fraction of the skeleton's reference span

            // marker radius, as a fraction of the half-width of the thinnest shape meeting the joint: it tucks
            // inside a fat octahedron, stands proud of a thin stick, and carries the joint alone when no bone is drawn
            const float octahedronMarker = 0.5f;
            const float stickMarker = 1.5f;
            const float bareMarker = 2.5f;

            var edges = CollectEdges();
            var lengths = edges.Select(edge => edge.Length).ToArray();
            Array.Sort(lengths);

            var reference = lengths.Length > 0 ? lengths[lengths.Length / 2] : 0f;

            // how many children each bone has: what tells a chain link apart from a fan-out. Only drawn edges
            // count, so a child sitting on top of its parent cannot make it one
            var childCounts = new int[count];
            foreach (var edge in edges) childCounts[edge.Head]++;

            // the smallest marker any shape meeting the joint will tolerate, MaxValue where nothing is attached
            var markers = new float[count];
            Array.Fill(markers, float.MaxValue);

            if (shape is ESkeletonShape.Axes)
            {
                for (var i = 0; i < count; i++)
                {
                    AppendAxes((uint) i, reference * 0.5f, reference * stickGirth);
                }
            }

            foreach (var edge in edges)
            {
                if (shape is ESkeletonShape.Joints or ESkeletonShape.Axes)
                {
                    // no bone drawn here, so the marker carries the joint on its own
                    MarkJoint(edge, reference * stickGirth * (shape is ESkeletonShape.Joints ? bareMarker : stickMarker));
                }
                else if (IsOctahedral(edge))
                {
                    var radius = edge.Length * (1f - inset * 2f) * boneGirth;
                    AppendOctahedron(edge, radius);
                    MarkJoint(edge, radius * octahedronMarker);
                }
                else
                {
                    var radius = reference * stickGirth;
                    AppendStick(edge, radius);
                    MarkJoint(edge, radius * stickMarker);
                }
            }

            for (var i = 0; i < count; i++)
            {
                if (markers[i] < float.MaxValue) AppendBall(matrices[i].Translation, markers[i], (uint) i);
            }

            Vertices = vertices.ToArray();
            Indices = indices.ToArray();
            BoneInfluences = influences.ToArray();
            BoneInfluenceCounts = influenceCounts.ToArray();

            List<SkeletonEdge> CollectEdges()
            {
                var collected = new List<SkeletonEdge>(count);
                for (var i = 0; i < count; i++)
                {
                    var parent = descriptor.GetBoneParentIndex(i);
                    if (parent < 0) continue;

                    var origin = matrices[parent].Translation;
                    var axis = matrices[i].Translation - origin;
                    var length = axis.Length();
                    if (length < 1e-6f) continue; // coincident joints, nothing to draw

                    collected.Add(new SkeletonEdge((uint) parent, (uint) i, origin, axis / length, length));
                }
                return collected;
            }

            void AppendOctahedron(in SkeletonEdge edge, float radius)
            {
                var ringT = inset + (1f - inset * 2f) * shoulder;
                var (side, up) = Frame(edge.Direction);

                Span<Vector3> ring = stackalloc Vector3[4];
                Ring(edge, side, up, ringT, radius, ring);

                var head = edge.At(inset);
                var tail = edge.At(1f - inset);
                for (var k = 0; k < 4; k++)
                {
                    var n = (k + 1) % 4;
                    AppendTriangle(edge, head, inset, ring[k], ringT, ring[n], ringT); // head cap
                    AppendTriangle(edge, tail, 1f - inset, ring[n], ringT, ring[k], ringT); // tail cap
                }
            }

            void AppendStick(in SkeletonEdge edge, float radius)
            {
                const float headT = inset;
                const float tailT = 1f - inset;
                var (side, up) = Frame(edge.Direction);

                Span<Vector3> head = stackalloc Vector3[4];
                Span<Vector3> tail = stackalloc Vector3[4];
                Ring(edge, side, up, headT, radius, head);
                Ring(edge, side, up, tailT, radius, tail);

                for (var k = 0; k < 4; k++)
                {
                    var n = (k + 1) % 4;
                    AppendTriangle(edge, head[k], headT, head[n], headT, tail[n], tailT);
                    AppendTriangle(edge, head[k], headT, tail[n], tailT, tail[k], tailT);
                }
            }

            void AppendAxes(uint bone, float length, float radius)
            {
                var matrix = matrices[bone];
                var origin = matrix.Translation;
                Span<Vector3> axes =
                [
                    new(matrix.M11, matrix.M12, matrix.M13),
                    new(matrix.M21, matrix.M22, matrix.M23),
                    new(matrix.M31, matrix.M32, matrix.M33)
                ];

                foreach (var axis in axes)
                {
                    if (axis.Length() < 1e-6f) continue; // zero-scaled bone axis, no direction to draw
                    AppendStick(new SkeletonEdge(bone, bone, origin, Vector3.Normalize(axis), length), radius);
                }
            }

            // an octahedron points at one specific child, so it only says something where there is exactly one
            // to point at: a fan-out stacks several of them on the same joint. Long strays (IK targets, weapon
            // and attach bones) are out too, since they carry no chain to show and their girth grows with that
            // length, which is what makes them swamp the view
            bool IsOctahedral(in SkeletonEdge edge) => shape switch
            {
                ESkeletonShape.Octahedral => true,
                ESkeletonShape.Adaptive => childCounts[edge.Head] == 1 && edge.Length < reference * 2f,
                _ => false
            };

            void MarkJoint(in SkeletonEdge edge, float radius)
            {
                if (radius < markers[edge.Head]) markers[edge.Head] = radius;
                if (radius < markers[edge.Tail]) markers[edge.Tail] = radius;
            }

            static (Vector3 Side, Vector3 Up) Frame(Vector3 dir)
            {
                var reference = MathF.Abs(dir.Y) < 0.99f ? Vector3.UnitY : Vector3.UnitX;
                var side = Vector3.Normalize(Vector3.Cross(reference, dir));
                return (side, Vector3.Cross(dir, side));
            }

            static void Ring(in SkeletonEdge edge, Vector3 side, Vector3 up, float t, float radius, Span<Vector3> ring)
            {
                var center = edge.At(t);
                ring[0] = center + side * radius;
                ring[1] = center + up * radius;
                ring[2] = center - side * radius;
                ring[3] = center - up * radius;
            }

            void AppendBall(Vector3 center, float radius, uint bone)
            {
                const uint slices = 6; // longitude
                const uint stacks = 4; // latitude

                var top = (uint) vertices.Count;
                AppendVertex(center + Vector3.UnitY * radius, Vector3.UnitY, Vector3.UnitX, bone, bone, 0f);

                var first = (uint) vertices.Count;
                for (var stack = 1u; stack < stacks; stack++)
                {
                    var phi = MathF.PI * stack / stacks;
                    var y = MathF.Cos(phi);
                    var r = MathF.Sin(phi);
                    for (var slice = 0u; slice < slices; slice++)
                    {
                        var theta = MathF.Tau * slice / slices;
                        var normal = new Vector3(r * MathF.Cos(theta), y, r * MathF.Sin(theta));
                        var tangent = new Vector3(-MathF.Sin(theta), 0f, MathF.Cos(theta));
                        AppendVertex(center + normal * radius, normal, tangent, bone, bone, 0f);
                    }
                }

                var bottom = (uint) vertices.Count;
                AppendVertex(center - Vector3.UnitY * radius, -Vector3.UnitY, Vector3.UnitX, bone, bone, 0f);

                var last = first + (stacks - 2) * slices;
                for (var slice = 0u; slice < slices; slice++)
                {
                    var next = (slice + 1) % slices;
                    indices.Add(top); indices.Add(first + slice); indices.Add(first + next);
                    indices.Add(bottom); indices.Add(last + next); indices.Add(last + slice);
                }

                for (var stack = 0u; stack < stacks - 2; stack++)
                {
                    var a = first + stack * slices;
                    var b = a + slices;
                    for (var slice = 0u; slice < slices; slice++)
                    {
                        var next = (slice + 1) % slices;
                        indices.Add(a + slice); indices.Add(b + slice); indices.Add(b + next);
                        indices.Add(a + slice); indices.Add(b + next); indices.Add(a + next);
                    }
                }
            }

            void AppendTriangle(in SkeletonEdge edge, Vector3 a, float ta, Vector3 b, float tb, Vector3 c, float tc)
            {
                var normal = Vector3.Normalize(Vector3.Cross(b - a, c - a));

                var centroid = (a + b + c) / 3f;
                var onAxis = edge.Origin + edge.Direction * Vector3.Dot(centroid - edge.Origin, edge.Direction);
                if (Vector3.Dot(normal, centroid - onAxis) < 0f) normal = -normal;

                var start = (uint) vertices.Count;
                AppendVertex(a, normal, edge.Direction, edge.Head, edge.Tail, ta);
                AppendVertex(b, normal, edge.Direction, edge.Head, edge.Tail, tb);
                AppendVertex(c, normal, edge.Direction, edge.Head, edge.Tail, tc);
                indices.Add(start);
                indices.Add(start + 1);
                indices.Add(start + 2);
            }

            void AppendVertex(Vector3 position, Vector3 normal, Vector3 tangent, uint head, uint tail, float t)
            {
                vertices.Add(new Vertex(position, new Vector4(normal, 1f), tangent, Vector2.Zero, 0));

                var tailWeight = (uint) MathF.Round(Math.Clamp(t, 0f, 1f) * 255f);
                if (head == tail || tailWeight == 0)
                {
                    influences.Add((head << 16) | 0xFFu);
                    influenceCounts.Add(1);
                }
                else if (tailWeight == 255)
                {
                    influences.Add((tail << 16) | 0xFFu);
                    influenceCounts.Add(1);
                }
                else
                {
                    influences.Add((head << 16) | (255u - tailWeight));
                    influences.Add((tail << 16) | tailWeight);
                    influenceCounts.Add(2);
                }
            }
        }

        private readonly record struct SkeletonEdge(uint Head, uint Tail, Vector3 Origin, Vector3 Direction, float Length)
        {
            public Vector3 At(float t) => Origin + Direction * (Length * t);
        }
    }

    public override string Icon => "\uf5d7";
}

public enum ESkeletonShape
{
    Adaptive, // Octahedral on links that continue a chain, Stick on fan-outs and long strays
    Octahedral,
    Stick,
    Joints,
    Axes
}
