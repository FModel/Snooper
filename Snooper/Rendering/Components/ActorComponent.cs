using CUE4Parse.UE4.Assets.Exports.Component;
using ImGuiNET;
using Snooper.Core.Systems;
using Snooper.Rendering.Actors;
using Snooper.Rendering.Components.Transforms;
using Snooper.UI;

namespace Snooper.Rendering.Components;

public abstract partial class ActorComponent
{
    private static uint _nextId = 1;
    public readonly uint Id = _nextId++;

    public readonly string Name;
    protected readonly string Header;
    private readonly string? _exportType;
    private readonly string? _internalType;

    public bool IsSelected { get; internal set; }

    public bool IsOutlined => IsSelected || Actor is { IsOutlined: true };
    internal virtual string Icon => "component";

    protected ActorComponent(string? name = null, string? exportType = null, string? internalType = null)
    {
        Name = name ?? Settings.NoName;
        Header = UpperCaseToSpace().Replace(GetType().Name[..^"Component".Length], " $1");

        _exportType = exportType;
        _internalType = internalType;
    }

    protected ActorComponent(UActorComponent component) : this(component.Name, component.ExportType, component.GetType().Name)
    {

    }

    private Actor? _actor;
    public Actor? Actor
    {
        get => _actor;
        internal set
        {
            if (_actor == value) return;

            if (_actor != null) OnActorDetached(_actor);
            _actor = value;
            if (_actor != null) OnActorAttached(_actor);

            if (this is SpatialComponent { Relation: null } spatial)
            {
                spatial.Relation = _actor?.RootComponent;
            }
        }
    }

    private DirtyFlags _dirtyFlags = DirtyFlags.None;
    internal bool IsDirty(DirtyFlags flags) => (_dirtyFlags & flags) != 0;
    internal void MarkDirty(DirtyFlags flags) => _dirtyFlags |= flags;
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
        if (condition)
        {
            ImGui.Spacing();
        }

        controllable.DrawControls();

        ImGui.PopID();
    }

    [System.Text.RegularExpressions.GeneratedRegex("(?<!^)([A-Z])")]
    private partial System.Text.RegularExpressions.Regex UpperCaseToSpace();
}
