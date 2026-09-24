using System.Numerics;

namespace Snooper.Rendering;

public sealed class WireframeOptions
{
    public bool Enabled;
    public Vector3 Color = new(0.85f, 0.85f, 0.85f);
    public float Width = 1f; // pixels
    public bool Overlay;
}
