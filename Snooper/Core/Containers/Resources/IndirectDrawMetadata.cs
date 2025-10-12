namespace Snooper.Core.Containers.Resources;

public struct IndirectDrawMetadata()
{
    public int DrawId = -1; // one draw per section
    public int BaseInstance = -1; // base instance in the matrix buffer
    public int OverrideLod = -1; // tracked LOD override value (-1 for automatic)
    public uint ModelId = 0; // model ID in the primitive descriptor buffer
}