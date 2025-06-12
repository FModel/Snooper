using System.Numerics;
using OpenTK.Graphics.OpenGL4;
using Snooper.Core.Containers;
using Snooper.Core.Containers.Buffers;
using Snooper.Core.Containers.Programs;
using Snooper.Core.Systems;
using Snooper.Rendering.Components;
using Snooper.Rendering.Components.Camera;

namespace Snooper.Rendering.Systems;

public abstract class PrimitiveSystem<TVertex, TComponent> : ActorSystem<TComponent> where TComponent : TPrimitiveComponent<TVertex> where TVertex : unmanaged
{
    public override uint Order => 20;
    protected override bool AllowDerivation => false;

    protected readonly DrawIndirectBuffer Commands = new(500);
    protected readonly ShaderStorageBuffer<Matrix4x4> Matrices = new(500);

    protected readonly VertexArray VAO = new();
    protected readonly ElementArrayBuffer<uint> EBO = new(100000);
    protected readonly ArrayBuffer<TVertex> VBO = new(50000);

    protected abstract Action<ArrayBuffer<TVertex>> PointersFactory { get; }
    protected abstract PolygonMode PolygonMode { get; }

    protected virtual ShaderProgram Shader { get; } = new(
"""
#version 460 core
layout (location = 0) in vec3 aPos;

layout(std430, binding = 0) buffer ModelMatrices
{
    mat4 uModelMatrices[];
};

uniform mat4 uViewProjectionMatrix;

void main()
{
    gl_Position = uViewProjectionMatrix * uModelMatrices[gl_DrawID] * vec4(aPos, 1.0);
}
""",
"""
#version 460 core

out vec4 FragColor;

void main()
{
   FragColor = vec4(0.0f, 0.0f, 1.0f, 1.0f);
}
""");

    public override void Load()
    {
        base.Load();

        Commands.Generate();
        Matrices.Generate();
        VAO.Generate();
        EBO.Generate();
        VBO.Generate();

        Commands.Bind();
        Matrices.Bind();
        VAO.Bind();
        EBO.Bind();
        VBO.Bind();

        foreach (var component in Components)
        {
            component.Generate(Commands, EBO, VBO);
            Matrices.Add(component.GetModelMatrix());
        }
        PointersFactory(VBO);

        VAO.Unbind();
        EBO.Unbind();
        VBO.Unbind();
        Matrices.Unbind();
        Commands.Unbind();

        Shader.Generate();
        Shader.Link();
    }

    public override void Update(float delta)
    {
        base.Update(delta);

        Commands.Bind();
        Matrices.Bind();
        VAO.Bind();
        EBO.Bind();
        VBO.Bind();

        foreach (var component in Components)
        {
            component.Update(Commands, EBO, VBO);
            Matrices.Update(component.DrawId, component.GetModelMatrix());
            Commands.UpdateInstanceCount(component.DrawId, component.IsVisible ? 1u : 0u);
        }

        VAO.Unbind();
        EBO.Unbind();
        VBO.Unbind();
        Matrices.Unbind();
        Commands.Unbind();
    }

    public override void Render(CameraComponent camera)
    {
        Shader.Use();
        Shader.SetUniform("uViewProjectionMatrix", camera.ViewProjectionMatrix);
        Matrices.Bind(0);

        RenderComponents();
    }

    protected void RenderComponents()
    {
        Commands.Bind();
        VAO.Bind();

        GL.MultiDrawElementsIndirect(PrimitiveType.Triangles, DrawElementsType.UnsignedInt, IntPtr.Zero, Commands.Count, 0);

        VAO.Unbind();
        Commands.Unbind();
    }
}

public class PrimitiveSystem<TComponent> : PrimitiveSystem<Vector3, TComponent> where TComponent : TPrimitiveComponent<Vector3>
{
    protected override Action<ArrayBuffer<Vector3>> PointersFactory { get; } = buffer =>
    {
        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, buffer.Stride, 0);
        GL.EnableVertexAttribArray(0);
    };

    protected override PolygonMode PolygonMode => PolygonMode.Fill;
}

public class PrimitiveSystem : PrimitiveSystem<PrimitiveComponent>;
