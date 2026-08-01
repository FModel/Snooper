using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Animation;

namespace Snooper.Rendering.Components.Descriptors;

public sealed class NotifyDescriptor
{
    public string Name { get; }
    public float TriggerTime { get; }
    public float Duration { get; }
    public int TrackIndex { get; }
    public UObject? Notify { get; } // TODO: we should get rid of this smh

    public bool IsState => Duration > 0f;

    public NotifyDescriptor(FAnimNotifyEvent notify)
    {
        Notify = notify.NotifyStateClass?.Load() ?? notify.Notify?.Load();
        Name = notify.NotifyName?.Text ?? Notify?.Name ?? "Notify";
        Duration = notify.Duration;
        TrackIndex = notify.TrackIndex;
        TriggerTime = notify.GetTime() + notify.TriggerTimeOffset;
    }
}
