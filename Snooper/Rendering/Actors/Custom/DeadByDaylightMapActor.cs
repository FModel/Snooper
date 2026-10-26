using CUE4Parse.GameTypes.DBD.Objects.ProceduralMaps.Layout;
using CUE4Parse.UE4.Assets.Exports.Paper2D;
using CUE4Parse_Conversion;
using CUE4Parse_Conversion.Exporters.Custom;
using Serilog;
using Snooper.Core;
using Snooper.Core.Managers;
using Snooper.Rendering.Components.Transforms;

namespace Snooper.Rendering.Actors.Custom;

public sealed partial class DeadByDaylightMapActor : Actor
{
    private readonly UPaperTileMap _map;
    private FProceduralLevelLayout _layout;
    private List<PlacedActor> _placements;
    private CancellationTokenSource? _regeneration;

    public override string Icon => Settings.EarthEuropeIcon;
    public int Seed => _layout.Seed;
    public bool IsRegenerating => _regeneration != null;
    public string? GenerationError { get; private set; }
    public bool HasMapLighting => _layout.LightingBlueprint != null;
    public float OverviewDistance => MathF.Max(_layout.Width * _layout.TileSize.X, _layout.Height * _layout.TileSize.Y) * Settings.GlobalScale;

    public DeadByDaylightMapActor(UPaperTileMap asset, IProgress<string>? progress = null, int seed = 1, CancellationToken ct = default) : base(asset)
    {
        _map = asset;
        _layout = new FProceduralLevelLayout(asset, seed, ct);
        Components.Add(new MapRoot(this));
        _placements = BuildPlacements(_layout, [], progress, ct);
        foreach (var placement in _placements)
        {
            Children.Add(placement.Actor);
        }
    }

    public override void Export(ExportSession session, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        session.Add(new DeadByDaylightMapExporter(_map, Seed));
    }

    public void Regenerate(int seed)
    {
        if (IsRegenerating || seed == Seed || ActorManager is not SceneManager scene)
            return;

        var cancellation = _regeneration = new CancellationTokenSource();
        var token = cancellation.Token;
        var previous = _layout;
        var available = _placements.Where(placement => placement.Actor.Parent == this)
            .Concat(_cachedPlacements)
            .GroupBy(placement => placement.Placement.Key)
            .ToDictionary(group => group.Key, group => new Queue<Actor>(group.Select(placement => placement.Actor)), StringComparer.Ordinal);

        GenerationError = null;
        ThreadManager.Enqueue(() =>
        {
            FProceduralLevelLayout? layout = null;
            List<PlacedActor>? placements = null;
            Exception? error = null;
            try
            {
                layout = previous.Regenerate(seed, token);
                placements = BuildPlacements(layout, available, null, token);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                error = exception;
                Log.Error(exception, "Could not regenerate {Map} with seed {Seed}", Name, seed);
            }

            if (token.IsCancellationRequested || ActorManager != scene)
                return;

            scene.Invoke(() =>
            {
                try
                {
                    if (token.IsCancellationRequested || ActorManager != scene)
                        return;

                    if (error != null || layout == null || placements == null)
                    {
                        GenerationError = "Could not regenerate the map. See the log for details.";
                        return;
                    }

                    ApplyPlacements(placements);
                    _layout = layout;
                }
                finally
                {
                    if (ReferenceEquals(_regeneration, cancellation))
                    {
                        _regeneration = null;
                        cancellation.Dispose();
                    }
                }
            });
        });
    }

    private void CancelRegeneration()
    {
        _regeneration?.Cancel();
        _regeneration?.Dispose();
        _regeneration = null;
        _cachedPlacements.Clear();
    }

    private sealed class MapRoot(DeadByDaylightMapActor owner) : SpatialComponent(name: "MapRoot")
    {
        protected override void EndPlay(EEndPlayReason reason)
        {
            owner.CancelRegeneration();
            base.EndPlay(reason);
        }
    }
}
