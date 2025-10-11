using CUE4Parse.UE4.Assets.Exports.Component.TextRender;
using CUE4Parse.UE4.Objects.Core.i18N;
using CUE4Parse.UE4.Objects.Core.Math;
using ImGuiNET;
using Snooper.Rendering.Components.Transforms;
using Snooper.UI;

namespace Snooper.Rendering.Components.Primitive;

public class TextRenderComponent : SpatialComponent // TODO: add text rendering
{
    public readonly string Text;
    
    public TextRenderComponent(UTextRenderComponent component) : base(component)
    {
        Text = component.GetOrDefault<FText?>("Text")?.Text ?? "DefaultText";
        var hAlignment = component.GetOrDefault("HorizontalAlignment", EHorizTextAligment.EHTA_Left);
        var vAlignment = component.GetOrDefault("VerticalAlignment", EVerticalTextAligment.EVRTA_TextBottom);
        var color = component.GetOrDefault("TextRenderColor", new FColor(255, 255, 255, 255));
        var worldSize = component.GetOrDefault("WorldSize", 30.0f) * Settings.GlobalScale;
    }

    internal override string Icon => "text";

    public override void DrawControls()
    {
        base.DrawControls();
        
        EditorUI.CollapsingTable("Text", ImGuiTreeNodeFlags.DefaultOpen, () =>
        {
            EditorUI.Text("Content", Text);
        });
    }
}