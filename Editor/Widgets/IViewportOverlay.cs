using Editor.Managers;
using Snooper.Rendering.Components;

namespace Editor.Widgets;

public interface IViewportTool<in T> where T : ActorComponent
{
    public bool Draw(in ViewportContext viewport, T component);
    public void Reset();
}

public interface IViewportCard
{
    public string Title { get; }
    public bool IsOpen { get; set; }

    public void Draw(EditorManager editor);
}
