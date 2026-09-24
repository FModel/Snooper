using Editor.Managers;
using ImGuiNET;
using Snooper;

namespace Editor.Widgets.Cache;

public interface ICacheTab
{
    public string Title { get; }

    public void Draw();
}

public class CacheWidget : PanelWidget
{
    public override string PanelTitle => Settings.CacheWindow;
    public override PanelGroup Group => PanelGroup.Engine;

    public override bool IsOpen { get; set; }

    private readonly ICacheTab[] _tabs =
    [
        new TextureCacheTab(),
    ];

    protected override void DrawContents(EditorManager editor)
    {
        if (!ImGui.BeginTabBar("##CacheTabs")) return;

        foreach (var tab in _tabs)
        {
            if (!ImGui.BeginTabItem(tab.Title)) continue;

            tab.Draw();
            ImGui.EndTabItem();
        }

        ImGui.EndTabBar();
    }
}
