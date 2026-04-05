using CUE4Parse.UE4.Assets.Exports.Animation;
using CUE4Parse.UE4.Assets.Exports.Component.SkeletalMesh;
using CUE4Parse.UE4.Assets.Exports.SkeletalMesh;
using ImGuiNET;
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
            field = value;
            IsPlayingAnimation = true;
        }
    }

    public float MaxAnimationDuration
    {
        get
        {
            if (Relation is SkeletalMeshComponent skeletal)
            {
                return skeletal.MaxAnimationDuration;
            }
            return Animation?.Duration ?? 0.0f;
        }
    }

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

    public SkeletalMeshComponent(USkeletalMesh skeletalMesh, Transform? transform = null) : base(skeletalMesh, transform)
    {

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

    public void SetAnimation(UAnimationAsset animToPlay, float startTime = 0f, float playRate = 1f)
    {
        Animation = new AnimationDescriptor(animToPlay, startTime, playRate);
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

        var open = ImGui.CollapsingHeader(HeaderLabel, ImGuiTreeNodeFlags.DefaultOpen | ImGuiTreeNodeFlags.AllowOverlap);
        HeaderButtons.Draw(ImGui.GetItemRectMin(), ImGui.GetItemRectSize());

        if (!open) return;

        ImGui.TextUnformatted(Animation.Name);
    }
}
