using CUE4Parse.UE4.Assets;
using CUE4Parse.UE4.Assets.Exports.Engine;
using CUE4Parse.UE4.Objects.Core.Misc;
using CUE4Parse.UE4.Objects.Engine;
using CUE4Parse.UE4.Objects.UObject;
using Serilog;
using Snooper.Rendering.Components.Transforms;

namespace Snooper.Rendering.Actors;

public class BlueprintActor : UnrealActor
{
    public BlueprintActor(UBlueprintGeneratedClass blueprint) : base(blueprint)
    {
        var bps = new List<UBlueprintGeneratedClass>();
        var current = blueprint;
        while (current != null)
        {
            bps.Add(current);
            if (current.Owner is { } pkg)
                _blueprintPackages.Add(pkg);

            current = current.Super?.Load<UBlueprintGeneratedClass>();
        }
        bps.Reverse();

        foreach (var bp in bps)
        {
            var handler = bp.InheritableComponentHandler?.Load<UInheritableComponentHandler>();
            if (handler == null) continue;

            foreach (var record in handler.Records)
            {
                if (record.ComponentTemplate == null || record.ComponentTemplate.IsNull) continue;
                _overrides[record.ComponentKey.AssociatedGuid] = record.ComponentTemplate;
            }
        }

        var candidates = new HashSet<FPackageIndex?>();
        foreach (var bp in bps)
        {
            var script = bp.SimpleConstructionScript?.Load<USimpleConstructionScript>();
            if (script == null) continue;

            candidates.Add(script.DefaultSceneRootNode);
            candidates.UnionWith(script.RootNodes);
        }

        foreach (var candidate in candidates)
        {
            if (candidate?.TryLoad<USCS_Node>(out var node) == true)
            {
                ProcessNode(node);
            }
        }

        _overrides.Clear();
        _blueprintPackages.Clear();
    }

    private readonly Dictionary<FGuid, FPackageIndex> _overrides = [];
    private readonly List<IPackage> _blueprintPackages = [];

    private void ProcessNode(USCS_Node node, SpatialComponent? parent = null)
    {
        _overrides.TryGetValue(node.VariableGuid, out var templateOverride);

        var template = templateOverride ?? node.ComponentTemplate;
        if (template == null || template.IsNull)
        {
            Log.Warning("Node {NodeName} has no component template, skipping.", node.InternalVariableName.Text);
            return;
        }

        var component = CreateComponentPair(template).Component;
        component.AttachSocketName = node.GetOrDefault<FName?>("AttachToName")?.Text;
        if (node.GetOrDefault<FName?>("InternalVariableName") is { } internalVariableName)
        {
            component.Name = internalVariableName.Text;
        }
        if (node.GetOrDefault<FName?>("ParentComponentOrVariableName") is { } parentComponentOrVariableName)
        {
            parent = node.GetOrDefault("bIsParentComponentNative", false) ? LoadNativeComponent(parentComponentOrVariableName.Text) : Components.OfType<SpatialComponent>().FirstOrDefault(c => c.Name == parentComponentOrVariableName.Text);
        }
        component.Relation = parent;
        Components.Add(component);

        foreach (var child in node.GetChildNodes())
        {
            ProcessNode(child, component);
        }
    }

    private SpatialComponent? LoadNativeComponent(string name)
    {
        foreach (var package in _blueprintPackages)
        {
            var exportIdx = package.GetExportIndex(name);
            if (exportIdx < 0) continue;

            var component = CreateComponentPair(new FPackageIndex(package, exportIdx + 1)).Component;
            Components.Add(component);
            return component;
        }
        return null;
    }

    internal override string Icon => "\uf46d";
}
