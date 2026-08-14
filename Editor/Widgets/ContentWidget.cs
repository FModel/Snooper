using Editor.Managers;
using Snooper;

namespace Editor.Widgets;

public class ContentWidget : PanelWidget
{
    public override string PanelTitle => Settings.ContentWindow;
    public override PanelGroup Group => PanelGroup.Engine;

    protected override void DrawContents(EditorManager editor)
    {
    }
}
