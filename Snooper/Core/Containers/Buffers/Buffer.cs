using System.Runtime.InteropServices;
using OpenTK.Graphics.OpenGL4;
using Serilog;
using Snooper.Extensions;

namespace Snooper.Core.Containers.Buffers;

public abstract class Buffer<T>(int initialCapacity, BufferTarget target, BufferUsageHint usageHint) : HandledObject, IBind, IMemorySizeProvider where T : unmanaged
{
    public abstract GetPName Name { get; }

    public int PreviousHandle { get; private set; }
    public int Count { get; private set; }
    public int Stride { get; } = Marshal.SizeOf<T>();
    protected BufferTarget Target { get; } = target;

    private int _capacity = initialCapacity;
    private bool _bInitialized;
    private readonly Stack<Range> _freeRanges = new();

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
        Log.Verbose("Resizing buffer {0} ({1}) from {2} to {3} (initialized ? {4})", Handle, Name, _capacity, newSize, _bInitialized);
        _capacity = newSize;

        if (_bInitialized)
        {
            if (copy)
            {
                var oldBuffer = Handle;
                var oldSize = Count * Stride;

                var vao = 0;
                if (Target == BufferTarget.ElementArrayBuffer)
                {
                    vao = GL.GetInteger(GetPName.VertexArrayBinding);
                    if (vao != 0)
                    {
                        Unbind();
                        GL.BindVertexArray(0);
                    }
                }
                else Unbind();

                Generate();
                GL.BindBuffer(BufferTarget.CopyWriteBuffer, Handle);
                GL.BufferData(BufferTarget.CopyWriteBuffer, newSize * Stride, IntPtr.Zero, usageHint);

                GL.BindBuffer(BufferTarget.CopyReadBuffer, oldBuffer);
                GL.CopyBufferSubData(BufferTarget.CopyReadBuffer, BufferTarget.CopyWriteBuffer, IntPtr.Zero, IntPtr.Zero, oldSize);

                GL.BindBuffer(BufferTarget.CopyWriteBuffer, 0);
                GL.BindBuffer(BufferTarget.CopyReadBuffer, 0);
                GL.DeleteBuffer(oldBuffer);

                if (vao != 0)
                {
                    GL.BindVertexArray(vao);
                }
                Bind();

                Log.Verbose("Buffer {OldBuffer} ({GetPName}) has a new handle {I}.", oldBuffer, Name, Handle);
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

        GL.BufferData(Target, _capacity * Stride, IntPtr.Zero, usageHint);
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

        _bInitialized = true;
    }

    public int Add(T data)
    {
        if (!_bInitialized)
        {
            Allocate(data);
            Count = 1;
            return 0;
        }

        var index = GetValidIndex(1);
        if (index >= _capacity)
        {
            ResizeIfNeeded(index + 1, copy: true);
        }

        GL.BufferSubData(Target, index * Stride, Stride, ref data);
        if (index == Count) Count++;

        return index;
    }

    public int AddRange(T[] data)
    {
        var length = data.Length;
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);
        if (!_bInitialized)
        {
            Allocate(data);
            Count = length;
            return 0;
        }

        var index = GetValidIndex(length);
        var newSize = index + length;
        if (newSize >= _capacity)
        {
            ResizeIfNeeded(newSize, copy: true);
        }

        GL.BufferSubData(Target, index * Stride, length * Stride, data);
        if (index == Count) Count = newSize;

        return index;
    }

    public void Insert(int index, T data)
    {
        if (!_bInitialized)
        {
            if (index != 0)
                throw new ArgumentOutOfRangeException(nameof(index), $"Buffer is not initialized. Cannot insert at index {index}.");

            Add(data);
            return;
        }

        ArgumentOutOfRangeException.ThrowIfNegative(index);
        if (index >= _capacity)
        {
            Log.Verbose("attempt to insert at index {Index} in buffer {I} ({GetPName}) with capacity {Capacity}. Resizing...", index, Handle, Name, _capacity);
            ResizeIfNeeded(index + 1, copy: true);
        }

        GL.BufferSubData(Target, index * Stride, Stride, ref data);
        Count++;
    }

    public void Remove(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        if (index >= _capacity) throw new ArgumentOutOfRangeException(nameof(index), $"Cannot remove at index {index} in buffer {Handle} ({Name}) with capacity {_capacity}.");

        _freeRanges.Push(new Range(index, 1));
    }

    public virtual void RemoveRange(int[] indices)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(indices.Length);
        if (indices.Length > _capacity) throw new ArgumentOutOfRangeException(nameof(indices), $"Cannot remove range of {indices.Length} indices in buffer {Handle} ({Name}) with capacity {_capacity}.");
        
        _freeRanges.Push(new Range(indices[0], indices.Length - 1));
    }

    public void Update(int index, T data) => Update(index, [data]);
    public void Update(int index, T[] data)
    {
        var length = data.Length;
        if (length == 0) return;
        if (!_bInitialized) throw new InvalidOperationException("Buffer is not initialized. Use SetData method to initialize it.");
        ArgumentOutOfRangeException.ThrowIfNegative(index);

        var count = index + length;
        if (count > _capacity)
        {
            throw new ArgumentOutOfRangeException(nameof(index), $"Cannot update at index {index} with size {length} in buffer {Handle} ({Name}) with capacity {_capacity}. Consider resizing the buffer.");
        }

        GL.BufferSubData(Target, index * Stride, length * Stride, data);
        if (count > Count) Count = count;
    }

    public void Update(int count, nint data)
    {
        Count = count;
        ResizeIfNeeded(Count);
        GL.BufferSubData(Target, 0, Count * Stride, data);
    }

    public T[] GetData(int index = 0, int size = -1)
    {
        if (!_bInitialized) throw new InvalidOperationException("Buffer is not initialized. Use SetData method to initialize it.");
        if (size < 0) size = Count;
        if (index < 0 || index + size > Count) throw new ArgumentOutOfRangeException(nameof(index), "Index is out of range.");

        var data = new T[size];
        GL.GetBufferSubData(Target, index * Stride, size * Stride, data);
        return data;
    }
    
    public long Allocated => _capacity * Stride;
    public long Used => Count * Stride;
    public string GetFormattedSpace() => Used.GetReadableSizeOutOf(Allocated);

    public override void Dispose()
    {
        GL.DeleteBuffer(Handle);
    }

    private struct Range(int index, int length)
    {
        public readonly int Index = index;
        public readonly int Length = length;
    }

    private int GetValidIndex(int length)
    {
        var index = Count;
        if (_freeRanges.Count > 0)
        {
            var range = _freeRanges.Pop();
            if (range.Length == length)
            {
                index = range.Index;
            }
            else if (range.Length > length)
            {
                index = range.Index;
                _freeRanges.Push(new Range(index + length, range.Length - length));
            }
            else if (range.Length < length)
            {
                _freeRanges.Push(range);
            }
        }

        return index;
    }
}
