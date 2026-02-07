using System.Collections.Specialized;
using System.Numerics;
using ImGuiNET;
using OpenTK.Windowing.Desktop;
using Snooper.Rendering.Managers;
using Snooper.UI.Widgets;

namespace Snooper.UI.Systems;

public class EditorSystem : InterfaceSystem
{
    private readonly List<IWidget> _widgets = [];

    private Viewport? _mainViewport;

    public EditorSystem(GameWindow wnd) : base(wnd)
    {
        Viewports.CollectionChanged += OnViewportsCollectionChanged;

#if DEBUG
        _widgets.Add(new ImGuiDemo());
#endif
    }

    protected override void RenderInterface()
    {
        ImGui.DockSpaceOverViewport();

        if (ImGui.Begin("Render Settings"))
        {
            if (_mainViewport is null)
            {
                EditorUI.CenteredErrorText("No viewport selected");
            }
            else
            {
                DrawControls();
            }
        }
        ImGui.End();

        foreach (var widget in _widgets)
        {
            widget.Render();
        }
    }

    private void OnViewportsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                foreach (var viewport in e.NewItems!.Cast<Viewport>())
                {
                    _widgets.Add(viewport);
                    _mainViewport ??= viewport;
                }
                break;
            case NotifyCollectionChangedAction.Remove:
                foreach (var viewport in e.OldItems!.Cast<Viewport>())
                {
                    _widgets.Remove(viewport);
                    if (_mainViewport == viewport)
                    {
                        _mainViewport = Viewports.FirstOrDefault();
                    }
                }
                break;
        }
    }
}
