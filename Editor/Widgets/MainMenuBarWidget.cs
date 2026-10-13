using System.Diagnostics;
using System.Numerics;
using Editor.Managers;
using ImGuiNET;
using Serilog;
using Snooper;
using Snooper.Hosting;
using Snooper.UI;

namespace Editor.Widgets;

public class MainMenuBarWidget
{
    private const string DocumentationUrl = "https://github.com/FModel/Snooper";

    private static readonly (string Group, (string Keys, string Action)[] Bindings)[] _hotkeys =
    [
        ("Camera", [
            ("Right Mouse (hold)", "Look around"),
            ("W / A / S / D", "Move forward, left, back, right"),
            ("Q / E", "Move down, up"),
            ("Shift", "Move twice as fast"),
            ("Mouse Wheel (while looking)", "Change the camera speed, five times faster with Shift"),
            ("X / C", "Widen, narrow the field of view"),
            ("W / S (orbit camera)", "Zoom in, out; A or D switches to the free camera"),
        ]),
        ("Viewport", [
            ("Double Click", "Teleport to the selection"),
        ]),
        ("Panels", [
            ("Double Click (Hierarchy / Inspector)", "Teleport to the selection"),
            ("Shift + Visibility", "Show or hide the children too"),
        ]),
        ("Window", [
            ("F11", "Toggle fullscreen"),
            ("F10", "Hide or show the interface"),
            ("Escape", "Close Snooper"),
        ]),
    ];

    private static readonly (string Name, string Licence, string Url)[] _credits =
    [
        ("CUE4Parse", "Apache 2.0", "https://github.com/FabianFG/CUE4Parse"),
        ("Font Awesome Free", "OFL 1.1, CC BY 4.0", "https://github.com/FortAwesome/Font-Awesome"),
        ("ImGui.NET", "MIT", "https://github.com/mellinoe/ImGui.NET"),
        ("Newtonsoft.Json", "MIT", "https://github.com/JamesNK/Newtonsoft.Json"),
        ("OpenTK", "MIT", "https://github.com/opentk/opentk"),
        ("Serilog", "Apache 2.0", "https://github.com/serilog/serilog"),
    ];

    private bool _showHotkeys;
    private bool _showAbout;

    public void Draw(EditorManager editor)
    {
        DrawHotkeys();
        DrawAbout();

        if (!ImGui.BeginMainMenuBar()) return;

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

    private static void DrawViewMenu(EditorManager editor)
    {
        // if (ImGui.BeginMenu($"{Settings.PaletteIcon}  Modes"))
        // {
        //     for (var i = 0; i < FragmentColorMode.Labels.Length; i++)
        //     {
        //         if (MenuToggle(FragmentColorMode.Labels[i], editor.FragmentColor == i)) editor.FragmentColor = (uint) i;
        //     }
        //     ImGui.EndMenu();
        // }

        // if (ImGui.BeginMenu($"{Settings.EyeIcon}  Show"))
        // {
        //     if (editor.GetSystem<GridSystem>() is { } grid) MenuToggle("Grid", ref grid.IsEnabled);
        //     ImGui.MenuItem("Bounds", "", false, false);
        //     ImGui.MenuItem("Skeletons", "", false, false);
        //     ImGui.MenuItem("Colliders", "", false, false);
        //     ImGui.MenuItem("Light Volumes", "", false, false);
        //     ImGui.EndMenu();
        // }

        // if (ImGui.BeginMenu($"{Settings.CameraIcon}  Camera"))
        // {
        //     ImGui.MenuItem("Perspective", "", true, false);
        //     ImGui.MenuItem("Orthographic", "", false, false);
        //     ImGui.Separator();
        //     ImGui.MenuItem("Reset Camera", "", false, false);
        //     ImGui.EndMenu();
        // }

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
        if (ImGui.MenuItem("Reset Layout"))
        {
            editor._resetLayout = true;
        }
    }

    private void DrawHelpMenu()
    {
        if (ImGui.MenuItem($"{Settings.BookIcon}  Documentation")) OpenUrl(DocumentationUrl);
        ImGui.MenuItem($"{Settings.KeyboardIcon}  Hotkeys", "", ref _showHotkeys);

        ImGui.Separator();
        ImGui.MenuItem($"{Settings.CircleInfoIcon}  About Snooper", "", ref _showAbout);
    }

    private void DrawAbout()
    {
        if (!_showAbout) return;

        var height = ImGui.GetFrameHeight();
        ImGui.SetNextWindowSize(new Vector2(height * 20, 0), ImGuiCond.FirstUseEver);
        if (ImGui.Begin($"{Settings.CircleInfoIcon}  About Snooper", ref _showAbout, ImGuiWindowFlags.NoDocking | ImGuiWindowFlags.NoCollapse))
        {
            ImGui.TextUnformatted("Snooper");
            ImGui.TextDisabled("Real-time viewer for Unreal Engine assets");
            ImGui.Spacing();

            EditorUI.PropertyValueTable("About", () =>
            {
                EditorUI.Text("Version", $"{Settings.APP_SHORT_COMMIT_ID} ({Settings.APP_BUILD_DATE:yyyy-MM-dd})");
                EditorUI.Text("Host", Bridge.Host.Name);
            }, false);

            if (ImGui.Button($"{Settings.BookIcon}  GitHub")) OpenUrl(DocumentationUrl);

            ImGui.Spacing();
            if (ImGui.CollapsingHeader("Credits"))
            {
                ImGui.TextDisabled("Snooper is licensed under the GNU GPL v3.");
                EditorUI.PropertyValueTable("Credits", () =>
                {
                    foreach (var (name, licence, url) in _credits)
                    {
                        EditorUI.Property(name);
                        if (ImGui.Selectable($"{licence}##{name}")) OpenUrl(url);
                        if (ImGui.IsItemHovered()) ImGui.SetTooltip(url);
                    }
                });
            }
        }
        ImGui.End();
    }

    private static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception e)
        {
            Log.Warning(e, "Could not open {Url}", url);
        }
    }

    private void DrawHotkeys()
    {
        if (!_showHotkeys) return;

        var height = ImGui.GetFrameHeight();
        ImGui.SetNextWindowSize(new Vector2(height * 22, height * 20), ImGuiCond.FirstUseEver);
        if (ImGui.Begin($"{Settings.KeyboardIcon}  Hotkeys", ref _showHotkeys, ImGuiWindowFlags.NoDocking | ImGuiWindowFlags.NoCollapse))
        {
            foreach (var (group, bindings) in _hotkeys)
            {
                if (!ImGui.CollapsingHeader(group, ImGuiTreeNodeFlags.DefaultOpen)) continue;

                EditorUI.PropertyValueTable(group, () =>
                {
                    foreach (var (keys, action) in bindings)
                    {
                        EditorUI.Text(keys, action);
                    }
                });
            }
        }
        ImGui.End();
    }

    private static bool MenuToggle(string label, ref bool value, string shortcut = "", bool enabled = true)
    {
        ImGui.PushItemFlag(ImGuiItemFlags.AutoClosePopups, false);
        var clicked = ImGui.MenuItem(label, shortcut, ref value, enabled);
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
