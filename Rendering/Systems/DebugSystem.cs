using System.Numerics;
using Snooper.Rendering.Components;
using Snooper.Rendering.Components.Camera;

namespace Snooper.Rendering.Systems;

public class DebugSystem : PrimitiveSystem<Vector3, DebugComponent>
{
    public override uint Order => 100;
    protected override bool AllowDerivation => true;

    public override void Load()
    {
        Shader.Fragment =
"""
#version 330 core

out vec4 FragColor;

void main()
{
    FragColor = vec4(1.0f, 1.0f, 1.0f, 0.5f);
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
        base.Render(camera);
    }
}
