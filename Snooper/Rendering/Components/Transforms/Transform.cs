using System.Numerics;
using CUE4Parse.UE4.Objects.Core.Math;

namespace Snooper.Rendering.Components.Transforms;

public class Transform() : ICloneable
{
    public static Transform Identity => new();

    public Vector3 Position = Vector3.Zero;
    public Quaternion Rotation = Quaternion.Identity;
    public Vector3 Scale = Vector3.One;

    private const float Deg2Rad = MathF.PI / 180.0f;

    private Transform(Transform other) : this()
    {
        Position = other.Position;
        Rotation = other.Rotation;
        Scale = other.Scale;
    }

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

    public Transform(Vector3 position, Vector3 rotation) : this(position, Quaternion.CreateFromYawPitchRoll(rotation.X * Deg2Rad, rotation.Y * Deg2Rad, rotation.Z * Deg2Rad))
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
    public static implicit operator Transform(Vector3 position) => new(position);
    public static implicit operator Transform(Quaternion rotation) => new(rotation);
    public object Clone() => new Transform(this);
}
