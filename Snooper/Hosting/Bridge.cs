using CUE4Parse.UE4.Assets.Exports;
using Snooper.Core;
using Snooper.Rendering.Components.Descriptors;
using Snooper.Rendering.Components.Mesh;

namespace Snooper.Hosting;

public static class Bridge
{
    public static IBridgeHost Host { get; set; } = new StandaloneHost();
    public static SnooperOptions Options { get; } = new();

    public static AssetRequest? PendingRequest
    {
        get;
        private set
        {
            if (field == value) return;
            field = value;
            PendingRequestChanged(value);
        }
    }

    public static event Action<AssetRequest?> PendingRequestChanged = request =>
    {
        if (request is not null)
            Notifications.Push("bridge.request", Settings.FolderOpenIcon, $"{request.DisplayText}, pick one in {Host.Name}");
    };

    public static void RequestAnimation(SkeletalMeshComponent target) => Request(AssetRequest.Animation(target));
    // public static void RequestMaterial(MeshComponent target, MaterialSection section) => Request(AssetRequest.Material(target, section));

    private static void Request(AssetRequest request)
    {
        if (!Host.CanBrowseAssets)
        {
            Notifications.Push("bridge.request", Settings.CircleInfoIcon, $"{Host.Name} cannot browse assets");
            return;
        }

        PendingRequest = request;
    }

    private static volatile Delivery? _delivery;
    public static bool TryDeliver(UObject asset)
    {
        if (PendingRequest is not { } request || !request.Accepts(asset)) return false;

        _delivery = new Delivery(request, asset);
        PendingRequest = null;
        return true;
    }

    public static void CancelRequest() => PendingRequest = null;

    internal static void Drain()
    {
        if (_delivery is not { } delivery) return;

        _delivery = null;
        delivery.Request.Apply(delivery.Asset);
    }

    internal static void Reset()
    {
        CancelRequest();
        _delivery = null;
    }

    private sealed record Delivery(AssetRequest Request, UObject Asset);
}
