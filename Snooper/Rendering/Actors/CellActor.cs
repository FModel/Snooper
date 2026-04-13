using System.Numerics;
using CUE4Parse.UE4.Assets.Exports.WorldPartition;
using CUE4Parse.UE4.Objects.Core.Math;
using CUE4Parse.UE4.Objects.Engine;
using CUE4Parse.UE4.Objects.UObject;
using Snooper.Core.Containers.Textures;
using Snooper.Rendering.Components.Primitive;
using Snooper.Rendering.Components.Transforms;

namespace Snooper.Rendering.Actors;

public class CellActor : StreamableActor
{
    public bool IsNonSpatiallyLoaded { get; }
    public string[] DataLayers { get; }

    public CellActor(UWorldPartitionRuntimeCell cell, Vector3? color = null, bool isNonSpatiallyLoaded = false) : base(cell)
    {
        IsNonSpatiallyLoaded = isNonSpatiallyLoaded;
        DataLayers = cell.DataLayers?.DataLayers.Select(x => x.Text).ToArray() ?? [];

        if (cell.RuntimeCellData?.TryLoad<UWorldPartitionRuntimeCellData>(out var data) == true)
        {
            FVector center;
            FVector extents;
            if (data is UWorldPartitionRuntimeCellDataSpatialHash spatial && spatial.Position != FVector.ZeroVector)
            {
                center = spatial.Position * Settings.GlobalScale;
                extents = new FVector(spatial.Extent * Settings.GlobalScale);
            }
            else
            {
                var box = (data.CellBounds ?? data.ContentBounds) * Settings.GlobalScale;
                box.GetCenterAndExtents(out center, out extents);
            }

            // TODO: not clean
            if (DataLayers.Length > 0)
            {
                var hue = DataLayers.Aggregate(0f, (current1, dl) => dl.Aggregate(current1, (current, c) => current + c));
                hue = (hue * 0.618033988749895f) % 1f;
                var h = hue * 6;
                var x = 1 - MathF.Abs(h % 2 - 1);
                color = h switch
                {
                    < 1 => new Vector3(1, x, 0),
                    < 2 => new Vector3(x, 1, 0),
                    < 3 => new Vector3(0, 1, x),
                    < 4 => new Vector3(0, x, 1),
                    < 5 => new Vector3(x, 0, 1),
                    _ => new Vector3(1, 0, x)
                } * 0.5f;
            }
            else
            {
                color ??= new Vector3(cell.CellDebugColor.R, cell.CellDebugColor.G, cell.CellDebugColor.B);
            }

            Components.Add(new BoxComponent(new Vector3(extents.X, extents.Z, extents.Y), color.Value, 5.0f, new Transform(new Vector3(center.X, center.Z, center.Y)), "CellBounds"));

            var spanX = extents.X * 2;
            var spanY = extents.Y * 2;
            var useY = spanY > spanX;
            var fontWidth = (useY ? spanY : spanX) * 0.9f;
            var atlasFontSize = FontAtlasTexture.Instance.FontSize;
            var estimatedPixelWidth = Name.Length * atlasFontSize * 0.6f;
            var pixelToWorld = Settings.GlobalScale / atlasFontSize;
            var fontSize = fontWidth / (estimatedPixelWidth * pixelToWorld);

            Components.Add(new TextRenderComponent(Name, fontSize, color, transform: new Transform
            {
                Position = new Vector3(0, extents.Z, 0),
                Rotation = useY
                    ? Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2)
                    : Quaternion.Identity
            }, name: Name));
        }

        if (cell is UWorldPartitionRuntimeLevelStreamingCell streaming &&
            streaming.LevelStreaming?.TryLoad<ULevelStreaming>(out var level) == true &&
            level.WorldAsset is { } world)
        {
            OnLoad += () => AddWorld(world);
            if (IsNonSpatiallyLoaded)
            {
                Load();
            }
        }
    }

    public CellActor(FSoftObjectPath worldAsset, UWorld world) : base(world)
    {
        DataLayers = [];

        OnLoad += () => AddWorld(worldAsset);
    }

    private void AddWorld(FSoftObjectPath world)
    {
        var w = new WorldActor(world.Load<UWorld>());
        if (w.RootComponent != null && RootComponent != null)
        {
            w.RootComponent.SetLocalTransform(RootComponent.LocalTransform.Inverse());
        }

        Children.Add(w);
    }
}
