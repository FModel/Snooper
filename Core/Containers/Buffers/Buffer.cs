using System.Runtime.InteropServices;
using OpenTK.Graphics.OpenGL4;

namespace Snooper.Core.Containers.Buffers;

public abstract class Buffer<T>(int initialSize, BufferTarget target, BufferUsageHint usageHint) : HandledObject, IBind where T : unmanaged
{
    public int Size { get; private set; } = initialSize;
    public int Stride { get; } = Marshal.SizeOf<T>();
    public BufferTarget Target { get; internal set; } = target;
    public BufferUsageHint UsageHint { get; internal set; } = usageHint;

    private int _capacity = initialSize;
    private bool _bInitialized;

    public override void Generate()
    {
        Handle = GL.GenBuffer();
    }

    public void Bind()
    {
        GL.BindBuffer(Target, Handle);
    }

    private void ResizeIfNeeded(int newSize, double factor = 1.5, bool keep = false)
    {
        if (newSize <= _capacity) return;

        newSize = (int) Math.Max(_capacity * factor, newSize);
        Console.WriteLine("Resizing buffer {0} from {1} to {2} (initialized ? {3})", Handle, _capacity, newSize, _bInitialized);
        _capacity = newSize;

        if (_bInitialized)
        {
            if (keep)
            {
                var oldBuffer = Handle;
                var oldSize = Size * Stride;

                Generate();
                GL.BindBuffer(BufferTarget.CopyWriteBuffer, Handle);
                GL.BufferData(BufferTarget.CopyWriteBuffer, newSize * Stride, IntPtr.Zero, UsageHint);

                GL.BindBuffer(BufferTarget.CopyReadBuffer, oldBuffer);
                GL.CopyBufferSubData(BufferTarget.CopyReadBuffer, BufferTarget.CopyWriteBuffer, IntPtr.Zero, IntPtr.Zero, oldSize);

                GL.DeleteBuffer(oldBuffer);
                Bind();
            }
            else
            {
                _bInitialized = false;
                SetData();
            }
        }
    }

    public void SetData() => SetData(IntPtr.Zero);
    public void SetData(IntPtr data) => SetData(data, _capacity);
    public void SetData(IntPtr data, int count)
    {
        if (_bInitialized) throw new InvalidOperationException("Buffer is already initialized. Use Update method to modify data.");
        if (count > _capacity) throw new ArgumentException($"Data count {count} exceeds buffer capacity {_capacity}");
        GL.BufferData(Target, count * Stride, data, UsageHint);
        _bInitialized = true;
    }

    public void SetData(T[] data)
    {
        if (_bInitialized)
            throw new InvalidOperationException("Buffer is already initialized. Use Update method to modify data.");

        Size = data.Length;
        if (Size > _capacity)
        {
            if (_capacity > 0) throw new ArgumentException($"Data length {Size} exceeds buffer capacity {_capacity}");
            ResizeIfNeeded(Size); // buffer is empty, we implicitly resize it before setting data
        }
        GL.BufferData(Target, _capacity * Stride, data, UsageHint);
        _bInitialized = true;
    }

    public void Add(T data) => AddRange([data]);
    public void AddRange(T[] data)
    {
        if (data.Length == 0) return;
        if (!_bInitialized)
        {
            SetData(data);
            return;
        }

        var offset = Size * Stride;
        var newSize = Size + data.Length;
        ResizeIfNeeded(newSize, keep: true);

        GL.BufferSubData(Target, offset, data.Length * Stride, data);
        Size = newSize;
    }

    public void Update(T data, int offset = 0) => Update([data], offset);
    public void Update(T[] data, int offset = 0)
    {
        Size = data.Length;
        ResizeIfNeeded(Size);
        GL.BufferSubData(Target, offset * Stride, Size * Stride, data);
    }

    public void Update(int count, nint data)
    {
        Size = count;
        ResizeIfNeeded(Size);
        GL.BufferSubData(Target, 0, Size * Stride, data);
    }

    public T[] GetData(int offset = 0, int size = -1)
    {
        if (!_bInitialized) throw new InvalidOperationException("Buffer is not initialized. Use SetData method to initialize it.");
        if (size < 0) size = Size;
        if (offset < 0 || offset + size > Size) throw new ArgumentOutOfRangeException(nameof(offset), "Offset is out of range.");

        var data = new T[size];
        GL.GetBufferSubData(Target, offset * Stride, size * Stride, data);
        return data;
    }

    public override void Dispose()
    {
        GL.DeleteBuffer(Handle);
    }
}
