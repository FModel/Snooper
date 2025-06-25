using OpenTK.Graphics.OpenGL4;
using Snooper.Core.Systems;
using Snooper.Rendering.Components;
using Snooper.Rendering.Components.Camera;

namespace Snooper.Rendering.Systems;

public class DebugSystem : PrimitiveSystem<DebugComponent>
{
    public override uint Order => 100;
    protected override bool AllowDerivation => true;
    protected override bool IsRenderable => ActorManager?.DrawBoundingBoxes ?? false;

    public override void Load()
    {
        Shader.Fragment =
"""
#version 460 core

out vec4 FragColor;

void main()
{
    FragColor = vec4(vec3(0.75), 1.0);
}
""";

        base.Load();
    }

    public override void Update(float delta)
    {
        if (!IsRenderable) return;
        base.Update(delta);
    }
    
    protected override void PreRender(CameraComponent camera)
    {
        _polygonMode = (PolygonMode)GL.GetInteger(GetPName.PolygonMode);
        _bDiff = _polygonMode != PolygonMode.Line;
        if (_bDiff) GL.PolygonMode(TriangleFace.FrontAndBack, PolygonMode.Line);
        
        base.PreRender(camera);
    }
    
    private bool _bDiff;
    private PolygonMode _polygonMode;

    protected override void PostRender(CameraComponent camera)
    {
        if (_bDiff) GL.PolygonMode(TriangleFace.FrontAndBack, _polygonMode);
    }
}
