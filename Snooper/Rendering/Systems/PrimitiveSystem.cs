using System.Numerics;
using OpenTK.Graphics.OpenGL4;
using Snooper.Core.Containers.Buffers;
using Snooper.Core.Containers.Programs;
using Snooper.Rendering.Components;
using Snooper.Rendering.Components.Camera;

namespace Snooper.Rendering.Systems;

public abstract class PrimitiveSystem<TVertex, TComponent>(int initialDrawCapacity) : IndirectRenderSystem<TVertex, TComponent>(initialDrawCapacity) where TVertex : unmanaged where TComponent : TPrimitiveComponent<TVertex>
{
    public override uint Order => 20;
    protected override bool AllowDerivation => false;

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

    public override void Render(CameraComponent camera)
    {
        Shader.Use();
        Shader.SetUniform("uViewMatrix", camera.ViewMatrix);
        Shader.SetUniform("uProjectionMatrix", camera.ProjectionMatrix);

        Resources.Render();
    }
}

public class PrimitiveSystem<TComponent>() : PrimitiveSystem<Vector3, TComponent>(10) where TComponent : TPrimitiveComponent<Vector3>
{
    protected override Action<ArrayBuffer<Vector3>> PointersFactory { get; } = buffer =>
    {
        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, buffer.Stride, 0);
        GL.EnableVertexAttribArray(0);
    };
}

public class PrimitiveSystem : PrimitiveSystem<PrimitiveComponent>;
