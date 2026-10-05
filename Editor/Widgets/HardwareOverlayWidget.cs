using System.Numerics;
using Editor.Managers;
using ImGuiNET;
using Snooper;
using Snooper.Core.Hardware;
using Snooper.Core.Managers;
using Snooper.Extensions;
using Snooper.Rendering.Cache;
using Snooper.UI;

namespace Editor.Widgets;

public class HardwareOverlayWidget : IViewportCard
{
    public string Title => "Hardware";
    public bool IsOpen { get => RendererInfo.TrackMemory; set => RendererInfo.TrackMemory = value; }

    private static float Unit => ImGui.GetFrameHeight();
    private static float Line => ImGui.GetTextLineHeightWithSpacing();
    private static float Gap => 0.3f * Unit;
    private static float BarHeight => 0.35f * Unit;
    private static Vector2 TextOffset => new(0f, (Line - ImGui.GetFontSize()) * 0.5f);

    private static readonly uint _labelColor = Color(0.6f, 0.6f, 0.65f);
    private static readonly uint _valueColor = Color(0.85f, 0.85f, 0.88f);
    private static readonly uint _warnColor = Color(0.95f, 0.75f, 0.25f);
    private static readonly uint _alertColor = Color(0.88f, 0.35f, 0.32f);

    // a fraction below zero draws no bar, the details are the tooltip
    private readonly struct Meter(string label, string value, float fraction, string details)
    {
        public readonly string Label = label;
        public readonly string Value = value;
        public readonly float Fraction = fraction;
        public readonly string Details = details;
    }

    private readonly struct Row(string label, string value, string right, bool warn = false)
    {
        public readonly string Label = label;
        public readonly string Value = value;
        public readonly string Right = right;
        public readonly bool Warn = warn;
    }

    /// <summary>
    /// How often the readouts are regenerated. Every row is a freshly formatted string, and none of these values say
    /// anything new between two frames — a memory figure refreshed at frame rate is just unreadable flicker.
    /// </summary>
    private const float RebuildInterval = 0.1f;

    private readonly List<Meter> _meters = [];
    private readonly Row[] _rows = new Row[2];
    private Row[]? _device;
    private Row[]? _limits;
    private Row[]? _features;
    private float _sinceRebuild = float.MaxValue;
    private string _search = string.Empty;

    public void Draw(EditorManager editor)
    {
        _sinceRebuild += ImGui.GetIO().DeltaTime;
        if (_sinceRebuild >= RebuildInterval)
        {
            _sinceRebuild = 0f;
            _meters.Clear();
            Build(editor);
        }

        var renderer = editor.Renderer;
        var device = renderer.DeviceInfo;
        var support = device.ExtensionSupport;

        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var y = origin.Y;

        foreach (var meter in _meters)
        {
            var height = DrawMeter(drawList, meter, new Vector2(origin.X, y), width);
            if (ImGui.IsWindowHovered() && ImGui.IsMouseHoveringRect(new Vector2(origin.X, y), new Vector2(origin.X + width, y + height)))
            {
                EditorUI.Tooltip(meter.Details);
            }

            y += height + Gap;
        }

        y = DrawRows(drawList, _rows, new Vector2(origin.X, y), width);

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, y - origin.Y));

        Node("Device", _device ??=
        [
            new Row("GPU", string.Empty, device.Name),
            new Row("Vendor", string.Empty, device.Vendor),
            new Row("OpenGL", string.Empty, renderer.Name),
            new Row("GLSL", string.Empty, device.ShadingLanguage),
        ], ImGuiTreeNodeFlags.DefaultOpen);

        Node("Limits", _limits ??=
        [
            new Row("Buffer Bindings", string.Empty, $"{DeviceInfo.MaxShaderStorageBufferBindings:N0}"),
            new Row("Texture Size", string.Empty, $"{device.MaxTextureSize:N0}"),
            new Row("Texture Layers", string.Empty, $"{device.MaxArrayTextureLayers:N0}"),
            new Row("Anisotropy", string.Empty, $"{DeviceInfo.MaxAnisotropy:0.#}x")
        ]);

        Node("Features", _features ??=
        [
            Feature("Bindless Textures", support.SupportBindlessTextures),
            Feature("Wireframe", DeviceInfo.HasFragmentBarycentric),
            new Row("Memory Counters", string.Empty, device.Memory.Source switch
            {
                GpuMemoryQuerySource.Nvidia => "NVX_gpu_memory_info",
                GpuMemoryQuerySource.Amd => "ATI_meminfo",
                _ => "none"
            })
        ]);

        DrawExtensions(support);

        void Node(string title, Row[] rows, ImGuiTreeNodeFlags flags = ImGuiTreeNodeFlags.None)
        {
            if (!ImGui.TreeNodeEx(title, ImGuiTreeNodeFlags.SpanAvailWidth | ImGuiTreeNodeFlags.NoTreePushOnOpen | flags)) return;

            var start = ImGui.GetCursorScreenPos();
            var end = DrawRows(drawList, rows, start, width);
            ImGui.Dummy(new Vector2(width, end - start.Y));
        }

        Row Feature(string label, bool supported)
        {
            return new Row(label, string.Empty, supported ? "yes" : "no", !supported);
        }
    }

    private void Build(EditorManager editor)
    {
        var gpu = editor.Renderer.DeviceInfo.Memory;
        var ram = editor.Renderer.SystemMemory;

        if (gpu.IsAvailable)
        {
            var details = $"Video Memory\nFree {gpu.AvailableBytes.GetReadableSize()}";
            if (gpu.DedicatedBytes > 0) details += $"\nBoard {gpu.DedicatedBytes.GetReadableSize()}";
            if (gpu.EvictionCount > 0) details += $"\nEvicted {gpu.EvictedBytes.GetReadableSize()} ({gpu.EvictionCount:N0}x)";

            var value = gpu.UsedBytes.GetReadableSizeOutOf(gpu.TotalBytes);
            _meters.Add(new Meter("VRAM", gpu.IsTotalEstimated ? $"~{value}" : value, (float) gpu.UsedBytes / gpu.TotalBytes, details));
        }
        else
        {
            _meters.Add(new Meter("VRAM", "unsupported", -1f, "Video Memory\nNeeds NVX_gpu_memory_info or ATI_meminfo"));
        }

        _meters.Add(new Meter(
            "RAM",
            ram.UsedBytes.GetReadableSizeOutOf(ram.TotalBytes),
            ram.TotalBytes > 0 ? (float) ram.UsedBytes / ram.TotalBytes : 0f,
            $"System Memory\nProcess {ram.ProcessBytes.GetReadableSize()}\nManaged heap {ram.ManagedBytes.GetReadableSize()}"));

        var allocated = editor.Allocated;
        var used = editor.Used;
        _meters.Add(new Meter(
            "Buffers",
            used.GetReadableSizeOutOf(allocated),
            allocated > 0 ? (float) used / allocated : 0f,
            $"GPU Buffers\nWasted {(allocated - used).GetReadableSize()}"));

        _meters.Add(new Meter(
            "Textures",
            TextureCache.ResidentBytes.GetReadableSizeOutOf(TextureCache.TextureBudgetBytes),
            (float) TextureCache.ResidentBytes / TextureCache.TextureBudgetBytes,
            $"Texture Cache\n{TextureCache.LoadedTextureCount:N0} loaded\n{TextureCache.PendingTextureCount:N0} pending\n{TextureCache.EvictableTextureCount:N0} evictable"));

        _rows[0] = new Row(
            "GC",
            $"{GC.CollectionCount(0)} / {GC.CollectionCount(1)} / {GC.CollectionCount(2)}",
            $"paused {GC.GetTotalPauseDuration().TotalSeconds:F2} s");

        var queued = ThreadManager.CurrentQueuedJobs;
        _rows[1] = new Row(
            "Jobs",
            $"{ThreadManager.BusyWorkers} / {ThreadManager.WorkerCount} busy",
            $"{queued:N0} queued",
            queued > 0);
    }

    private float DrawMeter(ImDrawListPtr drawList, Meter meter, Vector2 pos, float width)
    {
        var right = pos.X + width;
        var hasBar = meter.Fraction >= 0f;

        drawList.AddText(pos + TextOffset, _labelColor, meter.Label);
        drawList.AddText(new Vector2(right - ImGui.CalcTextSize(meter.Value).X, pos.Y) + TextOffset, hasBar ? _valueColor : _warnColor, meter.Value);
        if (!hasBar) return Line;

        var fraction = Math.Clamp(meter.Fraction, 0f, 1f);
        var top = pos.Y + Line;
        var color = fraction switch
        {
            > 0.9f => _alertColor,
            > 0.75f => _warnColor,
            _ => Color(0.36f, 0.76f, 0.52f)
        };

        drawList.AddRectFilled(new Vector2(pos.X, top), new Vector2(right, top + BarHeight), Color(1f, 1f, 1f, 0.08f));
        if (fraction > 0f)
        {
            drawList.AddRectFilled(new Vector2(pos.X, top), new Vector2(pos.X + width * fraction, top + BarHeight), color);
        }

        return Line + BarHeight;
    }

    private float DrawRows(ImDrawListPtr drawList, Row[] rows, Vector2 pos, float width)
    {
        var y = pos.Y;
        foreach (var row in rows)
        {
            drawList.AddText(new Vector2(pos.X, y) + TextOffset, _labelColor, row.Label);
            if (row.Value.Length > 0)
            {
                drawList.AddText(new Vector2(pos.X + ImGui.CalcTextSize(row.Label).X + Gap * 2f, y) + TextOffset, _valueColor, row.Value);
            }

            drawList.AddText(new Vector2(pos.X + width - ImGui.CalcTextSize(row.Right).X, y) + TextOffset, row.Warn ? _warnColor : _valueColor, row.Right);

            y += Line;
        }

        return y;
    }

    private void DrawExtensions(ExtensionSupport support)
    {
        if (!ImGui.TreeNodeEx($"Extensions ({support.Extensions.Length})###Extensions", ImGuiTreeNodeFlags.SpanAvailWidth | ImGuiTreeNodeFlags.NoTreePushOnOpen)) return;

        ImGui.SetNextItemWidth(-1f);
        ImGui.InputTextWithHint("##ExtensionFilter", $"{Settings.MagnifyingGlassIcon}  Filter", ref _search, 128, ImGuiInputTextFlags.AutoSelectAll);

        if (ImGui.BeginChild("##ExtensionList", new Vector2(0f, Line * 12f)))
        {
            foreach (var extension in support.Extensions)
            {
                if (_search.Length > 0 && !extension.Contains(_search, StringComparison.OrdinalIgnoreCase)) continue;
                if (ImGui.Selectable(extension)) ImGui.SetClipboardText(extension);
            }
        }
        ImGui.EndChild();
    }

    private static uint Color(float r, float g, float b, float a = 1f) => (uint) (a * 255f) << 24 | (uint) (b * 255f) << 16 | (uint) (g * 255f) << 8 | (uint) (r * 255f);
}
