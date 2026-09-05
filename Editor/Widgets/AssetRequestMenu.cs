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

        ImGui.BeginDisabled(!canBrowse);
        if (ImGui.MenuItem(pending ? $"{Settings.BanIcon}  Cancel Animation Request" : $"{Settings.PlayIcon}  Set Animation"))
        {
            if (pending) Bridge.CancelRequest();
            else Bridge.RequestAnimation(component);
        }
        ImGui.EndDisabled();

        if (!canBrowse && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            EditorUI.Tooltip($"{Bridge.Host.Name} cannot browse assets");
        }
    }
}
