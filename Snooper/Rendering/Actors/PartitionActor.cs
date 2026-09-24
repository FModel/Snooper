using CUE4Parse.UE4.Assets.Exports.WorldPartition;
using Snooper.Rendering.Components.Transforms;
using System.Numerics;

namespace Snooper.Rendering.Actors;

public class PartitionActor : Actor
{
    public PartitionActor(UWorldPartition partition, IEnumerable<string> layers) : base(partition)
    {
        Components.Add(new SpatialComponent(null, "PartitionRoot"));

        switch (partition.RuntimeHash?.Load<UWorldPartitionRuntimeHash>())
        {
            case UWorldPartitionRuntimeHashSet set:
            {
                foreach (var streamingData in set.RuntimeStreamingData.OrderBy(x => x.LoadingRange))
                {
                    Children.Add(new HierarchicalActor(streamingData, GetMinRange(streamingData.Name.Text)));
                }

                float GetMinRange(string grid)
                {
                    foreach (var runtimePartition in set.RuntimePartitions)
                    {
                        var previous = runtimePartition.Name.Text;
                        foreach (var setup in runtimePartition.HLODSetups)
                        {
                            if (!setup.bIsSpatiallyLoaded) continue;
                            if (setup.Name.Text == grid)
                                return set.RuntimeStreamingData.FirstOrDefault(x => x.Name.Text == previous).LoadingRange * Settings.GlobalScale;

                            previous = setup.Name.Text;
                        }
                    }

                    return 0f;
                }
                break;
            }
            case UWorldPartitionRuntimeSpatialHash spatial:
            {
                var minRange = 0f;
                foreach (var grid in spatial.StreamingGrids.OrderBy(x => x.LoadingRange))
                {
                    Children.Add(new HierarchicalActor(grid, minRange));
                    minRange = grid.LoadingRange * Settings.GlobalScale;
                }
                break;
            }
        }

        foreach (var layer in layers)
        {
            SetDataLayerEnabled(layer, true);
        }
    }

    public void LoadAround(Vector3 position)
    {
        foreach (var hlod in Children.OfType<HierarchicalActor>().Where(x => x.IsVisible))
        {
            hlod.LoadAround(position);
        }
    }

    public void UnloadAll()
    {
        foreach (var hlod in Children.OfType<HierarchicalActor>())
        {
            hlod.UnloadAll();
        }
    }

    public override void DrawControls()
    {
        base.DrawControls();

        HierarchicalActor.DrawLoadControls(this, LoadAround, UnloadAll);
    }

    public void SetDataLayerEnabled(string dataLayer, bool enabled)
    {
        var cells = Children
            .SelectMany(x => x.Children)
            .OfType<CellActor>()
            .Where(x => x.DataLayers.Contains(dataLayer))
            .ToArray();

        foreach (var cell in cells)
        {
            cell.IsVisible = enabled || cell.IsPersistent;
        }
    }
}
