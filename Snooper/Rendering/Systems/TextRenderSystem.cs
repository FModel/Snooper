using System.Numerics;
using ImGuiNET;
using OpenTK.Graphics.OpenGL4;
using Snooper.Core.Containers;
using Snooper.Core.Containers.Buffers;
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
    protected override Dictionary<CommandBufferType, ShaderProgram> Shaders { get; } = new()
    {
        [CommandBufferType.Transparent] = new EmbeddedShader("text")
    };
    protected override Action<VertexArrayLayout> VertexLayout { get; } = layout => layout
        .Float(0, 2)
        .Float(1, 2, offset: 8);

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

    public override void Dispose()
    {
        base.Dispose();

        FontAtlasTexture.Invalidate();
    }
}
