namespace Snooper.Core.Containers.Resources;

public readonly struct ResourcesMetadata(GeometryHandle geometryHandle, int baseInstance, int baseMaterial, int[] drawIds)
{
    public readonly GeometryHandle GeometryHandle = geometryHandle;
    public readonly int BaseInstance = baseInstance;
    public readonly int BaseMaterial = baseMaterial;
    public readonly int[] DrawIds = drawIds; // we create one draw per section in lod 0
    
    public bool IsGenerated => DrawIds.Length > 0;
}
