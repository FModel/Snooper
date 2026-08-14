using Editor.Managers;
using ImGuiNET;
using Snooper.Core;

namespace Editor.Widgets;

public enum PanelGroup
{
    Editor,
    Engine,
    Tools,
}

public interface IPanelWidget
{
    public string PanelTitle { get; }
    public PanelGroup Group { get; }
    public bool IsOpen { get; set; }
    public bool CanClose { get; }

    public void Draw(EditorManager editor);
    public void Focus();
}

public abstract class PanelWidget : IPanelWidget
{
    public abstract string PanelTitle { get; }
    public abstract PanelGroup Group { get; }
    protected virtual ImGuiWindowFlags Flags => ImGuiWindowFlags.None;

    public virtual bool CanClose => true;
    public virtual bool IsOpen { get; set; } = true;

    public void Draw(EditorManager editor)
    {
        Tick(editor);
        if (!IsOpen) return;

        if (_focusRequested)
        {
            ImGui.SetNextWindowFocus();
            _focusRequested = false;
        }

        using (Profiler.Cpu(PanelTitle))
        {
            PushWindowStyle();
            var open = IsOpen;
            var visible = CanClose ? ImGui.Begin(PanelTitle, ref open, Flags) : ImGui.Begin(PanelTitle, Flags);
            PopWindowStyle();

            if (visible) DrawContents(editor);
            ImGui.End();

            if (CanClose) IsOpen = open;
        }
    }

    private bool _focusRequested;
    public void Focus()
    {
        IsOpen = true;
        _focusRequested = true;
    }

    protected abstract void DrawContents(EditorManager editor);

    protected virtual void Tick(EditorManager editor) { }
    protected virtual void PushWindowStyle() { }
    protected virtual void PopWindowStyle() { }
}
