using System.Collections.Specialized;
using ImGuiNET;
using OpenTK.Windowing.Desktop;
using Snooper.Rendering.Managers;
using Snooper.UI.Widgets;

namespace Snooper.UI.Systems;

public class EditorSystem : InterfaceSystem
{
    private readonly List<IWidget> _widgets = [];

    private Viewport? _mainViewport;
    private readonly ViewportSettings _viewportSettings;

    public EditorSystem(GameWindow wnd) : base(wnd)
    {
        Viewports.CollectionChanged += OnViewportsCollectionChanged;

        _viewportSettings = new ViewportSettings(Renderer);

#if DEBUG
        _widgets.Add(new ImGuiDemo());
#endif
    }

    protected override void RenderInterface()
    {
        ImGui.DockSpaceOverViewport();

        _viewportSettings.Render(_mainViewport);

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
