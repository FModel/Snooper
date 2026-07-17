using Snooper.Rendering.Components;
using Snooper.Rendering.Components.Camera;

namespace Snooper.Core.Systems;

public abstract class ComputeRenderSystem<TComponent> : ActorSystem<TComponent>, IComputeRenderSystem where TComponent : ActorComponent
{
    public void Execute(CameraComponent camera)
    {
        if (!IsEnabled) return;
        using (Profiler.Sample(DisplayName))
        {
            OnExecute(camera);
        }
    }

    protected abstract void OnExecute(CameraComponent camera);
}
