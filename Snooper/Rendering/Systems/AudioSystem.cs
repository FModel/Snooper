using System.Numerics;
using OpenTK.Audio.OpenAL;
using Serilog;
using Snooper.Core;
using Snooper.Core.Containers.Buffers;
using Snooper.Core.Systems;
using Snooper.Rendering.Cache;
using Snooper.Rendering.Components;
using Snooper.Rendering.Components.Audio;
using Snooper.Rendering.Components.Camera;
using Snooper.UI;

namespace Snooper.Rendering.Systems;

public sealed class AudioSystem : ComputeRenderSystem<AudioComponent>, IControllable
{
    public override ActorSystemType SystemType => ActorSystemType.Audio;
    public override uint Order => 100;
    public override int Capacity => 10000;

    private ALDevice _device;
    private ALContext _context;
    private readonly Dictionary<AudioComponent, AudioSource?> _sources = [];
    private readonly AudioCache _audioCache = new();

    private float _volume = 0.5f;
    private bool _volumeChanged;
    private const float MinDb = -35f;
    private const float MaxDb = 0f;

    public AudioSystem()
    {
        IsEnabled = false;
    }

    protected override void OnLoad()
    {
        base.OnLoad();

        _device = ALC.OpenDevice(null);
        if (_device == ALDevice.Null)
        {
            Log.Error("Failed to open OpenAL device");
            return;
        }

        _context = ALC.CreateContext(_device, (int[])null!);
        if (_context == ALContext.Null)
        {
            Log.Error("Failed to create OpenAL context");
            ALC.CloseDevice(_device);
            return;
        }

        ALC.MakeContextCurrent(_context);
        CheckAlError("Context initialization");
        Log.Information("OpenAL initialized successfully (Version: {Version})", AL.Get(ALGetString.Version));
    }

    protected override void OnUpdate(float delta)
    {
        if (_context == ALContext.Null) return;

        base.OnUpdate(delta);

        if (_volumeChanged)
        {
            foreach (var (component, source) in _sources)
            {
                source?.SetGain(component.VolumeMultiplier * LinearToLogarithmicVolume(_volume));
            }
            _volumeChanged = false;
        }
    }

    protected override void OnComponentUpdate(AudioComponent component, float delta)
    {
        if (component.ShouldPlay && !_sources.ContainsKey(component))
        {
            _sources[component] = CreateAudioSource(component);
        }

        if (!_sources.TryGetValue(component, out var source) || source == null)
            return;

        if (component.IsDirty(DirtyFlags.Transform))
        {
            source.SetPosition(component.WorldMatrix.Translation);
            source.SetDirection(Vector3.Transform(Vector3.UnitZ, component.LocalTransform.Rotation));
        }

        if (component.ShouldPlay && !source.IsPlaying)
        {
            source.Play();
        }
        else if (!component.ShouldPlay && source.IsPlaying)
        {
            source.Stop();
        }
    }

    protected override void OnExecute(CameraComponent camera)
    {
        if (_context == ALContext.Null) return;

        var position = camera.WorldMatrix.Translation;
        var forward = Vector3.Transform(Vector3.UnitZ, camera.LocalTransform.Rotation);
        var up      = Vector3.Transform(Vector3.UnitY, camera.LocalTransform.Rotation);

        ALC.MakeContextCurrent(_context);
        AL.Listener(ALListener3f.Position, position.X, position.Y, position.Z);
        AL.Listener(ALListenerfv.Orientation, [forward.X, forward.Y, forward.Z, up.X, up.Y, up.Z]);
        CheckAlError("Listener update");
    }

    protected override void OnActorComponentRemoved(AudioComponent component, EEndPlayReason reason)
    {
        base.OnActorComponentRemoved(component, reason);

        if (_sources.TryGetValue(component, out var source))
        {
            source?.Stop();
            source?.Dispose();
            _sources.Remove(component);
        }
    }

    private AudioSource? CreateAudioSource(AudioComponent component)
    {
        if (component.Sound == null) return null;

        var buffer = _audioCache.GetOrCreateBuffer(component.Sound);
        if (buffer == 0)
        {
            Log.Warning("Failed to load audio buffer for {Sound}", component.Sound.Name);
            return null;
        }

        Log.Debug("Creating audio source with buffer {BufferId} for component {Name}", buffer, component.Name);

        var source = new AudioSource(buffer);
        source.SetPosition(component.WorldMatrix.Translation);
        source.SetDirection(Vector3.Transform(Vector3.UnitZ, component.LocalTransform.Rotation));
        source.SetLooping(true);
        source.SetGain(component.VolumeMultiplier * LinearToLogarithmicVolume(_volume));
        source.SetReferenceDistance(component.AttenuationDistance);

        return source;
    }

    private float LinearToLogarithmicVolume(float linearVolume)
    {
        if (linearVolume <= 0f) return 0f;

        var db = MinDb + (MaxDb - MinDb) * linearVolume;
        return MathF.Pow(10f, db / 20f);
    }

    private void CheckAlError(string context)
    {
        var error = AL.GetError();
        if (error != ALError.NoError)
        {
            Log.Error("OpenAL Error ({Context}): {Error}", context, error);
        }
    }

    public override void Dispose()
    {
        base.Dispose();

        foreach (var source in _sources.Values)
        {
            source?.Dispose();
        }
        _sources.Clear();

        _audioCache.Dispose();

        if (_context != ALContext.Null)
        {
            ALC.MakeContextCurrent(ALContext.Null);
            ALC.DestroyContext(_context);
            _context = ALContext.Null;
        }

        if (_device != ALDevice.Null)
        {
            ALC.CloseDevice(_device);
            _device = ALDevice.Null;
        }
    }

    public void DrawControls()
    {
        EditorUI.PropertyValueTable("Audio Table", () =>
        {
            EditorUI.Text("Audio Sources", $"{ComponentsCount}/{Capacity}");
            _volumeChanged = EditorUI.DragFloat("Volume", ref _volume, 0.01f, 0.0f, 1.0f, $"{_volume * 100:F0}%%");
        });
    }
}
