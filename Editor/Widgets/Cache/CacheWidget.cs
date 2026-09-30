using Editor.Managers;
using ImGuiNET;
using Snooper;

namespace Editor.Widgets.Cache;

public interface ICacheTab
{
    public string Title { get; }

    public void Draw(EditorManager editor);
}

public class CacheWidget : PanelWidget
{
    public override string PanelTitle => Settings.MemoryWindow;
    public override PanelGroup Group => PanelGroup.Engine;

    public override bool IsOpen { get; set; }

    private readonly ICacheTab[] _tabs =
    [
        new BufferTab(),
        new TextureCacheTab(),
    ];

    protected override void DrawContents(EditorManager editor)
    {
        if (!ImGui.BeginTabBar("##CacheTabs")) return;

        foreach (var tab in _tabs)
        {
            if (!ImGui.BeginTabItem(tab.Title)) continue;

            tab.Draw(editor);
            ImGui.EndTabItem();
        }

        ImGui.EndTabBar();
    }
}
