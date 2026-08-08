using Snooper.Core.Systems;
using Snooper.Rendering.Components;
using Snooper.Rendering.Components.Mesh;

namespace Snooper.Rendering.Systems;

public class AnimationClockSystem : ActorSystem<SkeletalMeshComponent>
{
    public override ActorSystemType SystemType => ActorSystemType.Animation;
    public override uint Order => 6;

    private readonly HashSet<AnimationPlayback> _playbacks = [];

    protected override void OnUpdate(float delta)
    {
        base.OnUpdate(delta);

        _playbacks.Clear();
        foreach (var component in Components)
        {
            if (component.Playback is { } playback)
            {
                _playbacks.Add(playback);
            }
        }

        foreach (var playback in _playbacks)
        {
            playback.Spawn();
            playback.Advance(delta);
        }
    }
}
