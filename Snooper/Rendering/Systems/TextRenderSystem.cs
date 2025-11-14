using System.Numerics;
using ImGuiNET;
using OpenTK.Graphics.OpenGL4;
using Snooper.Core.Containers;
using Snooper.Core.Containers.Programs;
using Snooper.Core.Containers.Resources;
using Snooper.Core.Containers.Textures;
using Snooper.Rendering.Components.Camera;
using Snooper.Rendering.Components.Primitive;
using Snooper.Rendering.Primitives;
using Snooper.UI;

namespace Snooper.Rendering.Systems;

public class TextRenderSystem : PrimitiveSystem<Vector4, TextRenderComponent, PerInstanceData, PerMaterialTextData>, IControllable
{
    public override uint Order => 51;
    protected override ShaderProgram Shader { get; } = new EmbeddedShaderProgram("text");
    protected override Action<uint> VertexLayout { get; } = vao =>
    {
        GL.VertexArrayAttribFormat(vao, 0, 2, VertexAttribType.Float, false, 0);
        GL.VertexArrayAttribFormat(vao, 1, 2, VertexAttribType.Float, false, 8);
        GL.EnableVertexArrayAttrib(vao, 0);
        GL.EnableVertexArrayAttrib(vao, 1);
        GL.VertexArrayAttribBinding(vao, 0, 0);
        GL.VertexArrayAttribBinding(vao, 1, 0);
    };

    protected override void OnLoad()
    {
        base.OnLoad();
        
        FontAtlasTexture.Instance.Generate();
    }

    protected override void PreRender(CameraComponent camera, ShaderProgram shader)
    {
        base.PreRender(camera, shader);
        
        var fontAtlas = FontAtlasTexture.Instance;
        fontAtlas.Bind(0);
        shader.SetUniform("uTextTexture", 0);
    }
    
    public override long Allocated => base.Allocated + FontAtlasTexture.Instance.Allocated;
    public override long Used => base.Used + FontAtlasTexture.Instance.Used;
    public override IEnumerable<MemoryDetail> GetMemoryDetails()
    {
        foreach (var detail in base.GetMemoryDetails())
            yield return detail;
        
        yield return new MemoryDetail("Font Atlas Texture", FontAtlasTexture.Instance);
    }
    
    public void DrawControls()
    {
        ImGui.TextUnformatted($"Width: {FontAtlasTexture.Instance.Width}, Height: {FontAtlasTexture.Instance.Height}");

        var width = ImGui.GetWindowWidth() - ImGui.GetScrollX();
        var aspect = (float)FontAtlasTexture.Instance.Height / FontAtlasTexture.Instance.Width;
        
        ImGui.Image(FontAtlasTexture.Instance.GetPointer(), new Vector2(width, width * aspect));
    }
}