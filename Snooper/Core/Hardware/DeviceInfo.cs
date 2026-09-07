using OpenTK.Graphics.OpenGL4;

namespace Snooper.Core.Hardware;

public class DeviceInfo
{
    public string Name { get; private set; } = string.Empty;
    public string Vendor { get; private set; } = string.Empty;
    public int MaxShaderStorageBufferBindings { get; private set; }
    public ExtensionSupport ExtensionSupport { get; } = new();
    public GpuMemoryInfo Memory { get; } = new();

    public static string GlslDefines { get; private set; } = string.Empty;

    public void Load()
    {
        Name = GL.GetString(StringName.Renderer);
        Vendor = GL.GetString(StringName.Vendor);
        MaxShaderStorageBufferBindings = GL.GetInteger(GetPName.MaxShaderStorageBufferBindings);
        ExtensionSupport.Load();
        Memory.Load(ExtensionSupport);

        GlslDefines = Vendor.Contains("Intel", StringComparison.OrdinalIgnoreCase) ? "#define BINDLESS_RAW_HANDLES\n" : string.Empty;
    }
}
