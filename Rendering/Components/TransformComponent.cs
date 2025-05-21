using System.Numerics;
using Snooper.Core;
using Snooper.Rendering.Systems;

namespace Snooper.Rendering.Components;

[DefaultActorSystem(typeof(TransformSystem))]
public sealed class TransformComponent : ActorComponent
{
    public Matrix4x4 LocalMatrix = Matrix4x4.Identity;
    public Matrix4x4 WorldMatrix = Matrix4x4.Identity;

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

    public void UpdateLocalMatrix()
    {
        LocalMatrix = Matrix4x4.CreateScale(Scale) * Matrix4x4.CreateFromQuaternion(Rotation) * Matrix4x4.CreateTranslation(Position);
    }

    public void UpdateWorldMatrix()
    {
        UpdateLocalMatrix();
        UpdateWorldMatrixInternal(true);
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
}
