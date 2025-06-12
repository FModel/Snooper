using System.Runtime.InteropServices;
using OpenTK.Graphics.OpenGL4;

namespace Snooper.Core.Containers.Buffers;

public abstract class Buffer<T>(int initialCapacity, BufferTarget target, BufferUsageHint usageHint) : HandledObject, IBind where T : unmanaged
{
    public abstract GetPName Name { get; }

    public int PreviousHandle { get; private set; }
    public int Count { get; private set; }
    public int Stride { get; } = Marshal.SizeOf<T>();
    public BufferTarget Target { get; } = target;
    public BufferUsageHint UsageHint { get; } = usageHint;

    private int _capacity = initialCapacity;
    private bool _bInitialized;
    private readonly Stack<int> _freeIndices = new();

    public override void Generate()
    {
        Handle = GL.GenBuffer();
    }

    public void Bind()
    {
        PreviousHandle = GL.GetInteger(Name);
        GL.BindBuffer(Target, Handle);
    }

    public void Unbind()
    {
        GL.BindBuffer(Target, PreviousHandle);
    }

    private void ResizeIfNeeded(int newSize, double factor = 1.5, bool copy = false)
    {
        if (newSize <= _capacity) return;

        newSize = (int) Math.Max(_capacity * factor, newSize);
        Console.WriteLine("Resizing buffer {0} ({1}) from {2} to {3} (initialized ? {4})", Handle, Name, _capacity, newSize, _bInitialized);
        _capacity = newSize;

        if (_bInitialized)
        {
            if (copy)
            {
                var oldBuffer = Handle;
                var oldSize = Count * Stride;

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
                Allocate();
            }
        }
    }

    public void Allocate()
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(_capacity);
        if (_bInitialized)
            throw new InvalidOperationException("Buffer is already initialized. Use Update method to modify data.");

        GL.BufferData(Target, _capacity * Stride, IntPtr.Zero, UsageHint);
        Count = 0;

        _bInitialized = true;
    }

    public void Allocate(T data) => Allocate([data]);
    public void Allocate(T[] data)
    {
        var length = data.Length;

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);
        if (_bInitialized)
            throw new InvalidOperationException("Buffer is already initialized. Use Update method to modify data.");

        if (length > _capacity)
            ResizeIfNeeded(length);

        Allocate();
        GL.BufferSubData(Target, 0, length * Stride, data);
        Count = length;

        _bInitialized = true;
    }

    public int Add(T data)
    {
        if (!_bInitialized)
        {
            Allocate(data);
            return 0;
        }

        var index = _freeIndices.Count > 0 ? _freeIndices.Pop() : Count;
        if (index >= _capacity)
        {
            ResizeIfNeeded(index + 1, copy: true);
        }

        GL.BufferSubData(Target, index * Stride, Stride, ref data);
        Count = index + 1;

        return index;
    }

    public void AddRange(T[] data)
    {
        if (data.Length == 0) return;
        if (!_bInitialized)
        {
            Allocate(data);
            return;
        }

        var index = Count * Stride;
        var newSize = Count + data.Length;
        ResizeIfNeeded(newSize, copy: true);

        GL.BufferSubData(Target, index, data.Length * Stride, data);
        Count = newSize;
    }

    public void Update(int index, T data) => Update(index, [data]);
    public void Update(int index, T[] data)
    {
        if (data.Length == 0) return;
        if (!_bInitialized) throw new InvalidOperationException("Buffer is not initialized. Use SetData method to initialize it.");
        ArgumentOutOfRangeException.ThrowIfNegative(index);

        if (index >= _capacity)
        {
            Console.WriteLine($"attempt to update index {index} in buffer {Handle} ({Name}) with capacity {_capacity}. Resizing...");
            ResizeIfNeeded(index + data.Length, copy: true);
            Count += index + data.Length - Count;
        }

        GL.BufferSubData(Target, index * Stride, data.Length * Stride, data);
    }

    public void Update(int count, nint data)
    {
        Count = count;
        ResizeIfNeeded(Count);
        GL.BufferSubData(Target, 0, Count * Stride, data);
    }

    public T[] GetData(int offset = 0, int size = -1)
    {
        if (!_bInitialized) throw new InvalidOperationException("Buffer is not initialized. Use SetData method to initialize it.");
        if (size < 0) size = Count;
        if (offset < 0 || offset + size > Count) throw new ArgumentOutOfRangeException(nameof(offset), "Offset is out of range.");

        var data = new T[size];
        GL.GetBufferSubData(Target, offset * Stride, size * Stride, data);
        return data;
    }

    public override void Dispose()
    {
        GL.DeleteBuffer(Handle);
    }
}
