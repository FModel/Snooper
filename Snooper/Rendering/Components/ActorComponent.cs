using CUE4Parse.UE4.Assets.Exports.Component;
using ImGuiNET;
using Newtonsoft.Json;
using Snooper.Core.Systems;
using Snooper.Rendering.Actors;
using Snooper.Rendering.Components.Transforms;
using Snooper.Rendering.Components.Visualization;
using Snooper.UI;

namespace Snooper.Rendering.Components;

public abstract partial class ActorComponent(string? name = null, string? exportType = null) : IControllable
{
    private static int _nextId = 1;
    public int Id { get; } = _nextId++;
    public string Name { get; internal set; } = name ?? Settings.NoName;
    public string? ExportType { get; } = exportType;
    public string? ObjectPath { get; protected init; }
    public readonly string[]? JsonProperties;

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
    public event Action<ActorComponent, string[]>? OnJsonRequested;

    public void FireJsonRequested() => OnJsonRequested?.Invoke(this, JsonProperties ?? []);

    protected ActorComponent(UActorComponent component) : this(component.Name, component.ExportType)
    {
        ObjectPath = component.GetPathName();

        var jsonProperties = new List<string> { JsonConvert.SerializeObject(component, Formatting.Indented) };

        var templatePtr = component.Template;
        while (templatePtr?.TryLoad(out var template) == true)
        {
            jsonProperties.Add(JsonConvert.SerializeObject(template, Formatting.Indented));
            templatePtr = template.Template;
        }

        JsonProperties = jsonProperties.ToArray();
    }

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
        actor.OnOutlinedChanged += UpdateIsOutlined;
    }
    protected virtual void OnActorDetached(Actor actor)
    {
        actor.OnAttachedToScene -= OnActorAttachedToScene;
        actor.OnDetachedFromScene -= OnActorDetachedFromScene;
        actor.OnOutlinedChanged -= UpdateIsOutlined;
    }

    protected virtual void OnActorAttachedToScene(IGameSystem scene)
    {

    }
    protected virtual void OnActorDetachedFromScene(IGameSystem scene)
    {

    }

    private void UpdateIsOutlined()
    {
        IsOutlined = Selected || Actor is { IsOutlined: true };
    }

    public virtual void DrawControls()
    {
        ImGui.SeparatorText($"{Name} Details");
    }

    [System.Text.RegularExpressions.GeneratedRegex("(?<!^)([A-Z])")]
    private partial System.Text.RegularExpressions.Regex UpperCaseToSpace();

    public static bool operator ==(ActorComponent? left, ActorComponent? right)
    {
        if (left is null && right is null) return true;
        if (left is null || right is null) return false;
        return left.Id == right.Id;
    }
    public static bool operator !=(ActorComponent? left, ActorComponent? right) => !(left == right);

    public virtual string Icon => "\uf111";
    public bool Open { get; set; }
    public bool Selected
    {
        get;
        set
        {
            field = value;
            UpdateIsOutlined();
        }
    }
    public virtual bool ScrollToMe
    {
        get;
        set
        {
            field = value;
            if (field) Open = true;
        }
    }
    public int Depth { get; set; }
    public int Index { get; set; }
}
