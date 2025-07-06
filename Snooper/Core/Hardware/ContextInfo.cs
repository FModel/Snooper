using OpenTK.Graphics.OpenGL4;

namespace Snooper.Core.Hardware;

public readonly struct ContextInfo()
{
    public readonly string Name = GL.GetString(StringName.Version);
    public readonly double Version = Convert.ToInt32($"{GL.GetInteger(GetPName.MajorVersion)}{GL.GetInteger(GetPName.MinorVersion)}") / 10.0;
    public readonly DeviceInfo DeviceInfo = new();
}