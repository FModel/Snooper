using System.Numerics;
using CUE4Parse.UE4.Objects.Core.Math;

namespace Snooper.Rendering.Components.Transforms;

public class Transform()
{
    public static Transform Identity => new();

    public Vector3 Position = Vector3.Zero;
    public Quaternion Rotation = Quaternion.Identity;
    public Vector3 Scale = Vector3.One;

    public Transform(Vector3 position, Quaternion rotation) : this()
    {
        Position = position;
        Rotation = rotation;
    }

    public Transform(Vector3 position) : this(position, Quaternion.Identity)
    {

    }

    public Transform(Quaternion rotation) : this(Vector3.Zero, rotation)
    {

    }

    public Transform(FVector position, FQuat rotation, FVector scale) : this()
    {
        Position = new Vector3(position.X, position.Z, position.Y) * Settings.GlobalScale;
        Rotation = new Quaternion(rotation.X, rotation.Z, rotation.Y, -rotation.W);
        Scale = new Vector3(scale.X, scale.Z, scale.Y);
    }

    public Transform(FTransform transform) : this(transform.Translation, transform.Rotation, transform.Scale3D)
    {

    }

    public Matrix4x4 ToMatrix()
    {
        return Matrix4x4.CreateScale(Scale) *
               Matrix4x4.CreateFromQuaternion(Quaternion.Normalize(Rotation)) *
               Matrix4x4.CreateTranslation(Position);
    }

    public Transform Inverse()
    {
        var invRotation = Quaternion.Inverse(Rotation);
        var invPosition = Vector3.Transform(-Position, invRotation);
        return new Transform(invPosition, invRotation);
    }

    public static implicit operator Transform(FTransform transform) => new(transform);
}
