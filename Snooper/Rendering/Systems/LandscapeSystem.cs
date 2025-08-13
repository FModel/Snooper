using System.Numerics;
using ImGuiNET;
using OpenTK.Graphics.OpenGL4;
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
    private readonly List<string> _layers = [];
    private ColorMode _colorMode = ColorMode.Heightmap;
    private int _selectedLayer;

    public override void Load()
    {
        base.Load();

        var sizeQuads = 0.0f;

        _scales.Generate();
        _scales.Bind();
        foreach (var component in Components)
        {
            _scales.AddRange(component.Scales);
            foreach (var layer in component.Layers)
            {
                if (!_layers.Contains(layer.Name))
                    _layers.Add(layer.Name);
            }
            sizeQuads = Math.Max(sizeQuads, component.SizeQuads);
        }
        _scales.Unbind();
        
        Shader.Use();
        Shader.SetUniform("uSizeQuads", sizeQuads);
        Shader.SetUniform("uQuadCount", (float)Settings.TessellationQuadCount);
        Shader.SetUniform("uGlobalScale", Settings.GlobalScale);
    }

    protected override void PreRender(CameraComponent camera, int batchIndex = 0)
    {
        base.PreRender(camera, batchIndex);
    
        Shader.SetUniform("uColorMode", (uint)_colorMode);
        if (_colorMode == ColorMode.Weightmap)
        {
            Shader.SetUniform("uLayerName", _layers.Count > _selectedLayer ? _layers[_selectedLayer] : string.Empty);
        }
        
        _scales.Bind(2);
    }

    public void DrawControls()
    {
        var c = (int) _colorMode;
        ImGui.Combo("Color Mode", ref c, "Heightmap\0Weightmap\0");
        if (_layers.Count == 0)
        {
            c = (int) ColorMode.Heightmap; // Default to Heightmap if no layers are available
        }
        _colorMode = (ColorMode) c;
        
        if (_colorMode == ColorMode.Weightmap)
        {
            ImGui.Combo("Weightmap Layer", ref _selectedLayer, _layers.ToArray(), _layers.Count);
        }
    }
}

public enum ColorMode : byte
{
    Heightmap,
    Weightmap
}