using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Animation;
using CUE4Parse.UE4.Assets.Exports.Material;
using Snooper.Rendering.Components;
using Snooper.Rendering.Components.Descriptors;
using Snooper.Rendering.Components.Mesh;

namespace Snooper.Hosting;

public enum AssetRequestKind
{
    StaticMesh,
    SkeletalMesh,
    Animation,
    Material,
    Texture
}

public sealed class AssetRequest
{
    public readonly AssetRequestKind Kind;
    public readonly Type AssetType;
    public readonly ActorComponent Target;
    public readonly object? Subject;
    public string DisplayText { get; }

    private readonly Action<UObject> _apply;

    private AssetRequest(AssetRequestKind kind, Type assetType, ActorComponent target, object? subject, string text, Action<UObject> apply)
    {
        Kind = kind;
        AssetType = assetType;
        Target = target;
        Subject = subject;
        DisplayText = text;
        _apply = apply;
    }

    public bool Accepts(UObject asset) => AssetType.IsInstanceOfType(asset);
    public bool IsFor(ActorComponent component) => Target == component;
    public bool IsFor(ActorComponent component, object subject) => IsFor(component) && Equals(Subject, subject);

    internal void Apply(UObject asset)
    {
        if (Target.Scene is null) return;
        _apply(asset);
    }

    internal static AssetRequest Animation(SkeletalMeshComponent target)
        => new(AssetRequestKind.Animation, typeof(UAnimationAsset), target, null, $"Requesting an animation for {target.Name}",
            asset => target.SetAnimation((UAnimationAsset) asset));

    // internal static AssetRequest Material(MeshComponent target, MaterialSection section)
    //     => new(AssetRequestKind.Material, typeof(UMaterialInterface), target, section, $"Requesting a material for {target.Name} (slot {section.Index})",
    //         asset => target.SwapMaterial(section, (UUnrealMaterial) asset));
}
