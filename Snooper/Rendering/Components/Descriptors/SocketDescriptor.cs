using System.Numerics;
using CUE4Parse.UE4.Assets.Exports.SkeletalMesh;
using CUE4Parse.UE4.Assets.Exports.StaticMesh;
using Snooper.Rendering.Components.Transforms;

namespace Snooper.Rendering.Components.Descriptors;

public interface ISocketDescriptor
{
    public string Name { get; }
    public Matrix4x4 LocalMatrix { get; }
}

public class StaticMeshSocketDescriptor(UStaticMeshSocket socket) : ISocketDescriptor
{
    public string Name { get; } = socket.SocketName.Text;
    public Matrix4x4 LocalMatrix { get; } = new Transform(socket.RelativeLocation, socket.RelativeRotation.Quaternion(), socket.RelativeScale).ToMatrix();
}

public class SkeletalMeshSocketDescriptor(USkeletalMeshSocket socket) : ISocketDescriptor
{
    public string Name { get; } = socket.SocketName.Text;
    public Matrix4x4 LocalMatrix { get; } = new Transform(socket.RelativeLocation, socket.RelativeRotation.Quaternion(), socket.RelativeScale).ToMatrix();
    public string BoneName { get; } = socket.BoneName.Text;
}
