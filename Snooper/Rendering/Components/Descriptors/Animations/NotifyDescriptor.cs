using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Animation;
using CUE4Parse.UE4.Objects.UObject;

namespace Snooper.Rendering.Components.Descriptors.Animations;

public sealed class NotifyDescriptor
{
    public readonly string Name;
    public readonly float TriggerTime;
    public readonly float Duration;
    public readonly int TrackIndex;

    public bool IsState => Duration > 0f;

    private FPackageIndex? _notify;

    public NotifyDescriptor(FAnimNotifyEvent notify, bool load)
    {
        if (load)
        {
            _notify = notify.NotifyStateClass ?? notify.Notify;
        }

        Name = notify.NotifyName?.Text ?? "Notify";
        Duration = notify.Duration;
        TrackIndex = notify.TrackIndex;
        TriggerTime = notify.GetTime() + notify.TriggerTimeOffset;
    }

    internal UObject? Consume()
    {
        var notify = _notify;
        _notify = null;
        return notify?.Load();
    }
}
