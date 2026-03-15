using OpenTK.Graphics.OpenGL4;

namespace Snooper.Core.Containers.Buffers;

public sealed class DrawIndirectBuffer(BufferUsageHint usageHint = BufferUsageHint.DynamicDraw) : Buffer<DrawElementsIndirectCommand>(BufferTarget.DrawIndirectBuffer, usageHint)
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
    // end of struct for indirect draw commands
    // anything extra can be used as ssbo data

    public uint BaseGeometry; // index into the culling buffer for the geometry of this draw
    public uint BaseColor;
    public uint BaseBoneInfluence;
    public uint BaseBone; // first bone index in the bone/pose buffers for this mesh
    public uint BaseMaterial; // first index into the material buffer for this draw
    public uint MaterialIndex; // index of the material this draw should use relative to BaseMaterial
    public uint PickingId;
    public uint OriginalInstanceCount;
    public uint OriginalBaseInstance;
    public uint SectionId;
    public uint CastShadow;
}
