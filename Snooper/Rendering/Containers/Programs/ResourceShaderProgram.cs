using System.Reflection;
using OpenTK.Graphics.OpenGL4;
using Snooper.Core.Containers.Programs;

namespace Snooper.Rendering.Containers.Programs;

public class ResourceShaderProgram : ShaderProgram
{
    private readonly Assembly _assembly = Assembly.GetExecutingAssembly();
    
    public ResourceShaderProgram(string vertex, string fragment) : base(vertex, fragment)
    {
        
    }

    protected override int CompileShader(ShaderType type, string file)
    {
        using var stream = _assembly.GetManifestResourceStream($"{_assembly.GetName().Name}.Resources.{file}");
        using var reader = new StreamReader(stream);
        return base.CompileShader(type, reader.ReadToEnd());
    }
}