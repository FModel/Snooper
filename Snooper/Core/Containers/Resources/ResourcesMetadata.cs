namespace Snooper.Core.Containers.Resources;

public struct ResourcesMetadata(int baseGeometry, int baseInstance, int baseMaterial, int[] drawIds)
{
    public readonly int BaseGeometry = baseGeometry;
    public readonly int BaseInstance = baseInstance;
    public readonly int BaseMaterial = baseMaterial;
    public readonly int[] DrawIds = drawIds; // we create one draw per section in lod 0
    
    public int OverrideLod { get; set; } = -1;
    
    public bool IsGenerated => DrawIds.Length > 0;
}

