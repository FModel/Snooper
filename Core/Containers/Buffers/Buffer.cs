using System.Runtime.InteropServices;
using OpenTK.Graphics.OpenGL4;

namespace Snooper.Core.Containers.Buffers;

public abstract class Buffer<T>(int size, BufferTarget target, BufferUsageHint usageHint) : Object, IBind where T : unmanaged
{
    private int _size = size;
    public readonly int Stride = Marshal.SizeOf<T>();

    public override void Generate()
    {
        Handle = GL.GenBuffer();
    }

    public void Bind()
    {
        GL.BindBuffer(target, Handle);
    }

    public void Unbind()
    {
        GL.BindBuffer(target, 0);
    }

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
        if (!CanExecute()) throw new Exception("trying to set data on a buffer that is not yet bound");
        GL.BufferData(target, count * Stride, data, usageHint);
    }

    public void SetSubData(int count, nint data)
    {
        if (!CanExecute()) throw new Exception("trying to set sub data on a buffer that is not yet bound");
        GL.BufferSubData(target, 0, count * Stride, data);
    }

    public override void Dispose()
    {
        GL.DeleteBuffer(Handle);
    }
}
