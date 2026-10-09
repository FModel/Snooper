using Snooper;
using Snooper.Rendering.Cache;
using Snooper.Rendering.Components.Descriptors;
using Snooper.Rendering.Components.Mesh;

namespace Editor.Widgets.Cache;

internal class MeshCacheTab : DescriptorTab<PrimitiveDescriptor<Vertex>>
{
    public override string Title => "Meshes";

    protected override string[] Kinds { get; } = ["Static", "Skinned"];
    protected override uint[] Colors { get; } = [MemoryChart.AccentColor, MemoryChart.Color(0.85f, 0.45f, 0.75f)];
    protected override IEnumerable<PrimitiveDescriptor<Vertex>> Entries => MeshCache.Descriptors;

    protected override int KindOf(PrimitiveDescriptor<Vertex> entry) => entry.Skeleton is null ? 0 : 1;
    protected override string NameOf(PrimitiveDescriptor<Vertex> entry) => entry.Name ?? Settings.NoName;

    protected override string ValueOf(PrimitiveDescriptor<Vertex> entry)
    {
        var value = $"{entry.Lods.Length} LOD{(entry.Lods.Length != 1 ? "s" : "")}   {Vertices(entry):N0} vertices";
        return entry.Skeleton is { } skeleton ? $"{value}   {skeleton.BoneCount:N0} bones" : value;
    }

    protected override void Summarize(List<PrimitiveDescriptor<Vertex>> entries, MemoryChart.Tile[] tiles)
    {
        var lods = 0;
        var ready = 0;
        var vertices = 0L;
        foreach (var entry in entries)
        {
            lods += entry.Lods.Length;
            vertices += Vertices(entry);
            foreach (var lod in entry.Lods)
            {
                if (lod.IsPrimitiveReady) ready++;
            }
        }

        tiles[0] = new MemoryChart.Tile("Meshes", $"{entries.Count:N0}");
        tiles[1] = new MemoryChart.Tile("LODs", $"{lods:N0}");
        tiles[2] = new MemoryChart.Tile("Vertices", $"{vertices:N0}");
        tiles[3] = new MemoryChart.Tile("Decoded", $"{ready:N0} of {lods:N0} LODs");
    }

    private static long Vertices(PrimitiveDescriptor<Vertex> entry)
    {
        var vertices = 0L;
        foreach (var lod in entry.Lods)
        {
            vertices += lod.VertexCount;
        }
        return vertices;
    }
}
