using System.Numerics;
using System.Runtime.InteropServices;
using Snooper.Core.Containers.Buffers;
using Snooper.Core.Systems;
using Snooper.Rendering.Components.Camera;
using Snooper.Rendering.Components.Light;
using Snooper.Rendering.Containers;

namespace Snooper.Rendering.Systems;

[StructLayout(LayoutKind.Sequential)]
public struct LightData
{
    public Vector3 Position;      // World space position
    public float Range;           // Light range/radius
    public Vector3 Color;         // Light color
    public uint Type;             // 0 = point/sphere, 1 = spot
    public Vector3 Direction;     // Spot light direction (world space)
    public float SpotAngle;       // Spot light inner cone angle (cosine)
    public float SpotOuterAngle;  // Spot light outer cone angle (cosine)
    public float Intensity;       // Light intensity
    public uint Padding1;
    public uint Padding2;
}

public class LightSystem : ActorSystem<LightComponent>
{
    public override uint Order => 99;
    public override int Capacity => ClusteringConstants.MaxLights;

    private readonly ShaderStorageBuffer<LightData> _lightDataBuffer = new();
    private DirectionalLightComponent? _directionalLightComponent;

    protected override void OnLoad()
    {
        base.OnLoad();

        _lightDataBuffer.Generate();
        _lightDataBuffer.Allocate(ComponentsCount);
    }

    protected override void OnRender(CameraComponent camera)
    {

    }

    protected override void OnComponentUpdate(LightComponent component, float delta)
    {
        base.OnComponentUpdate(component, delta);

        if (component._lightDataAllocation is null)
        {
            component._lightDataAllocation = _lightDataBuffer.Add(component.GetLightData());
        }
        else
        {
            _lightDataBuffer.Update(component._lightDataAllocation.Value, component.GetLightData());
        }
    }

    protected override void OnActorComponentAdded(LightComponent component)
    {
        base.OnActorComponentAdded(component);

        if (component is DirectionalLightComponent dirLight)
        {
            _directionalLightComponent = dirLight;
        }
    }

    internal ShaderStorageBuffer<LightData> GetDataBuffer() => _lightDataBuffer;
    internal DirectionalLightComponent? GetDirectionalLight() => _directionalLightComponent;
}
