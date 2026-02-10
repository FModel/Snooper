using ImGuiNET;
using Snooper.Core.Containers.Buffers;
using Snooper.UI;

namespace Snooper.Core.Containers.Resources;

public readonly struct ResourcesMetadata(GeometryHandle geometryHandle, BufferAllocation instanceAllocation, BufferAllocation materialAllocation, BufferAllocation[] drawAllocations, CommandBufferType bufferType) : IControllable
{
    public readonly GeometryHandle GeometryHandle = geometryHandle;
    public readonly BufferAllocation InstanceAllocation = instanceAllocation;
    public readonly BufferAllocation MaterialAllocation = materialAllocation;
    public readonly BufferAllocation[] DrawAllocations = drawAllocations; // we create one draw per section in lod 0
    public readonly CommandBufferType BufferType = bufferType; // TODO: support per draw buffer types

    public void DrawControls()
    {
        if (ImGui.BeginTable("Allocations", 5, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.Resizable | ImGuiTableFlags.NoSavedSettings))
        {
            ImGui.TableSetupColumn("Allocation", ImGuiTableColumnFlags.WidthStretch, 0.3f);
            ImGui.TableSetupColumn("ID", ImGuiTableColumnFlags.WidthStretch, 0.2f);
            ImGui.TableSetupColumn("Start", ImGuiTableColumnFlags.WidthStretch, 0.15f);
            ImGui.TableSetupColumn("Length", ImGuiTableColumnFlags.WidthStretch, 0.15f);
            ImGui.TableSetupColumn("End", ImGuiTableColumnFlags.WidthStretch, 0.2f);
            ImGui.TableHeadersRow();

            ImGui.TableNextRow();
            ImGui.TableNextColumn(); ImGui.Text("Geometry Culling");
            ImGui.TableNextColumn(); ImGui.Text(GeometryHandle.CullingAllocation.AllocationId.ToString());
            ImGui.TableNextColumn(); ImGui.Text(GeometryHandle.CullingAllocation.StartIndex.ToString());
            ImGui.TableNextColumn(); ImGui.Text(GeometryHandle.CullingAllocation.Length.ToString());
            ImGui.TableNextColumn(); ImGui.Text(GeometryHandle.CullingAllocation.EndIndex.ToString());

            ImGui.TableNextRow();
            ImGui.TableNextColumn(); ImGui.Text("Instance");
            ImGui.TableNextColumn(); ImGui.Text(InstanceAllocation.AllocationId.ToString());
            ImGui.TableNextColumn(); ImGui.Text(InstanceAllocation.StartIndex.ToString());
            ImGui.TableNextColumn(); ImGui.Text(InstanceAllocation.Length.ToString());
            ImGui.TableNextColumn(); ImGui.Text(InstanceAllocation.EndIndex.ToString());

            ImGui.TableNextRow();
            ImGui.TableNextColumn(); ImGui.Text("Material");
            ImGui.TableNextColumn(); ImGui.Text(MaterialAllocation.AllocationId.ToString());
            ImGui.TableNextColumn(); ImGui.Text(MaterialAllocation.StartIndex.ToString());
            ImGui.TableNextColumn(); ImGui.Text(MaterialAllocation.Length.ToString());
            ImGui.TableNextColumn(); ImGui.Text(MaterialAllocation.EndIndex.ToString());

            for (var i = 0; i < DrawAllocations.Length; i++)
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn(); ImGui.Text($"Draw {i}");
                var draw = DrawAllocations[i];
                ImGui.TableNextColumn(); ImGui.Text(draw.AllocationId.ToString());
                ImGui.TableNextColumn(); ImGui.Text(draw.StartIndex.ToString());
                ImGui.TableNextColumn(); ImGui.Text(draw.Length.ToString());
                ImGui.TableNextColumn(); ImGui.Text(draw.EndIndex.ToString());
            }

            ImGui.EndTable();
        }
    }
}
