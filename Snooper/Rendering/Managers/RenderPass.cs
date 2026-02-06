namespace Snooper.Rendering.Managers;

public abstract class RenderPass(string name)
{
    public string Name { get; } = name;

    public abstract void Run(IRenderContext context);
}

public sealed class RenderPass<TContext>(string name) : RenderPass(name) where TContext : IRenderContext
{
    public Func<TContext, bool>? CanRun { get; init; }

    public Action<TContext>? PrePass { get; init; }
    public Action<TContext>? Execute { get; init; }
    public Action<TContext>? PostPass { get; init; }

    public override void Run(IRenderContext ctx)
    {
        if (ctx is not TContext context)
            throw new InvalidOperationException($"RenderPass '{Name}' expected context of type {typeof(TContext).Name} but received {ctx.GetType().Name}.");

        if (CanRun != null && !CanRun(context))
            return;

        PrePass?.Invoke(context);
        Execute?.Invoke(context);
        PostPass?.Invoke(context);
    }
}
