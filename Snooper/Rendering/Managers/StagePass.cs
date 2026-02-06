using System.Numerics;
using OpenTK.Graphics.OpenGL4;
using Snooper.Core.Containers.Programs;

namespace Snooper.Rendering.Managers;

public abstract class StagePass(string name) : IDisposable
{
    public string Name { get; } = name;

    public abstract void Run(IStageContext context, Action<Action?> render);

    public abstract void Dispose();
}

public sealed class StagePass<TContext>(string name, EmbeddedShader shader, Vector4 background, ClearBufferMask buffer, DrawBufferMode attachment) : StagePass(name) where TContext : IStageContext
{
    public Func<TContext, bool>? CanRun { get; init; }
    public Action<TContext, EmbeddedShader>? SetupBindings { get; init; }

    public override void Run(IStageContext ctx, Action<Action?> render)
    {
        if (ctx is not TContext context)
            throw new InvalidOperationException($"StagePass '{Name}' expected context of type {typeof(TContext).Name} but received {ctx.GetType().Name}.");

        if (CanRun != null && !CanRun(context))
            return;

        GL.DrawBuffer(attachment);
        GL.ClearColor(background.X, background.Y, background.Z, background.W);
        GL.Clear(buffer);

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
