using System.Runtime.InteropServices;
using OpenTK.Graphics.OpenGL4;

namespace Snooper.Core.Containers.Buffers;

public abstract class Buffer<T>(int size, BufferTarget target, BufferUsageHint usageHint) : IHandle, IBind where T : unmanaged
{
    public int Handle { get; private set; }
    private int _size = size;
    private int _stride { get; } = Marshal.SizeOf<T>();

    public void Generate()
    {
        Handle = GL.GenBuffer();
    }

    public void Generate(string name)
    {
        Generate();
        GL.ObjectLabel(ObjectLabelIdentifier.Buffer, Handle, name.Length, name);
    }

    public void Bind()
    {
        GL.BindBuffer(target, Handle);
    }

    public void Unbind()
    {
        GL.BindBuffer(target, 0);
    }

    public abstract bool IsBound();

    public void ResizeIfNeeded(int newSize, double factor = 1.5)
    {
        if (newSize <= _size) return;
        Resize((int) Math.Max(_size * factor, newSize));
    }

    public void Resize(int newSize)
    {
        _size = newSize;
        SetData();
    }

    public void SetData() => SetData(IntPtr.Zero);
    public void SetData(IntPtr data) => SetData(data, _size);
    public void SetData(IntPtr data, int count)
    {
        GL.BufferData(target, count * _stride, data, usageHint);
    }

    public void SetSubData(int count, nint data)
    {
        GL.BufferSubData(target, 0, count * _stride, data);
    }

    public void Dispose()
    {
        GL.DeleteBuffer(Handle);
    }
}
