using System.Numerics;
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
            if (field == value) return;

            field = value;

            IsPlayingAnimation = field != null;
            if (!IsPlayingAnimation)
            {
                Descriptor.Skeleton?.ResetAllBones();
                MarkDirty(DirtyFlags.Animation);
            }
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

    public void SetAnimation(UAnimationAsset? animToPlay, float startTime = 0f, float playRate = 1f)
    {
        Animation = animToPlay != null ? new AnimationDescriptor(animToPlay, startTime, playRate) : null;
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
            ImGui.DragFloat("##StartTime", ref Animation.StartTime, Animation.Duration / 1000f, 0f, Animation.Duration, "%.2fs", ImGuiSliderFlags.AlwaysClamp);
            EditorUI.Property("Play Rate");
            ImGui.DragFloat("##PlayRate", ref Animation.PlayRate, 0.01f, 0.1f, 5f, "%.2fx", ImGuiSliderFlags.AlwaysClamp);
        });
    }

    private void DrawInfoPopup()
    {
        var viewport = ImGui.GetMainViewport();
        ImGui.SetNextWindowSize(viewport.WorkSize * 0.75f, ImGuiCond.Appearing);
        ImGui.SetNextWindowPos(viewport.GetCenter(), ImGuiCond.Appearing, new Vector2(0.5f));

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
