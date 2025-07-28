using System.Numerics;
using OpenTK.Graphics.OpenGL4;

namespace Snooper.Core.Containers.Programs;

public class ShaderProgram(string vertex, string fragment) : Program
{
    public string Vertex { get; set; } = vertex;
    public string Fragment { get; set; } = fragment;
    public string? Geometry { get; set; }
    public string? TessellationControl { get; init; }
    public string? TessellationEvaluation { get; init; }
    public string? Compute { get; init; }

    private readonly List<int> _shaderHandles = [];
    private readonly Dictionary<string, int> _uniformsLocation = [];

    public sealed override void Generate()
    {
        base.Generate();

        if (!string.IsNullOrEmpty(Vertex)) _shaderHandles.Add(CompileShader(ShaderType.VertexShader, Vertex));
        if (!string.IsNullOrEmpty(Fragment)) _shaderHandles.Add(CompileShader(ShaderType.FragmentShader, Fragment));
        if (!string.IsNullOrEmpty(Geometry)) _shaderHandles.Add(CompileShader(ShaderType.GeometryShader, Geometry));
        if (!string.IsNullOrEmpty(TessellationControl)) _shaderHandles.Add(CompileShader(ShaderType.TessControlShader, TessellationControl));
        if (!string.IsNullOrEmpty(TessellationEvaluation)) _shaderHandles.Add(CompileShader(ShaderType.TessEvaluationShader, TessellationEvaluation));
        if (!string.IsNullOrEmpty(Compute)) _shaderHandles.Add(CompileShader(ShaderType.ComputeShader, Compute));
    }

    public sealed override void Link()
    {
        foreach (var shaderHandle in _shaderHandles)
        {
            GL.AttachShader(Handle, shaderHandle);
        }

        base.Link();

        // program is self-contained
        foreach (var shaderHandle in _shaderHandles)
        {
            GL.DetachShader(Handle, shaderHandle);
            GL.DeleteShader(shaderHandle);
        }
    }

    protected virtual int CompileShader(ShaderType type, string content)
    {
        var handle = GL.CreateShader(type);
        GL.ShaderSource(handle, content);
        GL.CompileShader(handle);

        var infoLog = GL.GetShaderInfoLog(handle);
        if (!string.IsNullOrWhiteSpace(infoLog))
        {
            GL.DeleteShader(handle);
            throw new Exception($"{type} failed to compile with error {infoLog}");
        }

        return handle;
    }

    public void SetUniform(string name, int value)
    {
        GL.Uniform1(GetUniformLocation(name), value);
    }
    
    public void SetUniform(string name, int count, int[] values)
    {
        GL.Uniform1(GetUniformLocation(name), count, values);
    }

    public unsafe void SetUniform(string name, Matrix4x4 value) => UniformMatrix4(name, (float*) &value);
    private unsafe void UniformMatrix4(string name, float* value)
    {
        GL.UniformMatrix4(GetUniformLocation(name), 1, false, value);
    }

    public void SetUniform(string name, bool value) => SetUniform(name, Convert.ToUInt32(value));

    public void SetUniform(string name, uint value)
    {
        GL.Uniform1(GetUniformLocation(name), value);
    }

    public void SetUniform(string name, float value)
    {
        GL.Uniform1(GetUniformLocation(name), value);
    }

    public void SetUniform(string name, Vector2 value) => SetUniform2(name, value.X, value.Y);
    private void SetUniform2(string name, float x, float y)
    {
        GL.Uniform2(GetUniformLocation(name), x, y);
    }

    public void SetUniform(string name, Vector3 value) => SetUniform3(name, value.X, value.Y, value.Z);
    private void SetUniform3(string name, float x, float y, float z)
    {
        GL.Uniform3(GetUniformLocation(name), x, y, z);
    }

    public void SetUniform(string name, Vector4 value) => SetUniform4(name, value.X, value.Y, value.Z, value.W);
    private void SetUniform4(string name, float x, float y, float z, float w)
    {
        GL.Uniform4(GetUniformLocation(name), x, y, z, w);
    }
    
    public unsafe void SetUniform(string name, Plane[] value)
    {
        var length = value.Length;
        var planes = stackalloc float[4 * length];
        for (var i = 0; i < length; i++)
        {
            planes[i * 4] = value[i].Normal.X;
            planes[i * 4 + 1] = value[i].Normal.Y;
            planes[i * 4 + 2] = value[i].Normal.Z;
            planes[i * 4 + 3] = value[i].D;
        }

        GL.Uniform4(GetUniformLocation(name), length, planes);
    }

    private int GetUniformLocation(string name)
    {
        VerifyCurrent();

        if (!_uniformsLocation.TryGetValue(name, out int location))
        {
            location = GL.GetUniformLocation(Handle, name);
            _uniformsLocation.Add(name, location);
            if (location == -1)
            {
                throw new Exception($"{name} uniform not found on shader.");
            }
        }
        return location;
    }
}
