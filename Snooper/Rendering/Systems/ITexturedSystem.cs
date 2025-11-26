using Snooper.Core;
using Snooper.Core.Containers;

namespace Snooper.Rendering.Systems;

public interface ITexturedSystem : IMemoryDetailsProvider
{
    public TextureManager TextureManager { get; }
}
