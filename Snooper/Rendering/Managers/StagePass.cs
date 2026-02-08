using OpenTK.Graphics.OpenGL4;
using Snooper.Core.Containers.Programs;
using Snooper.Core.Containers.Textures;

namespace Snooper.Rendering.Managers;

public abstract class StagePass(string name) : IDisposable
{
    private static int _currentDrawBufferIndex;
    private static readonly DrawBufferMode[] _pingPongDrawBuffers =
    [
        DrawBufferMode.ColorAttachment1,
    ];

    public string Name { get; } = name;

    public abstract void Run(IStageContext context, uint framebufferHandle, Action<Action?> render);

    public abstract void Dispose();

    protected static DrawBufferMode GetNextDrawBuffer()
    {
        var attachment = _pingPongDrawBuffers[_currentDrawBufferIndex];
        _currentDrawBufferIndex = (_currentDrawBufferIndex + 1) % _pingPongDrawBuffers.Length;
        return attachment;
    }
}

public sealed class StagePass<TContext>(string name, EmbeddedShader shader, DrawBufferMode? explicitDrawBuffer = null) : StagePass(name) where TContext : IStageContext
{
    public Texture? OutputTexture { get; init; }
    public Action<TContext, EmbeddedShader>? SetupBindings { get; init; }

    public override void Run(IStageContext ctx, uint framebufferHandle, Action<Action?> render)
    {
        if (ctx is not TContext context)
            throw new InvalidOperationException($"StagePass '{Name}' expected context of type {typeof(TContext).Name} but received {ctx.GetType().Name}.");

        var drawBuffer = explicitDrawBuffer ?? GetNextDrawBuffer();
        if (OutputTexture != null && drawBuffer != DrawBufferMode.ColorAttachment0)
        {
            var attachmentNumber = drawBuffer - DrawBufferMode.ColorAttachment0;
            var fbAttachment = FramebufferAttachment.ColorAttachment0 + attachmentNumber;
            GL.NamedFramebufferTexture(framebufferHandle, fbAttachment, OutputTexture, 0);
        }

        GL.DrawBuffer(drawBuffer);
        GL.ClearColor(0, 0, 0, 0);
        GL.Clear(ClearBufferMask.ColorBufferBit);

        render(() =>
        {
            shader.Use();
            SetupBindings?.Invoke(context, shader);
        });
    }

    public override void Dispose()
    {
        shader.Dispose();
    }
}
