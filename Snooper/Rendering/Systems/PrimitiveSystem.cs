using System.Numerics;
using OpenTK.Graphics.OpenGL4;
using Snooper.Core.Containers.Buffers;
using Snooper.Core.Containers.Programs;
using Snooper.Rendering.Components;
using Snooper.Rendering.Components.Camera;

namespace Snooper.Rendering.Systems;

public abstract class PrimitiveSystem<TVertex, TComponent>(int initialDrawCapacity, PrimitiveType type = PrimitiveType.Triangles) : IndirectRenderSystem<TVertex, TComponent>(initialDrawCapacity, type) where TVertex : unmanaged where TComponent : TPrimitiveComponent<TVertex>
{
    public override uint Order => 20;
    protected override bool AllowDerivation => false;
    protected abstract int BatchCount { get; }

    protected virtual ShaderProgram Shader { get; } = new(
"""
#version 460 core
layout (location = 0) in vec3 aPos;

layout(std430, binding = 0) readonly buffer ModelMatrices
{
    mat4 uModelMatrices[];
};

uniform mat4 uViewMatrix;
uniform mat4 uProjectionMatrix;

void main()
{
    gl_Position = uProjectionMatrix * uViewMatrix * uModelMatrices[gl_BaseInstance + gl_InstanceID] * vec4(aPos, 1.0);
}
""",
"""
#version 460 core

out vec4 FragColor;

void main()
{
   FragColor = vec4(0.0f, 0.0f, 1.0f, 0.75f);
}
""");

    public override void Load()
    {
        base.Load();

        Shader.Generate();
        Shader.Link();
    }

    protected virtual void PreRender(CameraComponent camera, int batchIndex = 0)
    {
        Shader.Use();
        Shader.SetUniform("uViewMatrix", camera.ViewMatrix);
        Shader.SetUniform("uProjectionMatrix", camera.ProjectionMatrix);
    }

    public sealed override void Render(CameraComponent camera)
    {
        if (!IsRenderable) return;

        for (var batchIndex = 0; batchIndex < Resources.Count; batchIndex += BatchCount)
        {
            PreRender(camera, batchIndex);
            Resources.RenderBatch(batchIndex, BatchCount);
            PostRender(camera, batchIndex);
        }
    }

    protected virtual void PostRender(CameraComponent camera, int batchIndex = 0)
    {
        
    }
}

public class PrimitiveSystem<TComponent>() : PrimitiveSystem<Vector3, TComponent>(10) where TComponent : TPrimitiveComponent<Vector3>
{
    protected override Action<ArrayBuffer<Vector3>> PointersFactory { get; } = buffer =>
    {
        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, buffer.Stride, 0);
        GL.EnableVertexAttribArray(0);
    };

    protected override int BatchCount => int.MaxValue;
}

public class PrimitiveSystem : PrimitiveSystem<PrimitiveComponent>;
