using System.Numerics;
using CUE4Parse.UE4.Assets.Exports.Component;
using ImGuiNET;
using Snooper.Core;
using Snooper.Core.Containers.Resources;
using Snooper.Core.Containers.Textures;
using Snooper.Rendering.Components.Primitive;
using Snooper.Rendering.Components.Transforms;
using Snooper.Rendering.Primitives;
using Snooper.Rendering.Systems;
using Snooper.UI;

namespace Snooper.Rendering.Components.Visualization;

public readonly struct PerMaterialDebugData : IPerMaterialData
{
    public bool IsReady { get; init; }
    public float LineThickness { get; init; }
    public ulong Padding { get; init; }
    public Vector3 LineColor { get; init; }
}

[DefaultActorSystem(typeof(DebugSystem))]
public abstract class DebugComponent : PrimitiveComponent<PerMaterialDebugData>
{
    protected DebugComponent(Vector3 color, float lineThickness = 1.0f, Transform? transform = null, string? name = null) : base(transform, name)
    {
        Materials[0].InlineContainer = new MaterialDataContainer(color, lineThickness);
    }

    protected DebugComponent(UPrimitiveComponent component) : base(component)
    {

    }

    private const string HeaderLabel = "Wireframe";
    private HeaderButtons HeaderButtons => field ??= new HeaderButtons(HeaderLabel)
        .Add(() => IsVisible ? Settings.EyeIcon : Settings.EyeSlashIcon, () => "Toggle Visibility",
            () => { IsVisible = !IsVisible; }, null,
            () => IsVisible ? null : Settings.RedColor);

    public override void DrawControls()
    {
        base.DrawControls();

        var open = ImGui.CollapsingHeader(HeaderLabel, ImGuiTreeNodeFlags.DefaultOpen | ImGuiTreeNodeFlags.AllowOverlap);
        HeaderButtons.Draw(ImGui.GetItemRectMin(), ImGui.GetItemRectSize());

        if (!open) return;

        EditorUI.PropertyValueTable(HeaderLabel, () =>
        {
            if (Materials[0].MaterialDataContainer is not { } container)
            {
                EditorUI.Property(string.Empty);
                ImGui.TextColored(Settings.OrangeColor, "No material data container available.");
            }
            else
            {
                container.DrawControls();
            }
        });
    }

    protected class MaterialDataContainer(Vector3 color, float lineThickness = 1.0f) : IMaterialDataContainer
    {
        public string Name => Settings.NoName;
        public bool HasTextures => false;
        public bool IsTranslucent => false;
        public Dictionary<string, Texture> GetTextures() => throw new NotImplementedException();
        public void SetBindlessTexture(string key, BindlessTexture bindless) => throw new NotImplementedException();

        public void FinalizeGpuData()
        {
            if (Raw is not null)
                throw new InvalidOperationException("GPU data has already been finalized and sent.");

            Raw = new PerMaterialDebugData
            {
                IsReady = true,
                LineColor = color,
                LineThickness = lineThickness,
            };
        }

        public IPerMaterialData? Raw { get; private set; }

        public void DrawControls()
        {
            EditorUI.Property("Color");
            ImGui.ColorButton("##LineColor", new Vector4(color, 1.0f), ImGuiColorEditFlags.NoPicker | ImGuiColorEditFlags.NoTooltip);

            EditorUI.Text("Thickness", $"{lineThickness}");
        }
    }

    protected abstract class DebugGeometry : PrimitiveData;
}
