namespace Snooper.Core.Containers.Buffers;

public abstract class Bindings
{
    public const uint DrawCommands = 0;
    public const uint InstanceData = 1;
    public const uint MaterialData = 2;
    public const uint DrawData = 3;
    public const uint MeshData = 4;
    public const uint VertexColors = 5;
    public const uint CullLodData = 6;
    public const uint CullSections = 7;
    public const uint BaseMaxBinding = CullSections;

    protected static string Define(string name, uint binding) => $"BINDING_{name} {binding}";

    public static string GlslDefines { get; } = string.Join('\n',
        $"#define BINDING_DRAW_COMMANDS {DrawCommands}",
        $"#define BINDING_INSTANCE_DATA {InstanceData}",
        $"#define BINDING_MATERIAL_DATA {MaterialData}",
        $"#define BINDING_DRAW_DATA {DrawData}",
        $"#define BINDING_MESH_DATA {MeshData}",
        $"#define BINDING_VERTEX_COLORS {VertexColors}",
        $"#define BINDING_CULL_LOD_DATA {CullLodData}",
        $"#define BINDING_CULL_SECTIONS {CullSections}") + "\n";
}
