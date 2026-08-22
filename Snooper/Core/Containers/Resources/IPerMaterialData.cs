using Snooper.Core.Containers.Textures;
using Snooper.UI;
using System.Runtime.InteropServices;

namespace Snooper.Core.Containers.Resources;

public interface IPerMaterialData
{
    public bool IsReady { get; } // TODO: get rid of this
}

[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 4)]
public struct PerMaterialData : IPerMaterialData
{
    public bool IsReady { get; init; }
}

public interface IMaterialDataContainer : IControllable
{
    public string Name { get; }
    public IPerMaterialData? Raw { get; }
    public bool HasTextures { get; }
    public bool IsTranslucent { get; }

    public Dictionary<string, Texture> GetTextures();
    public void SetBindlessTexture(string key, BindlessTexture bindless);

    public void FinalizeGpuData();

    public void DrawSummary(int layerIndex = 0);
}
