using System.Runtime.InteropServices;
using OpenTK.Graphics.OpenGL4;
using Serilog;

namespace Snooper;

#if DEBUG
public partial class SnooperWindow
{
    private static readonly DebugProc _debugMessageDelegate = OnDebugMessage;
    private static void OnDebugMessage(DebugSource source, DebugType type, int id, DebugSeverity severity, int length, IntPtr pMessage, IntPtr pUserParam)
    {
        if (severity == DebugSeverity.DebugSeverityNotification) return;

        var message = Marshal.PtrToStringAnsi(pMessage, length);
        Log.Debug("[{0} source={1} type={2} id={3}] {4}", severity.ToString()[13..], source.ToString()[11..], type.ToString()[9..], id, message);

        if (type == DebugType.DebugTypeError)
        {
            throw new Exception(message);
        }
    }
}
#endif