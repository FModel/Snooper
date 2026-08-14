using Editor.Managers;
using Snooper;

namespace Editor.Widgets;

public class WorldSettingsWidget : PanelWidget
{
    public override string PanelTitle => Settings.WorldSettingsWindow;
    public override PanelGroup Group => PanelGroup.Engine;

    protected override void DrawContents(EditorManager editor) => editor.DrawControls(); // TODO: either improve or remove
}
