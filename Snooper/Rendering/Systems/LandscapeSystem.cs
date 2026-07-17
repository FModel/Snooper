using System.Numerics;
using ImGuiNET;
using OpenTK.Graphics.OpenGL4;
using Serilog;
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
    protected override Action<uint> VertexLayout { get; } = vao =>
    {
        GL.VertexArrayAttribFormat(vao, 0, 2, VertexAttribType.Float, false, 0);
        GL.EnableVertexArrayAttrib(vao, 0);
        GL.VertexArrayAttribBinding(vao, 0, 0);
    };

    private readonly ShaderStorageBuffer<Vector2> _scales = new();
    private readonly ShaderStorageBuffer<WeightHighlightMapping> _mapping = new();
    protected override IEnumerable<(uint, IIndexedBind)> SystemBuffers =>
    [
        (LandscapeBindings.Scales, _scales),
        (LandscapeBindings.WeightMapping, _mapping)
    ];

    private readonly List<string> _layers = ["None"];
    private float _sizeQuads = 0.0f;
    private ColorMode _colorMode = ColorMode.Weightmap;
    private int _selectedLayer;
    private bool _updateMapping;

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

        foreach (var layer in component.Layers.Keys)
        {
            if (!_layers.Contains(layer)) _layers.Add(layer);
        }
        _sizeQuads = Math.Max(_sizeQuads, component.SizeQuads);
    }

    protected override void OnUpdate(float delta)
    {
        base.OnUpdate(delta);

        if (!_updateMapping || _colorMode != ColorMode.Weightmap)
            return;

        var layer = _layers[_selectedLayer];
        Log.Information("Updating weightmap highlight for layer {Layer}", layer);

        foreach (var component in Components)
        {
            if (component.Metadata is not { MaterialAllocation: { } allocation })
                continue;

            var m = new WeightHighlightMapping();
            if (component.Layers.TryGetValue(layer, out var mapping))
            {
                m = new WeightHighlightMapping
                {
                    WeightmapIndex = mapping.TextureIndex,
                    ChannelIndex = mapping.ChannelIndex,
                    DebugColor = mapping.DebugColor
                };
            }

            // MaterialAllocation.StartIndex is draw.BaseMaterial
            // for a single section landscape tile draw.MaterialIndex is always 0
            // meaning the mapping and the material buffers are aligned and we can use the same index to update the mapping buffer
            _mapping.Upsert(allocation.StartIndex, m);
        }

        _updateMapping = false;
    }

    protected override void PreRender(CameraComponent camera, ShaderProgram shader)
    {
        base.PreRender(camera, shader);

        shader.SetUniform("uColorMode", (uint)_colorMode);
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
        yield return new MemoryDetail("Weightmap Highlight Buffer", _mapping);
    }

    public void DrawControls()
    {
        EditorUI.PropertyValueTable("Landscape Table", () =>
        {
            EditorUI.Property("Color Mode");
            var c = (int) _colorMode;
            ImGui.Combo("##Color Mode", ref c, "Heightmap\0Weightmap\0");
            _colorMode = (ColorMode) c;

            if (_colorMode == ColorMode.Weightmap)
            {
                var before = _selectedLayer;
                EditorUI.Property("Weightmap Layer");
                ImGui.Combo("##Weightmap Layer", ref _selectedLayer, _layers.ToArray(), _layers.Count);
                if (!_updateMapping) _updateMapping = before != _selectedLayer;
            }
        });
    }

    private enum ColorMode : byte
    {
        Heightmap,
        Weightmap
    }

    private readonly struct WeightHighlightMapping
    {
        public uint WeightmapIndex { get; init; }
        public uint ChannelIndex { get; init; }
        public Vector2 Padding { get; init; }
        public Vector4 DebugColor { get; init; }
    }
}
