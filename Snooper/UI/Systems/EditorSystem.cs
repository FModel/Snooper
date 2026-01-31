using ImGuiNET;
using OpenTK.Windowing.Desktop;
using Snooper.UI.Widgets;

namespace Snooper.UI.Systems;

public class EditorSystem : InterfaceSystem
{
    private readonly List<IWidget> _widgets = [];

    private Viewport? _mainViewport;
    private readonly ViewportSettings _viewportSettings;

    public EditorSystem(GameWindow wnd) : base(wnd)
    {
        _viewportSettings = new ViewportSettings(Renderer);

        OnSceneCameraAdded += pair =>
        {
            var viewport = new Viewport(wnd, $"Viewport##{pair.Camera.PairIndex}", pair);
            _mainViewport ??= viewport;
            _widgets.Add(viewport);
        };

        OnSceneCameraRemoved += pair =>
        {
            var viewport = _widgets.OfType<Viewport>().FirstOrDefault(v => v.Equals(pair));
            if (viewport != null)
            {
                _widgets.Remove(viewport);
                if (_mainViewport == viewport)
                {
                    _mainViewport = _widgets.OfType<Viewport>().FirstOrDefault();
                }
            }
        };

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
}
