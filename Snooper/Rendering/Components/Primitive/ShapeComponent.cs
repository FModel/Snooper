using System.Numerics;
using CUE4Parse.UE4.Assets.Exports.Component;
using CUE4Parse.UE4.Objects.Core.Math;
using Snooper.Rendering.Components.Descriptors;

namespace Snooper.Rendering.Components.Primitive;

public abstract class ShapeComponent : DebugComponent
{
    protected Vector3? Color;

    protected ShapeComponent(UShapeComponent component) : base(component)
    {
        if (component.TryGetValue(out FColor color, "ShapeColor"))
        {
            Color = new Vector3(color.R, color.G, color.B) / 255f;
        }
    }
}

public class BoxComponent : ShapeComponent
{
    public BoxComponent(UBoxComponent component) : base(component)
    {
        Color ??= new Vector3(0.45f, 0.15f, 0.15f);
                
        var extent = FVector.OneVector / 2;
        if (component.TryGetValue(out FVector boxExtent, "BoxExtent"))
        {
            extent = boxExtent * Settings.GlobalScale;
        }
                
        var bounds = new CullingBounds(extent);
        Descriptor = new PrimitiveDescriptor<Vector3>(bounds, () => new Geometry(bounds));
        
        Materials[0].DrawDataContainer = new DrawDataContainer(Color.Value);
    }
}

public class SphereComponent : ShapeComponent
{
    public SphereComponent(USphereComponent component) : base(component)
    {
        Color ??= new Vector3(0.15f, 0.45f, 0.15f);
                
        var radius = 0.5f;
        if (component.TryGetValue(out float sphereRadius, "SphereRadius"))
        {
            radius = sphereRadius * Settings.GlobalScale;
        }
                
        Descriptor = new PrimitiveDescriptor<Vector3>(new CullingBounds(radius), () => new Geometry(radius));
        
        Materials[0].DrawDataContainer = new DrawDataContainer(Color.Value);
    }
}

public class CapsuleComponent : ShapeComponent
{
    public CapsuleComponent(UCapsuleComponent component) : base(component)
    {
        Color ??= new Vector3(0.15f, 0.15f, 0.45f);
                
        var radius = 0.5f;
        if (component.TryGetValue(out float capsuleRadius, "CapsuleRadius"))
        {
            radius = capsuleRadius * Settings.GlobalScale;
        }
                
        var halfHeight = 0.5f;
        if (component.TryGetValue(out float capsuleHalfHeight, "CapsuleHalfHeight"))
        {
            halfHeight = capsuleHalfHeight * Settings.GlobalScale;
        }

        var bounds = new CullingBounds(Vector3.Zero, new Vector3(radius, halfHeight, radius), halfHeight);
        Descriptor = new PrimitiveDescriptor<Vector3>(bounds, () => new Geometry(radius, halfHeight));
        
        Materials[0].DrawDataContainer = new DrawDataContainer(Color.Value);
    }
}