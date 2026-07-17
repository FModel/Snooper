using System.Runtime.InteropServices;
using OpenTK.Graphics.OpenGL4;
using Snooper.Core.Containers.Resources;
using Snooper.Rendering.Components.Descriptors;

namespace Snooper.Core.Containers.Buffers;

public sealed class DrawIndirectBuffer(BufferUsageHint usageHint = BufferUsageHint.StaticDraw) : Buffer<DrawElementsIndirectCommand>(BufferTarget.DrawIndirectBuffer, usageHint)
{
    public override GetPName PName => GetPName.DrawIndirectBufferBinding;

    public void Bind(uint index)
    {
        GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, index, Handle);
    }
}

public readonly struct DrawElementsIndirectCommand(SectionDescriptor section, uint instanceCount, GeometryHandle geometry, uint baseInstance)
{
    public readonly uint IndexCount = section.IndexCount;
    public readonly uint InstanceCount = instanceCount;
    public readonly uint FirstIndex = geometry.FirstIndex + section.FirstIndex;
    public readonly uint BaseVertex = geometry.BaseVertex;
    public readonly uint BaseInstance = baseInstance;

    public static readonly int InstanceCountOffset = (int)Marshal.OffsetOf<DrawElementsIndirectCommand>(nameof(InstanceCount));
}
