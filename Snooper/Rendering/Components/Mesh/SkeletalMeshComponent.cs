using System.Numerics;
using CUE4Parse_Conversion;
using CUE4Parse.GameTypes.FN.Assets.Exports.Animation;
using CUE4Parse.GameTypes.NetEase.MAR.Assets.Exports.Animation;
using CUE4Parse.UE4.Assets.Exports.Animation;
using CUE4Parse.UE4.Assets.Exports.Component.SkeletalMesh;
using CUE4Parse.UE4.Assets.Exports.SkeletalMesh;
using CUE4Parse.UE4.Assets.Exports.StaticMesh;
using CUE4Parse.UE4.Objects.Core.Math;
using ImGuiNET;
using Snooper.Rendering.Actors;
using Snooper.Rendering.Components.Audio;
using Snooper.Rendering.Components.Descriptors;
using Snooper.Rendering.Components.Transforms;
using Snooper.UI;

namespace Snooper.Rendering.Components.Mesh;

public class SkeletalMeshComponent : SkinnedMeshComponent
{
    protected override DirtyFlags SupportedDirtyFlags => base.SupportedDirtyFlags | DirtyFlags.Animation;

    public AnimationDescriptor? Animation
    {
        get;
        private set
        {
            if (field == value) return;

            field = value;

            if (field == null)
            {
                IsPlayingAnimation = false;
                Descriptor.Skeleton?.ResetAllBones();
                MarkDirty(DirtyFlags.Animation);
            }
            else
            {
                IsPlayingAnimation = true;
                AnimationTime = field.StartTime;
            }
        }
    }

    public bool IsLooping => Relation is not SkeletalMeshComponent;

    public bool IsPlayingAnimation
    {
        get;
        set
        {
            if (field == value) return;

            field = value;
            if (field) MarkDirty(DirtyFlags.Animation);
        }
    }

    public float AnimationTime
    {
        get;
        set
        {
            var duration = Animation?.Duration ?? 0f;
            value = IsLooping && duration > 0f
                ? (value % duration + duration) % duration // the second modulo brings a rewind past zero back into range
                : Math.Clamp(value, 0f, duration);

            // going backwards is a fresh play, so everything this animation drives starts over with it
            if (value < field)
            {
                foreach (var component in Children.OfType<SkeletalMeshComponent>())
                {
                    component.AnimationTime = component.Animation?.StartTime ?? 0f;
                }
            }

            field = value;
            MarkDirty(DirtyFlags.Animation);
        }
    }

    internal void AdvanceAnimation(float delta)
    {
        if (!IsPlayingAnimation) return;

        AnimationTime += delta * (Animation?.PlayRate ?? 1f);
    }

    private SkeletalMeshComponent(SkeletalMeshComponent other) : base(other)
    {
        if (other.Animation != null)
        {
            Animation = (AnimationDescriptor) other.Animation.Clone();
        }
    }

    public SkeletalMeshComponent(USkeletalMesh skeletalMesh, Transform? transform = null) : base(skeletalMesh, transform)
    {

    }

    public SkeletalMeshComponent(USkeletalMesh skeletalMesh, Transform? transform, UAnimationAsset? animToPlay, float startTime = 0f) : this(skeletalMesh, transform)
    {
        SetAnimation(animToPlay, startTime);
    }

    public SkeletalMeshComponent(UAnimationAsset animToPlay, float startTime = 0f, float playRate = 1f) : this(animToPlay.Skeleton.Load<USkeleton>() ?? throw new InvalidOperationException($"Animation {animToPlay.Name} has no skeleton"))
    {
        SetAnimation(animToPlay, startTime, playRate);
    }

    public SkeletalMeshComponent(USkeleton skeleton, Transform? transform = null) : base(skeleton, transform)
    {

    }

    public SkeletalMeshComponent(USkeletalMesh skeletalMesh, USkeletalMeshComponent component) : base(skeletalMesh, component)
    {
        if (component.AnimationData is { } animationData && animationData.AnimToPlay.TryLoad<UAnimationAsset>(out var animToPlay))
        {
            SetAnimation(animToPlay, animationData.SavedPosition, animationData.SavedPlayRate);
        }
    }

    private readonly List<(SpatialComponent Component, string? Socket)> _pendingNotifies = [];
    private readonly List<ActorComponent> _notifyComponents = [];

    public void SetAnimation(UAnimationAsset? animToPlay, float startTime = 0f, float playRate = 1f)
    {
        Animation = animToPlay != null ? new AnimationDescriptor(animToPlay, startTime, playRate) : null;

        // TODO: do not spawn notifies if it's already part of this actor and it's not attached to anyone
        ClearNotifyComponents();
        BuildNotifyComponents();

        if (Actor != null) FlushNotifyComponents();
    }

    private void BuildNotifyComponents()
    {
        if (Animation is null) return;

        foreach (var descriptor in Animation.Notifies)
        {
            switch (descriptor.Notify)
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
                        _pendingNotifies.Add((component, sp.SocketName?.Text));
                    }
                    break;
                }
                case UFortAnimNotifyState_EmoteSound es:
                {
                    _pendingNotifies.Add((new AudioComponent(es, descriptor.Name), es.AttachName?.Text));
                    break;
                }
                // Marvel Rivals
                case UAnimNotifyState_TimedSkeletonAnimation tsa when tsa.SkeletalMeshTemplate?.TryLoad<USkeletalMesh>(out var sk) == true:
                {
                    var transform = new Transform(tsa.LocationOffset, tsa.RotationOffset.Quaternion(), FVector.OneVector);
                    var component = new SkeletalMeshComponent(sk, transform, tsa.AnimToPlay?.Load<UAnimationAsset>(), tsa.AnimStartPos);
                    _pendingNotifies.Add((component, tsa.SocketName?.Text));
                    break;
                }
                case UAN_AkEvent ae:
                {
                    _pendingNotifies.Add((new AudioComponent(ae, descriptor.Name), ae.AttachName?.Text));
                    break;
                }
            }
        }
    }

    private void FlushNotifyComponents()
    {
        if (Actor is null || _pendingNotifies.Count == 0) return;

        foreach (var (component, socket) in _pendingNotifies)
        {
            component.AttachSocketName = socket;
            component.Relation = this;

            Actor.Components.Add(component);
            _notifyComponents.Add(component);
        }
        _pendingNotifies.Clear();
    }

    private void ClearNotifyComponents()
    {
        _pendingNotifies.Clear();
        foreach (var component in _notifyComponents)
        {
            Actor?.Components.Remove(component);
        }
        _notifyComponents.Clear();
    }

    protected override void OnActorAttached(Actor actor)
    {
        base.OnActorAttached(actor);

        FlushNotifyComponents();
    }

    public override void Export(ExportSession session, CancellationToken ct = default)
    {
        // export the mesh then export whatever animation was set to this component, if any
        base.Export(session, ct);
        if (Actor?.ActorManager is not { } manager || Animation is null)
            return;

        try
        {
            session.Add(manager.FileProvider.LoadPackageObject(Animation.Path, Animation.Name));
        }
        catch
        {
            //
        }
    }

    private const string HeaderLabel = "Animation";
    private HeaderButtons HeaderButtons => field ??= new HeaderButtons(HeaderLabel)
        .Add(() => IsPlayingAnimation ? "\uf04c" : "\uf04b", "Play/Pause", () => IsPlayingAnimation = !IsPlayingAnimation)
        .Add("\uf0c5", "Copy Path", () => ImGui.SetClipboardText(Animation?.Path))
        .Add("\uf05a", "Animation Info", () => ImGui.OpenPopup("##AnimationInfo"));

    public override void DrawControls()
    {
        base.DrawControls();
        if (Animation == null) return;

        var compatible = Animation.Skeleton.Guid == Descriptor.Skeleton?.Guid;
        if (!compatible)
        {
            ImGui.PushStyleColor(ImGuiCol.Header, new Vector4(1.0f, 0.5f, 0.0f, 0.5f));
            ImGui.PushStyleColor(ImGuiCol.HeaderHovered, new Vector4(1.0f, 0.5f, 0.2f, 0.6f));
            ImGui.PushStyleColor(ImGuiCol.HeaderActive, ImGui.GetColorU32(ImGuiCol.HeaderHovered));
        }
        var open = ImGui.CollapsingHeader(HeaderLabel, ImGuiTreeNodeFlags.DefaultOpen | ImGuiTreeNodeFlags.AllowOverlap);
        if (!compatible)
        {
            ImGui.PopStyleColor(3);
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("This animation's skeleton does not match the mesh's skeleton.\nAnimation playback may not work as expected.");
            }
        }
        HeaderButtons.Draw(ImGui.GetItemRectMin(), ImGui.GetItemRectSize());

        DrawInfoPopup();

        if (!open) return;

        EditorUI.PropertyValueTable(HeaderLabel, () =>
        {
            EditorUI.Text("Name", Animation.Name);
            EditorUI.Text("Duration", $"{Animation.Duration:0.00} seconds");
            ImGui.SameLine();
            ImGui.TextDisabled($"(in {Animation.Sequences.Length} sequence{(Animation.Sequences.Length != 1 ? "s" : "")})");
            EditorUI.Property("Start Time");
            if (ImGui.DragFloat("##StartTime", ref Animation.StartTime, Animation.Duration / 1000f, 0f, Animation.Duration, "%.2fs", ImGuiSliderFlags.AlwaysClamp))
            {
                AnimationTime = Animation.StartTime; // the start time is where playback begins, so moving it seeks there
            }
            EditorUI.Property("Play Rate");
            ImGui.DragFloat("##PlayRate", ref Animation.PlayRate, 0.01f, 0.1f, 5f, "%.2fx", ImGuiSliderFlags.AlwaysClamp);
        });
    }

    private void DrawInfoPopup()
    {
        var viewport = ImGui.GetMainViewport();
        ImGui.SetNextWindowSize(viewport.WorkSize * 0.75f, ImGuiCond.Always);
        ImGui.SetNextWindowPos(viewport.GetCenter(), ImGuiCond.Always, new Vector2(0.5f));

        var open = true;
        var flags = ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize;
        if (ImGui.BeginPopupModal("##AnimationInfo", ref open, flags))
        {
            if (ImGui.BeginChild("##AnimationInfoBody", Vector2.Zero, ImGuiChildFlags.FrameStyle))
            {
                Animation?.DrawControls();
            }
            ImGui.EndChild();
            ImGui.EndPopup();
        }
    }

    public override object Clone() => new SkeletalMeshComponent(this);
}
