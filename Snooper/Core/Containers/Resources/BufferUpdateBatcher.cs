using System.Collections.Concurrent;
using Snooper.Core.Containers.Buffers;

namespace Snooper.Core.Containers.Resources;

public class BufferUpdateBatcher<T> where T : unmanaged
{
    private readonly ConcurrentDictionary<BufferAllocation, T[]> _updates = [];

    public void Add(BufferAllocation allocation, T data) => _updates[allocation] = [data];
    public void Add(BufferAllocation allocation, T[] data) => _updates[allocation] = data;

    public void Flush(Buffer<T> buffer)
    {
        if (_updates.IsEmpty) return;

        foreach (var (allocation, data) in BatchConsecutiveUpdates())
        {
            buffer.UpdateBatch(allocation, data);
        }

        Clear();
    }

    public void Clear() => _updates.Clear();

    public int Count => _updates.Count;

    private List<(BufferAllocation allocation, T[] data)> BatchConsecutiveUpdates()
    {
        if (_updates.IsEmpty) return [];

        var sorted = _updates.OrderBy(kvp => kvp.Key.StartIndex).ToList();
        var batches = new List<(BufferAllocation allocation, T[] data)>();

        var currentAllocation = sorted[0].Key;
        var currentBatch = new List<T>(sorted[0].Value);

        for (var i = 1; i < sorted.Count; i++)
        {
            var (allocation, values) = sorted[i];

            // Check if this update is consecutive to the current batch
            if (allocation.StartIndex == currentAllocation.StartIndex + currentBatch.Count)
            {
                currentBatch.AddRange(values);
                // Update the current allocation to span the combined range
                currentAllocation = new BufferAllocation(
                    currentAllocation.AllocationId,
                    currentAllocation.StartIndex,
                    currentAllocation.Length + allocation.Length
                );
            }
            else
            {
                // Finalize current batch and start a new one
                batches.Add((currentAllocation, [.. currentBatch]));
                currentAllocation = allocation;
                currentBatch = [.. values];
            }
        }

        // Add the last batch
        batches.Add((currentAllocation, [.. currentBatch]));

        return batches;
    }
}
