namespace Snooper.Core.Containers.Buffers;

public abstract class Bindings
{
    public const uint InstanceData = 0;
    public const uint MaterialData = 1;
    public const uint DrawStatic = 2;
    public const uint MeshData = 3;
    public const uint VertexColors = 4;
    public const uint DrawCulled = 5;
    public const uint MaterialTable = 6;
    public const uint BaseMaxBinding = MaterialTable;

    // a copy of SkinnedBindings.MaxBinding to keep in sync.
    // a translucent mesh computes lights and shadows in its own shader, for every fragment, where the deferred pass does it once per pixel.
    // so it is drawn with the lights bound next to its own buffers: this offsets the light bindings past them, and everyone pays the price for it
    // TODO: could be reworked
    protected const uint TranslucentMaxBinding = BaseMaxBinding + 9;

    // ------------ scene lighting ------------
    // shared with the shaders, bound by each pass that reads them (forward, lighting)
    // uniform block indices
    public const uint SceneDataBlock = 0;
    public const uint ShadowViewsBlock = 1;
    // texture unit, reserved, its compare sampler stays on it after the pass
    public const uint ShadowMapUnit = 15;

    protected static string Define(string name, uint binding) => $"BINDING_{name} {binding}";

    public static string GlslDefines { get; } = string.Join('\n',
        $"#define BINDING_SCENE_DATA {SceneDataBlock}",
        $"#define BINDING_SHADOW_VIEWS {ShadowViewsBlock}",
        $"#define UNIT_SHADOW_MAP {ShadowMapUnit}",
        $"#define MAX_SHADOW_VIEWS {Settings.MaxShadowViews}",
        $"#define BINDING_INSTANCE_DATA {InstanceData}",
        $"#define BINDING_MATERIAL_DATA {MaterialData}",
        $"#define BINDING_DRAW_STATIC {DrawStatic}",
        $"#define BINDING_MESH_DATA {MeshData}",
        $"#define BINDING_VERTEX_COLORS {VertexColors}",
        $"#define BINDING_DRAW_CULLED {DrawCulled}",
        $"#define BINDING_MATERIAL_TABLE {MaterialTable}") + "\n";
}
