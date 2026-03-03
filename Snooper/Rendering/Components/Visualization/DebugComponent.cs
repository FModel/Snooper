using System.Numerics;
using CUE4Parse.UE4.Assets.Exports.Component;
using Snooper.Core;
using Snooper.Core.Containers.Resources;
using Snooper.Core.Containers.Textures;
using Snooper.Rendering.Components.Primitive;
using Snooper.Rendering.Components.Transforms;
using Snooper.Rendering.Primitives;
using Snooper.Rendering.Systems;

namespace Snooper.Rendering.Components.Visualization;

public struct PerMaterialDebugData : IPerMaterialData
{
    public bool IsReady { get; init; }
    public float LineThickness { get; init; }
    public ulong Padding { get; init; }
    public Vector3 LineColor { get; init; }
}

[DefaultActorSystem(typeof(DebugSystem))]
public abstract class DebugComponent : PrimitiveComponent<PerMaterialDebugData>
{
    protected DebugComponent(Vector3 color, float lineThickness = 1.0f, Transform? transform = null, string? name = null) : base(transform, name)
    {
        Materials[0].InlineContainer = new MaterialDataContainer(color, lineThickness);
    }

    protected DebugComponent(UPrimitiveComponent component) : base(component)
    {

    }

    protected class MaterialDataContainer(Vector3 color, float lineThickness = 1.0f) : IMaterialDataContainer
    {
        public string Name => Settings.NoName;
        public bool HasTextures => false;
        public bool IsTranslucent => false;
        public Dictionary<string, Texture> GetTextures() => throw new NotImplementedException();
        public void SetBindlessTexture(string key, BindlessTexture bindless) => throw new NotImplementedException();

        public void FinalizeGpuData()
        {
            if (Raw is not null)
                throw new InvalidOperationException("GPU data has already been finalized and sent.");

            Raw = new PerMaterialDebugData
            {
                IsReady = true,
                LineColor = color,
                LineThickness = lineThickness,
            };
        }

        public IPerMaterialData? Raw { get; private set; }

        public void DrawControls()
        {

        }

        public void Dispose()
        {
            Raw = null;
        }
    }

    protected abstract class DebugGeometry : PrimitiveData;
}
