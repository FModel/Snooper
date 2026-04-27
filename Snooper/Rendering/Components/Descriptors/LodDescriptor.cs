using CUE4Parse.UE4.Objects.Core.Math;
using CUE4Parse.UE4.Objects.Meshes;
using ImGuiNET;
using Snooper.Rendering.Primitives;
using Snooper.UI;
using System.Numerics;
using CUE4Parse_Conversion.V2.Dto;

namespace Snooper.Rendering.Components.Descriptors;

public class LodDescriptor<TVertex> : IControllable where TVertex : unmanaged
{
    public uint IndexCount { get; }
    public uint VertexCount { get; }
    public float ScreenSize { get; }
    public uint LayerCount { get; }
    public bool HasColoredVertices { get; }
    public bool HasSkinnedVertices { get; }
    public SectionDescriptor[] Sections { get; }

    private TPrimitiveData<TVertex>? _primitive;
    private readonly Func<TPrimitiveData<TVertex>>? _factory;

    public LodDescriptor(TPrimitiveData<TVertex> primitive)
    {
        _primitive = primitive;
        _factory = null;

        IndexCount = (uint)(_primitive?.Indices?.Length ?? 0);
        VertexCount = (uint)(_primitive?.Vertices?.Length ?? 0);
        ScreenSize = 0.0f;
        LayerCount = 1;
        HasColoredVertices = _primitive?.Colors?.Length > 0;
        HasSkinnedVertices = _primitive?.BoneInfluences?.Length > 0 && _primitive?.BoneInfluenceCounts?.Length > 0;
        Sections = [new SectionDescriptor(0, IndexCount, 0)];
    }

    private LodDescriptor(uint indexCount, uint vertexCount, float screenSize, uint layerCount, bool hasColoredVertices, bool hasSkinnedVertices, SectionDescriptor[] sections, Func<TPrimitiveData<TVertex>>? factory)
    {
        IndexCount = indexCount;
        VertexCount = vertexCount;
        ScreenSize = screenSize;
        LayerCount = layerCount;
        HasColoredVertices = hasColoredVertices;
        HasSkinnedVertices = hasSkinnedVertices;
        Sections = sections;
        _factory = factory;
    }

    internal static LodDescriptor<TVertex> FromLod<TMeshVertex>(MeshLod<TMeshVertex> lod, Func<TMeshVertex[], uint[], FColor[]?, FMeshUVFloat[]?, TPrimitiveData<TVertex>> factory) where TMeshVertex : struct, IMeshVertex
    {
        if (lod.Vertices is not { Length: > 0 } vertices)
            throw new ArgumentException("LOD does not contain valid vertices.", nameof(lod));
        if (lod.Indices is not { Length: > 0 } indices)
            throw new ArgumentException("LOD does not contain valid indices.", nameof(lod));
        if (lod.Sections is not { Length: > 0 } sections)
            throw new ArgumentException("LOD does not contain valid sections.", nameof(lod));

        // capture vertices and indices for lazy factory creation
        var cVertices = (TMeshVertex[])vertices.Clone();
        var cIndices = (uint[])indices.Clone();

        var cSections = new SectionDescriptor[sections.Length];
        for (var i = 0; i < cSections.Length; i++)
        {
            var section = sections[i];
            cSections[i] = new SectionDescriptor(
                (uint) section.FirstIndex, (uint) section.NumFaces * 3,
                (uint) section.MaterialIndex, section.CastShadow,
                lod.Owner.GetMaterial(section)?.SlotName);
        }

        FColor[]? cColors = null;
        if (lod.VertexColors is { Length: > 0 } colors)
        {
            cColors = (FColor[])colors[0].Colors.Clone();
        }

        FMeshUVFloat[]? cExtraUvs = null;
        if (lod.ExtraUvs is { Length: > 0 } extraUvs && extraUvs[0] is { Length: > 0 } extraUv1)
        {
            cExtraUvs = (FMeshUVFloat[])extraUv1.Clone();
        }

        return new LodDescriptor<TVertex>(
            (uint) indices.Length,
            (uint) vertices.Length,
            lod.ScreenSize,
            (uint) lod.ExtraUvs.Length + 1,
            cColors != null,
            vertices is SkinnedMeshVertex[],
            cSections,
            () => factory(cVertices, cIndices, cColors, cExtraUvs));
    }

    internal TPrimitiveData<TVertex> CreatePrimitive()
    {
        if (_primitive != null)
            return _primitive;

        if (_factory == null)
            throw new InvalidOperationException("Cannot create primitive: no factory available.");

        _primitive = _factory();
        return _primitive;
    }

    public void DrawControls()
    {
        if (Sections.Length > 0)
        {
            var rowH = ImGui.GetTextLineHeightWithSpacing();
            var tblH = Math.Min(Sections.Length, 8) * rowH + ImGui.GetFrameHeightWithSpacing();
            var flags = ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.NoSavedSettings | ImGuiTableFlags.ScrollY;

            if (ImGui.BeginTable("##LodSectionTable", 5, flags, new Vector2(0, tblH)))
            {
                ImGui.TableSetupScrollFreeze(0, 1);
                ImGui.TableSetupColumn("#", ImGuiTableColumnFlags.WidthStretch, 0.05f);
                ImGui.TableSetupColumn("Slot Name", ImGuiTableColumnFlags.WidthStretch, 1.0f);
                ImGui.TableSetupColumn("Idx Count", ImGuiTableColumnFlags.WidthStretch, 0.7f);
                ImGui.TableSetupColumn("Material Idx", ImGuiTableColumnFlags.WidthStretch, 0.2f);
                ImGui.TableSetupColumn("Shadow", ImGuiTableColumnFlags.WidthStretch, 0.2f);
                ImGui.TableHeadersRow();

                for (var i = 0; i < Sections.Length; i++)
                {
                    var sec = Sections[i]; ImGui.TableNextRow();
                    ImGui.TableNextColumn(); ImGui.TextUnformatted($"{i}");
                    ImGui.TableNextColumn(); ImGui.TextUnformatted(sec.Name);
                    ImGui.TableNextColumn(); ImGui.TextUnformatted($"{sec.IndexCount:N0}");
                    ImGui.TableNextColumn(); ImGui.TextUnformatted($"{sec.MaterialIndex:N0}");
                    ImGui.TableNextColumn(); ImGui.TextUnformatted(sec.CastShadow ? "\uf00c" : "\uf00d");
                }
                ImGui.EndTable();
            }
        }
        else
        {
            ImGui.TextDisabled("No sections");
        }
    }
}
