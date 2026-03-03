using System.Numerics;
using Snooper.Rendering.Components.Descriptors;
using Snooper.Rendering.Components.Mesh;
using Snooper.Rendering.Components.Primitive;

namespace Snooper.Rendering.Components.Visualization;

public class SplineMeshComponentVisualization : DebugComponent
{
    public SplineMeshComponentVisualization(SplineMeshComponent spline) : base(new Vector3(0.0f, 1.0f, 1.0f), 5, name: $"{spline.Name} (Visualization)")
    {
        Descriptor = new PrimitiveDescriptor<Vector3>(new CullingBounds(), () => new Geometry(spline.SplineParams));
    }

    private class Geometry : DebugGeometry
    {
        private const int Segments    = 16;
        private const int CircleSegs  = 12;
        private const float CircleRadius = 0.2f;
        private const float DebugLift    = 1.0f; // units above mesh surface along SplineUpDir

        public Geometry(SplineMeshParams p)
        {
            var lineVerts   = new List<Vector3>();
            var lineIndices = new List<uint>();

            // --- spline curve with offset ---
            for (var i = 0; i < Segments; i++)
            {
                var t0  = (float) i       / Segments;
                var t1  = (float)(i + 1)  / Segments;
                var idx = (uint)lineVerts.Count;
                lineVerts.Add(EvalWithOffset(p, t0));
                lineVerts.Add(EvalWithOffset(p, t1));
                lineIndices.Add(idx);
                lineIndices.Add(idx + 1);
            }

            // --- start / end circles perpendicular to spline direction ---
            AddCircle(lineVerts, lineIndices, p, 0f, CircleRadius);
            AddCircle(lineVerts, lineIndices, p, 1f, CircleRadius);

            Vertices = lineVerts.ToArray();
            Indices  = lineIndices.ToArray();
        }

        private static void AddCircle(List<Vector3> verts, List<uint> indices, SplineMeshParams p, float t, float radius)
        {
            var center  = EvalWithOffset(p, t);
            var tangent = HermiteTangent(p, t);

            var splineDir = SafeNormalize(tangent);
            var upDir     = SafeNormalize(new Vector3(p.SplineUpDir.X, p.SplineUpDir.Z, p.SplineUpDir.Y));
            var baseX     = SafeNormalize(Vector3.Cross(upDir, splineDir));
            var baseY     = SafeNormalize(Vector3.Cross(splineDir, baseX));

            // center already lifted by DebugLift via EvalWithOffset — no additional lift needed

            for (var i = 0; i < CircleSegs; i++)
            {
                var a0 = 2f * MathF.PI * i       / CircleSegs;
                var a1 = 2f * MathF.PI * (i + 1) / CircleSegs;
                var idx = (uint)verts.Count;
                verts.Add(center + radius * (MathF.Cos(a0) * baseX + MathF.Sin(a0) * baseY));
                verts.Add(center + radius * (MathF.Cos(a1) * baseX + MathF.Sin(a1) * baseY));
                indices.Add(idx);
                indices.Add(idx + 1);
            }
        }

        // Replicates the shader: evaluates spline position then applies sliceOffset in the spline frame + debug lift
        private static Vector3 EvalWithOffset(SplineMeshParams p, float t)
        {
            var pos     = HermitePos(p, t);
            var tangent = HermiteTangent(p, t);

            var splineDir = SafeNormalize(tangent);
            var upDir     = new Vector3(p.SplineUpDir.X, p.SplineUpDir.Z, p.SplineUpDir.Y); // UE→renderer

            var baseX = SafeNormalize(Vector3.Cross(upDir, splineDir));
            var baseY = SafeNormalize(Vector3.Cross(splineDir, baseX));

            var offset = Vector2.Lerp(p.StartOffset, p.EndOffset, t);

            return pos + offset.X * baseX + offset.Y * baseY + SafeNormalize(upDir) * DebugLift;
        }

        private static Vector3 HermitePos(SplineMeshParams p, float t)
        {
            var t2  = t * t;
            var t3  = t2 * t;
            var h00 =  2*t3 - 3*t2 + 1;
            var h10 =    t3 - 2*t2 + t;
            var h01 = -2*t3 + 3*t2;
            var h11 =    t3 -   t2;
            var pos = h00 * p.StartPos + h10 * p.StartTangent + h01 * p.EndPos + h11 * p.EndTangent;
            return new Vector3(pos.X, pos.Z, pos.Y); // UE Z-up → Y-up
        }

        private static Vector3 HermiteTangent(SplineMeshParams p, float t)
        {
            // derivative of Hermite: (6t²-6t)P0 + (3t²-4t+1)T0 + (-6t²+6t)P1 + (3t²-2t)T1
            var c = 6*p.StartPos   + 3*p.StartTangent + 3*p.EndTangent - 6*p.EndPos;
            var d = -6*p.StartPos  - 4*p.StartTangent - 2*p.EndTangent + 6*p.EndPos;
            var raw = c * (t*t) + d * t + p.StartTangent;
            return new Vector3(raw.X, raw.Z, raw.Y); // UE Z-up → Y-up
        }

        private static Vector3 SafeNormalize(Vector3 v)
        {
            var len = v.Length();
            return len > 1e-8f ? v / len : Vector3.Zero;
        }
    }
}
