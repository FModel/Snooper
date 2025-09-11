using Snooper.Core.Containers.Textures;
using Snooper.UI;
using System.Runtime.InteropServices;

namespace Snooper.Core.Containers.Resources;

/// <summary>
/// read back: gl_DrawID
/// </summary>
public interface IPerDrawData
{
    public bool IsReady { get; }
}

[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 4)]
public struct PerDrawData : IPerDrawData
{
    public bool IsReady { get; init; }
}

public interface IDrawDataContainer : IControllable, IDisposable
{
    public IPerDrawData? Raw { get; }
    public bool HasTextures { get; }
    public bool IsTranslucent { get; }
    
    public Dictionary<string, Texture> GetTextures();
    public void SetBindlessTexture(string key, BindlessTexture bindless);
    
    public void FinalizeGpuData();
}