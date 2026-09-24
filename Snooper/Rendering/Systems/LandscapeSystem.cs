using System.Numerics;
using System.Runtime.CompilerServices;
using ImGuiNET;
using OpenTK.Graphics.OpenGL4;
using Snooper.Core;
using Snooper.Core.Containers;
using Snooper.Core.Containers.Buffers;
using Snooper.Core.Containers.Programs;
using Snooper.Core.Containers.Resources;
using Snooper.Rendering.Components.Camera;
using Snooper.Rendering.Components.Mesh;
using Snooper.UI;

namespace Snooper.Rendering.Systems;

public class LandscapeSystem() : PrimitiveSystem<Vector2, LandscapeMeshComponent, PerInstanceData, PerMaterialLandscapeData>(PrimitiveType.Patches), IControllable
{
    private abstract class LandscapeBindings : Bindings
    {
        public const uint Scales = BaseMaxBinding + 1;
        public const uint WeightMapping = BaseMaxBinding + 2;
        public const uint MaxBinding = WeightMapping;

        public static readonly string[] OwnDefines =
        [
            Define("LANDSCAPE_SCALES", Scales),
            Define("LANDSCAPE_WEIGHT_MAPPING", WeightMapping)
        ];
    }

    public override uint Order => 21;
    public override uint? MaxBindingUsed => LandscapeBindings.MaxBinding;
    protected override Dictionary<CommandBufferType, ShaderProgram> Shaders { get; } = new()
    {
        [CommandBufferType.Opaque] = new EmbeddedShader("Landscape/landscape")
        {
            TessellationControl = "Landscape/landscape.tesc",
            TessellationEvaluation = "Landscape/landscape.tese",
            Defines = LandscapeBindings.OwnDefines
        }
    };
    protected override Action<VertexArrayLayout> VertexLayout { get; } = layout => layout.Float(0, 2);

    private readonly ShaderStorageBuffer<Vector2> _scales = new();
    private readonly ShaderStorageBuffer<TileLayers> _mapping = new();
    protected override IEnumerable<(uint, IIndexedBind)> SystemBuffers =>
    [
        (LandscapeBindings.Scales, _scales),
        (LandscapeBindings.WeightMapping, _mapping)
    ];

    private readonly List<string> _layers = [];
    private readonly Vector4[] _palette = new Vector4[MaxLayers]; // the color of each of _layers, same index
    private float _sizeQuads = 0.0f;
    private ColorMode _colorMode = ColorMode.Slope;
    private float _steepestSlope = 45f; // degrees
    private int _isolatedLayer = AllLayers;
    private Vector2 _heightRange = new(0f, 1f);
    private bool _updateHeightRange;
    private bool _contours = true;
    private float _contourInterval = 5f;
    private bool _autoContourInterval = true;

    protected override void OnLoad()
    {
        base.OnLoad();

        _scales.Generate();
        _mapping.Generate();

        _scales.Allocate(Settings.TessellationQuadCountTotal);
        _scales.AddRange(CreateSubPatchOffsets());

        _mapping.Allocate((int)Counts.Materials);

        Vector2[] CreateSubPatchOffsets()
        {
            const int quadCount = Settings.TessellationQuadCount;
            var offsets = new Vector2[Settings.TessellationQuadCountTotal];

            for (var x = 0; x < quadCount; x++)
            {
                for (var y = 0; y < quadCount; y++)
                {
                    offsets[x * quadCount + y] = new Vector2(x, y);
                }
            }

            return offsets;
        }
    }

    protected override void OnResourcesAdded(LandscapeMeshComponent component, ResourcesMetadata metadata)
    {
        base.OnResourcesAdded(component, metadata);

        _sizeQuads = Math.Max(_sizeQuads, component.SizeQuads);
        _updateHeightRange = true;

        if (metadata.MaterialAllocation is not { } allocation) return;

        var layers = new TileLayers();
        foreach (var (name, layer) in component.Layers)
        {
            if (name == VisibilityLayer) continue;
            layers[(int) (layer.TextureIndex * 4 + layer.ChannelIndex)] = GetPaletteIndex(name, layer.DebugColor) + 1;
        }

        // MaterialAllocation.StartIndex is draw.BaseMaterial
        // for a single section landscape tile draw.MaterialIndex is always 0
        // meaning the mapping and the material buffers are aligned and we can use the same index to update the mapping buffer
        _mapping.Upsert(allocation.StartIndex, layers);

        int GetPaletteIndex(string name, Vector4 debugColor)
        {
            var index = _layers.IndexOf(name);
            if (index >= 0) return index;
            if (_layers.Count == MaxLayers) return -1;

            index = _layers.Count;
            _layers.Add(name);
            _palette[index] = GetLayerColor(name, debugColor);

            return index;
        }

        Vector4 GetLayerColor(string name, Vector4 debugColor)
        {
            if (debugColor.X + debugColor.Y + debugColor.Z > 0.01f)
                return debugColor with { W = 1f };

            var hash = 2166136261u;
            foreach (var c in name)
            {
                hash = (hash ^ c) * 16777619u;
            }

            var h = hash % 360u / 60f;
            var x = 1f - MathF.Abs(h % 2f - 1f);
            var rgb = h switch
            {
                < 1f => new Vector3(1f, x, 0f),
                < 2f => new Vector3(x, 1f, 0f),
                < 3f => new Vector3(0f, 1f, x),
                < 4f => new Vector3(0f, x, 1f),
                < 5f => new Vector3(x, 0f, 1f),
                _ => new Vector3(1f, 0f, x)
            };

            return new Vector4(Vector3.Lerp(Vector3.One, rgb, 0.65f) * 0.9f, 1f);
        }
    }

    protected override void OnActorComponentRemoved(LandscapeMeshComponent component, EEndPlayReason reason)
    {
        base.OnActorComponentRemoved(component, reason);

        _updateHeightRange = true;
    }

    protected override void OnUpdate(float delta)
    {
        base.OnUpdate(delta);

        if (_updateHeightRange)
        {
            var min = float.MaxValue;
            var max = float.MinValue;
            foreach (var component in Components)
            {
                var bounds = component.Descriptor.Bounds;
                var low = Vector3.Transform(bounds.Center - bounds.Extents, component.WorldMatrix).Y;
                var high = Vector3.Transform(bounds.Center + bounds.Extents, component.WorldMatrix).Y;

                min = Math.Min(min, Math.Min(low, high));
                max = Math.Max(max, Math.Max(low, high));
            }

            if (min <= max)
            {
                _heightRange = new Vector2(min, max);

                if (_autoContourInterval && max - min > 0f)
                {
                    // about 40 lines from the lowest to the highest point, on a 1-2-5 step
                    var raw = (max - min) / 40f;
                    var magnitude = MathF.Pow(10f, MathF.Floor(MathF.Log10(raw)));
                    var step = (raw / magnitude) switch
                    {
                        < 1.5f => 1f,
                        < 3.5f => 2f,
                        < 7.5f => 5f,
                        _ => 10f
                    };
                    _contourInterval = step * magnitude;
                }
            }

            _updateHeightRange = false;
        }
    }

    protected override void PreRender(CameraComponent camera, ShaderProgram shader)
    {
        base.PreRender(camera, shader);

        shader.SetUniform("uColorMode", (uint)_colorMode);
        shader.SetUniform("uLayerPalette", _palette);
        shader.SetUniform("uIsolatedLayer", _isolatedLayer);
        shader.SetUniform("uSteepestSlope", _steepestSlope);
        shader.SetUniform("uHeightRange", _heightRange);
        shader.SetUniform("uContourInterval", _contours ? _contourInterval : 0f);
        shader.SetUniform("uSizeQuads", _sizeQuads);
        shader.SetUniform("uQuadCount", (float)Settings.TessellationQuadCount);
        shader.SetUniform("uGlobalScale", Settings.GlobalScale);
    }

    public override long Allocated => base.Allocated + _scales.Allocated + _mapping.Allocated;
    public override long Used => base.Used + _scales.Used + _mapping.Used;
    public override IEnumerable<MemoryDetail> GetMemoryDetails()
    {
        foreach (var detail in base.GetMemoryDetails())
            yield return detail;

        yield return new MemoryDetail("Scales Buffer", _scales);
        yield return new MemoryDetail("Tile Layers Buffer", _mapping);
    }

    public void DrawControls()
    {
        EditorUI.PropertyValueTable("Landscape Table", () =>
        {
            EditorUI.Property("Color Mode");
            var c = (int) _colorMode;
            ImGui.SetNextItemWidth(-1);
            ImGui.Combo("##Color Mode", ref c, "Slope\0Layers\0Height\0Tiles\0");
            _colorMode = (ColorMode) c;

            switch (_colorMode)
            {
                case ColorMode.Slope:
                    EditorUI.Property("Steepest Slope");
                    ImGui.SetNextItemWidth(-1);
                    ImGui.SliderFloat("##Steepest Slope", ref _steepestSlope, 10f, 89f, "%.0f deg");
                    break;
                case ColorMode.Layers:
                    EditorUI.Property("Layers");
                    DrawLayers();
                    break;
                case ColorMode.Height:
                    EditorUI.Text("Height Range", $"{_heightRange.X:F1} to {_heightRange.Y:F1}");
                    break;
            }

            EditorUI.Property("Contour Lines");
            ImGui.Checkbox("##Contour Lines", ref _contours);
            if (_contours)
            {
                EditorUI.Property("Contour Interval");
                ImGui.SetNextItemWidth(-1);
                if (ImGui.SliderFloat("##Contour Interval", ref _contourInterval, 0.25f, 100f, "%.2f", ImGuiSliderFlags.Logarithmic))
                    _autoContourInterval = false;
            }
        });
    }

    private void DrawLayers()
    {
        const int maxVisibleRows = 8;

        var rowHeight = ImGui.GetTextLineHeightWithSpacing();
        var rows = Math.Min(_layers.Count + 1, maxVisibleRows);
        var height = rows * rowHeight + ImGui.GetStyle().FramePadding.Y * 2;

        if (ImGui.BeginChild("##Landscape Layers", new Vector2(-1, height), ImGuiChildFlags.FrameStyle))
        {
            if (ImGui.Selectable("All", _isolatedLayer == AllLayers)) _isolatedLayer = AllLayers;

            var swatch = new Vector2(ImGui.GetTextLineHeight());
            for (var i = 0; i < _layers.Count; i++)
            {
                var name = _layers[i];

                ImGui.ColorButton($"##Color{name}", _palette[i], ImGuiColorEditFlags.NoTooltip | ImGuiColorEditFlags.NoPicker | ImGuiColorEditFlags.NoAlpha, swatch);
                ImGui.SameLine();
                if (ImGui.Selectable(name, _isolatedLayer == i)) _isolatedLayer = _isolatedLayer == i ? AllLayers : i;
            }
        }
        ImGui.EndChild();
    }

    // the values are the COLOR_ defines of landscape.frag
    private enum ColorMode : byte
    {
        Slope,
        Layers,
        Height,
        Tiles
    }

    public const string VisibilityLayer = "__LANDSCAPE_VISIBILITY__"; // holes
    private const int AllLayers = -1;
    private const int MaxLayers = 64;

    [InlineArray(LandscapeMeshComponent.MaxWeightmaps * 4)]
    private struct TileLayers
    {
        private int _first;
    }
}
