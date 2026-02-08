using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.SkeletalMesh;
using CUE4Parse.UE4.Assets.Exports.StaticMesh;
using CUE4Parse.UE4.Objects.Core.Math;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using Serilog;
using Snooper.Core;
using Snooper.Core.Managers;
using Snooper.Rendering.Actors;
using Snooper.Rendering.Cache;
using Snooper.Rendering.Systems;
using Snooper.UI.Systems;

namespace Snooper;

public partial class SnooperWindow : GameWindow
{
    private readonly InterfaceSystem _interface;

    public SnooperWindow(double fps, int width, int height, bool startVisible = true) : base(
        new GameWindowSettings { UpdateFrequency = fps },
        new NativeWindowSettings
        {
            ClientSize = new OpenTK.Mathematics.Vector2i(width, height),
            WindowBorder = WindowBorder.Resizable,
#if DEBUG
            Flags = ContextFlags.ForwardCompatible | ContextFlags.Debug,
#else
            Flags = ContextFlags.ForwardCompatible,
#endif
            Profile = ContextProfile.Core,
            Vsync = VSyncMode.Adaptive,
            APIVersion = new Version(4, 6),
            StartVisible = startVisible,
            StartFocused = startVisible,
            Title = $"Snooper ({Settings.APP_SHORT_COMMIT_ID} - {Settings.APP_BUILD_DATE:MMM d, yyyy})"
        })
    {
        ActorManager.RegisterSystemFactory<SkyboxSystem>();
        ActorManager.RegisterSystemFactory<GridSystem>();
        ActorManager.RegisterSystemFactory<TransformSystem>();
        ActorManager.RegisterSystemFactory<CameraSystem>();
        ActorManager.RegisterSystemFactory<PrimitiveSystem>();
        ActorManager.RegisterSystemFactory<LandscapeSystem>();
        ActorManager.RegisterSystemFactory<RenderSystem>();
        ActorManager.RegisterSystemFactory<DeferredRenderSystem>();
        ActorManager.RegisterSystemFactory<SplineRenderSystem>();
        ActorManager.RegisterSystemFactory<BillboardSystem>();
        ActorManager.RegisterSystemFactory<TextRenderSystem>();
        ActorManager.RegisterSystemFactory<AudioSystem>();
        ActorManager.RegisterSystemFactory<ClusteredLightSystem>();
        ActorManager.RegisterSystemFactory<DebugSystem>();

        PropertyUtil.SearchPropertyInTemplate = true; // search template properties when looking for a prop via GetOrDefault and cie

        // TODO: move this into its own Editor project
        _interface = new EditorSystem(this);

        Closing += _ =>
        {
            _interface.Dispose();
            MeshCache.ClearAndDispose();
            MaterialCache.ClearAndDispose();
            Log.CloseAndFlush();
        };
    }

    public void AddToScene(UObject actor) => AddToScene(actor, FTransform.Identity);
    public void AddToScene(UObject actor, FTransform transform)
    {
        switch (actor)
        {
            case UStaticMesh staticMesh:
            {
                var mesh = new MeshActor(staticMesh, transform);
                AddToScene(mesh);
                break;
            }
            case USkeletalMesh skeletalMesh:
            {
                var mesh = new MeshActor(skeletalMesh, transform);
                AddToScene(mesh);
                break;
            }
            default:
                throw new NotImplementedException($"Actor type {actor.GetType()} is not supported.");
        }
    }

    public void AddToScene(Actor actor)
    {
        if (_interface.RootActor is null)
        {
            _interface.RootActor = actor;
        }
        else
        {
            _interface.RootActor.Children.Add(actor);
        }
    }

    protected override void OnLoad()
    {
        base.OnLoad();

        GL.Enable(EnableCap.DepthTest);
        GL.DepthFunc(DepthFunction.Less);

        GL.Enable(EnableCap.Blend);
        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

        GL.PatchParameter(PatchParameterInt.PatchVertices, 4);

        // GL.Enable(EnableCap.VertexProgramPointSize);
        // GL.StencilOp(StencilOp.Keep, StencilOp.Replace, StencilOp.Replace);
#if DEBUG
        GL.DebugMessageCallback(_debugMessageDelegate, IntPtr.Zero);
        GL.Enable(EnableCap.DebugOutput);
        GL.Enable(EnableCap.DebugOutputSynchronous);
#endif

        _interface.Load();
        OnFramebufferResize(new FramebufferResizeEventArgs(ClientSize)); // we initialize a bunch of stuff to 1x1 by default until we know the true size of the framebuffer

        CenterWindow();
        IsVisible = true;
    }

    protected override void OnUpdateFrame(FrameEventArgs args)
    {
        if (!IsFocused) return;
        base.OnUpdateFrame(args);

        var delta = (float) args.Time;
        _interface.Update(delta);
    }

    protected override void OnRenderFrame(FrameEventArgs args)
    {
        if (!IsFocused) return;
        base.OnRenderFrame(args);

        _interface.Render();

        SwapBuffers();
    }

    protected override void OnTextInput(TextInputEventArgs e)
    {
        if (!IsFocused) return;
        base.OnTextInput(e);

        _interface.TextInput((char) e.Unicode);
    }

    protected override void OnFramebufferResize(FramebufferResizeEventArgs e)
    {
        base.OnFramebufferResize(e);

        _interface.Resize(e.Width, e.Height);
    }
}
