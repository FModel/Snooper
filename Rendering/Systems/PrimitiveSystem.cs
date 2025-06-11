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

    protected readonly DrawIndirectBuffer Commands = new(0);
    protected readonly ShaderStorageBuffer<Matrix4x4> Matrices = new(0);

    protected readonly VertexArray VAO = new();
    protected readonly ElementArrayBuffer<uint> EBO = new(0);
    protected readonly ArrayBuffer<TVertex> VBO = new(0);
    protected abstract Action<ArrayBuffer<TVertex>> PointersFactory { get; }

    protected virtual ShaderProgram Shader { get; } = new(
"""
#version 430 core
layout (location = 0) in vec3 aPos;

layout(std430, binding = 0) buffer ModelMatrices
{
    mat4 uModelMatrices[];
};

uniform mat4 uViewProjectionMatrix;

void main()
{
    gl_Position = uViewProjectionMatrix * uModelMatrices[0] * vec4(aPos, 1.0);
}
""",
"""
#version 430 core

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

        Shader.Generate();
        Shader.Link();
    }

    public override void Update(float delta)
    {
        base.Update(delta);

        Commands.Bind();
        // Matrices.Bind();
        EBO.Bind();
        VBO.Bind();
        for (var i = 0; i < Components.Count; i++)
        {
            var component = Components.ElementAt(i);
            component.Update(Commands, EBO, VBO);
            // Matrices.Update(component.GetModelMatrix(), i);
        }
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
        VAO.Bind();
        GL.MultiDrawElementsIndirect(PrimitiveType.Triangles, DrawElementsType.UnsignedInt, IntPtr.Zero, Commands.Size, 0);
    }
}

public class PrimitiveSystem<TComponent> : PrimitiveSystem<Vector3, TComponent> where TComponent : TPrimitiveComponent<Vector3>
{
    protected override Action<ArrayBuffer<Vector3>> PointersFactory { get; } = buffer =>
    {
        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, buffer.Stride, 0);
        GL.EnableVertexAttribArray(0);
    };
}

public class PrimitiveSystem : PrimitiveSystem<PrimitiveComponent>;
