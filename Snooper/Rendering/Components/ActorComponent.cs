using CUE4Parse_Conversion;
using CUE4Parse.UE4.Assets.Exports.Component;
using CUE4Parse.UE4.Objects.UObject;
using CUE4Parse.Utils;
using ImGuiNET;
using Snooper.Core.Systems;
using Snooper.Rendering.Actors;
using Snooper.Rendering.Components.Transforms;
using Snooper.Rendering.Components.Visualization;
using Snooper.UI;

namespace Snooper.Rendering.Components;

public abstract class ActorComponent : TreeNode
{
    protected ActorComponent(ActorComponent other) : base(other)
    {

    }

    protected ActorComponent(string? name = null) : base(name ?? Settings.NoName)
    {

    }

    protected ActorComponent(UActorComponent component) : base(component)
    {

    }

    private DebugComponent? _visualization;
    protected virtual DebugComponent? CreateDebugVisualization() => null;

    public void SetDebugVisualizationVisibility(bool visible)
    {
        if (_visualization is null)
        {
            if (!visible) return;
            if (Actor is null)
                throw new InvalidOperationException("Cannot create debug visualization for a component that is not attached to an actor.");

            _visualization = CreateDebugVisualization();
            if (_visualization is null)
                return;

            if (this is SpatialComponent relation)
                _visualization.Relation = relation;

            Actor.Components.Add(_visualization);
        }

        _visualization.IsVisible = visible;
    }

    public event Action<ActorComponent>? OnRequestSystemUpdate;

    public Actor? Actor
    {
        get;
        internal set
        {
            if (field == value) return;

            if (field != null) OnActorDetached(field);
            field = value;
            if (field != null) OnActorAttached(field);

            if (this is SpatialComponent { Relation: null } spatial)
            {
                spatial.Relation = field?.RootComponent;
            }
        }
    }

    public override void Export(ExportSession session, CancellationToken ct = default)
    {
        return; // TODO: this is presumably too expensive atm
        if (Actor?.ActorManager is not { } manager || string.IsNullOrEmpty(Path))
            return;

        // this only works for components created from UActorComponent, not those manual
        // generic exporter, entirely based on what C4P supports
        // override this in specific components for more control

        try
        {
            // Path may contain subobjects that needs to be resolved, currently only FSoftObjectPath supports that
            var parts = Path.Split(':');
            var softObject = new FSoftObjectPath(new FName(parts[0]), parts.Length > 1 ? parts[1] : string.Empty);
            if (softObject.TryLoad(manager.FileProvider, out var export))
            {
                session.Add(export);
            }
        }
        catch
        {
            //
        }
    }

    protected virtual DirtyFlags SupportedDirtyFlags => DirtyFlags.None;

    private DirtyFlags _dirtyFlags = DirtyFlags.All;
#if DEBUG
    private DirtyFlags _lastDirtyFlags = DirtyFlags.None;
    private readonly Dictionary<DirtyFlags, long> _timestamps = new();
#endif
    internal bool IsDirty(DirtyFlags flags) => (_dirtyFlags & flags) != 0;
    internal void MarkDirty(DirtyFlags flags)
    {
        var supportedFlags = flags & SupportedDirtyFlags;
        if (supportedFlags == DirtyFlags.None) return;

#if DEBUG
        for (var i = 0; i < 32; i++)
        {
            var bit = (DirtyFlags)(1 << i);
            if ((supportedFlags & bit) != DirtyFlags.None)
                _timestamps[bit] = Environment.TickCount64;
        }
        _lastDirtyFlags |= supportedFlags;
#endif
        _dirtyFlags |= supportedFlags;
        OnRequestSystemUpdate?.Invoke(this);
    }
    internal void MarkClean(DirtyFlags flags)
    {
        if (flags == DirtyFlags.All)
            _dirtyFlags = DirtyFlags.None;
        else
            _dirtyFlags &= ~flags;
    }

    protected virtual void OnActorAttached(Actor actor)
    {
        actor.OnAttachedToScene += OnActorAttachedToScene;
        actor.OnDetachedFromScene += OnActorDetachedFromScene;

        // subscribing is only enough for a component the actor already carried when it joined the scene.
        // One added afterwards has missed that event for good, and for a mesh that means never resolving
        // its materials — the sections stay null and the first indirect draw that reads them faults
        if (actor.ActorManager is { } scene) OnActorAttachedToScene(scene);
    }
    protected virtual void OnActorDetached(Actor actor)
    {
        if (actor.ActorManager is { } scene) OnActorDetachedFromScene(scene);

        actor.OnAttachedToScene -= OnActorAttachedToScene;
        actor.OnDetachedFromScene -= OnActorDetachedFromScene;
    }

    protected virtual void OnActorAttachedToScene(IGameSystem scene)
    {

    }
    protected virtual void OnActorDetachedFromScene(IGameSystem scene)
    {

    }

    private static int _nextId = 1;
    public override int Id { get; } = _nextId++;
    public override string Icon => "\uf111";
    public override void SetOutlined(bool state)
    {
        IsOutlined = state;
        // TODO: we could get rid of the backing field, if we trigger a mask buffer clear another way
    }
    public override bool ShouldScrollHere { get; set; }
    public bool IsOutlined
    {
        get;
        private set
        {
            if (field == value) return;

            field = value;
            MarkDirty(DirtyFlags.Outline);
        }
    }
    public override void DrawControls()
    {
#if DEBUG
        const long dirtyDisplayMs = 1500;
        var now = Environment.TickCount64;
        var mostRecentTs = long.MinValue;
        for (var i = 0; i < 32; i++)
        {
            var bit = (DirtyFlags)(1 << i);
            if ((_lastDirtyFlags & bit) == DirtyFlags.None) continue;

            if (!_timestamps.TryGetValue(bit, out var ts) || now - ts > dirtyDisplayMs)
                _lastDirtyFlags &= ~bit;
            else if (ts > mostRecentTs)
                mostRecentTs = ts;
        }
        var remaining = mostRecentTs != long.MinValue ? Math.Max(0.0, (dirtyDisplayMs - (now - mostRecentTs)) / 1000.0) : 0.0;

        ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetColorU32(ImGuiCol.TextDisabled));
        ImGui.SeparatorText("\uf188  Debug");
        ImGui.PopStyleColor();

        ImGui.TextDisabled("ID:");
        ImGui.SameLine();
        ImGui.Text($"{Id}");

        ImGui.TextDisabled("Dirty:");
        ImGui.SameLine();
        ImGui.PushStyleColor(ImGuiCol.Text, _lastDirtyFlags != 0 ? ImGui.GetColorU32(ImGuiCol.Text) : ImGui.GetColorU32(ImGuiCol.TextDisabled));
        ImGui.TextUnformatted(_lastDirtyFlags.ToStringBitfield());
        ImGui.PopStyleColor();
        ImGui.SameLine();
        ImGui.TextDisabled($"({remaining:F1}s)");

        ImGui.TextDisabled("Systems:");
        if (OnRequestSystemUpdate == null)
        {
            ImGui.SameLine();
            ImGui.TextDisabled("None");
        }
        else foreach (var d in OnRequestSystemUpdate.GetInvocationList())
        {
            ImGui.SameLine();
            ImGui.TextUnformatted($"{d.Target?.GetType().Name ?? "N/A"}");
        }
#endif
        ImGui.SeparatorText($"{Name} Details");
    }
}
