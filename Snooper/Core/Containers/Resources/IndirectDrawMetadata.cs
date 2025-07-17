namespace Snooper.Core.Containers.Resources;

public struct IndirectDrawMetadata()
{
    public int DrawId = -1; // one draw per section
    public int BaseInstance = -1; // base instance in the matrix buffer
}