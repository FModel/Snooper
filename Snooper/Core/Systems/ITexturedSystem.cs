using Snooper.Core.Containers;
using Snooper.Core.Managers;

namespace Snooper.Core.Systems;

public interface ITexturedSystem : IMemoryDetailsProvider
{
    public TextureManager TextureManager { get; }
}
