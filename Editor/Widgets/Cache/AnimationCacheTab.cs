using Snooper;
using Snooper.Rendering.Cache;
using Snooper.Rendering.Components.Descriptors;
using Snooper.Rendering.Components.Descriptors.Animations;
using Snooper.UI;

namespace Editor.Widgets.Cache;

internal class AnimationCacheTab : DescriptorTab<IControllable>
{
    public override string Title => "Animations";

    protected override string[] Kinds { get; } = ["Sequence", "Composite", "Montage", "Skeleton"];
    protected override uint[] Colors { get; } = [MemoryChart.AccentColor, MemoryChart.Color(0.35f, 0.75f, 0.55f), MemoryChart.Color(0.95f, 0.65f, 0.30f), MemoryChart.Color(0.85f, 0.45f, 0.75f)];
    protected override IEnumerable<IControllable> Entries => AnimationCache.Animations.Concat<IControllable>(AnimationCache.Skeletons);

    protected override int KindOf(IControllable entry) => entry switch
    {
        MontageDescriptor => 2,
        CompositeDescriptor => 1,
        SequenceBaseDescriptor => 0,
        _ => 3
    };

    protected override string NameOf(IControllable entry) => entry switch
    {
        AnimationDescriptor animation => animation.Name,
        SkeletonDescriptor skeleton => skeleton.Name ?? Settings.NoName,
        _ => Settings.NoName
    };

    protected override string ValueOf(IControllable entry) => entry switch
    {
        SequenceBaseDescriptor animation => $"{animation.Duration:0.00} s   {animation.Segments.Count:N0} segment{(animation.Segments.Count != 1 ? "s" : "")}   {animation.Notifies.Length:N0} notif{(animation.Notifies.Length != 1 ? "ies" : "y")}",
        SkeletonDescriptor skeleton => $"{skeleton.BoneCount:N0} bones",
        _ => string.Empty
    };

    protected override void Summarize(List<IControllable> entries, MemoryChart.Tile[] tiles)
    {
        var animations = 0;
        var skeletons = 0;
        var duration = 0f;
        var notifies = 0;
        foreach (var entry in entries)
        {
            switch (entry)
            {
                case SequenceBaseDescriptor animation:
                    animations++;
                    duration += animation.Duration;
                    notifies += animation.Notifies.Length;
                    break;
                case SkeletonDescriptor:
                    skeletons++;
                    break;
            }
        }

        tiles[0] = new MemoryChart.Tile("Animations", $"{animations:N0}");
        tiles[1] = new MemoryChart.Tile("Skeletons", $"{skeletons:N0}");
        tiles[2] = new MemoryChart.Tile("Duration", $"{duration:0.00} s");
        tiles[3] = new MemoryChart.Tile("Notifies", $"{notifies:N0}");
    }
}
