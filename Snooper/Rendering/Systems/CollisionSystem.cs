using System.Numerics;
using CUE4Parse.UE4.Objects.PhysicsEngine;
using ImGuiNET;
using OpenTK.Graphics.OpenGL4;
using Snooper.Core.Containers.Buffers;
using Snooper.Core.Containers.Programs;
using Snooper.Core.Containers.Resources;
using Snooper.Core.Systems;
using Snooper.Rendering.Components.Camera;
using Snooper.Rendering.Components.Visualization;
using Snooper.UI;

namespace Snooper.Rendering.Systems;

public class CollisionSystem : PrimitiveSystem<CollisionComponentVisualization, PerInstanceData, PerMaterialCollisionData>, IControllable
{
    public override ActorSystemType SystemType => ActorSystemType.Overlay;
    public override uint Order => 28;
    protected override Dictionary<CommandBufferType, ShaderProgram> Shaders { get; } = new()
    {
        [CommandBufferType.Transparent] = new EmbeddedShader("collision")
    };

    private readonly Vector3[] _palette = // ECollisionEnabled
    [
        new(0.50f, 0.50f, 0.50f), // NoCollision
        new(1.00f, 0.60f, 0.10f), // QueryOnly
        new(0.20f, 0.55f, 1.00f), // PhysicsOnly
        new(0.95f, 0.20f, 0.15f), // QueryAndPhysics
        new(0.60f, 0.35f, 1.00f), // ProbeOnly
        new(1.00f, 0.35f, 0.75f)  // QueryAndProbe
    ];
    private bool _throughMeshes;
    private bool _scanPaused;
    private float _scanSpeed = 3f;

    protected override void PreRender(CameraComponent camera, ShaderProgram shader)
    {
        base.PreRender(camera, shader);

        shader.SetUniform("uPalette", _palette);
        shader.SetUniform("uScanOffset", (ActorManager?.Time ?? 0f) * _scanSpeed);
        shader.SetUniform("uScanPaused", _scanPaused);

        GL.DepthMask(false);
        GL.Enable(EnableCap.PolygonOffsetFill);
        GL.PolygonOffset(0.01f, 16f); // ahead of what lies exactly on the same surface, reversed depth so positive
        GL.ColorMask(1, false, false, false, false); // disable picking on collision meshes
        if (_throughMeshes) GL.Disable(EnableCap.DepthTest);
    }

    protected override void PostRender(CameraComponent camera, ShaderProgram shader)
    {
        if (_throughMeshes) GL.Enable(EnableCap.DepthTest);
        GL.ColorMask(1, true, true, true, true);
        GL.PolygonOffset(0f, 0f);
        GL.Disable(EnableCap.PolygonOffsetFill);
        GL.DepthMask(true);

        base.PostRender(camera, shader);
    }

    public override void RenderMask(CameraComponent camera)
    {
        // no outline whatsoever
    }

    public void DrawControls()
    {
        EditorUI.PropertyValueTable("Collision Table", () =>
        {
            for (var type = ECollisionEnabled.QueryOnly; type <= ECollisionEnabled.QueryAndProbe; type++)
            {
                EditorUI.ColorEdit3(type.ToString(), ref _palette[(int) type]);
            }
            EditorUI.Checkbox("Through Meshes", ref _throughMeshes);
            EditorUI.Checkbox("Pause Scan", ref _scanPaused);
            ImGui.BeginDisabled(_scanPaused);
            EditorUI.SliderFloat("Scan Speed", ref _scanSpeed, 0.5f, 20f, "%.1f m/s");
            ImGui.EndDisabled();
        });
    }
}
