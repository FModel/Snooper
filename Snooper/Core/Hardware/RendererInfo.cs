using OpenTK.Graphics.OpenGL4;

namespace Snooper.Core.Hardware;

public class RendererInfo
{
    private const float MemoryPollRate = 0.5f;

    public static bool TrackMemory;

    public string Name { get; private set; } = string.Empty;
    public double Version { get; private set; }
    public DeviceInfo DeviceInfo { get; } = new();
    public SystemMemoryInfo SystemMemory { get; } = new();

    public void Load()
    {
        Name = GL.GetString(StringName.Version);
        Version = Convert.ToInt32($"{GL.GetInteger(GetPName.MajorVersion)}{GL.GetInteger(GetPName.MinorVersion)}") / 10.0;
        DeviceInfo.Load();
        SystemMemory.Update();
    }

    private float _sinceLastPoll;
    public void Update(float delta)
    {
        if (!TrackMemory)
        {
            _sinceLastPoll = MemoryPollRate; // poll right away once something turns back on
            return;
        }

        _sinceLastPoll += delta;
        if (_sinceLastPoll < MemoryPollRate) return;

        _sinceLastPoll = 0.0f;
        DeviceInfo.Memory.Update();
        SystemMemory.Update();
    }
}
