using OpenTK.Graphics.OpenGL4;

namespace Snooper.Core.Containers.Buffers;

public sealed class DrawIndirectBuffer(BufferUsageHint usageHint = BufferUsageHint.StaticDraw) : Buffer<DrawElementsIndirectCommand>(BufferTarget.DrawIndirectBuffer, usageHint)
{
    public override GetPName PName => GetPName.DrawIndirectBufferBinding;

    public void Bind(uint index)
    {
        GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, index, Handle);
    }
}

public struct DrawElementsIndirectCommand
{
    public uint IndexCount;
    public uint InstanceCount;
    public uint FirstIndex;
    public uint BaseVertex;
    public uint BaseInstance;

    public const int InstanceCountOffset = 4;
}
