using System.Runtime.InteropServices;
using OpenTK.Graphics.OpenGL4;

namespace Snooper.Core.Containers.Buffers;

public abstract class Buffer<T>(int size, BufferTarget target, BufferUsageHint usageHint) : HandledObject, IBind where T : unmanaged
{
    public int Size { get; private set; } = size;
    public int Stride { get; } = Marshal.SizeOf<T>();

    protected BufferTarget Target { get; } = target;
    protected BufferUsageHint UsageHint { get; } = usageHint;

    public override void Generate()
    {
        Handle = GL.GenBuffer();
    }

    public void Bind()
    {
        GL.BindBuffer(Target, Handle);
    }

    public void ResizeIfNeeded(int newSize, double factor = 1.5)
    {
        if (newSize <= Size) return;
        Resize((int) Math.Max(Size * factor, newSize));
    }

    public void Resize(int newSize)
    {
        Size = newSize;
        SetData();
    }

    public void SetData() => SetData(IntPtr.Zero);
    public void SetData(IntPtr data) => SetData(data, Size);
    public void SetData(IntPtr data, int count)
    {
        GL.BufferData(target, count * Stride, data, usageHint);
    }

    public void SetData(T[] data)
    {
        ResizeIfNeeded(data.Length);
        GL.BufferData(target, Size * Stride, data, usageHint);
    }

    public void SetSubData(int count, nint data)
    {
        GL.BufferSubData(target, 0, count * Stride, data);
    }

    public override void Dispose()
    {
        GL.DeleteBuffer(Handle);
    }
}
