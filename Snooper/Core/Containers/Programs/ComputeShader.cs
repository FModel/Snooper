namespace Snooper.Core.Containers.Programs;

public class ComputeShader : EmbeddedShader
{
    public ComputeShader(string compute) : base(string.Empty, string.Empty)
    {
        Compute = compute;
    }
}
