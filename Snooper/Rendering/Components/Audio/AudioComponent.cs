using CUE4Parse.UE4.Assets.Exports.Component;
using CUE4Parse.UE4.Assets.Exports.Sound;
using ImGuiNET;
using Snooper.Core;
using Snooper.Rendering.Components.Transforms;
using Snooper.Rendering.Systems;
using Snooper.UI;

namespace Snooper.Rendering.Components.Audio;

[DefaultActorSystem(typeof(AudioSystem))]
public class AudioComponent : SpatialComponent
{
    public readonly USoundBase? Sound;
    
    public bool ForcePlay;
    public float VolumeMultiplier = 1;
    public bool IsLooping { get; } = true;
    public float Pitch { get; } = 1.0f;
    public float AttenuationDistance { get; private set; } = 1.0f;
    
    public AudioComponent(USoundBase? sound = null, Transform? transform = null, string? name = null) : base(transform, name)
    {
        Sound = sound;
    }
    
    public AudioComponent(UAudioComponent component) : base(component)
    {
        Sound = component.GetOrDefault<USoundBase?>(nameof(Sound));
        
        var overrideAttenuation = component.GetOrDefault<bool>("bOverrideAttenuation");
        if (overrideAttenuation && component.TryGetValue(out FSoundAttenuationSettings attenuation, "AttenuationOverrides"))
        {
            AttenuationDistance = attenuation.FalloffDistance * Settings.GlobalScale;
        }
    }

    public override void DrawControls()
    {
        base.DrawControls();
        
        EditorUI.CollapsingTable("Audio", ImGuiTreeNodeFlags.DefaultOpen, () =>
        {
            EditorUI.Text("Sound", Sound?.Name ?? "N/A");
            EditorUI.Property("Volume Multiplier");
            ImGui.SliderFloat("Volume Multiplier", ref VolumeMultiplier, 0f, 4f, $"x{VolumeMultiplier:F}");
            EditorUI.Checkbox("Play", ref ForcePlay);
        });
    }
}