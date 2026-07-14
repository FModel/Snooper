namespace Snooper.Core.Containers.Buffers;

public static class BindingPoints
{
    public const uint DrawCommands = 0;
    public const uint InstanceData = 1;
    public const uint MaterialData = 2;
    public const uint DrawData = 3;
    public const uint MeshData = 4;
    public const uint VertexColors = 5;
    public const uint CullLodData = 6;
    public const uint CullSections = 7;

    public const uint SkinPoses = 10;
    public const uint SkinInverseBind = 11;
    public const uint SkinBoneInfluences = 12;
    public const uint SkinBoneInfluenceOffsets = 13;
    public const uint SkinMeshData = 14;
    public const uint SkinPoseMapping = 15;

    public const uint SplineMapping = 16;
    public const uint SplineParams = 17;

    public const uint LandscapeScales = 18;
    public const uint LandscapeWeightMapping = 19;

    public const uint LightData = 20;
    public const uint LightClusterData = 21;
    public const uint LightIndexList = 22;
    public const uint LightClusterAabbs = 23;
    public const uint LightGlobalIndexCount = 24;

    public static string GlslDefines { get; } = string.Join('\n',
        $"#define BINDING_DRAW_COMMANDS {DrawCommands}",
        $"#define BINDING_INSTANCE_DATA {InstanceData}",
        $"#define BINDING_MATERIAL_DATA {MaterialData}",
        $"#define BINDING_DRAW_DATA {DrawData}",
        $"#define BINDING_MESH_DATA {MeshData}",
        $"#define BINDING_VERTEX_COLORS {VertexColors}",
        $"#define BINDING_CULL_LOD_DATA {CullLodData}",
        $"#define BINDING_CULL_SECTIONS {CullSections}",
        $"#define BINDING_SKIN_POSES {SkinPoses}",
        $"#define BINDING_SKIN_INVERSE_BIND {SkinInverseBind}",
        $"#define BINDING_SKIN_BONE_INFLUENCES {SkinBoneInfluences}",
        $"#define BINDING_SKIN_BONE_INFLUENCE_OFFSETS {SkinBoneInfluenceOffsets}",
        $"#define BINDING_SKIN_MESH_DATA {SkinMeshData}",
        $"#define BINDING_SKIN_POSE_MAPPING {SkinPoseMapping}",
        $"#define BINDING_SPLINE_MAPPING {SplineMapping}",
        $"#define BINDING_SPLINE_PARAMS {SplineParams}",
        $"#define BINDING_LANDSCAPE_SCALES {LandscapeScales}",
        $"#define BINDING_LANDSCAPE_WEIGHT_MAPPING {LandscapeWeightMapping}",
        $"#define BINDING_LIGHT_DATA {LightData}",
        $"#define BINDING_LIGHT_CLUSTER_DATA {LightClusterData}",
        $"#define BINDING_LIGHT_INDEX_LIST {LightIndexList}",
        $"#define BINDING_LIGHT_CLUSTER_AABBS {LightClusterAabbs}",
        $"#define BINDING_LIGHT_GLOBAL_INDEX_COUNT {LightGlobalIndexCount}") + "\n";
}
