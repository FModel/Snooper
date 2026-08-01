namespace Snooper.Rendering.Components.Descriptors;

public readonly struct LodSectionDescriptor(uint firstIndex, uint indexCount, uint materialIndex, bool castShadow = false, string? name = null)
{
    public readonly uint FirstIndex = firstIndex;
    public readonly uint IndexCount = indexCount;
    public readonly uint MaterialIndex = materialIndex;
    public readonly bool CastShadow = castShadow;
    public readonly string Name = name ?? Settings.NoName;
}

public readonly struct AnimationSectionDescriptor(string name, float startTime, float endTime, int nextIndex)
{
    public readonly string Name = name;
    public readonly float StartTime = startTime;
    public readonly float EndTime = endTime;
    public readonly int NextIndex = nextIndex;

    public float Duration => EndTime - StartTime;

    public bool IsActiveAt(float time) => time >= StartTime && time < EndTime;
}
