using System.Numerics;
using CUE4Parse.UE4.Assets.Exports.Component;
using CUE4Parse.UE4.Assets.Exports.Texture;
using CUE4Parse.UE4.Objects.Core.Math;
using CUE4Parse.UE4.Objects.Core.Misc;
using Snooper.Core;
using Snooper.Core.Containers.Resources;
using Snooper.Core.Containers.Textures;
using Snooper.Rendering.Primitives;
using Snooper.Rendering.Systems;

namespace Snooper.Rendering.Components.Primitive;

public struct PerDrawBillboardData : IPerDrawData
{
    public bool IsReady { get; init; }
    public float OpacityMask;
    public ulong Sprite;
}

[DefaultActorSystem(typeof(BillboardSystem))]
public class BillboardComponent : PrimitiveComponent<Vector2, PerDrawBillboardData>
{
    public BillboardComponent(UBillboardComponent component) : base(component)
    {
        SetGeometry(FGuid.Random(), new Geometry(), new FBox());

        if (component.GetSprite() is { } sprite)
        {
            Materials[0].DrawDataContainer = new DrawDataContainer(new Texture2D(sprite), component.GetOrDefault("OpacityMaskRefVal", 0.5f));
        }
    }
    
    private class DrawDataContainer(Texture sprite, float opacityMask) : IDrawDataContainer
    {
        private BindlessTexture? _sprite;
        
        public bool HasTextures => true;
        public bool IsTranslucent => true;

        public Dictionary<string, Texture> GetTextures() => new() { { "Sprite", sprite } };

        public void SetBindlessTexture(string key, BindlessTexture bindless)
        {
            switch (key)
            {
                case "Sprite":
                    _sprite = bindless;
                    break;
                default:
                    throw new ArgumentException($"Unknown texture key: {key}");
            }
        }

        public void FinalizeGpuData()
        {
            if (_sprite is null)
            {
                throw new InvalidOperationException("Unset textures. Ensure that SetBindlessTexture is called for all textures.");
            }
            
            _sprite.Generate();
            _sprite.MakeResident();

            Raw = new PerDrawBillboardData
            {
                IsReady = true,
                OpacityMask = opacityMask,
                Sprite = _sprite,
            };
        }
        
        public IPerDrawData? Raw { get; private set; }
        
        public void DrawControls()
        {
            
        }

        public void Dispose()
        {
            _sprite?.Dispose();
            _sprite = null;
            
            Raw = null;
        }
    }
    
    private class Geometry : PrimitiveData<Vector2>
    {
        public Geometry()
        {
            Vertices =
            [
                new Vector2(-1f, -1f),
                new Vector2(1f, -1f),
                new Vector2(1f, 1f),
                new Vector2(-1f, 1f)
            ];
            
            Indices = [0, 1, 2, 2, 3, 0];
        }
    }
}