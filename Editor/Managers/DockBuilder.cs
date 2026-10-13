using System.Numerics;
using System.Runtime.InteropServices;
using ImGuiNET;

namespace Editor.Managers;

internal static class DockBuilder
{
    private const string Library = "cimgui";
    private const int DockSpaceFlag = 1 << 10; // ImGuiDockNodeFlags_DockSpace

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern uint igDockBuilderAddNode(uint nodeId, int flags);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern void igDockBuilderRemoveNode(uint nodeId);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr igDockBuilderGetNode(uint nodeId);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern void igDockBuilderSetNodeSize(uint nodeId, Vector2 size);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern uint igDockBuilderSplitNode(uint nodeId, ImGuiDir direction, float ratio, out uint atDirection, out uint opposite);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern void igDockBuilderDockWindow([MarshalAs(UnmanagedType.LPUTF8Str)] string windowName, uint nodeId);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern void igDockBuilderFinish(uint nodeId);

    public static bool HasNode(uint nodeId) => igDockBuilderGetNode(nodeId) != IntPtr.Zero;

    public static void Reset(uint dockSpaceId, Vector2 size)
    {
        igDockBuilderRemoveNode(dockSpaceId);
        igDockBuilderAddNode(dockSpaceId, DockSpaceFlag);
        igDockBuilderSetNodeSize(dockSpaceId, size);
    }

    public static uint Split(uint nodeId, ImGuiDir direction, float ratio, out uint remaining)
    {
        igDockBuilderSplitNode(nodeId, direction, ratio, out var atDirection, out remaining);
        return atDirection;
    }

    public static void Dock(string windowName, uint nodeId) => igDockBuilderDockWindow(windowName, nodeId);

    public static void Finish(uint dockSpaceId) => igDockBuilderFinish(dockSpaceId);
}
