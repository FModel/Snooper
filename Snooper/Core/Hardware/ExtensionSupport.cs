using OpenTK.Graphics.OpenGL4;

namespace Snooper.Core.Hardware;

public class ExtensionSupport
{
    /// <summary>
    /// GL_ARB_bindless_texture
    /// </summary>
    public bool SupportBindlessTextures { get; private set; }

    public string[] Extensions { get; private set; } = [];

    public void Load()
    {
        Extensions = new string[GL.GetInteger(GetPName.NumExtensions)];
        for (var i = 0; i < Extensions.Length; i++)
        {
            var ext = GL.GetString(StringNameIndexed.Extensions, i);
            if (ext == "GL_ARB_bindless_texture")
            {
                SupportBindlessTextures = true;
            }

            Extensions[i] = ext;
        }
    }
}
