using CUE4Parse.UE4.Assets.Exports.WorldPartition;
using ImGuiNET;
using Snooper.Rendering.Components.Transforms;

namespace Snooper.Rendering.Actors;

public class PartitionActor : Actor
{
    private HierarchicalActor? _hlod;
    public HierarchicalActor? Selected
    {
        get => _hlod;
        set
        {
            if (_hlod == value) return;

            _hlod = value;
            foreach (var child in Children)
            {
                child.IsVisible = child == _hlod;
            }
        }
    }
    
    public PartitionActor(UWorldPartition partition) : base(partition)
    {
        Components.Add(new SpatialComponent(null, "PartitionRoot"));
        
        switch (partition.RuntimeHash?.Load<UWorldPartitionRuntimeHash>())
        {
            case UWorldPartitionRuntimeHashSet set:
            {
                var sortedData = set.RuntimeStreamingData.OrderBy(x => x.LoadingRange).ToArray();
                for (var i = 0; i < sortedData.Length; i++)
                {
                    Children.Add(new HierarchicalActor(sortedData[i], i, i == sortedData.Length - 1));
                }
                break;
            }
            case UWorldPartitionRuntimeSpatialHash spatial when spatial.StreamingGrids[0].GridLevels.Length > 0:
            {
                break;
            }
        }

        Selected = Children.OfType<HierarchicalActor>().LastOrDefault();
    }

    internal override void DrawInterface()
    {
        base.DrawInterface();

        var count = Children.Count;
        if (count == 0)
        {
            ImGui.TextUnformatted("This partition has no HLODs.");
            return;
        }

        ImGui.SeparatorText($"{count} HLOD{(count > 1 ? "s" : "")}");

        if (ImGui.BeginCombo("##HLODs", Selected?.Name ?? "Select HLOD"))
        {
            foreach (var child in Children.OfType<HierarchicalActor>())
            {
                var selected = child.IsVisible;
                if (ImGui.Selectable(child.Name, selected))
                {
                    Selected = child;
                }

                if (selected) ImGui.SetItemDefaultFocus();
            }
            ImGui.EndCombo();
        }

        if (Selected is { } hlod)
        {
            ImGui.TextUnformatted($"Loading Range: {hlod.LoadingRange:F2}");
            ImGui.BeginDisabled(hlod.Index == 0 || hlod.Children.OfType<CellActor>().All(c => c.IsLoaded || !c.CanLoad));
            if (ImGui.Button("Load All Cells"))
            {
                foreach (var child in hlod.Children.OfType<CellActor>())
                {
                    if (child is { IsLoaded: false, IsLoading: false })
                    {
                        child.Load();
                    }
                }
            }
            ImGui.EndDisabled();
        }
    }
}