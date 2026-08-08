using CUE4Parse.GameTypes.FN.Assets.Exports.Animation;
using CUE4Parse.GameTypes.NetEase.MAR.Assets.Exports.Animation;
using CUE4Parse.UE4.Assets.Exports.Animation;
using CUE4Parse.UE4.Assets.Exports.SkeletalMesh;
using CUE4Parse.UE4.Assets.Exports.StaticMesh;
using CUE4Parse.UE4.Objects.Core.Math;
using Snooper.Rendering.Actors;
using Snooper.Rendering.Components.Audio;
using Snooper.Rendering.Components.Descriptors.Animations;
using Snooper.Rendering.Components.Mesh;
using Snooper.Rendering.Components.Transforms;

namespace Snooper.Rendering.Components;

public sealed class AnimationPlayback
{
    public readonly SequenceBaseDescriptor Animation;

    public float Time { get; private set; }
    public float PlayPosition;
    public float PlayRate;
    public bool IsDriven;
    public bool IsLooping = true;

    public bool IsPlaying
    {
        get;
        set
        {
            if (field == value) return;

            field = value;
            if (field) MarkBoundDirty();
        }
    }

    public float Duration => Animation.Duration;

    private readonly List<SkeletalMeshComponent> _components = []; // components animated by this animation
    public IReadOnlyList<SkeletalMeshComponent> Components => _components;

    private SkeletalMeshComponent? LeaderComponent
    {
        get
        {
            SkeletalMeshComponent? richest = null;
            var bones = -1;
            foreach (var component in _components)
            {
                var count = component.Descriptor.Skeleton?.BoneCount ?? 0;
                if (count <= bones) continue;

                bones = count;
                richest = component;
            }

            return richest;
        }
    }

    private Actor? Actor
    {
        get
        {
            foreach (var component in _components)
            {
                if (component.Actor is { } actor) return actor;
            }

            return null;
        }
    }

    private readonly List<(SpatialComponent Component, string? Socket)> _notifies = [];
    private bool _spawned;

    public static AnimationPlayback? Create(UAnimationAsset animation, float playPosition = 0f, float playRate = 1f)
    {
        SequenceBaseDescriptor? descriptor = animation switch
        {
            UAnimMontage montage => new MontageDescriptor(montage),
            UAnimComposite composite => new CompositeDescriptor(composite),
            UAnimSequence sequence => new SequenceDescriptor(sequence),
            _ => null
        };

        return descriptor == null ? null : new AnimationPlayback(descriptor, playPosition, playRate);
    }

    private AnimationPlayback(SequenceBaseDescriptor animation, float playPosition = 0f, float playRate = 1f)
    {
        Animation = animation;
        PlayPosition = playPosition;
        PlayRate = playRate;
        Time = playPosition;
        IsPlaying = true;

        BuildNotifies();
    }

    private void BuildNotifies()
    {
        foreach (var descriptor in Animation.Notifies)
        {
            switch (descriptor.Consume())
            {
                // Fortnite
                case UFortAnimNotifyState_SpawnProp sp:
                {
                    var transform = new Transform(sp.LocationOffset, sp.RotationOffset.Quaternion(), sp.Scale);

                    SpatialComponent? component = null;
                    if (sp.SkeletalMeshProp?.TryLoad<USkeletalMesh>(out var sk) == true)
                    {
                        component = new SkeletalMeshComponent(sk, transform, sp.SkeletalMeshPropAnimation?.Load<UAnimationAsset>());
                    }
                    else if (sp.StaticMeshProp?.TryLoad<UStaticMesh>(out var sm) == true)
                    {
                        component = new StaticMeshComponent(sm, transform);
                    }

                    if (component != null)
                    {
                        _notifies.Add((component, sp.SocketName?.Text));
                    }
                    break;
                }
                case UFortAnimNotifyState_EmoteSound es:
                {
                    _notifies.Add((new AudioComponent(es, descriptor.Name), es.AttachName?.Text));
                    break;
                }
                // Marvel Rivals
                case UAnimNotifyState_TimedSkeletonAnimation tsa when tsa.SkeletalMeshTemplate?.TryLoad<USkeletalMesh>(out var sk) == true:
                {
                    var transform = new Transform(tsa.LocationOffset, tsa.RotationOffset.Quaternion(), FVector.OneVector);
                    var component = new SkeletalMeshComponent(sk, transform, tsa.AnimToPlay?.Load<UAnimationAsset>(), tsa.AnimStartPos);
                    _notifies.Add((component, tsa.SocketName?.Text));
                    break;
                }
                case UAN_AkEvent ae:
                {
                    _notifies.Add((new AudioComponent(ae, descriptor.Name), ae.AttachName?.Text));
                    break;
                }
            }
        }

        foreach (var (component, _) in _notifies)
        {
            if (component is not SkeletalMeshComponent { Playback: { } driven }) continue;

            driven.IsDriven = true;
            driven.IsLooping = false;
        }
    }

    public void Advance(float delta)
    {
        if (!IsPlaying || _components.Count == 0) return;

        var value = Animation.Follow(Time, Time + delta * PlayRate);
        if (IsLooping && Duration > 0f) value = (value % Duration + Duration) % Duration;

        Seek(value);
    }

    public void Seek(float value)
    {
        value = Math.Clamp(value, 0f, Duration);
        var rewound = value < Time;

        Time = value;
        MarkBoundDirty();

        if (rewound)
        {
            foreach (var component in _components)
            {
                foreach (var child in component.Children)
                {
                    if (child is SkeletalMeshComponent { Playback: { IsDriven: true } driven } && !ReferenceEquals(driven, this))
                    {
                        driven.Seek(value);
                    }
                }
            }
        }
    }

    internal void Attach(SkeletalMeshComponent component)
    {
        if (_components.Contains(component)) return;

        _components.Add(component);
        component.MarkDirty(DirtyFlags.Animation);

        RefreshAttachments();
    }

    internal void Detach(SkeletalMeshComponent component)
    {
        if (!_components.Remove(component)) return;

        component.Descriptor.Skeleton?.ResetAllBones();
        component.MarkDirty(DirtyFlags.Animation);

        RefreshAttachments();
    }

    private void MarkBoundDirty()
    {
        foreach (var component in _components)
        {
            component.MarkDirty(DirtyFlags.Animation);
        }
    }

    internal void Spawn()
    {
        if (_spawned || _notifies.Count == 0 || Actor is not { } actor) return;

        _spawned = true;

        foreach (var (component, socket) in _notifies)
        {
            component.AttachSocketName = socket;
            component.Relation = ResolveAttachment(socket);

            actor.Components.Add(component);
        }
    }

    internal void Despawn()
    {
        if (!_spawned || Actor is not { } actor) return;

        _spawned = false;

        foreach (var (component, _) in _notifies)
        {
            actor.Components.Remove(component);
        }
    }

    private void RefreshAttachments()
    {
        foreach (var (component, socket) in _notifies)
        {
            component.AttachSocketName = socket;
            component.Relation = ResolveAttachment(socket);
        }
    }

    private SkeletalMeshComponent? ResolveAttachment(string? socket)
    {
        if (_components.Count == 0) return null;
        if (string.IsNullOrEmpty(socket)) return LeaderComponent;

        SkeletalMeshComponent? match = null;
        var matches = 0;
        foreach (var component in _components)
        {
            if (!component.Descriptor.HasSocket(socket)) continue;

            match ??= component;
            matches++;
        }

        return matches == 1 ? match : LeaderComponent;
    }
}
