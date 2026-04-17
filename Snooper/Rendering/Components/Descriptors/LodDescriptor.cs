using CUE4Parse_Conversion.Meshes.PSK;
using CUE4Parse.UE4.Objects.Core.Math;
using CUE4Parse.UE4.Objects.Meshes;
using ImGuiNET;
using Snooper.Rendering.Primitives;
using Snooper.UI;
using System.Numerics;

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

    public LodDescriptor(CMeshLod lod, Func<CMeshVertex[], uint[], FColor[]?, FMeshUVFloat[]?, TPrimitiveData<TVertex>> factory)
    {
        var vertices = lod switch
        {
            CStaticMeshLod staticLod => staticLod.Verts,
            CSkelMeshLod skelLod => skelLod.Verts,
            _ => throw new NotSupportedException($"Unsupported mesh type: {lod.GetType().Name}")
        };

        if (vertices is not { Length: > 0 })
            throw new ArgumentException("LOD does not contain valid vertices.", nameof(lod));
        if (lod.Indices?.Value is not { Length: > 0 } indices)
            throw new ArgumentException("LOD does not contain valid indices.", nameof(lod));
        if (lod.Sections?.Value is not { Length: > 0 } sections)
            throw new ArgumentException("LOD does not contain valid sections.", nameof(lod));

        IndexCount = (uint)indices.Length;
        VertexCount = (uint)vertices.Length;
        ScreenSize = lod.ScreenSize;
        LayerCount = (uint)Math.Max(1, lod.NumTexCoords);

        Sections = new SectionDescriptor[sections.Length];
        for (var i = 0; i < Sections.Length; i++)
        {
            var section = sections[i];
            Sections[i] = new SectionDescriptor((uint)section.FirstIndex, (uint)section.NumFaces * 3, (uint)section.MaterialIndex, section.CastShadow, section.MaterialName);
        }

        // capture vertices and indices for lazy factory creation
        var cVertices = (CMeshVertex[])vertices.Clone();
        var cIndices = (uint[])indices.Clone();

        FColor[]? cColors = null;
        if (lod.VertexColors is { Length: > 0 } colors)
        {
            cColors = (FColor[])colors.Clone();
        }

        FMeshUVFloat[]? cExtraUvs = null;
        if (lod.ExtraUV?.Value is { Length: > 0 } extraUvs && extraUvs[0] is { Length: > 0 } extraUv1)
        {
            cExtraUvs = (FMeshUVFloat[])extraUv1.Clone();
        }

        HasColoredVertices = cColors != null;
        HasSkinnedVertices = lod is CSkelMeshLod;

        _factory = () => factory(cVertices, cIndices, cColors, cExtraUvs);
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
                ImGui.TableSetupColumn("#", ImGuiTableColumnFlags.WidthFixed, 24f);
                ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch, 1.0f);
                ImGui.TableSetupColumn("Idx Count", ImGuiTableColumnFlags.WidthStretch, 0.7f);
                ImGui.TableSetupColumn("Material", ImGuiTableColumnFlags.WidthFixed, 58f);
                ImGui.TableSetupColumn("Shadow", ImGuiTableColumnFlags.WidthFixed, 58f);
                ImGui.TableHeadersRow();

                for (var i = 0; i < Sections.Length; i++)
                {
                    var sec = Sections[i]; ImGui.TableNextRow();
                    ImGui.TableNextColumn(); ImGui.TextUnformatted($"{i}");
                    ImGui.TableNextColumn(); ImGui.TextUnformatted(sec.Name);
                    ImGui.TableNextColumn(); ImGui.TextUnformatted($"{sec.IndexCount:N0}");
                    ImGui.TableNextColumn(); ImGui.TextUnformatted($"Slot {sec.MaterialIndex}");
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
