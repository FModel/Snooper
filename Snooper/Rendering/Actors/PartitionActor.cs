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
                var sortedData = set.RuntimeStreamingData.OrderBy(x => x.LoadingRange).ToArray();
                for (var i = 0; i < sortedData.Length; i++)
                {
                    Children.Add(new HierarchicalActor(sortedData[i], i, i > 0));
                }
                break;
            }
            case UWorldPartitionRuntimeSpatialHash spatial:
            {
                // TODO: does not seem to be correct when one StreamingGrids and multiple grid levels
                // on COE33, one grid level is one hlod?
                foreach (var grid in spatial.StreamingGrids)
                {
                    Children.Add(new HierarchicalActor(grid));
                }
                break;
            }
        }
    }

    public void UpdateCellVisibility(Vector3 position)
    {
        var hlods = Children.OfType<HierarchicalActor>().ToArray();
        for (var i = 0; i < hlods.Length; i++)
        {
            hlods[i].UpdateCellVisibility(position, i > 0 ? hlods[i - 1].LoadingRange : 0f);
        }

        // TODO: no holes + no overlaps between HLODs
    }
}
