using OpenTK.Graphics.OpenGL4;

namespace Snooper.Core.Hardware;

public class ExtensionSupport
{
    /// <summary>
    /// GL_ARB_bindless_texture
    /// </summary>
    public bool SupportBindlessTextures { get; private set; }

    /// <summary>
    /// GL_NVX_gpu_memory_info
    /// </summary>
    public bool SupportNvidiaMemoryInfo { get; private set; }

    /// <summary>
    /// GL_ATI_meminfo
    /// </summary>
    public bool SupportAtiMemoryInfo { get; private set; }

    public string[] Extensions { get; private set; } = [];

    public void Load()
    {
        Extensions = new string[GL.GetInteger(GetPName.NumExtensions)];
        for (var i = 0; i < Extensions.Length; i++)
        {
            var ext = GL.GetString(StringNameIndexed.Extensions, i);
            switch (ext)
            {
                case "GL_ARB_bindless_texture":
                    SupportBindlessTextures = true;
                    break;
                case "GL_NVX_gpu_memory_info":
                    SupportNvidiaMemoryInfo = true;
                    break;
                case "GL_ATI_meminfo":
                    SupportAtiMemoryInfo = true;
                    break;
            }

            Extensions[i] = ext;
        }
    }
}
