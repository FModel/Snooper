using System.Numerics;
using CUE4Parse.FileProvider;
using Editor.Modals;
using OpenTK.Windowing.Desktop;
using Snooper.Rendering.Actors;
using Snooper.Rendering.Components;
using Editor.Widgets;
using Editor.Widgets.Cache;
using Editor.Widgets.Timeline;
using Snooper;
using Snooper.UI;

namespace Editor.Managers;

public class EditorManager(GameWindow wnd, IFileProvider fileProvider) : InterfaceManager(wnd, fileProvider)
{
    private readonly MainMenuBarWidget _mainMenuBar = new();
    private static readonly MaterialInspectorWidget _materialInspector = new();
    private static readonly SkeletonTreeWidget _skeletonTree = new();

    internal IReadOnlyList<IPanelWidget> Panels { get; } =
    [
        new ViewportWidget(),
        new SceneHierarchyWidget(),
        new InspectorWidget(),
        new TimelineWidget(),
        new LogWidget(),
        new SettingsWidget(),
        new CacheWidget(),
        // new ContentWidget(),
        new MorphTargetWidget(),
        _skeletonTree,
        _materialInspector,
        new TextureInspectorWidget(),
    ];

    internal readonly ViewportAxisWidget _viewportAxis = new();
    internal readonly ProfilerOverlayWidget _profilerOverlay = new();
    internal readonly HardwareOverlayWidget _hardwareOverlay = new();
    internal readonly NotificationOverlayWidget _notificationOverlay = new();
    internal readonly JsonViewerWidget _jsonViewer = new();
    internal readonly SkeletonEditorWidget _skeletonEditor = new();
    internal readonly SunOverlayWidget _sunOverlay = new();

    internal IReadOnlyList<IViewportCard> Cards => field ??= [_profilerOverlay, _hardwareOverlay];
    internal bool IsSkeletonTreeOpen => _skeletonTree.IsOpen;

    public override void Update(float delta)
    {
        base.Update(delta);

        _viewportAxis.Update(delta);
    }

    protected override void RenderInterface()
    {
        _mainMenuBar.Draw(this);

        base.RenderInterface();

        if (WindowRequests.TryTake(out var requested))
        {
            OpenPanel(requested);
        }

        foreach (var panel in Panels)
        {
            panel.Draw(this);
        }

        _jsonViewer.DrawAll();
        ExportModal.Instance.Draw();
        TexturePickerModal.Instance.Draw();
        MaterialPickerModal.Instance.Draw();
        AddComponentModal.Instance.Draw();
    }

    private void OpenPanel(string title)
    {
        foreach (var panel in Panels)
        {
            if (panel.PanelTitle != title) continue;
            panel.Focus();
            break;
        }
    }

    public override void OnViewportLeftClick(Vector2 mousePos, Vector2 windowPos, Vector2 windowSize)
    {
        base.OnViewportLeftClick(mousePos, windowPos, windowSize);

        WindowRequests.Request(Settings.SceneHierarchyWindow);
    }

    protected override void OnSelectionChanged(Actor? actor, ActorComponent? component)
    {
        _skeletonEditor.Reset();
        _sunOverlay.Reset();
        _materialInspector.Reset();
    }
}
