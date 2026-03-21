using CUE4Parse.UE4.Assets.Exports.Engine;
using CUE4Parse.UE4.Objects.Core.Misc;
using CUE4Parse.UE4.Objects.Engine;
using CUE4Parse.UE4.Objects.UObject;
using Snooper.Rendering.Components.Transforms;

namespace Snooper.Rendering.Actors;

public class BlueprintActor : UnrealActor
{
    private readonly Dictionary<FGuid, (USCS_Node, FPackageIndex?)> _nodes = [];

    public BlueprintActor(UBlueprintGeneratedClass blueprint) : base(blueprint)
    {
        var supers = new List<UBlueprintGeneratedClass>();
        var current = blueprint;
        while (current != null)
        {
            supers.Add(current);
            current = current.Super?.Load<UBlueprintGeneratedClass>();
        }
        supers.Reverse();

        foreach (var super in supers)
        {
            if (super.SimpleConstructionScript?.TryLoad<USimpleConstructionScript>(out var construction) == true)
            {
                EnqueuePointers(construction.GetOrDefault<FPackageIndex?>("DefaultSceneRootNode"));
                EnqueuePointers(construction.GetOrDefault<FPackageIndex?[]>("RootNodes"));
                EnqueuePointers(construction.GetOrDefault<FPackageIndex?[]>("AllNodes"));
            }

            foreach (var ptr in _ptrs)
            {
                if (!ptr.TryLoad<USCS_Node>(out var node)) continue;

                var guid = node.GetOrDefault<FGuid>("VariableGuid");
                _nodes[guid] = (node, null);
            }

            if (super.InheritableComponentHandler?.TryLoad<UInheritableComponentHandler>(out var handler) == true)
            {
                foreach (var record in handler.GetRecords())
                {
                    var guid = record.ComponentKey.AssociatedGuid;
                    if (record.ComponentTemplate is { IsNull: false })
                    {
                        var node = _nodes[guid];
                        node.Item2 = record.ComponentTemplate;
                        _nodes[guid] = node;
                    }
                }
            }

            _ptrs.Clear();
        }

        foreach (var node in _nodes.Values)
        {
            var pair = CreateComponentPair(node.Item2 ?? node.Item1.GetOrDefault<FPackageIndex>("ComponentTemplate"));

            if (node.Item1.GetOrDefault<FName?>("ParentComponentOrVariableName") is { } parentComponentOrVariableName &&
                Components.FirstOrDefault(c => c.Name == parentComponentOrVariableName.Text) is SpatialComponent parentComponent)
            {
                pair.Component.AttachSocketName = node.Item1.GetOrDefault<FName?>("AttachToName")?.Text;
                pair.Component.Relation = parentComponent;
            }
            if (node.Item1.GetOrDefault<FName?>("InternalVariableName") is { } internalVariableName)
            {
                pair.Component.Name = internalVariableName.Text;
            }

            Components.Add(pair.Component);
        }
    }

    private readonly HashSet<FPackageIndex> _ptrs = [];
    private void EnqueuePointers(params FPackageIndex?[]? ptrs)
    {
        foreach (var ptr in ptrs ?? [])
        {
            if (ptr is { IsNull: false })
            {
                _ptrs.Add(ptr);
            }
        }
    }
}
