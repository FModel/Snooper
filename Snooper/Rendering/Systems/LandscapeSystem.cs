using System.Numerics;
using OpenTK.Graphics.OpenGL4;
using Snooper.Core.Containers.Buffers;
using Snooper.Core.Containers.Programs;
using Snooper.Core.Containers.Resources;
using Snooper.Core.Systems;
using Snooper.Rendering.Components.Camera;
using Snooper.Rendering.Components.Mesh;

namespace Snooper.Rendering.Systems;

public class LandscapeSystem() : PrimitiveSystem<Vector2, LandscapeMeshComponent, PerInstanceData, PerDrawLandscapeData>(100, PrimitiveType.Patches)
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

    public override void Load()
    {
        base.Load();

        var sizeQuads = 0.0f;

        _scales.Generate();
        _scales.Bind();
        foreach (var component in Components)
        {
            _scales.AddRange(component.Scales);
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
    
        _scales.Bind(2);
    }
}