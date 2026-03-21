using CUE4Parse.UE4.Assets.Exports.Component;
using ImGuiNET;
using Newtonsoft.Json;
using Snooper.Core.Systems;
using Snooper.Rendering.Actors;
using Snooper.Rendering.Components.Transforms;
using Snooper.Rendering.Components.Visualization;
using Snooper.UI;
using System.Numerics;

namespace Snooper.Rendering.Components;

public abstract partial class ActorComponent
{
    private static uint _nextId = 1;
    public readonly uint Id = _nextId++;

    protected readonly string Header;
    private readonly string? _exportType;
    private readonly string? _internalType;
    private readonly string[]? _jsonProperties;

    public string Name { get; internal set; }
    public string? ObjectPath { get; protected init; }

    public bool IsSelected
    {
        get;
        internal set
        {
            if (field == value) return;

            field = value;
            UpdateIsOutlined();
        }
    }

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

    internal virtual string Icon => "\uf111";

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

    protected ActorComponent(string? name = null, string? exportType = null, string? internalType = null)
    {
        Name = name ?? Settings.NoName;
        Header = UpperCaseToSpace().Replace(GetType().Name[..^"Component".Length], " $1");

        _exportType = exportType;
        _internalType = internalType;
    }

    protected ActorComponent(UActorComponent component) : this(component.Name, component.ExportType, component.GetType().Name)
    {
        ObjectPath = component.GetPathName();

        var jsonProperties = new List<string> { JsonConvert.SerializeObject(component, Formatting.Indented) };

        var templatePtr = component.Template;
        while (templatePtr?.TryLoad(out var template) == true)
        {
            jsonProperties.Add(JsonConvert.SerializeObject(template, Formatting.Indented));
            templatePtr = template.Template;
        }

        _jsonProperties = jsonProperties.ToArray();
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

    private DirtyFlags _dirtyFlags = DirtyFlags.None;
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
        IsOutlined = IsSelected || Actor is { IsOutlined: true };
    }

    internal void DrawInterface()
    {
        if (this is not IControllable controllable) return;

        ImGui.PushID((int)Id);

        var condition = false;
        if (_exportType != null)
        {
            ImGui.Text($"Export Type: {_exportType}");
            condition = true;
        }
        if (_internalType != null)
        {
            ImGui.Text($"Internal Type: {_internalType}");
            condition = true;
        }
        if (ObjectPath != null)
        {
            if (ImGui.SmallButton("Copy Path: "))
                ImGui.SetClipboardText(ObjectPath);

            ImGui.SameLine();
            ImGui.TextWrapped(ObjectPath);
            condition = true;
        }
        if (condition)
        {
            ImGui.Spacing();
        }

        if (_jsonProperties is { Length: > 0 })
        {
            ImGui.CollapsingHeader("JSON Properties", ImGuiTreeNodeFlags.AllowOverlap | ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.Bullet | ImGuiTreeNodeFlags.NoTreePushOnOpen);

            var rectMin = ImGui.GetItemRectMin();
            var btnSize = new Vector2(ImGui.GetFrameHeight());
            var rightX = rectMin.X + ImGui.GetItemRectSize().X - ImGui.GetStyle().FramePadding.X;
            ImGui.SetCursorScreenPos(rectMin with { X = rightX - btnSize.X });

            ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 0f);
            ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 4f);
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0f, 0f, 0f, 0f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(1f, 1f, 1f, 0.08f));
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(1f, 1f, 1f, 0.15f));

            if (ImGui.Button("\uf1c9##OpenJson", btnSize))
                OnJsonRequested?.Invoke(this, _jsonProperties);

            ImGui.PopStyleColor(3);
            ImGui.PopStyleVar(2);

            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Open JSON");
        }
        controllable.DrawControls();

        ImGui.PopID();
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
}
