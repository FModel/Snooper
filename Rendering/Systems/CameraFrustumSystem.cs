using OpenTK.Graphics.OpenGL4;
using Snooper.Rendering.Components.Camera;

namespace Snooper.Rendering.Systems;

public class CameraFrustumSystem : PrimitiveSystem<CameraFrustumComponent>
{
    public override void Update(float delta)
    {
        foreach (var component in Components)
        {
            component.Update();
        }
    }

    public override void Render(CameraComponent camera)
    {
        GL.PolygonMode(TriangleFace.FrontAndBack, PolygonMode.Line);
        base.Render(camera);
        GL.PolygonMode(TriangleFace.FrontAndBack, PolygonMode.Fill);
    }
}
