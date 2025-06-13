using System.Numerics;
using OpenTK.Graphics.OpenGL4;
using Snooper.Core.Containers.Buffers;
using Snooper.Rendering.Components;
using Snooper.Rendering.Primitives;

namespace Snooper.Core.Containers.Resources;

public class IndirectResources<TVertex>(int initialDrawCapacity) : IBind where TVertex : unmanaged
{
    private readonly DrawIndirectBuffer _commands = new(initialDrawCapacity);
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
        _commands.Bind();
        _matrices.Bind();
        _vao.Bind();
        EBO.Bind();
        VBO.Bind();
    }

    public void Unbind()
    {
        _commands.Unbind();
        _matrices.Unbind();
        _vao.Unbind();
        EBO.Unbind();
        VBO.Unbind();
    }

    public int Add(TPrimitiveData<TVertex> primitive, Matrix4x4 matrix)
    {
        var drawId = _commands.Add(new DrawElementsIndirectCommand
        {
            Count = (uint) primitive.Indices.Length,
            InstanceCount = 1,
            FirstIndex = (uint) EBO.Count,
            BaseVertex = (uint) VBO.Count,
            BaseInstance = 0
        });

        _matrices.Insert(drawId, matrix);
        EBO.AddRange(primitive.Indices);
        VBO.AddRange(primitive.Vertices);

        return drawId;
    }

    public void Update(TPrimitiveComponent<TVertex> component)
    {
        _commands.UpdateInstanceCount(component.DrawId, component.IsVisible ? 1u : 0u);
        _matrices.Update(component.DrawId, component.GetModelMatrix());
    }

    public void UpdateVertices(int drawId, TVertex[] vertices)
    {
        var command = _commands[drawId];
        VBO.Update((int) command.BaseVertex, vertices);
    }

    public void UpdatePrimitive(int drawId, uint[] indices, TVertex[] vertices)
    {
        var command = _commands[drawId];
        EBO.Update((int) command.FirstIndex, indices);
        VBO.Update((int) command.BaseVertex, vertices);

        _commands.UpdateCount(drawId, (uint) indices.Length);
    }

    public void Remove(int drawId)
    {
        _commands.Bind();
        _matrices.Bind();

        _commands.Remove(drawId);
        _matrices.Remove(drawId);

        _commands.Unbind();
        _matrices.Unbind();
    }

    public void RemoveAt(int index)
    {

    }

    public void Render()
    {
        _commands.Bind();
        _matrices.Bind(0);
        _vao.Bind();

        GL.MultiDrawElementsIndirect(PrimitiveType.Triangles, DrawElementsType.UnsignedInt, IntPtr.Zero, _commands.Count, 0);

        // _vao.Unbind();
        // EBO.Unbind();
        _commands.Unbind();
    }

    public GetPName Name => throw new NotImplementedException();
    public int PreviousHandle => throw new NotImplementedException();
}
