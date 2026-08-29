namespace Snooper.Core.Containers.Buffers;

public abstract class Bindings
{
    public const uint InstanceData = 0;
    public const uint MaterialData = 1;
    public const uint DrawStatic = 2;
    public const uint MeshData = 3;
    public const uint VertexColors = 4;
    public const uint DrawCulled = 5;
    public const uint BaseMaxBinding = DrawCulled;

    protected static string Define(string name, uint binding) => $"BINDING_{name} {binding}";

    public static string GlslDefines { get; } = string.Join('\n',
        $"#define BINDING_INSTANCE_DATA {InstanceData}",
        $"#define BINDING_MATERIAL_DATA {MaterialData}",
        $"#define BINDING_DRAW_STATIC {DrawStatic}",
        $"#define BINDING_MESH_DATA {MeshData}",
        $"#define BINDING_VERTEX_COLORS {VertexColors}",
        $"#define BINDING_DRAW_CULLED {DrawCulled}") + "\n";
}
