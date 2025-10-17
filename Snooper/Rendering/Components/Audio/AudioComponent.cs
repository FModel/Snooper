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
    public readonly float VolumeMultiplier = 1.0f;
    public readonly float AttenuationDistance = 1.0f;

    public bool ShouldPlay;
    public bool IsLooping = true;

    public AudioComponent(UAudioComponent component) : base(component)
    {
        Sound = component.GetOrDefault<USoundBase?>(nameof(Sound));
        VolumeMultiplier = component.GetOrDefault(nameof(VolumeMultiplier), VolumeMultiplier);
        
        var overrideAttenuation = component.GetOrDefault<bool>("bOverrideAttenuation");
        if (overrideAttenuation && component.TryGetValue(out FSoundAttenuationSettings attenuation, "AttenuationOverrides"))
        {
            AttenuationDistance = attenuation.FalloffDistance * Settings.GlobalScale;
        }
    }
    
    internal override string Icon => "audio";

    public override void DrawControls()
    {
        base.DrawControls();
        
        EditorUI.CollapsingTable("Audio", ImGuiTreeNodeFlags.DefaultOpen, () =>
        {
            EditorUI.Text("Sound", Sound?.Name ?? "N/A");
            EditorUI.Text("Volume Multiplier", VolumeMultiplier.ToString("F"));
            EditorUI.Text("Attenuation Distance", AttenuationDistance.ToString("F"));
            
            EditorUI.Checkbox("Looping", ref IsLooping);
            EditorUI.Checkbox("Play", ref ShouldPlay);
        });
    }
}