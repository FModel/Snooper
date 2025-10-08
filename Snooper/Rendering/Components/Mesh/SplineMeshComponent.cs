using System.Numerics;
using System.Runtime.InteropServices;
using CUE4Parse.UE4.Assets.Exports.Component.SplineMesh;
using CUE4Parse.UE4.Assets.Exports.StaticMesh;
using Snooper.Core;
using Snooper.Rendering.Components.Descriptors;
using Snooper.Rendering.Systems;

namespace Snooper.Rendering.Components.Mesh;

[StructLayout(LayoutKind.Sequential)]
public struct SplineMeshParams
{
    public Vector3 StartPos;
    public float StartRoll;
    public Vector3 StartTangent;
    public float Padding1;
    public Vector2 StartScale;
    public Vector2 StartOffset;
    
    public Vector3 EndPos;
    public float EndRoll;
    public Vector3 EndTangent;
    public float Padding2;
    public Vector2 EndScale;
    public Vector2 EndOffset;

    public Vector3 SplineUpDir;
    public float SplineBoundaryMin;
    public float SplineBoundaryMax;
    public uint ForwardAxis;
    public uint bSmoothInterpRollScale;
    public float Padding3;

    public Vector3 MeshOrigin;
    public float Padding4;
    public Vector3 MeshExtent;
    public float Padding5;

    public SplineMeshParams(USplineMeshComponent component, CullingBounds bounds)
    {
        var p = component.SplineParams;
        
        StartPos = p.StartPos * Settings.GlobalScale;
        StartRoll = p.StartRoll;
        StartTangent = p.StartTangent * Settings.GlobalScale;
        StartScale = p.StartScale;
        StartOffset = p.StartOffset;
        
        EndPos = p.EndPos * Settings.GlobalScale;
        EndRoll = p.EndRoll;
        EndTangent = p.EndTangent * Settings.GlobalScale;
        EndScale = p.EndScale;
        EndOffset = p.EndOffset;
        
        SplineUpDir = component.SplineUpDir;
        SplineBoundaryMin = component.SplineBoundaryMin;
        SplineBoundaryMax = component.SplineBoundaryMax;
        ForwardAxis = (uint)component.ForwardAxis;
        bSmoothInterpRollScale = component.bSmoothInterpRollScale ? 1u : 0u;
        
        MeshOrigin = bounds.Center;
        MeshExtent = bounds.Extents;
    }
}

[DefaultActorSystem(typeof(SplineRenderSystem))]
public class SplineMeshComponent : StaticMeshComponent
{
    public SplineMeshParams SplineParams;
    
    public SplineMeshComponent(UStaticMesh staticMesh, USplineMeshComponent component) : base(staticMesh, component)
    {
        SplineParams = new SplineMeshParams(component, Descriptor.Bounds);
    }
}