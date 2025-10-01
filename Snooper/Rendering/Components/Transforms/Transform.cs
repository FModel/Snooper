using System.Numerics;
using CUE4Parse.UE4.Objects.Core.Math;

namespace Snooper.Rendering.Components.Transforms;

public class Transform()
{
    public static Transform Identity => new();
    
    public Vector3 Position = Vector3.Zero;
    public Quaternion Rotation = Quaternion.Identity;
    public Vector3 Scale = Vector3.One;
    
    public Transform(FTransform transform) : this()
    {
        Position = new Vector3(transform.Translation.X, transform.Translation.Z, transform.Translation.Y) * Settings.GlobalScale;
        Rotation = new Quaternion(transform.Rotation.X, transform.Rotation.Z, transform.Rotation.Y, -transform.Rotation.W);
        Scale = new Vector3(transform.Scale3D.X, transform.Scale3D.Z, transform.Scale3D.Y);
    }

    public Matrix4x4 ToMatrix()
    {
        return Matrix4x4.CreateScale(Scale) *
               Matrix4x4.CreateFromQuaternion(Quaternion.Normalize(Rotation)) *
               Matrix4x4.CreateTranslation(Position);
    }
    
    public static implicit operator Transform(FTransform transform) => new(transform);
}