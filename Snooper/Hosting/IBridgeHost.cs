using CUE4Parse_Conversion;
using CUE4Parse_Conversion.Options;
using Serilog;

namespace Snooper.Hosting;

public interface IBridgeHost
{
    public string Name { get; }
    public string ImGuiIniPath { get; }

    public bool CanBrowseAssets => false;

    public ExportSession Session { get; }
    public void ShowExportSession();
}

internal sealed class StandaloneHost : IBridgeHost
{
    public string Name => "Snooper";
    public string ImGuiIniPath => "./snooper.ini";

    public ExportSession Session { get; } = new();
    public void ShowExportSession() => throw new NotImplementedException();
}
