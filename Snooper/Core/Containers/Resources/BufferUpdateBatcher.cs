using Snooper.Core.Containers.Buffers;

namespace Snooper.Core.Containers.Resources;

public class BufferUpdateBatcher<T> where T : unmanaged
{
    private readonly Dictionary<int, T[]> _updates = [];
    
    public void Add(int offset, T data) => _updates[offset] = [data];
    public void Add(int offset, T[] data) => _updates[offset] = data;
    
    public void Flush(Buffer<T> buffer)
    {
        if (Count == 0) return;
        
        buffer.Bind();
        foreach (var (offset, data) in BatchConsecutiveUpdates())
        {
            buffer.Update(offset, data);
        }
        buffer.Unbind();
        
        Clear();
    }
    
    public void Clear() => _updates.Clear();
    
    public int Count => _updates.Count;
    
    private List<(int offset, T[] data)> BatchConsecutiveUpdates()
    {
        if (_updates.Count == 0) return [];
        
        var sorted = _updates.OrderBy(kvp => kvp.Key).ToList();
        var batches = new List<(int offset, T[] data)>();
        
        var currentOffset = sorted[0].Key;
        var currentBatch = new List<T>(sorted[0].Value);
        
        for (var i = 1; i < sorted.Count; i++)
        {
            var (offset, values) = sorted[i];
            
            // Check if this update is consecutive to the current batch
            if (offset == currentOffset + currentBatch.Count)
            {
                currentBatch.AddRange(values);
            }
            else
            {
                // Finalize current batch and start a new one
                batches.Add((currentOffset, [.. currentBatch]));
                currentOffset = offset;
                currentBatch = [.. values];
            }
        }
        
        // Add the last batch
        batches.Add((currentOffset, [.. currentBatch]));
        
        return batches;
    }
}

