using OpenTK.Graphics.OpenGL4;

namespace Snooper.Core.Hardware;

public class RendererInfo
{
    public string Name { get; private set; } = string.Empty;
    public double Version { get; private set; }
    public DeviceInfo DeviceInfo { get; } = new();

    public void Load()
    {
        Name = GL.GetString(StringName.Version);
        Version = Convert.ToInt32($"{GL.GetInteger(GetPName.MajorVersion)}{GL.GetInteger(GetPName.MinorVersion)}") / 10.0;
        DeviceInfo.Load();
    }
}
