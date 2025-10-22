using System.Numerics;
using ImGuiNET;
using OpenTK.Graphics.OpenGL4;
using Snooper.Core.Containers.Programs;
using Snooper.Core.Containers.Resources;
using Snooper.Core.Containers.Textures;
using Snooper.Rendering.Components.Camera;
using Snooper.Rendering.Components.Primitive;
using Snooper.Rendering.Primitives;
using Snooper.UI;

namespace Snooper.Rendering.Systems;

public class TextRenderSystem() : PrimitiveSystem<TextVertex, TextRenderComponent, PerInstanceData, PerMaterialTextData>(5), IControllable
{
    public override uint Order => 30;
    protected override bool IsCulled => false; // TODO: properly calculate bounding box then re-enable
    protected override ShaderProgram Shader { get; } = new EmbeddedShaderProgram("text");
    protected override Action<int> VertexLayout { get; } = stride =>
    {
        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, 0);
        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, stride, 12);
        GL.EnableVertexAttribArray(1);
    };

    public override void Load()
    {
        base.Load();
        
        FontAtlasTexture.Instance.Generate();
    }

    protected override void PreRender(CameraComponent camera, ShaderProgram shader)
    {
        base.PreRender(camera, shader);
        
        var fontAtlas = FontAtlasTexture.Instance;
        fontAtlas.Bind(TextureUnit.Texture0);
        shader.SetUniform("uTextTexture", 0);
    }
    
    public void DrawControls()
    {
        ImGui.TextUnformatted($"Width: {FontAtlasTexture.Instance.Width}, Height: {FontAtlasTexture.Instance.Height}");

        var width = ImGui.GetWindowWidth() - ImGui.GetScrollX();
        var aspect = (float)FontAtlasTexture.Instance.Height / FontAtlasTexture.Instance.Width;
        
        ImGui.Image(FontAtlasTexture.Instance.GetPointer(), new Vector2(width, width * aspect));
    }
}