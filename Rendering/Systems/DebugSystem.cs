using System.Numerics;
using OpenTK.Graphics.OpenGL4;
using Snooper.Core.Systems;
using Snooper.Rendering.Components;
using Snooper.Rendering.Components.Camera;

namespace Snooper.Rendering.Systems;

public class DebugSystem : PrimitiveSystem<Vector3, DebugComponent>
{
    public override uint Order => 100;
    public override ActorSystemType SystemType => ActorSystemType.Background;
    protected override bool AllowDerivation => true;

    public override void Load()
    {
        Shader.Fragment =
"""
#version 330 core

out vec4 FragColor;

void main()
{
    FragColor = vec4(1.0, 1.0, 1.0, 0.5);
}
""";

        base.Load();
    }

    public override void Update(float delta)
    {
        if (!DebugMode) return;
        base.Update(delta);
    }

    public override void Render(CameraComponent camera)
    {
        if (!DebugMode) return;

        var bCull = GL.GetBoolean(GetPName.CullFace);
        if (bCull) GL.Disable(EnableCap.CullFace);
        base.Render(camera);
        if (bCull) GL.Enable(EnableCap.CullFace);
    }
}
