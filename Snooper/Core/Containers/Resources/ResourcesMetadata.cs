namespace Snooper.Core.Containers.Resources;

public class ResourcesMetadata
{
    public uint ModelId { get; init; } = 0;
    public int BaseInstance { get; init; } = -1;
    public int OverrideLod { get; set; } = -1;
    public uint BaseMaterialOffset { get; init; } = 0;
    
    /// <summary>
    /// Draw IDs for each section in LOD 0
    /// </summary>
    public int[] SectionDrawIds { get; init; } = [];
    
    public bool IsGenerated => BaseInstance >= 0;
}

