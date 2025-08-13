using System.Numerics;
using CUE4Parse.UE4.Objects.Core.Math;
using ImGuiNET;
using Snooper.Core;
using Snooper.Rendering.Systems;
using Snooper.UI;

namespace Snooper.Rendering.Components.Transforms;

[DefaultActorSystem(typeof(TransformSystem))]
public sealed class TransformComponent() : ActorComponent, IControllable
{
    private Matrix4x4 _localMatrix = Matrix4x4.Identity;
    public Matrix4x4 LocalMatrix
    {
        get => _localMatrix;
        private set
        {
            if (_localMatrix == value)
                return;
            
            _localMatrix = value;
            Actor?.MarkDirty();
        }
    }
    
    private Matrix4x4 _worldMatrix = Matrix4x4.Identity;
    public Matrix4x4 WorldMatrix
    {
        get => _worldMatrix;
        private set
        {
            if (_worldMatrix == value)
                return;
            
            _worldMatrix = value;
            Actor?.MarkDirty();
        }
    }

    public Vector3 Position = Vector3.Zero;
    public Quaternion Rotation = Quaternion.Identity;
    public Vector3 Scale = Vector3.One;

    public TransformComponent? Relation
    {
        get => Actor?.Parent?.Transform;
        set
        {
            if (Actor != null)
                Actor.Parent = value?.Actor;
        }
    }

    public IEnumerable<TransformComponent> Children
    {
        get
        {
            if (Actor is null) yield break;
            foreach (var actor in Actor.Children)
            {
                yield return actor.Transform;
            }
        }
    }
    
    public TransformComponent(FTransform transform) : this()
    {
        Position = new Vector3(transform.Translation.X, transform.Translation.Z, transform.Translation.Y) * Settings.GlobalScale;
        Rotation = new Quaternion(transform.Rotation.X, transform.Rotation.Z, transform.Rotation.Y, -transform.Rotation.W);
        Scale = new Vector3(transform.Scale3D.X, transform.Scale3D.Z, transform.Scale3D.Y);
    }

    public void UpdateLocalMatrix()
    {
        LocalMatrix = Matrix4x4.CreateScale(Scale) * Matrix4x4.CreateFromQuaternion(Rotation) * Matrix4x4.CreateTranslation(Position);
    }

    public void UpdateWorldMatrix(bool recursive = true)
    {
        UpdateLocalMatrix();
        UpdateWorldMatrixInternal(recursive);
    }

    internal void UpdateWorldMatrixInternal(bool recursive)
    {
        if (Relation is null)
        {
            WorldMatrix = LocalMatrix;
        }
        else
        {
            if (recursive) Relation.UpdateWorldMatrix();
            WorldMatrix = LocalMatrix * Relation.WorldMatrix;
        }
    }
    
    public static implicit operator TransformComponent(FTransform transform) => new(transform);

    public void DrawControls()
    {
        ImGui.DragFloat3("Position", ref Position, 0.1f);
        
        ImGui.DragFloat("Rotation X", ref Rotation.X, 0.01f, 0, 1);
        ImGui.DragFloat("Rotation Y", ref Rotation.Y, 0.01f, 0, 1);
        ImGui.DragFloat("Rotation Z", ref Rotation.Z, 0.01f, 0, 1);
        ImGui.DragFloat("Rotation W", ref Rotation.W, 0.01f, 0, 1);
        
        ImGui.DragFloat3("Scale", ref Scale, 0.01f, 0.01f, 100f);
    }
}
