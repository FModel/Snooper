using System.Numerics;
using ImGuiNET;

namespace Snooper.UI;

public static class EditorUI
{
    public static bool DragFloat3(string label, ref Vector3 value, float speed = 0.01f, float min = float.MinValue, float max = float.MaxValue)
    {
        Property(label);
        return ImGui.DragFloat3("##" + label, ref value, speed, min, max);
    }
    
    public static bool DragFloat4(string label, ref Quaternion value, float speed = 0.01f, float min = float.MinValue, float max = float.MaxValue)
    {
        var vec = new Vector4(value.X, value.Y, value.Z, value.W);
        var changed = DragFloat4(label, ref vec, speed, min, max);
        if (changed)
            value = new Quaternion(vec.X, vec.Y, vec.Z, vec.W);
        return changed;
    }
    
    public static bool DragFloat4(string label, ref Vector4 value, float speed = 0.01f, float min = float.MinValue, float max = float.MaxValue)
    {
        Property(label);
        return ImGui.DragFloat4("##" + label, ref value, speed, min, max);
    }
    
    public static bool DragFloat(string label, ref float value, float speed = 0.01f, float min = float.MinValue, float max = float.MaxValue)
    {
        Property(label);
        return ImGui.DragFloat("##" + label, ref value, speed, min, max);
    }

    public static bool Checkbox(string label, ref bool value)
    {
        Property(label);
        return ImGui.Checkbox("##" + label, ref value);
    }

    public static void Text(string label, string value)
    {
        Property(label);
        ImGui.TextUnformatted(value);
    }
    
    public static void Property(string label)
    {
        ImGui.TableNextRow();
        ImGui.TableSetColumnIndex(0);
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(label);
        ImGui.TableSetColumnIndex(1);
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
    }
    
    public static void CollapsingTable(string label, ImGuiTreeNodeFlags flags, Action draws)
    {
        if (ImGui.CollapsingHeader(label, flags))
        {
            PropertyValueTable(label, draws);
        }
    }
    
    public static void PropertyValueTable(string label, Action draws)
    {
        ImGui.Indent();
        if (ImGui.BeginTable(label + "ControlsTable", 2))
        {
            ImGui.TableSetupColumn("Property", ImGuiTableColumnFlags.WidthStretch, 1.0f);
            ImGui.TableSetupColumn("Value", ImGuiTableColumnFlags.WidthStretch, 2.0f);
            
            draws.Invoke();

            ImGui.EndTable();
        }
        ImGui.Unindent();
    }
    
    /// <summary>
    /// Creates a tree node with shared state.
    /// Temporarily pops the current ID scope so the tree node state is global,
    /// then pushes it back for the content inside.
    /// </summary>
    public static bool SharedTreeNode(string label, ImGuiTreeNodeFlags flags, uint id, Action content)
    {
        ImGui.PopID();
        var isOpen = ImGui.TreeNodeEx(label, flags);
        
        if (isOpen)
        {
            ImGui.PushID((int)id);
            content.Invoke();
            ImGui.PopID();
            ImGui.TreePop();
        }
        
        ImGui.PushID((int)id);
        return isOpen;
    }
}