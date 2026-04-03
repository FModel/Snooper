using CUE4Parse.UE4.Assets.Exports.Component;
using ImGuiNET;
using Snooper.Core.Systems;
using Snooper.Rendering.Actors;
using Snooper.Rendering.Components.Transforms;
using Snooper.Rendering.Components.Visualization;
using Snooper.UI;

namespace Snooper.Rendering.Components;

public abstract class ActorComponent : TreeNode
{
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

    protected virtual DirtyFlags SupportedDirtyFlags => DirtyFlags.None;

    private DirtyFlags _dirtyFlags = DirtyFlags.All;
    internal bool IsDirty(DirtyFlags flags) => (_dirtyFlags & flags) != 0;
    internal void MarkDirty(DirtyFlags flags)
    {
        var supportedFlags = flags & SupportedDirtyFlags;
        if (supportedFlags == DirtyFlags.None) return;

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
    }
    protected virtual void OnActorDetached(Actor actor)
    {
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
        ImGui.SeparatorText($"{Name} Details");
    }
}
