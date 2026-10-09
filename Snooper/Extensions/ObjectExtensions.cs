using System.Runtime.CompilerServices;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Component.StaticMesh;
using CUE4Parse.UE4.Objects.Core.Misc;

namespace Snooper.Extensions;

public static class ObjectExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string? GetCleanPath(this UObject owner) => owner.Owner?.Provider?.FixPath(owner.Owner?.Name ?? owner.GetPathName());

    public static FGuid ToGuid(this UObject owner) => new((uint) (owner.GetCleanPath() ?? owner.Name).GetHashCode());

    public static FInstancedStaticMeshInstanceData[] GetSortedInstances(this UHierarchicalInstancedStaticMeshComponent component, out FClusterNode_DEPRECATED[]? tree)
    {
        var instances = component.GetInstances();
        var built = component.NumBuiltInstances;
        tree = component is { ClusterTree: { Length: > 0 } nodes } &&
               component.SortedInstances.Length == built &&
               built <= instances.Length && nodes[0].LastInstance + 1 == built ? nodes : null;
        if (tree is null) return instances;

        var sorted = (FInstancedStaticMeshInstanceData[]) instances.Clone(); // the instances after the built ones stay where they are
        for (var i = 0; i < built; i++)
        {
            sorted[i] = instances[component.SortedInstances[i]];
        }
        return sorted;
    }
}
