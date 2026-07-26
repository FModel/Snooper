using OpenTK.Graphics.OpenGL4;

namespace Snooper.Core.Hardware;

public enum GpuMemoryQuerySource
{
    /// <summary>
    /// No vendor extension exposes memory counters on this device.
    /// </summary>
    None,

    /// <summary>
    /// GL_NVX_gpu_memory_info
    /// </summary>
    Nvidia,

    /// <summary>
    /// GL_ATI_meminfo
    /// </summary>
    Amd
}

public class GpuMemoryInfo
{
    private const GetPName DedicatedVideoMemoryNvx = (GetPName) 0x9047;
    private const GetPName TotalAvailableMemoryNvx = (GetPName) 0x9048;
    private const GetPName CurrentAvailableVideoMemoryNvx = (GetPName) 0x9049;
    private const GetPName EvictionCountNvx = (GetPName) 0x904A;
    private const GetPName EvictedMemoryNvx = (GetPName) 0x904B;

    private const GetPName VboFreeMemoryAti = (GetPName) 0x87FB;
    private const GetPName TextureFreeMemoryAti = (GetPName) 0x87FC;

    private readonly int[] _atiQuery = new int[4];

    public GpuMemoryQuerySource Source { get; private set; }

    /// <summary>
    /// Total memory the driver is willing to hand out, in bytes.
    /// </summary>
    public long TotalBytes { get; private set; }

    /// <summary>
    /// Currently unused memory, in bytes.
    /// </summary>
    public long AvailableBytes { get; private set; }

    public long UsedBytes => Math.Max(0, TotalBytes - AvailableBytes);

    /// <summary>
    /// Physical memory soldered onto the board, in bytes. NVIDIA only.
    /// </summary>
    public long DedicatedBytes { get; private set; }

    /// <summary>
    /// Memory paged out of the board to system RAM, in bytes, since the driver started, system wide. NVIDIA only.
    /// </summary>
    public long EvictedBytes { get; private set; }

    /// <summary>
    /// Evictions performed since the driver started, system wide. NVIDIA only.
    /// </summary>
    public int EvictionCount { get; private set; }

    /// <summary>
    /// AMD exposes free memory but never a total, so <see cref="TotalBytes"/> is inferred
    /// from the highest free value ever observed and is only an approximation.
    /// </summary>
    public bool IsTotalEstimated => Source == GpuMemoryQuerySource.Amd;

    public bool IsAvailable => Source != GpuMemoryQuerySource.None && TotalBytes > 0;

    public void Load(ExtensionSupport support)
    {
        Source = support.SupportNvidiaMemoryInfo ? GpuMemoryQuerySource.Nvidia :
            support.SupportAtiMemoryInfo ? GpuMemoryQuerySource.Amd :
            GpuMemoryQuerySource.None;

        Update();
    }

    public void Update()
    {
        switch (Source)
        {
            case GpuMemoryQuerySource.Nvidia:
            {
                // every NVX counter is reported in KiB
                TotalBytes = GL.GetInteger(TotalAvailableMemoryNvx) * 1024L;
                AvailableBytes = GL.GetInteger(CurrentAvailableVideoMemoryNvx) * 1024L;
                DedicatedBytes = GL.GetInteger(DedicatedVideoMemoryNvx) * 1024L;
                EvictedBytes = GL.GetInteger(EvictedMemoryNvx) * 1024L;
                EvictionCount = GL.GetInteger(EvictionCountNvx);
                break;
            }
            case GpuMemoryQuerySource.Amd:
            {
                // 0: total free, 1: largest free block, 2: total auxiliary free, 3: largest auxiliary free block, all in KiB
                GL.GetInteger(VboFreeMemoryAti, _atiQuery);
                var free = _atiQuery[0] * 1024L;

                GL.GetInteger(TextureFreeMemoryAti, _atiQuery);
                free = Math.Max(free, _atiQuery[0] * 1024L);

                AvailableBytes = free;
                TotalBytes = Math.Max(TotalBytes, free);
                break;
            }
        }
    }
}
