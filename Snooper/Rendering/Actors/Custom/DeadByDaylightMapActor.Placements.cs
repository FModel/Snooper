using CUE4Parse.GameTypes.DBD.Assets.Exports.Components;
using CUE4Parse.GameTypes.DBD.Objects.ProceduralMaps.Blueprints;
using CUE4Parse.GameTypes.DBD.Objects.ProceduralMaps.Layout;
using CUE4Parse.UE4.Objects.Core.Math;
using CUE4Parse.UE4.Objects.Engine;
using Snooper.Rendering.Components.Mesh;
using Snooper.Rendering.Components.Transforms;

namespace Snooper.Rendering.Actors.Custom;

// It's done in this way so we can generate new seed without regenerating whole map, makes it really fast
public sealed partial class DeadByDaylightMapActor
{
    private const int MaxCachedPlacements = 32;
    private const int MaxCachedComponents = 2000;
    private readonly List<PlacedActor> _cachedPlacements = [];

    private sealed record Placement(string Name, UBlueprintGeneratedClass Blueprint, FTransform Transform, IReadOnlyDictionary<string, bool>? SpawnerActivation = null, IReadOnlyDictionary<string, UBlueprintGeneratedClass>? ComponentVariations = null)
    {
        public string Key { get; } = Blueprint.GetPathName() + '\n' + (SpawnerActivation == null ? string.Empty :
            string.Join('\n', SpawnerActivation.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => $"{pair.Key}={pair.Value}"))) + '\n' +
            (ComponentVariations == null ? string.Empty : string.Join('\n', ComponentVariations.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => $"{pair.Key}={pair.Value.GetPathName()}")));
    }

    private sealed record PlacedActor(Placement Placement, Actor Actor);

    private static IEnumerable<Placement> ReadPlacements(FProceduralLevelLayout map)
    {
        foreach (var tile in map.Placements)
            yield return new Placement($"Tile_{tile.X}_{tile.Y}_{tile.Tile.Blueprint.Name}", tile.Tile.Blueprint, tile.Transform(map.TileSize, map.Width, map.Height), tile.SpawnerActivation, tile.ComponentVariations);

        foreach (var placement in map.Quadrants.Concat(map.BasementActors).Concat(map.GameplayActors))
            yield return new Placement(placement.Name, placement.Blueprint, placement.Transform, placement.SpawnerActivation);

        if (map.LightingBlueprint is { } lighting)
            yield return new Placement("Environment", lighting, map.EnvironmentTransform);
    }

    private static List<PlacedActor> BuildPlacements(FProceduralLevelLayout map, Dictionary<string, Queue<Actor>> available, IProgress<string>? progress, CancellationToken ct)
    {
        var result = new List<PlacedActor>();
        foreach (var placement in ReadPlacements(map))
        {
            ct.ThrowIfCancellationRequested();
            progress?.Report(placement.Name);
            if (available.TryGetValue(placement.Key, out var pool) && pool.TryDequeue(out var reused))
            {
                result.Add(new PlacedActor(placement, reused));
                continue;
            }

            var actor = new Actor(placement.Name);
            actor.Components.Add(new SpatialComponent(placement.Transform, "Placement"));
            var blueprintActor = new BlueprintActor(placement.Blueprint);
            blueprintActor.RootComponent?.SetLocalTransform(FTransform.Identity);

            var pose = FProceduralComponentReader.ReadPreviewTransforms(placement.Blueprint);
            foreach (var component in blueprintActor.Components.OfType<SpatialComponent>())
            {
                if (pose.TryGetValue(component.Name, out var transform))
                {
                    component.SetLocalTransform(transform);
                }
            }

            ApplyComponentVariations(blueprintActor, placement);
            ApplySpawnerActivation(blueprintActor, placement);
            HideEventDecorations(blueprintActor);
            actor.Children.Add(blueprintActor);
            result.Add(new PlacedActor(placement, actor));
        }

        return result;
    }

    private void ApplyPlacements(List<PlacedActor> placements)
    {
        var retained = placements.Select(placement => placement.Actor).ToHashSet();
        var unused = _placements.Concat(_cachedPlacements).Where(placement => !retained.Contains(placement.Actor)).ToArray();
        foreach (var child in Children.ToArray())
        {
            if (!retained.Contains(child))
            {
                Children.Remove(child);
            }
        }

        _cachedPlacements.Clear();
        var cachedComponents = 0;
        foreach (var placement in unused)
        {
            if (placement.Actor.Parent != null || _cachedPlacements.Count >= MaxCachedPlacements)
                continue;

            var count = CountComponents(placement.Actor);
            if (cachedComponents + count > MaxCachedComponents)
                continue;

            _cachedPlacements.Add(placement);
            cachedComponents += count;
        }

        foreach (var (placement, actor) in placements)
        {
            actor.Name = placement.Name;
            actor.RootComponent?.SetLocalTransform(placement.Transform);
            if (actor.Parent == this)
                continue;

            actor.Parent?.Children.Remove(actor);
            Children.Add(actor);
        }
        _placements = placements;
    }

    private static int CountComponents(Actor actor) => actor.Components.Count + actor.Children.Sum(CountComponents);

    private static void HideEventDecorations(Actor actor)
    {
        if (FActorSpawnerVisualization.IsEventAsset(actor.Path))
        {
            actor.IsVisible = false;
            return;
        }

        foreach (var mesh in actor.Components.OfType<MeshComponent>())
            if (FActorSpawnerVisualization.IsEventAsset(mesh.Descriptor.Path))
                mesh.SetVisibility(false, true);

        foreach (var child in actor.Children)
            HideEventDecorations(child);
    }

    private static void ApplyComponentVariations(BlueprintActor actor, Placement placement)
    {
        if (placement.ComponentVariations is not { } variations)
            return;

        foreach (var component in actor.Components.OfType<SpatialComponent>())
        {
            if (!variations.TryGetValue(component.Name, out var blueprint))
                continue;

            foreach (var child in actor.Children.Where(child => child.RootComponent?.Relation == component).ToArray())
                actor.Children.Remove(child);

            actor.TryCreateChildActor(blueprint, component);
        }
    }

    private static void ApplySpawnerActivation(BlueprintActor actor, Placement placement)
    {
        if (placement.SpawnerActivation is not { } activation)
            return;

        IReadOnlyList<(string Name, UActorSpawner Component, FTransform Transform)>? spawners = null;
        foreach (var component in actor.Components.OfType<SpatialComponent>())
        {
            if (!activation.TryGetValue(component.Name, out var active))
                continue;

            var children = actor.Children.Where(child => child.RootComponent?.Relation == component).ToArray();
            if (!active)
            {
                foreach (var child in children)
                {
                    actor.Children.Remove(child);
                }
            }
            else if (children.Length == 0)
            {
                spawners ??= FProceduralComponentReader.ReadNamedComponents<UActorSpawner>(placement.Blueprint);
                var spawner = spawners.FirstOrDefault(entry => entry.Name == component.Name).Component;
                if (spawner != null && FActorSpawnerVisualization.GetBlueprint(spawner, true) is { } blueprint)
                    actor.TryCreateChildActor(blueprint, component);
            }
        }
    }
}
