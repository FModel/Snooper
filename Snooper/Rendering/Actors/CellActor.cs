using System.Numerics;
using CUE4Parse.UE4.Assets.Exports.WorldPartition;
using CUE4Parse.UE4.Objects.Engine;
using CUE4Parse.UE4.Objects.UObject;
using Snooper.Core.Containers.Textures;
using Snooper.Rendering.Components.Primitive;
using Snooper.Rendering.Components.Transforms;

namespace Snooper.Rendering.Actors;

public class CellActor : Actor
{
    public bool IsLoaded { get; private set; }
    public bool IsLoading { get; private set; }
    public bool CanLoad { get; }

    private readonly FSoftObjectPath? _worldAsset;
    private Task? _loadTask;
    
    public CellActor(UWorldPartitionRuntimeCell cell, bool load = false) : base(cell.Name)
    {
        if (cell.RuntimeCellData?.TryLoad<UWorldPartitionRuntimeCellData>(out var data) == true)
        {
            var color = new Vector3(cell.CellDebugColor.R, cell.CellDebugColor.G, cell.CellDebugColor.B);
            var box = (data.CellBounds ?? data.ContentBounds) * Settings.GlobalScale;
            box.GetCenterAndExtents(out var center, out var extents);

            Components.Add(new SpatialComponent(new Transform(new Vector3(center.X, center.Z, center.Y)), "CellRoot"));
            Components.Add(new DebugComponent(Vector3.Zero, new Vector3(extents.X, extents.Z, extents.Y), color, 5, "CellBounds"));
            
            var spanX = extents.X * 2;
            var spanY = extents.Y * 2;
            var useY = spanY > spanX;
            var fontWidth = (useY ? spanY : spanX) * 0.9f;
            var atlasFontSize = FontAtlasTexture.Instance.FontSize;
            var estimatedPixelWidth = Name.Length * atlasFontSize * 0.6f;
            var pixelToWorld = Settings.GlobalScale / atlasFontSize;
            var fontSize = fontWidth / (estimatedPixelWidth * pixelToWorld);
            
            Components.Add(new TextRenderComponent(Name, fontSize, color, name: Name) 
            {
                LocalTransform = new Transform 
                {
                    Position = new Vector3(0, extents.Z, 0),
                    Rotation = useY
                        ? Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2) 
                        : Quaternion.Identity
                }
            });
        }

        if (cell is UWorldPartitionRuntimeLevelStreamingCell streaming &&
            streaming.LevelStreaming?.TryLoad<ULevelStreaming>(out var level) == true &&
            level.WorldAsset is { } world)
        {
            if (load)
            {
                AddWorld(world);
            }
            else
            {
                _worldAsset = world;
                CanLoad = true;
            }
        }
    }

    public void Load()
    {
        if (!CanLoad || IsLoaded || IsLoading || _worldAsset == null)
            return;

        IsLoading = true;
        _loadTask = Task.Run(() =>
        {
            try
            {
                AddWorld(_worldAsset.Value);
            }
            finally
            {
                IsLoading = false;
            }
        });
    }
    
    private void AddWorld(FSoftObjectPath world)
    {
        var w = new WorldActor(world.Load<UWorld>());
        if (w.RootComponent != null && RootComponent != null)
        {
            w.RootComponent.LocalTransform = RootComponent.LocalTransform.Inverse();
        }

        Children.Add(w);
        IsLoaded = true;
    }
}