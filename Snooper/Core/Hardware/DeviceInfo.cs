using OpenTK.Graphics.OpenGL4;

namespace Snooper.Core.Hardware;

public class DeviceInfo
{
    public string Name { get; private set; } = string.Empty;
    public string Vendor { get; private set; } = string.Empty;
    public int MaxShaderStorageBufferBindings { get; private set; }
    public ExtensionSupport ExtensionSupport { get; } = new();
    public GpuMemoryInfo Memory { get; } = new();

    public static bool IsIntel { get; private set; }
    public static string GlslDefines { get; private set; } = string.Empty;

    public void Load()
    {
        Name = GL.GetString(StringName.Renderer);
        Vendor = GL.GetString(StringName.Vendor);
        MaxShaderStorageBufferBindings = GL.GetInteger(GetPName.MaxShaderStorageBufferBindings);
        ExtensionSupport.Load();
        Memory.Load(ExtensionSupport);

        IsIntel = Vendor.Contains("Intel", StringComparison.OrdinalIgnoreCase);
        GlslDefines = IsIntel ? "#define BINDLESS_RAW_HANDLES\n" : string.Empty;
    }
}
