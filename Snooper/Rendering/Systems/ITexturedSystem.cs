using Snooper.Core.Containers;
using Snooper.Core.Systems;

namespace Snooper.Rendering.Systems;

public interface ITexturedSystem : IMemoryDetailsProvider
{
    public TextureManager TextureManager { get; }
}