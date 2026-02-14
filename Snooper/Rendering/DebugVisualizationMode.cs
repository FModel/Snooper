using System.ComponentModel;

namespace Snooper.Rendering;

public enum DebugVisualizationMode
{
    None,
    PerComponent,
    PerInstance,
    PerMaterial,
    PerPrimitive,
    VertexColors,


    // new names
    Disabled,
    Clay,

    [Description("Show Components")]
    ComponentId,
    [Description("Show Instances")]
    InstanceId,
    [Description("Show Primitives")]
    PrimitiveId,
    [Description("Show Draws")]
    DrawId,

    [Description("Show Vertex Colors")]
    VertexColor,
    [Description("Show Normals")]
    Normals,
    [Description("Show Depth")]
    Depth,
    //[Description("Show Overdraw")]
    //Overdraw,

    [Description("Show LODs")]
    LODLevel,
    //[Description("Show Component Bounds")]
    //Bounds,

    [Description("Show Light Influence")]
    LightInfluence,
    //[Description("Show Light Bounds")]
    //LightBounds,

    [Description("Show Shadow Cascades")]
    ShadowCascades,
}
