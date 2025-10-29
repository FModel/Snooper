using System.Runtime.InteropServices;
using OpenTK.Graphics.OpenGL4;
using Serilog;

namespace Snooper.Core.Containers.Buffers;

public abstract class Buffer<T>(int initialCapacity, BufferTarget target, BufferUsageHint usageHint) : HandledObject, IBind where T : unmanaged
{
    public abstract GetPName PName { get; }
    public int PreviousHandle { get; private set; }
    
    public int Count { get; private set; }
    public int Stride { get; } = Marshal.SizeOf<T>();

    private int _capacity = initialCapacity;
    private bool _bInitialized;
    private readonly Stack<Range> _freeRanges = new();

    public override void Generate()
    {
        if (_bInitialized)
            throw new InvalidOperationException("Buffer is already initialized.");

        GL.CreateBuffers(1, out uint handle);
        Handle = handle;
        _bInitialized = false;
    }
    
    public void Bind()
    {
        PreviousHandle = GL.GetInteger(PName);
        GL.BindBuffer(target, Handle);
    }
    
    public void Unbind()
    {
        GL.BindBuffer(target, PreviousHandle);
    }

    private void ResizeIfNeeded(int newSize, double factor = 1.5, bool copy = false)
    {
        if (newSize <= _capacity) return;

        newSize = (int) Math.Max(_capacity * factor, newSize);
        
        var oldCapacity = _capacity;
        _capacity = newSize;

        if (_bInitialized)
        {
            Log.Verbose("Resizing buffer {0} ({1}) from {2} to {3} (initialized!!!!!!)", Handle, PName, oldCapacity, _capacity);

            if (copy)
            {
                var oldBuffer = Handle;
                var oldSize = Count * Stride;

                Generate();
                Allocate();

                GL.CopyNamedBufferSubData(oldBuffer, Handle, IntPtr.Zero, IntPtr.Zero, oldSize);
                GL.DeleteBuffer(oldBuffer);

                Log.Verbose("Buffer {OldBuffer} ({GetPName}) has a new handle {I}.", oldBuffer, PName, Handle);
            }
            else
            {
                _bInitialized = false;
                Allocate();
            }
        }
    }

    public void Allocate() => Allocate(_capacity);
    public void Allocate(uint size) => Allocate((int)size);
    public void Allocate(int size)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);
        if (_bInitialized)
            throw new InvalidOperationException("Buffer is already initialized. Use Update method to modify data.");
        
        if (size > _capacity)
            ResizeIfNeeded(size);
        else if (size < _capacity)
            _capacity = size;

        GL.NamedBufferData(Handle, _capacity * Stride, new T[_capacity], usageHint);

        Count = 0;
        _bInitialized = true;
    }

    public int Add(T data) => AddInternal([data]);
    public int AddRange(T[] data) => AddInternal(data);
    private int AddInternal(T[] data)
    {
        var length = data.Length;
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);
        
        var index = GetValidIndex(length);
        if (!_bInitialized)
        {
            Allocate(length);
        }
        else if (index + length > _capacity)
        {
            ResizeIfNeeded(index + length, copy: true);
        }

        GL.NamedBufferSubData(Handle, index * Stride, length * Stride, data);
        Count += length;

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
            Log.Verbose("attempt to insert at index {Index} in buffer {I} ({GetPName}) with capacity {Capacity}. Resizing...", index, Handle, PName, _capacity);
            ResizeIfNeeded(index + 1, copy: true);
        }

        GL.NamedBufferSubData(Handle, index * Stride, Stride, ref data);
        Count++;
    }

    public void Remove(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        if (index >= _capacity) throw new ArgumentOutOfRangeException(nameof(index), $"Cannot remove at index {index} in buffer {Handle} ({PName}) with capacity {_capacity}.");

        _freeRanges.Push(new Range(index, 1));
    }

    public virtual void RemoveRange(int[] indices)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(indices.Length);
        if (indices.Length > _capacity) throw new ArgumentOutOfRangeException(nameof(indices), $"Cannot remove range of {indices.Length} indices in buffer {Handle} ({PName}) with capacity {_capacity}.");
        
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
            throw new ArgumentOutOfRangeException(nameof(index), $"Cannot update at index {index} with size {length} in buffer {Handle} ({PName}) with capacity {_capacity}. Consider resizing the buffer.");
        }

        GL.NamedBufferSubData(Handle, index * Stride, length * Stride, data);
        if (count > Count) Count = count;
    }

    public void Update(int count, nint data)
    {
        Count = count;
        ResizeIfNeeded(Count);
        GL.NamedBufferSubData(Handle, 0, Count * Stride, data);
    }

    public T[] GetData(int index = 0, int size = -1)
    {
        if (!_bInitialized) throw new InvalidOperationException("Buffer is not initialized. Use SetData method to initialize it.");
        if (size < 0) size = Count;
        if (index < 0 || index + size > Count) throw new ArgumentOutOfRangeException(nameof(index), "Index is out of range.");

        var data = new T[size];
        GL.GetNamedBufferSubData(Handle, index * Stride, size * Stride, data);
        return data;
    }

    public override void Dispose()
    {
        GL.DeleteBuffer(Handle);
    }
    
    public override long Allocated => _capacity * Stride;
    public override long Used => Count * Stride;

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
