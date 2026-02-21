using System.Reflection;
using OpenTK.Graphics.OpenGL4;

namespace Snooper.Core.Containers.Programs;

public class EmbeddedShader(string vertex, string fragment, Assembly? assembly = null) : ShaderProgram(vertex, fragment)
{
    private readonly Assembly _assembly = assembly ?? Assembly.GetExecutingAssembly();

    public string[]? Defines { get; init; }

    public EmbeddedShader(string file, Assembly? assembly = null) : this($"{file}.vert", $"{file}.frag", assembly)
    {

    }

    protected override uint CompileShader(ShaderType type, string file)
    {
        var content = GetFileContent(file);
        ResolveIncludes(ref content);

        if (Defines is { Length: > 0 })
        {
            content = string.Join("\n", Defines.Select(d => $"#define {d}")) + "\n" + content;
        }

        return base.CompileShader(type, "#version 460 core\n\n" + content);
    }

    private string GetFileContent(string file)
    {
        var assemblyName = _assembly.GetName().Name;
        using var stream = _assembly.GetManifestResourceStream($"{assemblyName}.Shaders.{file.Replace('\\', '.').Replace('/', '.')}");
        if (stream == null)
            throw new FileNotFoundException($"Embedded shader file '{file}' not found in assembly '{assemblyName}'.");

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private void ResolveIncludes(ref string content)
    {
        const string include = "#include";

        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            if (line.StartsWith(include))
            {
                var includeFile = line[include.Length..].Trim().Trim('"');
                var includeContent = GetFileContent(includeFile);
                ResolveIncludes(ref includeContent);
                content = content.Replace(line, includeContent);
            }
        }
    }

    protected override ShaderProgram CloneShader()
    {
        return new EmbeddedShader(Vertex, Fragment, _assembly)
        {
            Geometry = Geometry,
            TessellationControl = TessellationControl,
            TessellationEvaluation = TessellationEvaluation,
            Defines = Defines
        };
    }
}
