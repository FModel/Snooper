using Snooper.Core;
using Snooper.Rendering.Primitives;
using Snooper.Rendering.Systems;

namespace Snooper.Rendering.Components;

[DefaultActorSystem(typeof(RenderSystem))]
public class StaticMeshComponent() : PrimitiveComponent(new Plane())
{
    
}