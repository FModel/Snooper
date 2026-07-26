using System.Runtime.InteropServices;

namespace Snooper.Core.Hardware;

public partial class SystemMemoryInfo
{
    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhys;
        public ulong AvailPhys;
        public ulong TotalPageFile;
        public ulong AvailPageFile;
        public ulong TotalVirtual;
        public ulong AvailVirtual;
        public ulong AvailExtendedVirtual;
    }

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);

    /// <summary>
    /// Total physical memory installed on the machine, in bytes.
    /// </summary>
    public long TotalBytes { get; private set; }

    /// <summary>
    /// Physical memory the OS can still hand out, in bytes.
    /// </summary>
    public long AvailableBytes { get; private set; }

    public long UsedBytes => Math.Max(0, TotalBytes - AvailableBytes);

    /// <summary>
    /// Physical memory currently owned by this process, in bytes.
    /// </summary>
    public long ProcessBytes { get; private set; }

    /// <summary>
    /// Portion of <see cref="ProcessBytes"/> held by the managed heap, in bytes.
    /// </summary>
    public long ManagedBytes { get; private set; }

    public bool IsAvailable => TotalBytes > 0;

    public void Update()
    {
        ProcessBytes = Environment.WorkingSet;
        ManagedBytes = GC.GetTotalMemory(false);

        var status = new MemoryStatusEx { Length = (uint) Marshal.SizeOf<MemoryStatusEx>() };
        if (GlobalMemoryStatusEx(ref status))
        {
            TotalBytes = (long) status.TotalPhys;
            AvailableBytes = (long) status.AvailPhys;
            return;
        }

        // no OS counters, fall back to what the runtime knows
        var info = GC.GetGCMemoryInfo();
        TotalBytes = info.TotalAvailableMemoryBytes;
        AvailableBytes = Math.Max(0, info.TotalAvailableMemoryBytes - info.MemoryLoadBytes);
    }
}
