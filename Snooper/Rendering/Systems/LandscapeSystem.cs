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

public class LandscapeSystem() : PrimitiveSystem<Vector2, LandscapeMeshComponent, PerInstanceData, PerDrawLandscapeData>(100, PrimitiveType.Patches), IControllable
{
    public override uint Order => 21;
    public override ActorSystemType SystemType => ActorSystemType.Deferred;
    protected override ShaderProgram Shader { get; } = new EmbeddedShaderProgram("Landscape/landscape")
    {
        TessellationControl = "Landscape/landscape.tesc",
        TessellationEvaluation = "Landscape/landscape.tese"
    };
    protected override Action<ArrayBuffer<Vector2>> PointersFactory { get; } = buffer =>
    {
        GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, buffer.Stride, 0);
        GL.EnableVertexAttribArray(0);
    };
    
    private readonly ShaderStorageBuffer<Vector2> _scales = new(100 * Settings.TessellationQuadCountTotal);
    private readonly ShaderStorageBuffer<WeightHighlightMapping> _mapping = new(100);
    private readonly List<string> _layers = ["None"];
    private ColorMode _colorMode = ColorMode.Heightmap;
    private int _selectedLayer;
    private bool _updateMapping;

    public override void Load()
    {
        base.Load();

        var sizeQuads = 0.0f;

        _scales.Generate();
        _scales.Bind();
        foreach (var component in Components)
        {
            _scales.AddRange(component.Scales);
            foreach (var layer in component.Layers.Keys)
            {
                if (!_layers.Contains(layer)) _layers.Add(layer);
            }
            sizeQuads = Math.Max(sizeQuads, component.SizeQuads);
        }
        _scales.Unbind();
        
        _mapping.Generate();
        _mapping.Bind();
        _mapping.Allocate(new WeightHighlightMapping[ComponentsCount]);
        _mapping.Unbind();
        
        Shader.Use();
        Shader.SetUniform("uSizeQuads", sizeQuads);
        Shader.SetUniform("uQuadCount", (float)Settings.TessellationQuadCount);
        Shader.SetUniform("uGlobalScale", Settings.GlobalScale);
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
            
            _mapping.Update(component.Materials[0].DrawMetadata.DrawId, m);
        }
        _mapping.Unbind();
        
        _updateMapping = false;
    }

    protected override void PreRender(CameraComponent camera, int batchIndex = 0)
    {
        base.PreRender(camera, batchIndex);
    
        Shader.SetUniform("uColorMode", (uint)_colorMode);
        
        _scales.Bind(2);
        _mapping.Bind(3);
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