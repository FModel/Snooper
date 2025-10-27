using ImGuiNET;
using Snooper.Core.Containers;
using Snooper.Extensions;

namespace Snooper.UI;

public static class MemoryDetailsUI
{
    public static void DrawMemoryDetails(IMemoryDetailsProvider provider)
    {
        var details = provider.GetMemoryDetails().ToList();
        if (details.Count == 0)
        {
            ImGui.TextDisabled("No child resources");
            return;
        }
        
        foreach (var detail in details)
        {
            var label = $"{detail.Name}: {detail.Used.GetReadableSizeOutOf(detail.Allocated)}";
            
            if (detail.HasChildren)
            {
                if (ImGui.TreeNode(label))
                {
                    ImGui.Indent();
                    DrawMemoryDetails(detail.Provider!);
                    ImGui.Unindent();
                    ImGui.TreePop();
                }
            }
            else
            {
                ImGui.Bullet();
                ImGui.SameLine();
                ImGui.TextUnformatted(label);
                
                if (ImGui.IsItemHovered())
                {
                    ImGui.BeginTooltip();
                    ImGui.TextUnformatted($"Type: {detail.Type}");
                    ImGui.TextUnformatted($"Allocated: {detail.Allocated.GetReadableSize()}");
                    ImGui.TextUnformatted($"Used: {detail.Used.GetReadableSize()}");
                    ImGui.TextUnformatted($"Wasted: {detail.Wasted.GetReadableSize()}");
                    ImGui.TextUnformatted($"Usage: {detail.UsagePercentage:F2}%");
                    ImGui.EndTooltip();
                }
            }
        }
    }
    
    public static void DrawMemoryTable(IMemoryDetailsProvider provider, bool showTrees = true, int depth = 0)
    {
        var beginTable = false;
        if (depth == 0 && ImGui.BeginTable("MemoryTable", 5, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.Resizable | ImGuiTableFlags.NoSavedSettings))
        {
            ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch, 0.55f);
            ImGui.TableSetupColumn("Type", ImGuiTableColumnFlags.WidthStretch, 0.15f);
            ImGui.TableSetupColumn("Used", ImGuiTableColumnFlags.WidthStretch, 0.1f);
            ImGui.TableSetupColumn("Allocated", ImGuiTableColumnFlags.WidthStretch, 0.1f);
            ImGui.TableSetupColumn("Usage %", ImGuiTableColumnFlags.WidthStretch, 0.1f);
            ImGui.TableHeadersRow();
            beginTable = true;
        }
        
        var details = provider.GetMemoryDetails().ToList();
        if (details.Count == 0)
        {
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.TextDisabled("No resources to display");
        }
        else foreach (var detail in details)
        {
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            
            var open = false;
            if (showTrees && detail.HasChildren)
            {
                open = ImGui.TreeNodeEx(detail.Name, ImGuiTreeNodeFlags.SpanFullWidth);
            }
            else
            {
                ImGui.Bullet();
                ImGui.SameLine();
                ImGui.TextUnformatted(detail.Name);
            }
            
            ImGui.TableNextColumn();
            ImGui.TextDisabled(detail.Type);
                    
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(detail.Used.GetReadableSize());
            
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(detail.Allocated.GetReadableSize());
                    
            ImGui.TableNextColumn();
            var percentage = detail.UsagePercentage;
            var color = percentage < 40 ? new System.Numerics.Vector4(1, 0, 0, 1) :
                percentage < 65 ? new System.Numerics.Vector4(1, 1, 0, 1) :
                new System.Numerics.Vector4(0, 1, 0, 1);
            ImGui.PushStyleColor(ImGuiCol.Text, color);
            ImGui.TextUnformatted($"{percentage:F2}%");
            ImGui.PopStyleColor();
            
            if (open)
            {
                DrawMemoryTable(detail.Provider!, showTrees, depth + 1);
                ImGui.TreePop();
            }
        }
        
        if (beginTable)
        {
            ImGui.EndTable();
        }
    }
    
    public static void DrawMemorySummary(IMemorySizeProvider provider)
    {
        var allocated = provider.Allocated;
        var used = provider.Used;
        var wasted = provider.Wasted;
        
        ImGui.TextUnformatted($"Total: {used.GetReadableSizeOutOf(allocated)}");
        
        if (allocated > 0)
        {
            var usageRatio = (float)used / allocated;
            ImGui.ProgressBar(usageRatio, new System.Numerics.Vector2(-1, 0), $"{provider.UsagePercentage:F2}%");
            
            ImGui.Spacing();
            ImGui.TextDisabled($"Wasted: {wasted.GetReadableSize()}");
        }
    }
}
