namespace Snooper.Rendering.Components.Descriptors.Animations;

public readonly struct SectionDescriptor(string name, float startTime, float endTime, int nextIndex)
{
    public readonly string Name = name;
    public readonly float StartTime = startTime;
    public readonly float EndTime = endTime;
    public readonly int NextIndex = nextIndex;

    public float Duration => EndTime - StartTime;

    public bool IsActiveAt(float time) => time >= StartTime && time < EndTime;
}
