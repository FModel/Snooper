using CUE4Parse.UE4.Assets.Exports.Component.StaticMesh;
using CUE4Parse.UE4.Assets.Exports.StaticMesh;
using Snooper.Extensions;

namespace Snooper.Rendering.Components.Mesh;

public class HierarchicalInstancedStaticMeshComponent : InstancedStaticMeshComponent
{
    public (int Start, int Count)[]? InstanceChunks { get; }

    public HierarchicalInstancedStaticMeshComponent(UStaticMesh staticMesh, UHierarchicalInstancedStaticMeshComponent component) : base(staticMesh, component, component.GetSortedInstances(out var tree))
    {
        if (tree is null) return;

        var chunks = new List<(int Start, int Count)>();
        Collect(0);

        // "The number of instances in the ClusterTree. Subsequent instances will always be rendered."
        var built = component.NumBuiltInstances;
        if (built < Instances.Count) chunks.Add((built, Instances.Count - built));

        InstanceChunks = [.. chunks];

        void Collect(int node)
        {
            var count = tree[node].LastInstance - tree[node].FirstInstance + 1;
            if (tree[node].FirstChild < 0 || count <= Settings.MaxInstancesPerDraw)
            {
                chunks.Add((tree[node].FirstInstance, count));
                return;
            }

            for (var child = tree[node].FirstChild; child <= tree[node].LastChild; child++)
            {
                Collect(child);
            }
        }
    }
}
