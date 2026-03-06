using System.Numerics;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Objects.Engine;
using CUE4Parse.UE4.Objects.UObject;
using ImGuiNET;
using Snooper.Rendering.Components.Transforms;

namespace Snooper.Rendering.Actors;

public class BlueprintLevelActor : LevelActor
{
    private readonly List<string> _parameters = [];
    private readonly List<string> _functions = [];

    public BlueprintLevelActor(UObject actor, Dictionary<FPackageIndex, SpatialComponent> components, UBlueprintGeneratedClass blueprint) : base(actor, components)
    {
        foreach (var child in blueprint.ChildProperties)
        {
            if (child is not FProperty property) continue;

            if (property.PropertyFlags == (EPropertyFlags.Edit | EPropertyFlags.BlueprintVisible))
            {
                _parameters.Add($"{property.Name}: {actor.GetOrDefault<object>(property.Name.Text)}");
            }
        }

        // foreach (var child in blueprint.Children)
        // {
        //     if (!child.TryLoad<UFunction>(out var function)) continue;
        //
        //     if (function.FunctionFlags.HasFlag(EFunctionFlags.FUNC_Public | EFunctionFlags.FUNC_BlueprintCallable))
        //     {
        //         _functions.Add(child.Name);
        //     }
        // }
    }

    internal override string Icon => "\uf46d";

    internal override void DrawInterface()
    {
        base.DrawInterface();

        var size = new Vector2(ImGui.GetContentRegionAvail().X, 0);

        ImGui.SeparatorText("Parameters");
        if (ImGui.BeginListBox("##Parameters", size))
        {
            foreach (var parameter in _parameters)
            {
                ImGui.TextUnformatted(parameter);
            }
            ImGui.EndListBox();
        }

        // ImGui.SeparatorText("Functions");
        // if (ImGui.BeginListBox("##Functions", size))
        // {
        //     foreach (var function in _functions)
        //     {
        //         ImGui.TextUnformatted(function);
        //     }
        //     ImGui.EndListBox();
        // }
    }
}
