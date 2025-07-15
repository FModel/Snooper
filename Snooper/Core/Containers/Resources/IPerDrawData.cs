namespace Snooper.Core.Containers.Resources;

/// <summary>
/// read back: gl_DrawID
/// </summary>
public interface IPerDrawData
{
    public void SetDefault();
}

public struct PerDrawData : IPerDrawData
{
    public void SetDefault()
    {
        
    }
}