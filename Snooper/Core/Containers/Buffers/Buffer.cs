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

public record BufferAllocationMetadata(
    int AllocationId,
    int StartIndex,
    int Length,
    DateTime CreatedAt,
    DateTime? LastModified = null
)
{
    public int EndIndex => StartIndex + Length - 1;
}

public abstract class Buffer<T>(BufferTarget target, BufferUsageHint usageHint) : HandledObject, IBufferStatisticsProvider, IBind where T : unmanaged
{
    public abstract GetPName PName { get; }
    public int PreviousHandle { get; private set; }

    public event Action<uint, uint>? OnHandleChanged;

    public int Stride { get; } = Marshal.SizeOf<T>();
    public int Count { get; private set; }
    public int Capacity { get; private set; }

    private bool _bInitialized;
    private readonly Dictionary<int, BufferAllocationMetadata> _allocations = new();
    private readonly SortedSet<FreeBlock> _freeBlocks = new(Comparer<FreeBlock>.Create((a, b) =>
    {
        var sizeCompare = a.Length.CompareTo(b.Length);
        return sizeCompare != 0 ? sizeCompare : a.StartIndex.CompareTo(b.StartIndex);
    }));
    private int _nextOffset;
    private int _allocationIdCounter;

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
        if (newSize <= Capacity) return;

        newSize = (int) Math.Max(Capacity * factor, newSize);

        var oldCapacity = Capacity;
        Capacity = newSize;

        if (_bInitialized)
        {
            Log.Warning("Resizing buffer {0} ({1}) from {2} to {3} (initialized!!!!!!)", Handle, PName, oldCapacity, Capacity);

            _bInitialized = false;
            if (copy)
            {
                var oldBuffer = Handle;

                Generate();
                Allocate(Capacity);

                GL.CopyNamedBufferSubData(oldBuffer, Handle, 0, 0, oldCapacity * Stride);
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

    public void Reallocate(int size)
    {
        _bInitialized = false;
        Allocate(size);
    }

    public void Allocate(uint size) => Allocate((int)size);
    public void Allocate(int size)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);
        if (_bInitialized)
            throw new InvalidOperationException("Buffer is already initialized. Use Update method to modify data.");

        if (size > Capacity)
            ResizeIfNeeded(size);
        else if (size < Capacity)
            Capacity = size;

        GL.NamedBufferData(Handle, Capacity * Stride, new T[Capacity], usageHint);

        // Count = 0;
        // _nextOffset = 0;
        // _allocationIdCounter = 0;
        // _allocations.Clear();
        // _freeBlocks.Clear();
        _bInitialized = true;
    }

    public BufferAllocation Add(T data) => AddInternal([data]);
    public BufferAllocation AddRange(T[] data) => AddInternal(data);
    private BufferAllocation AddInternal(T[] data)
    {
        var length = data.Length;
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);

        if (!_bInitialized)
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

    public void Update(BufferAllocation allocation, T data) => UpdateInternal(allocation.AllocationId, [data]);
    public void Update(BufferAllocation allocation, T[] data) => UpdateInternal(allocation.AllocationId, data);
    public void UpdateBatch(BufferAllocation startAllocation, T[] data) => UpdateInternal(startAllocation.AllocationId, data, true);
    public void Update(int allocationId, T data) => UpdateInternal(allocationId, [data]);
    public void Update(int allocationId, T[] data) => UpdateInternal(allocationId, data);
    private void UpdateInternal(int allocationId, T[] data, bool batched = false)
    {
        if (!_bInitialized)
            throw new InvalidOperationException("Buffer is not initialized. Use Add method to initialize it.");

        if (!_allocations.TryGetValue(allocationId, out var metadata))
            throw new ArgumentException($"Invalid allocation ID {allocationId}. This allocation does not exist or has been removed.", nameof(allocationId));

        var length = data.Length;
        if (!batched && length != metadata.Length)
            throw new ArgumentException($"Data length ({length}) does not match allocation length ({metadata.Length}). Cannot update with different size.", nameof(data));

        GL.NamedBufferSubData(Handle, metadata.StartIndex * Stride, length * Stride, data);

        _allocations[allocationId] = metadata with { LastModified = DateTime.UtcNow };
    }

    public void UpdateCustom<TCustom>(BufferAllocation allocation, TCustom data, int offset) where TCustom : unmanaged => UpdateCustomInternal(allocation.AllocationId, data, offset);
    private void UpdateCustomInternal<TCustom>(int allocationId, TCustom data, int offset) where TCustom : unmanaged
    {
        if (!_bInitialized)
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

        GL.NamedBufferSubData(Handle, metadata.StartIndex * Stride, metadata.Length * Stride, new T[metadata.Length]);

        _freeBlocks.Add(new FreeBlock(metadata.StartIndex, metadata.Length));
        MergeAdjacentFreeBlocks();

        _allocations.Remove(allocationId);
        Count -= metadata.Length;
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

    public override void Dispose()
    {
        GL.DeleteBuffer(Handle);
    }

    public override long Allocated => Capacity * Stride;
    public override long Used => Count * Stride;
    public BufferStatistics GetBufferStatistics()
    {
        var allocations = _allocations.Values.OrderBy(a => a.StartIndex).ToList();
        var freeBlocks = _freeBlocks.OrderBy(fb => fb.StartIndex).ToList();

        return new BufferStatistics(
            Capacity: Capacity,
            UsedItems: Count,
            FreeItems: Capacity - Count,
            Allocations: allocations,
            FreeBlocks: freeBlocks,
            FragmentationPercentage: CalculateFragmentation()
        );
    }

    private (int allocationId, int startIndex) AllocateSpace(int length)
    {
        var allocationId = _allocationIdCounter++;

        FreeBlock? suitableBlock = null;
        foreach (var block in _freeBlocks)
        {
            if (block.Length >= length)
            {
                suitableBlock = block;
                break;
            }
        }

        int startIndex;
        if (suitableBlock.HasValue)
        {
            startIndex = suitableBlock.Value.StartIndex;
            _freeBlocks.Remove(suitableBlock.Value);

            // If the block is larger than needed, split it
            if (suitableBlock.Value.Length > length)
            {
                var remainingBlock = new FreeBlock(
                    startIndex + length,
                    suitableBlock.Value.Length - length
                );
                _freeBlocks.Add(remainingBlock);
            }
        }
        else
        {
            startIndex = _nextOffset;
            _nextOffset += length;
        }

        return (allocationId, startIndex);
    }

    private void MergeAdjacentFreeBlocks()
    {
        var sortedBlocks = _freeBlocks.OrderBy(fb => fb.StartIndex).ToList();
        _freeBlocks.Clear();

        for (var i = 0; i < sortedBlocks.Count; i++)
        {
            var current = sortedBlocks[i];

            // try to merge with subsequent blocks
            while (i + 1 < sortedBlocks.Count && current.StartIndex + current.Length == sortedBlocks[i + 1].StartIndex)
            {
                current = new FreeBlock(current.StartIndex, current.Length + sortedBlocks[i + 1].Length);
                i++;
            }

            _freeBlocks.Add(current);
        }
    }

    private double CalculateFragmentation()
    {
        if (Capacity == 0 || _freeBlocks.Count == 0) return 0.0;

        var totalFreeSpace = _freeBlocks.Sum(fb => fb.Length);
        if (totalFreeSpace == 0) return 0.0;

        // Fragmentation is high when we have many small free blocks
        // Perfect score (0%) = one contiguous free block
        // Worst score (100%) = many tiny free blocks
        var largestFreeBlock = _freeBlocks.Max(fb => fb.Length);
        return (1.0 - (double)largestFreeBlock / totalFreeSpace) * 100.0;
    }
}
