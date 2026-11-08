using CUE4Parse.UE4.Assets.Exports.Engine;
using CUE4Parse.UE4.Assets.Exports.WorldPartition;
using CUE4Parse.UE4.Objects.Core.Math;
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

    public PartitionActor(UWorldComposition composition) : base(composition)
    {
        Components.Add(new SpatialComponent(null, "PartitionRoot"));

        var tiles = composition.Tiles ?? [];
        var indices = new Dictionary<string, int>(tiles.Length, StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < tiles.Length; i++)
        {
            indices.TryAdd(tiles[i].PackageName.Text, i);
        }

        var layers = new SortedDictionary<(bool, int, string), List<(FWorldCompositionTile Tile, FIntVector Position)>>();
        foreach (var tile in tiles)
        {
            var layer = tile.Info.Layer;
            var key = (!layer.DistanceStreamingEnabled, layer.StreamingDistance, layer.Name);
            if (!layers.TryGetValue(key, out var layerTiles))
            {
                layers[key] = layerTiles = [];
            }

            layerTiles.Add((tile, GetAbsolutePosition(tile)));
        }

        foreach (var layerTiles in layers.Values)
        {
            Children.Add(new HierarchicalActor(layerTiles[0].Tile.Info.Layer, layerTiles));
        }

        FIntVector GetAbsolutePosition(FWorldCompositionTile tile)
        {
            var position = new FIntVector();
            var info = tile.Info;
            for (var depth = 0; info != null && depth < tiles.Length; depth++)
            {
                position += info.Position;
                info = info.ParentTilePackageName is { } parent && indices.TryGetValue(parent, out var index) ? tiles[index].Info : null;
            }

            return position;
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
