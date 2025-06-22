using System.Numerics;
using System.Text;
using CUE4Parse_Conversion.Meshes.PSK;
using OpenTK.Graphics.OpenGL4;
using Snooper.Core.Containers.Buffers;
using Snooper.Rendering.Components;
using Snooper.Rendering.Primitives;

namespace Snooper.Core.Containers.Resources;

public class IndirectResources<TVertex>(int initialDrawCapacity) : IBind, IMemorySizeProvider where TVertex : unmanaged
{
    private readonly DoubleBuffer<DrawIndirectBuffer> _commands = new(() => new DrawIndirectBuffer(initialDrawCapacity));
    private readonly ShaderStorageBuffer<Matrix4x4> _matrices = new(initialDrawCapacity);

    private readonly VertexArray _vao = new();
    public readonly ElementArrayBuffer<uint> EBO = new(initialDrawCapacity * 200);
    public readonly ArrayBuffer<TVertex> VBO = new(initialDrawCapacity * 100);

    public void Generate()
    {
        _commands.Generate();
        _matrices.Generate();
        _vao.Generate();
        EBO.Generate();
        VBO.Generate();
    }

    public void Bind()
    {
        _commands.Current.Bind();
        _matrices.Bind();
        _vao.Bind();
        EBO.Bind();
        VBO.Bind();
    }

    public void Unbind()
    {
        _commands.Current.Unbind();
        _matrices.Unbind();
        _vao.Unbind();
        EBO.Unbind();
        VBO.Unbind();
    }

    public IndirectDrawMetadata Add(TPrimitiveData<TVertex> primitive, Matrix4x4[] matrices)
    {
        var firstIndex = EBO.AddRange(primitive.Indices);
        var baseVertex = VBO.AddRange(primitive.Vertices);
        var baseInstance = _matrices.AddRange(matrices);

        var drawId = _commands.Current.Add(new DrawElementsIndirectCommand
        {
            Count = (uint) primitive.Indices.Length,
            InstanceCount = (uint) matrices.Length,
            FirstIndex = (uint) firstIndex,
            BaseVertex = (uint) baseVertex,
            BaseInstance = (uint) baseInstance
        });

        return new IndirectDrawMetadata
        {
            DrawIds = [drawId],
            BaseInstance = baseInstance,
        };
    }
    
    public IndirectDrawMetadata Add2(TPrimitiveData<TVertex> primitive, CMeshSection[] sections, Matrix4x4[] matrices)
    {
        var firstIndex = EBO.AddRange(primitive.Indices);
        var baseVertex = VBO.AddRange(primitive.Vertices);
        var baseInstance = _matrices.AddRange(matrices);

        var metadata = new IndirectDrawMetadata { DrawIds = new int[sections.Length], BaseInstance = baseInstance };
        for (var i = 0; i < sections.Length; i++)
        {
            var section = sections[i];
            metadata.DrawIds[i] = _commands.Current.Add(new DrawElementsIndirectCommand
            {
                Count = (uint)section.NumFaces * 3,
                InstanceCount = (uint)matrices.Length,
                FirstIndex = (uint)(firstIndex + section.FirstIndex),
                BaseVertex = (uint)baseVertex,
                BaseInstance = (uint)baseInstance
            });
        }

        return metadata;
    }

    public void Update(TPrimitiveComponent<TVertex> component)
    {
        var instanceCount = component.Actor.VisibleInstances.End.Value - component.Actor.VisibleInstances.Start.Value;
        var baseInstance = component.DrawMetadata.BaseInstance + component.Actor.VisibleInstances.Start.Value;
        _commands.Current.UpdateInstance(component.DrawMetadata.DrawIds, component.IsVisible ? (uint)instanceCount : 0u, (uint)baseInstance);
        
        _matrices.Update(component.DrawMetadata.BaseInstance, component.GetWorldMatrices());
    }

    public void UpdateVertices(int drawId, TVertex[] vertices)
    {
        var command = _commands.Current[drawId];
        VBO.Update((int) command.BaseVertex, vertices);
    }

    public void UpdatePrimitive(int drawId, uint[] indices, TVertex[] vertices)
    {
        var command = _commands.Current[drawId];
        EBO.Update((int) command.FirstIndex, indices);
        VBO.Update((int) command.BaseVertex, vertices);

        _commands.Current.UpdateCount(drawId, (uint) indices.Length);
    }

    public void Remove(IndirectDrawMetadata metadata)
    {
        _commands.Current.Bind();
        _matrices.Bind();

        _commands.Current.RemoveRange(metadata.DrawIds);
        _matrices.Remove(metadata.BaseInstance);

        _commands.Current.Unbind();
        _matrices.Unbind();
    }

    public void Render()
    {
        _commands.Current.Bind();
        _matrices.Bind(0);
        _vao.Bind();

        GL.MultiDrawElementsIndirect(PrimitiveType.Triangles, DrawElementsType.UnsignedInt, IntPtr.Zero, _commands.Current.Count, 0);

        // _vao.Unbind();
        // EBO.Unbind();
        _commands.Current.Unbind();

        // _commands.Swap();
    }

    public string GetFormattedSpace()
    {
        var builder = new StringBuilder();
        builder.AppendLine($"IndirectResources<{typeof(TVertex).Name}>:");
        builder.AppendLine($"    Commands: {_commands.Current.GetFormattedSpace()}");
        builder.AppendLine($"    Matrices: {_matrices.GetFormattedSpace()}");
        builder.AppendLine($"    Indices:  {EBO.GetFormattedSpace()}");
        builder.AppendLine($"    Vertices: {VBO.GetFormattedSpace()}");
        return builder.ToString();
    }

    public GetPName Name => throw new NotImplementedException();
    public int PreviousHandle => throw new NotImplementedException();
}
