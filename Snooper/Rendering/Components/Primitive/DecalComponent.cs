using CUE4Parse.UE4.Assets.Exports.Component;

namespace Snooper.Rendering.Components.Primitive;

public class DecalComponent : BillboardComponent
{
    public DecalComponent(UDecalComponent component) : base(component, "S_DecalActorIcon")
    {

    }

    public override string Icon => "\uf5fd";
}
