using System.Numerics;
using Snooper.Rendering.Components.Descriptors;
using Snooper.Rendering.Components.Light;
using Snooper.Rendering.Primitives;

namespace Snooper.Rendering.Components.Visualization;

public abstract class LightComponentVisualization : DebugComponent
{
    protected LightComponentVisualization(LocalLightComponent light, Vector3 color, Func<PrimitiveData> factory) : base(color, name: $"{light.Name} (Visualization)")
    {
        Descriptor = new PrimitiveDescriptor<Vector3>(new CullingBounds(), factory);
    }
}
