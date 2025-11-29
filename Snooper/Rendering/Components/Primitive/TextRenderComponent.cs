using System.Numerics;
using CUE4Parse.UE4.Assets.Exports.Component.TextRender;
using CUE4Parse.UE4.Objects.Core.i18N;
using CUE4Parse.UE4.Objects.Core.Math;
using ImGuiNET;
using Snooper.Core;
using Snooper.Core.Containers.Resources;
using Snooper.Core.Containers.Textures;
using Snooper.Extensions;
using Snooper.Rendering.Components.Descriptors;
using Snooper.Rendering.Components.Transforms;
using Snooper.Rendering.Primitives;
using Snooper.Rendering.Systems;
using Snooper.UI;

namespace Snooper.Rendering.Components.Primitive;

public struct PerMaterialTextData : IPerMaterialData
{
    public bool IsReady { get; set; }
    public uint Padding1;
    public ulong Padding2;
    public Vector3 FontColor;
}

[DefaultActorSystem(typeof(TextRenderSystem))]
public class TextRenderComponent : PrimitiveComponent<Vector4, PerInstanceData, PerMaterialTextData>
{
    public sealed override MaterialSection[] Materials { get; } = [new()];

    private readonly string _text;
    private readonly EHorizTextAligment _horizontalAlignment;
    private readonly EVerticalTextAligment _verticalAlignment;

    public TextRenderComponent(string text, float fontSize = 11.0f, Vector3? color = null, EHorizTextAligment hAlign = EHorizTextAligment.EHTA_Center, EVerticalTextAligment vAlign = EVerticalTextAligment.EVRTA_TextCenter, Transform? transform = null, string? name = null) : base(transform, name)
    {
        _text = text;
        _horizontalAlignment = hAlign;
        _verticalAlignment = vAlign;

        var fontAtlas = FontAtlasTexture.Instance;
        var textQuad = new Geometry(_text, fontAtlas, _horizontalAlignment, _verticalAlignment, fontSize);
        Descriptor = new PrimitiveDescriptor<Vector4>(ComputeBounds(textQuad), () => textQuad);

        if (color is { } c)
        {
            Materials[0].MaterialDataContainer = new MaterialDataContainer(c);
        }
    }

    public TextRenderComponent(UTextRenderComponent component) : base(component)
    {
        _text = component.GetOrDefault<FText?>("Text")?.Text ?? "DefaultText";
        _horizontalAlignment = component.GetOrDefault("HorizontalAlignment", EHorizTextAligment.EHTA_Left);
        _verticalAlignment = component.GetOrDefault("VerticalAlignment", EVerticalTextAligment.EVRTA_TextCenter);

        var color = component.GetOrDefault<FColor?>("TextRenderColor");
        var worldSize = component.GetOrDefault("WorldSize", 30.0f);

        var fontAtlas = FontAtlasTexture.Instance;
        var textQuad = new Geometry(_text, fontAtlas, _horizontalAlignment, _verticalAlignment, worldSize);

        Descriptor = new PrimitiveDescriptor<Vector4>(ComputeBounds(textQuad), () => textQuad);

        if (color is { } c)
        {
            Materials[0].MaterialDataContainer = new MaterialDataContainer(new Vector3(c.R, c.G, c.B) / 255f);
        }

        var zFlip = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, MathF.PI);
        var zRotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, MathF.PI / 2f);
        var yRotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2f);
        LocalTransform.Rotation *= zFlip * zRotation * yRotation;
    }

    private CullingBounds ComputeBounds(Geometry geometry)
    {
        if (geometry.Vertices is { Length: > 0 } vertices)
        {
            float minX = float.MaxValue, maxX = float.MinValue;
            float minZ = float.MaxValue, maxZ = float.MinValue;
            foreach (var v in vertices)
            {
                minX = Math.Min(minX, v.X);
                maxX = Math.Max(maxX, v.X);
                minZ = Math.Min(minZ, v.Y);
                maxZ = Math.Max(maxZ, v.Y);
            }
            var center = new Vector3((minX + maxX) / 2, 0, (minZ + maxZ) / 2);
            var extents = new Vector3((maxX - minX) / 2, 0, (maxZ - minZ) / 2);
            return new CullingBounds(center, extents);
        }

        return new CullingBounds(Vector3.Zero, Vector3.One);
    }

    internal override string Icon => "text";

    public override void DrawControls()
    {
        base.DrawControls();

        EditorUI.CollapsingTable("Text", ImGuiTreeNodeFlags.DefaultOpen, () =>
        {
            EditorUI.Text("Content", _text);
            EditorUI.Text("H-Align", _horizontalAlignment.GetDescription());
            EditorUI.Text("V-Align", _verticalAlignment.GetDescription());
        });
    }

    private class MaterialDataContainer(Vector3 color) : IMaterialDataContainer
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

            Raw = new PerMaterialTextData
            {
                IsReady = true,
                FontColor = color,
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

    private class Geometry : PrimitiveData<Vector4>
    {
        public Geometry(string text, FontAtlasTexture fontAtlas, EHorizTextAligment hAlign, EVerticalTextAligment vAlign, float scale)
        {
            if (string.IsNullOrEmpty(text))
            {
                Vertices = [];
                Indices = [];
                return;
            }

            var vertices = new List<Vector4>();
            var indices = new List<uint>();

            var pixelToWorld = Settings.GlobalScale / fontAtlas.FontSize;
            var finalScale = scale * pixelToWorld;

            float totalWidth = 0;
            var lines = text.Split('\n');
            var lineWidths = new List<float>();

            foreach (var line in lines)
            {
                float lineWidth = 0;
                foreach (var c in line)
                {
                    if (fontAtlas.Characters.TryGetValue(c, out var charInfo))
                    {
                        lineWidth += charInfo.AdvanceX;
                    }
                }
                lineWidths.Add(lineWidth);
                totalWidth = Math.Max(totalWidth, lineWidth);
            }

            var lineHeight = fontAtlas.LineHeight;
            var totalHeight = lineHeight * lines.Length;

            var baseOffsetX = hAlign switch
            {
                EHorizTextAligment.EHTA_Center => -totalWidth * 0.5f,
                EHorizTextAligment.EHTA_Right => -totalWidth,
                _ => 0
            } * finalScale;

            // TODO: it's broken but it's fine for now
            var baseOffsetY = vAlign switch
            {
                EVerticalTextAligment.EVRTA_TextTop or EVerticalTextAligment.EVRTA_QuadTop => 0f,
                EVerticalTextAligment.EVRTA_TextCenter => totalHeight * 0.5f,
                _ => totalHeight
            } * finalScale;

            var cursorY = baseOffsetY;
            var lineIndex = 0;

            foreach (var line in lines)
            {
                var lineOffsetX = hAlign switch
                {
                    EHorizTextAligment.EHTA_Center => (totalWidth - lineWidths[lineIndex]) * 0.5f,
                    EHorizTextAligment.EHTA_Right => totalWidth - lineWidths[lineIndex],
                    _ => 0
                } * finalScale;

                var cursorX = baseOffsetX + lineOffsetX;

                foreach (var c in line)
                {
                    if (!fontAtlas.Characters.TryGetValue(c, out var charInfo))
                    {
                        if (fontAtlas.Characters.TryGetValue(' ', out var spaceInfo))
                        {
                            cursorX += spaceInfo.AdvanceX * finalScale;
                        }
                        continue;
                    }

                    // Position from cursor baseline
                    var x0 = cursorX + charInfo.OffsetX * finalScale;
                    var y0 = cursorY + charInfo.OffsetY * finalScale;
                    // Use glyph dimensions for quad size to match UV coverage
                    var x1 = x0 + charInfo.Width * finalScale;
                    var y1 = y0 + charInfo.Height * finalScale;

                    var baseIndex = (uint)vertices.Count;

                    // Create quad for this character (y1 is bottom, y0 is top)
                    vertices.Add(new Vector4(x0, y1, charInfo.U0, charInfo.V1)); // Bottom-left
                    vertices.Add(new Vector4(x1, y1, charInfo.U1, charInfo.V1)); // Bottom-right
                    vertices.Add(new Vector4(x1, y0, charInfo.U1, charInfo.V0)); // Top-right
                    vertices.Add(new Vector4(x0, y0, charInfo.U0, charInfo.V0)); // Top-left

                    // Two triangles
                    indices.Add(baseIndex + 0);
                    indices.Add(baseIndex + 1);
                    indices.Add(baseIndex + 2);

                    indices.Add(baseIndex + 0);
                    indices.Add(baseIndex + 2);
                    indices.Add(baseIndex + 3);

                    cursorX += charInfo.AdvanceX * finalScale;
                }

                cursorY -= lineHeight * finalScale;
                lineIndex++;
            }

            Vertices = vertices.ToArray();
            Indices = indices.ToArray();
        }
    }
}
