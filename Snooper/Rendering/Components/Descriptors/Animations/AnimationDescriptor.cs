using CUE4Parse.UE4.Assets.Exports.Animation;
using ImGuiNET;
using Snooper.Extensions;
using Snooper.UI;

namespace Snooper.Rendering.Components.Descriptors.Animations;

public abstract class AnimationDescriptor : IControllable
{
    public string Name { get; }
    public string Path { get; }

    public readonly SkeletonDescriptor Skeleton;

    protected AnimationDescriptor(UAnimationAsset owner, AnimationDescriptor? outer = null)
    {
        Name = owner.Name;
        Path = owner.GetCleanPath() ?? "N/A";
        Skeleton = outer?.Skeleton ?? Create();

        SkeletonDescriptor Create()
        {
            var skeleton = owner.Skeleton?.Load<USkeleton>() ?? throw new InvalidOperationException($"Failed to load skeleton for animation asset {owner.Name}");

            var descriptor = new SkeletonDescriptor(skeleton.ReferenceSkeleton);
            descriptor.SetOwner(skeleton);
            return descriptor;
        }
    }

    protected virtual string Subtitle => string.Empty;

    public virtual void DrawControls()
    {
        DrawHeader();

        ImGui.Spacing();
        ImGui.SeparatorText($"Bones  ({Skeleton.BoneCount})");
        Skeleton.DrawControls();
    }

    private void DrawHeader()
    {
        ImGui.TextUnformatted(Name);
        ImGui.SameLine();
        ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetColorU32(ImGuiCol.TextDisabled));
        ImGui.TextUnformatted(Subtitle);

        ImGui.SetWindowFontScale(0.85f);
        ImGui.TextUnformatted($"Animation: {Path}");
        ImGui.TextUnformatted($"Skeleton: {Skeleton.Path}");
        ImGui.SetWindowFontScale(1.0f);
        ImGui.PopStyleColor();
    }
}
