using System.Runtime.InteropServices;
using OpenTK.Graphics.OpenGL4;
using Serilog;

namespace Snooper.Core.Containers.Buffers;

public readonly struct FreeBlock(int startIndex, int length)
{
    public readonly int StartIndex = startIndex;
    public readonly int Length = length;
}

public readonly struct BufferAllocation(int allocationId, int startIndex, int length)
{
    public readonly int AllocationId = allocationId;
    public readonly int StartIndex = startIndex;
    public readonly int Length = length;
    public int EndIndex => StartIndex + Length - 1;
}

public record BufferAllocationMetadata(int AllocationId, int StartIndex, int Length, DateTime CreatedAt)
{
    public DateTime? LastModified;
    public int EndIndex => StartIndex + Length - 1;
}

public abstract class Buffer<T>(BufferTarget target, BufferUsageHint usageHint, int slices = 1) : HandledObject, IBufferStatisticsProvider, IBind where T : unmanaged
{
    public abstract GetPName PName { get; }

    public int TotalElements => Capacity * Slices;
    public int PreviousHandle { get; private set; }

    public event Action<uint, uint>? OnHandleChanged;

    public int Stride { get; } = Marshal.SizeOf<T>();
    public int Slices { get; } = slices;
    public int Count { get; private set; }
    public int Capacity { get; private set; }
    public int Extent { get; private set; }

    protected bool IsAllocated;
    private readonly Dictionary<int, BufferAllocationMetadata> _allocations = new();
    private readonly SortedList<int, int> _freeBlocks = new();

    private int _allocationIdCounter;

    public override void Generate()
    {
        if (IsAllocated)
            throw new InvalidOperationException("Buffer is already initialized.");

        GL.CreateBuffers(1, out uint handle);
        Handle = handle;
        IsAllocated = false;
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

    private void ResizeIfNeeded(int newSize, bool copy = false)
    {
        if (newSize <= Capacity) return;

        var oldCapacity = Capacity;
        Capacity = IsAllocated ? Math.Max(GetGrowCapacity(), newSize) : newSize;

        if (IsAllocated)
        {
            Log.Warning("Resizing buffer {0} ({1}) from {2} to {3} (asked: {4}) (initialized!!!!!!)", Handle, PName, oldCapacity, Capacity, newSize);

            IsAllocated = false;
            if (copy)
            {
                var oldBuffer = Handle;

                Generate();
                Allocate(Capacity);

                for (var i = 0; i < Slices; i++)
                {
                    GL.CopyNamedBufferSubData(oldBuffer, Handle, i * oldCapacity * Stride, i * Capacity * Stride, oldCapacity * Stride);
                }
                GL.DeleteBuffer(oldBuffer);

                Log.Verbose("Buffer {OldBuffer} ({GetPName}) has a new handle {I}.", oldBuffer, PName, Handle);

                OnHandleChanged?.Invoke(oldBuffer, Handle);
            }
            else
            {
                Allocate(Capacity);
            }
        }
    }

    private const int SmallCount = 512; // elements
    private const int LargeCount = 4 * 1024 * 1024;
    private const double SmallGrowth = 4.0;
    private const double LargeGrowth = 1.25;

    private int GetGrowCapacity()
    {
        var t = Math.Clamp(Math.Log2((double) Capacity / SmallCount) / Math.Log2((double) LargeCount / SmallCount), 0.0, 1.0);
        var factor = Math.Exp((1.0 - t) * Math.Log(SmallGrowth) + t * Math.Log(LargeGrowth));
        return (int) Math.Min(int.MaxValue, Math.Max(Capacity + 1.0, Math.Ceiling(Capacity * factor)));
    }

    public void Reallocate(int size)
    {
        IsAllocated = false;
        Allocate(size);
    }

    public void Allocate(uint size) => Allocate((int)size);
    public void Allocate(int size)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);
        if (IsAllocated)
            throw new InvalidOperationException("Buffer is already initialized. Use Update method to modify data.");

        if (size > Capacity)
            ResizeIfNeeded(size);
        else if (size < Capacity)
            Capacity = size;

        GL.NamedBufferData(Handle, TotalElements * Stride, IntPtr.Zero, usageHint); // reserve
        ClearStorage(0, TotalElements * Stride); // zeroes

        // Count = 0;
        // _nextOffset = 0;
        // _allocationIdCounter = 0;
        // _allocations.Clear();
        // _freeBlocks.Clear();
        IsAllocated = true;
    }

    public BufferAllocation Add(T data) => AddInternal([data]);
    public BufferAllocation AddRange(T[] data) => AddInternal(data);
    private BufferAllocation AddInternal(T[] data)
    {
        var length = data.Length;
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);

        if (!IsAllocated)
        {
            Allocate(length);
        }

        var (allocationId, startIndex) = AllocateSpace(length);
        if (startIndex + length > Capacity)
        {
            ResizeIfNeeded(startIndex + length, copy: true);
        }

        GL.NamedBufferSubData(Handle, startIndex * Stride, length * Stride, data);

        var metadata = new BufferAllocationMetadata(allocationId, startIndex, length, DateTime.UtcNow);
        _allocations[allocationId] = metadata;
        Count += length;

        return new BufferAllocation(allocationId, startIndex, length);
    }

    /// <summary>
    /// TODO: we should never have to call this
    /// find another way to upsert at a specific index
    /// the index must be provided by _allocations metadata, not by the caller
    /// </summary>
    internal void Upsert(int index, T data) => UpsertInternal(index, [data]);
    internal void UpsertRange(int index, T[] data) => UpsertInternal(index, data);
    private void UpsertInternal(int index, T[] data)
    {
        var length = data.Length;
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);
        ArgumentOutOfRangeException.ThrowIfNegative(index);

        if (!IsAllocated)
        {
            Allocate(index + length);
        }

        if (index + length > Capacity)
        {
            ResizeIfNeeded(index + length, copy: true);
        }

        GL.NamedBufferSubData(Handle, index * Stride, length * Stride, data);
    }

    public void Update(BufferAllocation allocation, T data) => UpdateInternal(allocation.AllocationId, [data]);
    public void Update(BufferAllocation allocation, T[] data) => UpdateInternal(allocation.AllocationId, data);
    public void UpdateBatch(BufferAllocation startAllocation, T[] data) => UpdateInternal(startAllocation.AllocationId, data, true);
    public void Update(int allocationId, T data) => UpdateInternal(allocationId, [data]);
    public void Update(int allocationId, T[] data) => UpdateInternal(allocationId, data);
    private void UpdateInternal(int allocationId, T[] data, bool batched = false)
    {
        if (!IsAllocated)
            throw new InvalidOperationException("Buffer is not initialized. Use Add method to initialize it.");

        if (!_allocations.TryGetValue(allocationId, out var metadata))
            throw new ArgumentException($"Invalid allocation ID {allocationId}. This allocation does not exist or has been removed.", nameof(allocationId));

        var length = data.Length;
        if (!batched && length != metadata.Length)
            throw new ArgumentException($"Data length ({length}) does not match allocation length ({metadata.Length}). Cannot update with different size.", nameof(data));

        GL.NamedBufferSubData(Handle, metadata.StartIndex * Stride, length * Stride, data);

        if (!batched)
            _allocations[allocationId] = metadata with { LastModified = DateTime.UtcNow };
    }

    public void UpdateCustom<TCustom>(BufferAllocation allocation, TCustom data, int offset) where TCustom : unmanaged => UpdateCustomInternal(allocation.AllocationId, data, offset);
    private void UpdateCustomInternal<TCustom>(int allocationId, TCustom data, int offset) where TCustom : unmanaged
    {
        if (!IsAllocated)
            throw new InvalidOperationException("Buffer is not initialized. Use Add method to initialize it.");

        if (!_allocations.TryGetValue(allocationId, out var metadata))
            throw new ArgumentException($"Invalid allocation ID {allocationId}. This allocation does not exist or has been removed.", nameof(allocationId));

        GL.NamedBufferSubData(Handle, metadata.StartIndex * Stride + offset, Marshal.SizeOf<TCustom>(), ref data);

        _allocations[allocationId] = metadata with { LastModified = DateTime.UtcNow };
    }

    public void Update(int count, nint data)
    {
        Count = count;
        ResizeIfNeeded(Count);
        GL.NamedBufferSubData(Handle, 0, Count * Stride, data);
    }

    public void Remove(BufferAllocation allocation) => RemoveInternal(allocation.AllocationId);
    public void Remove(int allocationId) => RemoveInternal(allocationId);
    private void RemoveInternal(int allocationId)
    {
        if (!_allocations.TryGetValue(allocationId, out var metadata))
            throw new ArgumentException($"Invalid allocation ID {allocationId}. This allocation does not exist or has been removed.", nameof(allocationId));

        ClearStorage(metadata.StartIndex * Stride, metadata.Length * Stride);
        Free(metadata.StartIndex, metadata.Length);

        _allocations.Remove(allocationId);
        Count -= metadata.Length;
    }

    private void Free(int startIndex, int length)
    {
        _freeBlocks.Add(startIndex, length);
        var index = _freeBlocks.IndexOfKey(startIndex);

        if (index + 1 < _freeBlocks.Count && _freeBlocks.Keys[index + 1] == startIndex + length)
        {
            length += _freeBlocks.Values[index + 1];
            _freeBlocks.RemoveAt(index + 1);
            _freeBlocks[startIndex] = length;
        }

        if (index > 0 && _freeBlocks.Keys[index - 1] + _freeBlocks.Values[index - 1] == startIndex)
        {
            _freeBlocks[_freeBlocks.Keys[index - 1]] += length;
            _freeBlocks.RemoveAt(index);
        }
    }

    public void RemoveRange(BufferAllocation[] allocations)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(allocations.Length);
        foreach (var allocation in allocations)
        {
            RemoveInternal(allocation.AllocationId);
        }
    }
    public void RemoveRange(int[] allocationIds)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(allocationIds.Length);
        foreach (var allocationId in allocationIds)
        {
            RemoveInternal(allocationId);
        }
    }

    public BufferAllocation CopyFrom(Buffer<T> sourceBuffer, BufferAllocation sourceAllocation)
    {
        var length = sourceAllocation.Length;
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);

        if (!IsAllocated)
        {
            Allocate(length);
        }

        var (allocationId, startIndex) = AllocateSpace(length);
        if (startIndex + length > Capacity)
        {
            ResizeIfNeeded(startIndex + length, copy: true);
        }

        GL.CopyNamedBufferSubData(sourceBuffer.Handle, Handle, sourceAllocation.StartIndex * Stride, startIndex * Stride, length * Stride);

        var metadata = new BufferAllocationMetadata(allocationId, startIndex, length, DateTime.UtcNow);
        _allocations[allocationId] = metadata;
        Count += length;

        return new BufferAllocation(allocationId, startIndex, length);
    }

    public void Clear()
    {
        if (!IsAllocated)
            throw new InvalidOperationException("Cannot clear a buffer that is not initialized.");

        ClearStorage(0, TotalElements * Stride);
        Count = 0;
        Extent = 0;
        _allocationIdCounter = 0;
        _allocations.Clear();
        _freeBlocks.Clear();
    }

    private void ClearStorage(int offset, int size)
    {
        GL.ClearNamedBufferSubData(Handle, PixelInternalFormat.R8, offset, size, PixelFormat.Red, PixelType.UnsignedByte, IntPtr.Zero);
    }

    public override void Dispose()
    {
        GL.DeleteBuffer(Handle);
    }

    public override long Allocated => (long) TotalElements * Stride;
    public override long Used => (long) Count * Slices * Stride;

    public BufferStatistics? GetBufferStatistics()
    {
        var allocations = _allocations.Values.OrderBy(a => a.StartIndex).ToList();
        var freeBlocks = _freeBlocks.Select(block => new FreeBlock(block.Key, block.Value)).ToList();

        return new BufferStatistics(Capacity, Count, Capacity - Count, allocations, freeBlocks, CalculateFragmentation());
    }

    private (int allocationId, int startIndex) AllocateSpace(int length)
    {
        var allocationId = _allocationIdCounter++;

        // first fit, lowest address first: keeps the extent short
        for (var i = 0; i < _freeBlocks.Count; i++)
        {
            var blockLength = _freeBlocks.Values[i];
            if (blockLength < length) continue;

            var startIndex = _freeBlocks.Keys[i];
            _freeBlocks.RemoveAt(i);
            if (blockLength > length) _freeBlocks.Add(startIndex + length, blockLength - length);

            return (allocationId, startIndex);
        }

        Extent += length;
        return (allocationId, Extent - length);
    }

    private double CalculateFragmentation()
    {
        if (Capacity == 0 || _freeBlocks.Count == 0) return 0.0;

        var totalFreeSpace = _freeBlocks.Values.Sum();
        if (totalFreeSpace == 0) return 0.0;

        // Fragmentation is high when we have many small free blocks
        // Perfect score (0%) = one contiguous free block
        // Worst score (100%) = many tiny free blocks
        var largestFreeBlock = _freeBlocks.Values.Max();
        return (1.0 - (double)largestFreeBlock / totalFreeSpace) * 100.0;
    }
}
