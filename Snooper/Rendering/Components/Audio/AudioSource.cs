using OpenTK.Audio.OpenAL;

namespace Snooper.Rendering.Components.Audio;

public class AudioSource : IDisposable
{
    private readonly int _sourceId;

    public AudioSource(int buffer)
    {
        _sourceId = AL.GenSource();
        AL.Source(_sourceId, ALSourcei.Buffer, buffer);
    }

    public ALSourceState State
    {
        get
        {
            AL.GetSource(_sourceId, ALGetSourcei.SourceState, out var state);
            return (ALSourceState)state;
        }
    }

    public bool IsPlaying => State == ALSourceState.Playing;

    public void Play()
    {
        AL.SourcePlay(_sourceId);
    }

    public void Pause()
    {
        AL.SourcePause(_sourceId);
    }

    public void Stop()
    {
        AL.SourceStop(_sourceId);
    }

    public void Rewind()
    {
        AL.SourceRewind(_sourceId);
    }

    public void SetPosition(float x, float y, float z)
    {
        AL.Source(_sourceId, ALSource3f.Position, x, y, z);
    }

    public void SetVelocity(float x, float y, float z)
    {
        AL.Source(_sourceId, ALSource3f.Velocity, x, y, z);
    }

    public void SetGain(float gain)
    {
        AL.Source(_sourceId, ALSourcef.Gain, Math.Clamp(gain, 0f, 10f));
    }

    public void SetPitch(float pitch)
    {
        AL.Source(_sourceId, ALSourcef.Pitch, Math.Max(pitch, 0.0001f));
    }

    public void SetLooping(bool loop)
    {
        AL.Source(_sourceId, ALSourceb.Looping, loop);
    }

    public void SetReferenceDistance(float distance)
    {
        AL.Source(_sourceId, ALSourcef.ReferenceDistance, distance);
    }

    public void SetMaxDistance(float distance)
    {
        AL.Source(_sourceId, ALSourcef.MaxDistance, distance);
    }

    public void SetRolloffFactor(float rolloff)
    {
        AL.Source(_sourceId, ALSourcef.RolloffFactor, rolloff);
    }

    public void Dispose()
    {
        Stop();
        AL.DeleteSource(_sourceId);
    }
}
