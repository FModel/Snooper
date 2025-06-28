using System.Numerics;
using System.Text;
using OpenTK.Graphics.OpenGL4;
using Snooper.Core.Containers.Buffers;
using Snooper.Rendering.Components;
using Snooper.Rendering.Components.Mesh;
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
    
    public IndirectDrawMetadata Add(TPrimitiveData<TVertex> primitive, MeshMaterialSection[] materialSections, Matrix4x4[] matrices)
    {
        var firstIndex = EBO.AddRange(primitive.Indices);
        var baseVertex = VBO.AddRange(primitive.Vertices);
        var baseInstance = _matrices.AddRange(matrices);

        var metadata = new IndirectDrawMetadata { DrawIds = new int[materialSections.Length], BaseInstance = baseInstance };
        for (var i = 0; i < materialSections.Length; i++)
        {
            var materialSection = materialSections[i];
            metadata.DrawIds[i] = _commands.Current.Add(new DrawElementsIndirectCommand
            {
                Count = (uint)materialSection.IndexCount,
                InstanceCount = (uint)matrices.Length,
                FirstIndex = (uint)(firstIndex + materialSection.FirstIndex),
                BaseVertex = (uint)baseVertex,
                BaseInstance = (uint)baseInstance
            });
        }

        return metadata;
    }

    public void Update(TPrimitiveComponent<TVertex> component)
    {
        if (!component.Actor.IsDirty) return;
        
        var instanceCount = component.Actor.VisibleInstances.End.Value - component.Actor.VisibleInstances.Start.Value;
        var baseInstance = component.DrawMetadata.BaseInstance + component.Actor.VisibleInstances.Start.Value;
        _commands.Current.UpdateInstance(component.DrawMetadata.DrawIds, component.Actor.IsVisible ? (uint)instanceCount : 0u, (uint)baseInstance);
        
        _matrices.Update(component.DrawMetadata.BaseInstance, component.Actor.GetWorldMatrices());
        component.Actor.IsDirty = false;
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
