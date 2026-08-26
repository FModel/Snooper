using ImGuiNET;
using OpenTK.Graphics.OpenGL4;

namespace Snooper.UI;

public sealed class ImGuiDrawCallbacks
{
    internal const nint Marker = 1;

    public static ImGuiDrawCallbacks Instance { get; } = new();

    private ImGuiDrawCallbacks() { }

    private readonly List<Action> _pending = [];

    private Action<int>? _isolateChannel;
    private Action<bool>? _encodeSrgb;

    internal void Bind(Action<int> isolateChannel, Action<bool> encodeSrgb)
    {
        _isolateChannel = isolateChannel;
        _encodeSrgb = encodeSrgb;
        Clear();
    }

    private void Add(ImDrawListPtr drawList, Action action)
    {
        drawList.AddCallback(Marker, _pending.Count);
        _pending.Add(action);
    }

    private Scope Begin(ImDrawListPtr drawList, Action enter, Action exit)
    {
        Add(drawList, enter);
        return new Scope(drawList, exit);
    }

    public Scope IsolateChannel(ImDrawListPtr drawList, int channel)
    {
        if (channel < 0 || _isolateChannel is not { } isolate) return default;
        return Begin(drawList, () => isolate(channel), () => isolate(-1));
    }

    public Scope EncodeSrgb(ImDrawListPtr drawList, bool enabled = true)
    {
        if (!enabled || _encodeSrgb is not { } encode) return default;
        return Begin(drawList, () => encode(true), () => encode(false));
    }

    public Scope IgnoreAlpha(ImDrawListPtr drawList, bool enabled = true)
    {
        if (!enabled) return default;
        return Begin(drawList,
            () => GL.BlendFunc(BlendingFactor.One, BlendingFactor.Zero),
            () => GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha));
    }

    internal void Invoke(int index)
    {
        if (index >= 0 && index < _pending.Count) _pending[index]();
    }

    internal void Clear() => _pending.Clear();

    public readonly struct Scope(ImDrawListPtr drawList, Action? exit) : IDisposable
    {
        public void Dispose()
        {
            if (exit is not null) Instance.Add(drawList, exit);
        }
    }
}
