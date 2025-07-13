using Snooper.Core;
using Snooper.Rendering.Primitives;
using Snooper.Rendering.Systems;

namespace Snooper.Rendering.Components;

[DefaultActorSystem(typeof(SkyboxSystem))]
public class CubeComponent() : PrimitiveComponent(new Cube());