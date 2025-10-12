using System.Numerics;
using System.Runtime.InteropServices;
using ImGuiNET;
using OpenTK.Audio.OpenAL;
using Serilog;
using Snooper.Core.Systems;
using Snooper.Rendering.Cache;
using Snooper.Rendering.Components.Audio;
using Snooper.Rendering.Components.Camera;
using Snooper.UI;

namespace Snooper.Rendering.Systems;

public sealed class AudioSystem : ActorSystem<AudioComponent>, IControllable
{
    public override ActorSystemType SystemType => ActorSystemType.Audio;
    public override uint Order => 100;
    
    private ALDevice _device;
    private ALContext _context;
    private float _linearVolume = 0.5f;
    private float _logarithmicVolume = 0.5f;
    private string[] _outputDevices = [];
    private string _selectedDevice = string.Empty;
    private readonly Dictionary<AudioComponent, AudioSource> _activeSources = [];
    private readonly AudioCache _audioCache = new();
    private AlcReopenDeviceSoft? _alcReopenDeviceSoft;

    private const float MinDb = -35;
    private const float MaxDb = 0;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate bool AlcReopenDeviceSoft(IntPtr device, string deviceName, IntPtr attribs);
    
    public override void Load()
    {
        base.Load();
        
        _device = ALC.OpenDevice(null);
        if (_device == ALDevice.Null)
        {
            Log.Error("Failed to open OpenAL device");
            return;
        }
        
        _selectedDevice = ALC.GetString(ALDevice.Null, AlcGetString.DefaultDeviceSpecifier);
        _outputDevices = ALC.GetString(AlcGetStringList.AllDevicesSpecifier).ToArray();
        
        _context = ALC.CreateContext(_device, (int[])null!);
        if (_context == ALContext.Null)
        {
            Log.Error("Failed to create OpenAL context");
            ALC.CloseDevice(_device);
            return;
        }

        ALC.MakeContextCurrent(_context);
        CheckAlError("Context initialization");

        Log.Information("OpenAL initialized successfully");
        Log.Information("OpenAL Vendor: {Vendor}", AL.Get(ALGetString.Vendor));
        Log.Information("OpenAL Renderer: {Renderer}", AL.Get(ALGetString.Renderer));
        Log.Information("OpenAL Version: {Version}", AL.Get(ALGetString.Version));

        foreach (var component in Components)
        {
            if (component.Sound == null) continue;
            
            var buffer = _audioCache.GetOrCreateBuffer(component.Sound);
            if (buffer == 0)
            {
                Log.Warning("Failed to load audio buffer for {Sound}", component.Sound.Name);
                return;
            }
        
            Log.Debug("Creating audio source with buffer {BufferId} for component {Name}", buffer, component.Name);
        
            var source = new AudioSource(buffer);
            source.SetLooping(component.IsLooping);
            source.SetGain(_logarithmicVolume * component.VolumeMultiplier);
            source.SetPitch(component.Pitch);
            source.SetReferenceDistance(component.AttenuationDistance);
            
            var position = component.WorldMatrix.Translation;
            source.SetPosition(position.X, position.Y, position.Z);
        
            _activeSources[component] = source;
        }
        
        if (!ALC.IsExtensionPresent(_device, "ALC_SOFT_reopen_device"))
        {
            Log.Error("ALC_SOFT_reopen_device extension not available. Use OpenAL-Soft");
            return;
        }

        var funcPtr = ALC.GetProcAddress(_device, "alcReopenDeviceSOFT");
        if (funcPtr == IntPtr.Zero)
        {
            Log.Error("Could not get pointer for alcReopenDeviceSOFT");
            return;
        }
        
        _alcReopenDeviceSoft = Marshal.GetDelegateForFunctionPointer<AlcReopenDeviceSoft>(funcPtr);
    }

    public override void Update(float delta)
    {
        base.Update(delta);

        if (_context == ALContext.Null) return;

        foreach (var (component, source) in _activeSources)
        {
            if (component.Sound == null)
            {
                source.Stop();
                continue;
            }

            if (component.ShouldPlay && !source.IsPlaying)
            {
                source.Play();
            }
            
            if (source.IsPlaying && !component.ShouldPlay)
            {
                source.Stop();
            }
            
            source.SetGain(component.VolumeMultiplier * _logarithmicVolume);
        }
    }

    public override void Render(CameraComponent camera)
    {
        if (_context == ALContext.Null) return;
        
        var position = camera.WorldMatrix.Translation;
        var forward = Vector3.Transform(Vector3.UnitZ, camera.LocalTransform.Rotation);
        var up      = Vector3.Transform(Vector3.UnitY, camera.LocalTransform.Rotation);

        float[] orientation =
        [
            forward.X, forward.Y, forward.Z,
            up.X, up.Y, up.Z
        ];

        ALC.MakeContextCurrent(_context);
        AL.Listener(ALListener3f.Position, position.X, position.Y, position.Z);
        AL.Listener(ALListenerfv.Orientation, orientation);
        CheckAlError("Listener update");
    }

    protected override void OnActorComponentRemoved(AudioComponent component)
    {
        base.OnActorComponentRemoved(component);
        
        if (_activeSources.TryGetValue(component, out var source))
        {
            source.Stop();
            source.Dispose();
            _activeSources.Remove(component);
        }
    }

    public override void Dispose()
    {
        base.Dispose();
        
        foreach (var source in _activeSources.Values)
        {
            source.Dispose();
        }
        _activeSources.Clear();

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

    private void SwitchAudioDevice(string deviceName)
    {
        if (_device == ALDevice.Null)
        {
            Log.Error("Failed to get ALDevice");
            return;
        }

        if (_alcReopenDeviceSoft == null)
        {
            Log.Error("Failed to get delegate for alcReopenDeviceSOFT. Device switching will not be available.");
            return;
        }
        
        if (_alcReopenDeviceSoft(_device, deviceName, IntPtr.Zero))
        {
            Log.Information("Successfully switched Output Device from {OldDevice} to {NewDevice}", _selectedDevice, deviceName);
            _selectedDevice = deviceName;
        }
        else
        {
            Log.Error("Unable to switch Output Device");
        }

        CheckAlError("Reopen Device");
    }
    
    private void CheckAlError(string context)
    {
        var error = AL.GetError();
        if (error != ALError.NoError)
        {
            Log.Error("OpenAL Error ({Context}): {Error}", context, error);
        }
    }

    public void DrawControls()
    {
        if (ImGui.SliderFloat("Volume", ref _linearVolume, 0f, 1f, $"{_linearVolume * 100:F}%%"))
        {
            if (_linearVolume == 0f)
            {
                _logarithmicVolume = 0;
                return;
            }
        
            var db = MinDb + (MaxDb - MinDb) * _linearVolume;
            _logarithmicVolume = MathF.Pow(10f, db / 20f);
        }
        
        if (ImGui.BeginCombo("Output Device", _selectedDevice))
        {
            foreach (var outputDevice in _outputDevices)
            {
                if (ImGui.Selectable(outputDevice))
                {
                    SwitchAudioDevice(outputDevice);
                }
                
                if (outputDevice == _selectedDevice) 
                    ImGui.SetItemDefaultFocus();
            }
            
            ImGui.EndCombo();
        }
    }
}

