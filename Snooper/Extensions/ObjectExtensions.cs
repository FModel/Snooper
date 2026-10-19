using System.Runtime.CompilerServices;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Animation;
using CUE4Parse.UE4.Assets.Exports.Component.StaticMesh;
using CUE4Parse.UE4.Assets.Exports.StaticMesh;
using CUE4Parse.UE4.Objects.Core.Misc;
using CUE4Parse.UE4.Objects.UObject;

namespace Snooper.Extensions;

public static class ObjectExtensions
{
    extension(UObject owner)
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string? GetCleanPath() => owner.Owner?.Provider?.FixPath(owner.Owner?.Name ?? owner.GetPathName());

        public FGuid ToGuid(uint index = 0) => new FGuid((uint) (owner.GetCleanPath() ?? owner.Name).GetHashCode() ^ index).WithArchive(owner);
    }

    // kills the owner reference, so it's not kept alive by whoever is holding the FPackageIndex
    public static FSoftObjectPath ToSoftObjectPath(this FPackageIndex? index)
    {
        var path = index?.ResolvedObject?.GetPathName() ?? "None";
        var subObject = path.IndexOf(':');
        return subObject < 0 ? new FSoftObjectPath(path, string.Empty) : new FSoftObjectPath(path[..subObject], path[(subObject + 1)..]);
    }
    public static FSoftObjectPath WithoutOwner(this FSoftObjectPath path) => new(path.AssetPathName, path.SubPathString);

    public static FGuid ToGuid(this UStaticMesh owner, uint index = 0) => new FGuid(owner.LightingGuid.A ^ index, owner.LightingGuid.B, owner.LightingGuid.C, owner.LightingGuid.D).WithArchive(owner);
    public static FGuid ToGuid(this USkeleton owner) => owner.Guid.WithArchive(owner);

    private static FGuid WithArchive(this FGuid guid, UObject owner) => owner.Owner is { ArchiveName: { } archive }
        ? new FGuid(guid.A ^ (uint) archive.GetHashCode(), guid.B, guid.C, guid.D)
        : guid;

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
