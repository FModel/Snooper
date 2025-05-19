using System.Numerics;
using OpenTK.Graphics.OpenGL4;

namespace Snooper.Core.Containers.Programs;

public sealed class ShaderProgram(string vertex, string fragment, string? geometry = null) : Program
{
    private readonly List<int> _shaderHandles = [];
    private readonly Dictionary<string, int> _uniformsLocation = [];

    public override void Generate()
    {
        base.Generate();

        _shaderHandles.Add(CompileShader(ShaderType.VertexShader, vertex));
        _shaderHandles.Add(CompileShader(ShaderType.FragmentShader, fragment));
        if (!string.IsNullOrEmpty(geometry)) _shaderHandles.Add(CompileShader(ShaderType.GeometryShader, geometry));
    }

    public override void Link()
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

    public void SetUniform(string name, int value)
    {
        GL.Uniform1(GetUniformLocation(name), value);
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

    private int GetUniformLocation(string name)
    {
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

    private int CompileShader(ShaderType type, string content)
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
}
