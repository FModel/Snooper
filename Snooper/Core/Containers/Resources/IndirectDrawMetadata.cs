namespace Snooper.Core.Containers.Resources;

public struct IndirectDrawMetadata()
{
    public int[] DrawIds = []; // one draw per section
    public int BaseInstance = -1; // base instance in the matrix buffer
}