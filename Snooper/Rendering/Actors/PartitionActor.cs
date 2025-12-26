using CUE4Parse.UE4.Assets.Exports.WorldPartition;
using Snooper.Rendering.Components.Transforms;
using System.Numerics;

namespace Snooper.Rendering.Actors;

public class PartitionActor : Actor
{
    public PartitionActor(UWorldPartition partition) : base(partition)
    {
        Components.Add(new SpatialComponent(null, "PartitionRoot"));

        switch (partition.RuntimeHash?.Load<UWorldPartitionRuntimeHash>())
        {
            case UWorldPartitionRuntimeHashSet set:
            {
                foreach (var streamingData in set.RuntimeStreamingData.OrderBy(x => x.LoadingRange))
                {
                    Children.Add(new HierarchicalActor(streamingData));
                }
                break;
            }
            case UWorldPartitionRuntimeSpatialHash spatial:
            {
                foreach (var grid in spatial.StreamingGrids)
                {
                    Children.Add(new HierarchicalActor(grid));
                }
                break;
            }
        }
    }

    public void SetVisibilityByDistance(Vector3 position)
    {
        var hlods = Children.Where(x => x.IsVisible).OfType<HierarchicalActor>().ToArray();
        for (var i = 0; i < hlods.Length; i++)
        {
            hlods[i].SetVisibilityByDistance(position, i > 0 ? hlods[i - 1].LoadingRange : 0f);
        }

        // TODO: no holes + no overlaps between HLODs
    }

    public void SetVisibilityByDataLayer(string dataLayer, bool isVisible)
    {
        var cells = Children
            .SelectMany(x => x.Children)
            .OfType<CellActor>()
            .Where(x => x.DataLayers.Contains(dataLayer))
            .ToArray();

        foreach (var cell in cells)
        {
            cell.IsVisible = isVisible || cell.IsNonSpatiallyLoaded;
        }
    }
}
