using ImGuiNET;
using Snooper;
using Snooper.Hosting;
using Snooper.Rendering.Components.Mesh;
using Snooper.UI;

namespace Editor.Widgets;

internal static class AssetRequestMenu
{
    public static void Animation(SkeletalMeshComponent component)
    {
        var pending = Bridge.PendingRequest is { Kind: AssetRequestKind.Animation } request && request.IsFor(component);
        var canBrowse = Bridge.Host.CanBrowseAssets;
        var playing = component.Animation is not null;

        ImGui.BeginDisabled(!canBrowse);
        if (ImGui.MenuItem(pending ? $"{Settings.BanIcon}  Cancel Animation Request" : playing ? $"{Settings.PlayIcon}  Play Another Animation" : $"{Settings.PlayIcon}  Play Animation"))
        {
            if (pending) Bridge.CancelRequest();
            else Bridge.RequestAnimation(component);
        }
        ImGui.EndDisabled();

        if (!canBrowse && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            EditorUI.Tooltip($"{Bridge.Host.Name} cannot browse assets");
        }

        if (playing && ImGui.MenuItem($"{Settings.StopIcon}  Stop Playing Animation")) component.SetAnimation(null);
    }
}
