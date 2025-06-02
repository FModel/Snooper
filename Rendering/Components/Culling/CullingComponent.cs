using Snooper.Core;
using Snooper.Rendering.Systems;

namespace Snooper.Rendering.Components.Culling;

[DefaultActorSystem(typeof(CullingSystem))]
public abstract class CullingComponent : ActorComponent;
