using CUE4Parse.GameTypes.FN.Assets.Exports.Animation;
using CUE4Parse.GameTypes.NetEase.MAR.Assets.Exports.Animation;
using CUE4Parse.UE4.Assets.Exports.Component;
using CUE4Parse.UE4.Assets.Exports.Sound;
using ImGuiNET;
using Snooper.Core;
using Snooper.Rendering.Components.Primitive;
using Snooper.Rendering.Systems;
using Snooper.UI;

namespace Snooper.Rendering.Components.Audio;

[DefaultActorSystem(typeof(AudioSystem))]
public class AudioComponent : BillboardComponent
{
    private const string BillboardSprite = "S_AudioComponent";

    public readonly USoundBase? Sound;
    public readonly float VolumeMultiplier = 1.0f;
    public readonly float AttenuationDistance = 1.0f;

    public bool ShouldPlay;

    public AudioComponent(UAudioComponent component) : base(component, BillboardSprite)
    {
        Sound = component.Sound;
        VolumeMultiplier = component.GetOrDefault(nameof(VolumeMultiplier), VolumeMultiplier);

        var overrideAttenuation = component.GetOrDefault<bool>("bOverrideAttenuation");
        if (overrideAttenuation && component.TryGetValue(out FSoundAttenuationSettings attenuation, "AttenuationOverrides"))
        {
            AttenuationDistance = attenuation.FalloffDistance * Settings.GlobalScale;
        }
    }

    public AudioComponent(UFortAnimNotifyState_EmoteSound notify, string? notifyName = null) : base(BillboardSprite, name: notify.SoundName?.Text ?? notifyName ?? notify.Name)
    {
        Sound = notify.EmoteSound3P?.Load<USoundBase>() ?? notify.EmoteSound1P?.Load<USoundBase>();
    }

    public AudioComponent(UAN_AkEvent wwise, string? notifyName = null) : base(BillboardSprite, name: notifyName ?? wwise.Name)
    {
        // Sound = wwise.Event?.Load<UAkAudioEvent>();
    }

    public override string Icon => "\uf6a8";

    public override void DrawControls()
    {
        base.DrawControls();

        EditorUI.CollapsingTable("Audio", ImGuiTreeNodeFlags.DefaultOpen, () =>
        {
            EditorUI.Text("Sound", Sound?.Name ?? "N/A");
            EditorUI.Text("Volume Multiplier", VolumeMultiplier.ToString("F"));
            EditorUI.Text("Attenuation Distance", AttenuationDistance.ToString("F"));

            EditorUI.Checkbox("Play", ref ShouldPlay);
        });
    }
}
