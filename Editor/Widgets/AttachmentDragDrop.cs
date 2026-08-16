using ImGuiNET;
using Snooper;
using Snooper.Rendering.Actors;
using Snooper.Rendering.Components;
using Snooper.Rendering.Components.Transforms;

namespace Editor.Widgets;

internal static class AttachmentDragDrop
{
    private const string ActorPayload = "SNOOPER_ACTOR";
    private const string ComponentPayload = "SNOOPER_COMPONENT";

    private const ImGuiDragDropFlags PeekFlags = ImGuiDragDropFlags.AcceptBeforeDelivery | ImGuiDragDropFlags.AcceptNoDrawDefaultRect;

    private static Actor? _draggedActor;
    private static ActorComponent? _draggedComponent;

    public static void Source(Actor actor)
    {
        if (!ImGui.BeginDragDropSource()) return;

        _draggedActor = actor;
        _draggedComponent = null;
        SetPayload(ActorPayload, actor.Id);

        ImGui.TextUnformatted($"{actor.Icon}  {actor.Name}");
        ImGui.EndDragDropSource();
    }

    public static void Source(ActorComponent component)
    {
        if (!ImGui.BeginDragDropSource()) return;

        _draggedComponent = component;
        _draggedActor = null;
        SetPayload(ComponentPayload, component.Id);

        ImGui.TextUnformatted($"{component.Icon}  {component.Name}");
        ImGui.EndDragDropSource();
    }


    public static bool ActorTarget(Actor target)
    {
        if (!ImGui.BeginDragDropTarget()) return false;

        var dropped = false;
        if (Peek(ActorPayload, out var payload) && _draggedActor is { } dragged)
        {
            dropped = Resolve(payload, dragged.Name, target.Name, CanAttach(dragged, target, out var reason), reason, () => dragged.AttachTo(target));
        }

        ImGui.EndDragDropTarget();
        return dropped;
    }

    public static bool ComponentTarget(ActorComponent target)
    {
        if (!ImGui.BeginDragDropTarget()) return false;

        var dropped = false;
        if (Peek(ActorPayload, out var actorPayload) && _draggedActor is { } draggedActor)
        {
            var can = CanAttach(draggedActor, target, out var reason);
            dropped = Resolve(actorPayload, draggedActor.Name, target.Name, can, reason,
                () => draggedActor.AttachTo(target.Actor!, (SpatialComponent) target));
        }
        else if (Peek(ComponentPayload, out var componentPayload) && _draggedComponent is { } draggedComponent)
        {
            var can = CanAttach(draggedComponent, target, out var reason);
            dropped = Resolve(componentPayload, draggedComponent.Name, target.Name, can, reason,
                () => ((SpatialComponent) draggedComponent).AttachTo((SpatialComponent) target));
        }

        ImGui.EndDragDropTarget();
        return dropped;
    }

    public static bool DetachTarget()
    {
        if (!ImGui.BeginDragDropTarget()) return false;

        var dropped = false;
        if (Peek(ActorPayload, out var payload) && _draggedActor is { } dragged)
        {
            var can = dragged.Parent != null;
            dropped = Resolve(payload, dragged.Name, "Scene Root", can, "The scene root cannot be detached", () => dragged.Detach(), highlight: false);
        }

        ImGui.EndDragDropTarget();
        return dropped;
    }

    private static unsafe void SetPayload(string type, int id) => ImGui.SetDragDropPayload(type, (nint) (&id), sizeof(int));

    private static unsafe bool Peek(string type, out ImGuiPayloadPtr payload)
    {
        payload = ImGui.AcceptDragDropPayload(type, PeekFlags);
        return payload.NativePtr != null;
    }

    private static bool Resolve(ImGuiPayloadPtr payload, string draggedName, string targetName, bool canAttach, string reason, Func<bool> attach, bool highlight = true)
    {
        ImGui.BeginTooltip();
        if (canAttach)
        {
            ImGui.TextUnformatted($"{draggedName}  \uf061  {targetName}");
        }
        else
        {
            ImGui.PushStyleColor(ImGuiCol.Text, Settings.RedColor);
            ImGui.TextUnformatted($"\uf05e  {reason}");
            ImGui.PopStyleColor();
        }
        ImGui.EndTooltip();

        if (canAttach && highlight)
        {
            ImGui.GetWindowDrawList().AddRect(ImGui.GetItemRectMin(), ImGui.GetItemRectMax(), ImGui.GetColorU32(ImGuiCol.DragDropTarget), 2f, ImDrawFlags.None, 2f);
        }

        if (!payload.Delivery) return false;

        _draggedActor = null;
        _draggedComponent = null;

        return canAttach && attach();
    }

    private static bool CanAttach(Actor dragged, Actor target, out string reason)
    {
        if (dragged.Parent is null)
        {
            reason = "The scene root cannot be attached";
            return false;
        }
        if (dragged == target)
        {
            reason = "Cannot attach an actor to itself";
            return false;
        }
        if (target.IsDescendantOf(dragged))
        {
            reason = $"{target.Name} is already below {dragged.Name}";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private static bool CanAttach(Actor dragged, ActorComponent target, out string reason)
    {
        if (target is not SpatialComponent)
        {
            reason = $"{target.Name} has no transform to attach to";
            return false;
        }
        if (target.Actor is not { } owner)
        {
            reason = $"{target.Name} has no actor";
            return false;
        }

        return CanAttach(dragged, owner, out reason);
    }

    private static bool CanAttach(ActorComponent dragged, ActorComponent target, out string reason)
    {
        if (dragged is not SpatialComponent draggedSpatial || target is not SpatialComponent targetSpatial)
        {
            reason = "Only spatial components can be attached";
            return false;
        }
        if (dragged.Actor != target.Actor)
        {
            reason = "A component cannot leave its actor";
            return false;
        }
        if (draggedSpatial == targetSpatial)
        {
            reason = "Cannot attach a component to itself";
            return false;
        }
        if (targetSpatial.IsAttachedTo(draggedSpatial))
        {
            reason = $"{target.Name} already hangs off {dragged.Name}";
            return false;
        }

        reason = string.Empty;
        return true;
    }
}
