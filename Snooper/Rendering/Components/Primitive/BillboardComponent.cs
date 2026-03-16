using System.Numerics;
using CUE4Parse.UE4.Assets.Exports.Component;
using Snooper.Core;
using Snooper.Core.Containers.Resources;
using Snooper.Core.Containers.Textures;
using Snooper.Rendering.Components.Descriptors;
using Snooper.Rendering.Primitives;
using Snooper.Rendering.Systems;

namespace Snooper.Rendering.Components.Primitive;

public struct PerMaterialBillboardData : IPerMaterialData
{
    public bool IsReady { get; init; }
    public float OpacityMask;
    public ulong Sprite;
}

[DefaultActorSystem(typeof(BillboardSystem))]
public class BillboardComponent : PrimitiveComponent<Vector2, PerMaterialBillboardData>
{
    public BillboardComponent(UBillboardComponent component) : base(component)
    {
        Descriptor = new PrimitiveDescriptor<Vector2>(new CullingBounds(Vector3.Zero, Vector3.One / 4), () => new Geometry());

        if (component.GetSprite() is { } sprite)
        {
            Materials[0].InlineContainer = new MaterialDataContainer(new Texture2D(sprite), component.GetOrDefault("OpacityMaskRefVal", 0.5f));
        }
    }

    internal override string Icon => "\uf51b";

    private class MaterialDataContainer(Texture sprite, float opacityMask) : IMaterialDataContainer
    {
        private BindlessTexture? _sprite;

        public string Name => Settings.NoName;
        public bool HasTextures => true;
        public bool IsTranslucent => true;

        public Dictionary<string, Texture> GetTextures() => new() { { "Sprite", sprite } };

        public void SetBindlessTexture(string key, BindlessTexture bindless)
        {
            _sprite = key switch
            {
                "Sprite" => bindless,
                _ => throw new ArgumentException($"Unknown texture key: {key}")
            };
        }

        public void FinalizeGpuData()
        {
            if (Raw is not null)
                throw new InvalidOperationException("GPU data has already been finalized and sent.");

            if (_sprite is null)
                throw new InvalidOperationException("Unset textures. Ensure that SetBindlessTexture is called for all textures.");

            Raw = new PerMaterialBillboardData
            {
                IsReady = true,
                OpacityMask = opacityMask,
                Sprite = _sprite,
            };
        }

        public IPerMaterialData? Raw { get; private set; }

        public void DrawControls()
        {

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
