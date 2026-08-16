using System.Numerics;
using CUE4Parse_Conversion;
using CUE4Parse.UE4.Assets.Exports.Animation;
using CUE4Parse.UE4.Assets.Exports.Component.SkeletalMesh;
using CUE4Parse.UE4.Assets.Exports.SkeletalMesh;
using ImGuiNET;
using Snooper.Core;
using Snooper.Rendering.Components.Descriptors.Animations;
using Snooper.Rendering.Components.Transforms;
using Snooper.Rendering.Systems;
using Snooper.UI;

namespace Snooper.Rendering.Components.Mesh;

[DefaultActorSystem(typeof(AnimationClockSystem))]
public class SkeletalMeshComponent : SkinnedMeshComponent
{
    protected override DirtyFlags SupportedDirtyFlags => base.SupportedDirtyFlags | DirtyFlags.Animation;

    public AnimationPlayback? Playback { get; private set; }
    public SequenceBaseDescriptor? Animation => Playback?.Animation;

    private SkeletalMeshComponent(SkeletalMeshComponent other) : base(other)
    {
        Playback = other.Playback;
    }

    public SkeletalMeshComponent(USkeletalMesh skeletalMesh, Transform? transform = null) : base(skeletalMesh, transform)
    {

    }

    public SkeletalMeshComponent(USkeletalMesh skeletalMesh, Transform? transform, UAnimationAsset? animToPlay, float playPosition = 0f) : this(skeletalMesh, transform)
    {
        SetAnimation(animToPlay, playPosition);
    }

    public SkeletalMeshComponent(UAnimationAsset animToPlay, float playPosition = 0f, float playRate = 1f) : this(animToPlay.Skeleton?.Load<USkeleton>() ?? throw new InvalidOperationException($"Failed to load skeleton for animation asset {animToPlay.Name}"))
    {
        SetAnimation(animToPlay, playPosition, playRate);
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

    public void SetAnimation(UAnimationAsset? animToPlay, float playPosition = 0f, float playRate = 1f)
    {
        Playback?.Despawn();
        Bind(animToPlay != null ? AnimationPlayback.Create(animToPlay, playPosition, playRate) : null);
    }

    public void Bind(AnimationPlayback? playback)
    {
        if (Playback == playback) return;

        Playback?.Detach(this);
        Playback = playback;
        Playback?.Attach(this);

        MarkDirty(DirtyFlags.Animation);
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
        .Add(() => Playback is { IsPlaying: true } ? "\uf04c" : "\uf04b", "Play/Pause", () => Playback?.IsPlaying = Playback is { IsPlaying: false })
        .Add("\uf0c5", "Copy Path", () => ImGui.SetClipboardText(Animation?.Path))
        .Add("\uf05a", "Animation Info", () => ImGui.OpenPopup("##AnimationInfo"));

    public override void DrawControls()
    {
        base.DrawControls();
        if (Playback is not { } playback || Animation is not { } animation) return;

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
            EditorUI.Text("Name", animation.Name);
            EditorUI.Text("Duration", $"{animation.Duration:0.00} seconds");
            ImGui.SameLine();
            ImGui.TextDisabled($"(in {animation.Segments.Count} segment{(animation.Segments.Count != 1 ? "s" : "")})");

            if (playback.Components.Count > 1)
            {
                EditorUI.Text("Shared With", $"{playback.Components.Count - 1} other mesh{(playback.Components.Count != 2 ? "es" : "")}");
            }

            EditorUI.Property("Play Position");
            if (ImGui.DragFloat("##PlayPosition", ref playback.PlayPosition, animation.Duration / 1000f, 0f, animation.Duration, "%.2fs", ImGuiSliderFlags.AlwaysClamp))
            {
                playback.Seek(playback.PlayPosition); // the play position is where playback begins, so moving it seeks there
            }
            EditorUI.Property("Play Rate");
            ImGui.DragFloat("##PlayRate", ref playback.PlayRate, 0.01f, 0.1f, 5f, "%.2fx", ImGuiSliderFlags.AlwaysClamp);
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
                Animation?.DrawControls(Playback?.Time ?? 0f);
            }
            ImGui.EndChild();
            ImGui.EndPopup();
        }
    }

    public override object Clone() => new SkeletalMeshComponent(this);
}
