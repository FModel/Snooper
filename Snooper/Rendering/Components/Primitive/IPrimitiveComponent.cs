using Snooper.Core.Containers.Resources;
using Snooper.Rendering.Components.Descriptors;

namespace Snooper.Rendering.Components.Primitive;

public interface IPrimitiveComponent
{
    public ResourcesMetadata? Metadata { get; }
    public MaterialSection[] Materials { get; }
    public bool IsOpaque { get; }
    public bool IsVisible { get; set; }
}