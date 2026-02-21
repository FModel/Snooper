namespace Snooper.Rendering.Components.Camera;

/// <summary>
/// camera that can be used for scene rendering
/// </summary>
public sealed class SceneCameraComponent : InteractiveCameraComponent
{
    internal int PairIndex = -1;
    public bool IsActive = false;

    public bool bFXAA = true;
    public bool bAmbientOcclusion = true;
    public bool bShadows = true;
    public float SsaoRadius = 1.5f;
}
