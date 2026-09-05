using Editor.Managers;
using ImGuiNET;
using Snooper;
using Snooper.Core;
using Snooper.Core.Hardware;
using Snooper.Hosting;
using Snooper.Rendering;
using Snooper.Rendering.Systems;

namespace Editor.Widgets;

public class MainMenuBarWidget
{
    public void Draw(EditorManager editor)
    {
        if (!ImGui.BeginMainMenuBar()) return;

        if (ImGui.BeginMenu("File"))
        {
            DrawFileMenu(editor);
            ImGui.EndMenu();
        }

        if (ImGui.BeginMenu("View"))
        {
            DrawViewMenu(editor);
            ImGui.EndMenu();
        }

        if (ImGui.BeginMenu("Window"))
        {
            DrawWindowMenu(editor);
            ImGui.EndMenu();
        }

        if (ImGui.BeginMenu("Help"))
        {
            DrawHelpMenu();
            ImGui.EndMenu();
        }

        DrawPendingRequest();

        ImGui.EndMainMenuBar();
    }

    private static void DrawPendingRequest()
    {
        if (Bridge.PendingRequest is not { } request) return;

        var text = $"{Settings.FolderOpenIcon}  {request.DisplayText} from {Bridge.Host.Name}";
        const string cancel = $"{Settings.BanIcon}  Cancel";
        var spacing = ImGui.GetStyle().ItemSpacing.X;

        ImGui.SameLine(ImGui.GetWindowWidth() - ImGui.CalcTextSize(text).X - ImGui.CalcTextSize(cancel).X - spacing * 4.0f);
        ImGui.TextColored(Settings.OrangeColor, text);
        if (ImGui.MenuItem(cancel)) Bridge.CancelRequest();
    }

    private static void DrawFileMenu(EditorManager editor)
    {
        if (ImGui.BeginMenu($"{Settings.FolderOpenIcon}  Open Recent", false))
        {
            ImGui.EndMenu();
        }

        ImGui.Separator();
        ImGui.MenuItem($"{Settings.FileImportIcon}  Import", "Ctrl+I", false, false);
        ImGui.MenuItem($"{Settings.FileExportIcon}  Export", "Ctrl+E", false, false);

        ImGui.Separator();
        if (ImGui.MenuItem($"{Settings.PowerOffIcon}  Exit", "Alt+F4")) editor.Window.Close();
    }

    private static void DrawViewMenu(EditorManager editor)
    {
        if (ImGui.BeginMenu($"{Settings.PaletteIcon}  Modes"))
        {
            for (var i = 0; i < FragmentColorMode.Labels.Length; i++)
            {
                if (MenuToggle(FragmentColorMode.Labels[i], editor.FragmentColor == i)) editor.FragmentColor = (uint) i;
            }
            ImGui.EndMenu();
        }

        if (ImGui.BeginMenu($"{Settings.EyeIcon}  Show"))
        {
            if (editor.GetSystem<GridSystem>() is { } grid) MenuToggle("Grid", ref grid.IsEnabled);
            ImGui.MenuItem("Bounds", "", false, false);
            ImGui.MenuItem("Skeletons", "", false, false);
            ImGui.MenuItem("Colliders", "", false, false);
            ImGui.MenuItem("Light Volumes", "", false, false);
            ImGui.EndMenu();
        }

        if (ImGui.BeginMenu($"{Settings.CameraIcon}  Camera"))
        {
            ImGui.MenuItem("Perspective", "", true, false);
            ImGui.MenuItem("Orthographic", "", false, false);
            ImGui.Separator();
            ImGui.MenuItem("Reset Camera", "", false, false);
            ImGui.EndMenu();
        }

        ImGui.Separator();
        MenuToggle("Profiler", ref Profiler.Enabled);
        MenuToggle("Track Hardware Memory", ref RendererInfo.TrackMemory);

        ImGui.Separator();
        var fullscreen = editor.IsFullscreen;
        if (MenuToggle("Fullscreen", ref fullscreen, "F11")) editor.IsFullscreen = fullscreen;
    }

    private static void DrawWindowMenu(EditorManager editor)
    {
        PanelGroup? previous = null;
        foreach (var panel in editor.Panels)
        {
            if (previous is { } group && panel.Group != group) ImGui.Separator();
            previous = panel.Group;

            PanelToggle(panel);
        }

        ImGui.Separator();
        ImGui.MenuItem("Reset Layout", "", false, false);
    }

    private static void DrawHelpMenu()
    {
        ImGui.MenuItem($"{Settings.BookIcon}  Documentation", "", false, false);
        ImGui.MenuItem($"{Settings.KeyboardIcon}  Hotkeys", "", false, false);

        ImGui.Separator();
        ImGui.MenuItem($"{Settings.CircleInfoIcon}  About Snooper", "", false, false);
    }

    private static bool MenuToggle(string label, ref bool value, string shortcut = "", bool enabled = true)
    {
        ImGui.PushItemFlag(ImGuiItemFlags.AutoClosePopups, false);
        var clicked = ImGui.MenuItem(label, shortcut, ref value, enabled);
        ImGui.PopItemFlag();
        return clicked;
    }

    private static bool MenuToggle(string label, bool selected, bool enabled = true)
    {
        ImGui.PushItemFlag(ImGuiItemFlags.AutoClosePopups, false);
        var clicked = ImGui.MenuItem(label, "", selected, enabled);
        ImGui.PopItemFlag();
        return clicked;
    }

    private static void PanelToggle(IPanelWidget panel)
    {
        var open = panel.IsOpen;
        if (!MenuToggle(panel.PanelTitle, ref open, "", panel.CanClose)) return;

        if (open) panel.Focus();
        else panel.IsOpen = false;
    }
}
