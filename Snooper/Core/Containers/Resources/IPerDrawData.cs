using Snooper.Core.Containers.Textures;
using Snooper.UI;

namespace Snooper.Core.Containers.Resources;

/// <summary>
/// read back: gl_DrawID
/// </summary>
public interface IPerDrawData
{
    public bool IsReady { get; }
}

public struct PerDrawData : IPerDrawData
{
    public bool IsReady { get; init; }
}

public interface IDrawDataContainer : IControllable, IDisposable
{
    public IPerDrawData? Raw { get; }
    public bool HasTextures { get; }
    
    public Dictionary<string, Texture> GetTextures();
    public void SetBindlessTexture(string key, BindlessTexture bindless);
    
    public void FinalizeGpuData();
}