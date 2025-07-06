using OpenTK.Graphics.OpenGL4;

namespace Snooper.Core.Hardware;

public readonly struct ExtensionSupport
{
    /// <summary>
    /// GL_ARB_bindless_texture
    /// </summary>
    public readonly bool BindlessTextures;
    
    public readonly string[] Extensions;

    public ExtensionSupport()
    {
        Extensions = new string[GL.GetInteger(GetPName.NumExtensions)];
        for (var i = 0; i < Extensions.Length; i++)
        {
            var ext = GL.GetString(StringNameIndexed.Extensions, i);
            if (ext == "GL_ARB_bindless_texture")
            {
                BindlessTextures = true;
            }
            
            Extensions[i] = ext;
        }
    }
}