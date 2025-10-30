using ImGuiNET;
using Snooper.Core.Containers;
using Snooper.Core.Containers.Buffers;
using Snooper.Extensions;
using System.Numerics;

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

            if (detail.Provider == null)
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
            else if (ImGui.TreeNode(label))
            {
                switch (detail.Provider)
                {
                    case IMemoryDetailsProvider d:
                        DrawMemoryDetails(d);
                        break;
                    default:
                        if (detail.Provider.GetBufferStatistics() is { } stats)
                            DrawBufferStatistics(stats, detail.Name);
                        break;
                }
                ImGui.TreePop();
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
            if (showTrees && detail.Provider is IMemoryDetailsProvider)
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
            var color = percentage < 40 ? new Vector4(1, 0, 0, 1) :
                percentage < 65 ? new Vector4(1, 1, 0, 1) :
                new Vector4(0, 1, 0, 1);
            ImGui.PushStyleColor(ImGuiCol.Text, color);
            ImGui.TextUnformatted($"{percentage:F2}%");
            ImGui.PopStyleColor();
            
            if (open && detail.Provider is IMemoryDetailsProvider d)
            {
                DrawMemoryTable(d, showTrees, depth + 1);
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
            ImGui.ProgressBar(usageRatio, new Vector2(-1, 0), $"{provider.UsagePercentage:F2}%");
            
            ImGui.Spacing();
            ImGui.TextDisabled($"Wasted: {wasted.GetReadableSize()}");
        }
    }
    
    private static void DrawBufferStatistics(BufferStatistics stats, string bufferName = "Buffer")
    {
        ImGui.TextUnformatted($"Capacity: {stats.Capacity} items");
        ImGui.TextUnformatted($"Used: {stats.UsedItems} items ({(float)stats.UsedItems / stats.Capacity * 100:F1}%)");
        ImGui.TextUnformatted($"Free: {stats.FreeItems} items ({(float)stats.FreeItems / stats.Capacity * 100:F1}%)");
        ImGui.TextUnformatted($"Fragmentation: {stats.FragmentationPercentage:F1}%");
        
        var fragColor = stats.FragmentationPercentage < 20 ? new Vector4(0, 1, 0, 1) :
                       stats.FragmentationPercentage < 50 ? new Vector4(1, 1, 0, 1) :
                       new Vector4(1, 0, 0, 1);
        ImGui.SameLine();
        ImGui.PushStyleColor(ImGuiCol.Text, fragColor);
        ImGui.TextUnformatted(stats.FragmentationPercentage < 20 ? "(Good)" : 
                             stats.FragmentationPercentage < 50 ? "(Moderate)" : "(High)");
        ImGui.PopStyleColor();
        
        ImGui.Spacing();
        
        DrawBufferVisualization(stats);
        
        ImGui.Spacing();
        
        DrawBufferBlocksTable(stats, bufferName);
    }
    
    private static void DrawBufferBlocksTable(BufferStatistics stats, string bufferName)
    {
        if (stats.Allocations.Count > 0)
        {
            ImGui.SeparatorText($"Allocations: {stats.Allocations.Count}");
            DrawPagedTable(
                $"Allocations###{bufferName}Allocs",
                stats.Allocations,
                (alloc, rowIndex) =>
                {
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted($"{alloc.StartIndex}");
                    
                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted($"{alloc.EndIndex}");
                    
                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted($"{alloc.Length}");
                    
                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted($"Created: {alloc.CreatedAt.ToLocalTime():HH:mm:ss}");
                    if (alloc.LastModified.HasValue)
                    {
                        ImGui.SameLine();
                        ImGui.TextUnformatted($"Modified: {alloc.LastModified.Value.ToLocalTime():HH:mm:ss}");
                    }
                }
            );
        }
        
        if (stats.FreeBlocks.Count > 0)
        {
            ImGui.Spacing();
            ImGui.SeparatorText($"Free Blocks: {stats.FreeBlocks.Count}");
            DrawPagedTable(
                $"FreeBlocks###{bufferName}Free",
                stats.FreeBlocks,
                (block, rowIndex) =>
                {
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted($"{block.StartIndex}");
                    
                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted($"{block.StartIndex + block.Length - 1}");
                    
                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted($"{block.Length}");
                    
                    ImGui.TableNextColumn();
                    ImGui.TextDisabled("Reusable memory block");
                }
            );
        }
    }
    
    private static void DrawPagedTable<T>(string tableId, IReadOnlyList<T> items, Action<T, int> drawRow)
    {
        var totalItems = items.Count;
        if (totalItems == 0) return;

        const int MaxItemsPerPage = 10;
        var totalPages = (int)Math.Ceiling((double)totalItems / MaxItemsPerPage);
        var currentPage = 0;
        
        if (totalPages > 1)
        {
            ImGui.TextUnformatted("Page:");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(60);
            ImGui.InputInt($"###{tableId}Page", ref currentPage, 1, 1);
            currentPage = Math.Clamp(currentPage, 0, totalPages - 1);
            
            ImGui.SameLine();
            ImGui.TextUnformatted($"{currentPage + 1}/{totalPages}");
        }
        
        var startIndex = currentPage * MaxItemsPerPage;
        var endIndex = Math.Min(startIndex + MaxItemsPerPage, totalItems);
        
        if (ImGui.BeginTable($"Table{tableId}", 4, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.Resizable | ImGuiTableFlags.NoSavedSettings))
        {
            ImGui.TableSetupColumn("Start", ImGuiTableColumnFlags.WidthFixed, 60);
            ImGui.TableSetupColumn("End", ImGuiTableColumnFlags.WidthFixed, 60);
            ImGui.TableSetupColumn("Length", ImGuiTableColumnFlags.WidthFixed, 60);
            ImGui.TableSetupColumn("Details", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableHeadersRow();
            
            for (var i = startIndex; i < endIndex; i++)
            {
                drawRow(items[i], i);
            }
            
            ImGui.EndTable();
        }
        
        if (totalPages > 1)
        {
            ImGui.TextUnformatted($"Showing items {startIndex + 1}-{endIndex} of {totalItems}");
        }
    }
    
    private static void DrawBufferVisualization(BufferStatistics stats)
    {
        if (stats.Capacity == 0) return;

        var drawList = ImGui.GetWindowDrawList();
        var cursorPos = ImGui.GetCursorScreenPos();
        var availWidth = ImGui.GetContentRegionAvail().X;
        var pixelsPerItem = availWidth / stats.Capacity;

        const float height = 40f;
        drawList.AddRectFilled(cursorPos, new Vector2(cursorPos.X + availWidth, cursorPos.Y + height), ImGui.GetColorU32(new Vector4(0.2f, 0.2f, 0.2f, 1)));
        
        var length = stats.Allocations.Count;
        for (var i = 0; i < length; i++)
        {
            var alloc = stats.Allocations[i];
            var startX = cursorPos.X + alloc.StartIndex * pixelsPerItem;
            var endX = cursorPos.X + (alloc.EndIndex + 1) * pixelsPerItem;

            drawList.AddRectFilled(cursorPos with { X = startX }, new Vector2(endX, cursorPos.Y + height), GenerateDistinctColor(i, length), 5f, ImDrawFlags.RoundCornersTop);
        }
        
        foreach (var block in stats.FreeBlocks)
        {
            var startX = cursorPos.X + block.StartIndex * pixelsPerItem;
            var endX = cursorPos.X + (block.StartIndex + block.Length) * pixelsPerItem;

            for (var x = startX; x < endX; x += 4)
            {
                drawList.AddLine(cursorPos with { X = x }, new Vector2(x + height, cursorPos.Y + height), ImGui.GetColorU32(new Vector4(0.5f, 0.5f, 0.5f, 0.5f)), 1f);
            }
        }
        
        ImGui.InvisibleButton("BufferVis", new Vector2(availWidth, height));
        if (ImGui.IsItemHovered())
        {
            var mousePos = ImGui.GetMousePos();
            var relativeX = mousePos.X - cursorPos.X;
            var index = (int)(relativeX / pixelsPerItem);
            
            if (index >= 0 && index < stats.Capacity)
            {
                ImGui.BeginTooltip();
                ImGui.TextUnformatted($"Index: {index}");
                
                var foundAlloc = stats.Allocations.FirstOrDefault(a => index >= a.StartIndex && index <= a.EndIndex);
                if (foundAlloc != null)
                {
                    ImGui.Separator();
                    ImGui.TextUnformatted($"Allocation ID: {foundAlloc.AllocationId}");
                    ImGui.TextUnformatted($"Range: [{foundAlloc.StartIndex}..{foundAlloc.EndIndex}]");
                    ImGui.TextUnformatted($"Length: {foundAlloc.Length}");
                    ImGui.TextUnformatted($"Created: {foundAlloc.CreatedAt.ToLocalTime():HH:mm:ss}");
                    if (foundAlloc.LastModified.HasValue)
                    {
                        ImGui.TextUnformatted($"Modified: {foundAlloc.LastModified.Value.ToLocalTime():HH:mm:ss}");
                    }
                }
                else
                {
                    var foundFree = stats.FreeBlocks.FirstOrDefault(fb => index >= fb.StartIndex && index < fb.StartIndex + fb.Length);
                    if (foundFree.StartIndex != 0 || foundFree.Length != 0)
                    {
                        ImGui.Separator();
                        ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1), "Free Block");
                        ImGui.TextUnformatted($"Range: [{foundFree.StartIndex}..{foundFree.StartIndex + foundFree.Length - 1}]");
                        ImGui.TextUnformatted($"Length: {foundFree.Length}");
                    }
                    else
                    {
                        ImGui.Separator();
                        ImGui.TextDisabled("Unused space");
                    }
                }
                
                ImGui.EndTooltip();
            }
        }
    }
    
    private static uint GenerateDistinctColor(int index, int total)
    {
        var hue = (float)index / total;
        return ImGui.GetColorU32(HsvToRgb(hue, 0.7f, 0.9f));
    }
    
    private static Vector4 HsvToRgb(float h, float s, float v)
    {
        var c = v * s;
        var x = c * (1 - MathF.Abs(h * 6 % 2 - 1));
        var m = v - c;
        
        float r, g, b;
        switch (h)
        {
            case < 1f / 6f:
                r = c; g = x; b = 0;
                break;
            case < 2f / 6f:
                r = x; g = c; b = 0;
                break;
            case < 3f / 6f:
                r = 0; g = c; b = x;
                break;
            case < 4f / 6f:
                r = 0; g = x; b = c;
                break;
            case < 5f / 6f:
                r = x; g = 0; b = c;
                break;
            default:
                r = c; g = 0; b = x;
                break;
        }
        
        return new Vector4(r + m, g + m, b + m, 1);
    }
}
