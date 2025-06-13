using System.Numerics;
using Snooper.Core;
using Snooper.Core.Containers.Resources;
using Snooper.Rendering.Primitives;
using Snooper.Rendering.Systems;

namespace Snooper.Rendering.Components;

public abstract class TPrimitiveComponent<T>(TPrimitiveData<T> primitive) : ActorComponent where T : unmanaged
{
    public int DrawId { get; private set; } = -1;

    public void Generate(IndirectResources<T> resources)
    {
        DrawId = resources.Add(primitive, GetModelMatrix());
    }

    public virtual void Update(IndirectResources<T> resources)
    {
        if (DrawId < 0)
        {
            Generate(resources);
        }
        else
        {
            resources.Update(this);
        }
    }
}

[DefaultActorSystem(typeof(PrimitiveSystem))]
public class PrimitiveComponent(IPrimitiveData primitive) : TPrimitiveComponent<Vector3>(primitive);
