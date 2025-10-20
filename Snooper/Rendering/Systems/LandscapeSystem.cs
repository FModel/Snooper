using System.Numerics;
using ImGuiNET;
using OpenTK.Graphics.OpenGL4;
using Serilog;
using Snooper.Core.Containers.Buffers;
using Snooper.Core.Containers.Programs;
using Snooper.Core.Containers.Resources;
using Snooper.Core.Systems;
using Snooper.Rendering.Components.Camera;
using Snooper.Rendering.Components.Mesh;
using Snooper.UI;

namespace Snooper.Rendering.Systems;

public class LandscapeSystem() : PrimitiveSystem<Vector2, LandscapeMeshComponent, PerInstanceData, PerMaterialLandscapeData>(100, PrimitiveType.Patches), IControllable
{
    public override uint Order => 21;
    public override ActorSystemType SystemType => ActorSystemType.Deferred;
    protected override ShaderProgram Shader { get; } = new EmbeddedShaderProgram("Landscape/landscape")
    {
        TessellationControl = "Landscape/landscape.tesc",
        TessellationEvaluation = "Landscape/landscape.tese"
    };
    protected override Action<int> VertexLayout { get; } = stride =>
    {
        GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, stride, 0);
        GL.EnableVertexAttribArray(0);
    };
    
    private readonly ShaderStorageBuffer<Vector2> _scales = new(100 * Settings.TessellationQuadCountTotal);
    private readonly ShaderStorageBuffer<WeightHighlightMapping> _mapping = new(100);
    private readonly List<string> _layers = ["None"];
    private float _sizeQuads = 0.0f;
    private ColorMode _colorMode = ColorMode.Heightmap;
    private int _selectedLayer;
    private bool _updateMapping;

    public override void Load()
    {
        base.Load();

        _scales.Generate();
        _scales.Bind();
        foreach (var component in Components)
        {
            _scales.AddRange(component.Scales);
            foreach (var layer in component.Layers.Keys)
            {
                if (!_layers.Contains(layer)) _layers.Add(layer);
            }
            _sizeQuads = Math.Max(_sizeQuads, component.SizeQuads);
        }
        _scales.Unbind();
        
        _mapping.Generate();
        _mapping.Bind();
        _mapping.Allocate(new WeightHighlightMapping[ComponentsCount]);
        _mapping.Unbind();
    }

    public override void Update(float delta)
    {
        base.Update(delta);
        if (!_updateMapping || _colorMode != ColorMode.Weightmap)
            return;

        var layer = _layers[_selectedLayer];
        Log.Information("Updating weightmap highlight for layer {Layer}", layer);
        
        _mapping.Bind();
        foreach (var component in Components)
        {
            if (component.Metadata is not { } metadata || metadata.DrawIds.Length == 0)
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
            
            _mapping.Update(metadata.DrawIds[0], m);
        }
        _mapping.Unbind();
        
        _updateMapping = false;
    }

    protected override void PreRender(CameraComponent camera, ShaderProgram shader)
    {
        base.PreRender(camera, shader);
    
        shader.SetUniform("uColorMode", (uint)_colorMode);
        shader.SetUniform("uSizeQuads", _sizeQuads);
        shader.SetUniform("uQuadCount", (float)Settings.TessellationQuadCount);
        shader.SetUniform("uGlobalScale", Settings.GlobalScale);
        
        _scales.Bind(3);
        _mapping.Bind(4);
    }

    public void DrawControls()
    {
        var c = (int) _colorMode;
        ImGui.Combo("Color Mode", ref c, "Heightmap\0Weightmap\0");
        _colorMode = (ColorMode) c;
        
        if (_colorMode == ColorMode.Weightmap)
        {
            var before = _selectedLayer;
            ImGui.Combo("Weightmap Layer", ref _selectedLayer, _layers.ToArray(), _layers.Count);
            if (!_updateMapping) _updateMapping = before != _selectedLayer;
        }
    }
    
    private enum ColorMode : byte
    {
        Heightmap,
        Weightmap
    }
    
    private struct WeightHighlightMapping
    {
        public uint WeightmapIndex;
        public uint ChannelIndex;
        public Vector2 Padding;
        public Vector4 DebugColor;
    }
}