using OpenTK.Graphics.OpenGL4;

namespace Snooper.Core.Hardware;

public readonly struct DeviceInfo()
{
    public readonly string Name = GL.GetString(StringName.Renderer);
    public readonly string Vendor = GL.GetString(StringName.Vendor);
    public readonly ExtensionSupport ExtensionSupport = new();
}