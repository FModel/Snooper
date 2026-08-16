using System.Numerics;
using CUE4Parse.FileProvider;
using CUE4Parse_Conversion.Dto;
using CUE4Parse.UE4.Assets.Exports.Animation;
using CUE4Parse.UE4.Assets.Exports.GeometryCollection;
using CUE4Parse.UE4.Assets.Exports.SkeletalMesh;
using CUE4Parse.UE4.Assets.Exports.StaticMesh;
using CUE4Parse.UE4.Objects.Core.Math;
using CUE4Parse.UE4.Objects.Core.Misc;
using CUE4Parse.UE4.Objects.Meshes;
using ImGuiNET;
using Snooper.Extensions;
using Snooper.Rendering.Cache;
using Snooper.Rendering.Primitives;
using Snooper.UI;

namespace Snooper.Rendering.Components.Descriptors;

public class PrimitiveDescriptor<TVertex> : IControllable, ICloneable where TVertex : unmanaged
{
    public string? Name { get; }
    public string? Path { get; }
    public FGuid Guid { get; } // this will be used by the geometry pool in order to not upload the geometry data twice on the gpu
    public uint ColorMode { get; } = FragmentColorMode.Disabled; // per-mesh shading mode, overridden by the global uniform when that one is set
    public CullingBounds Bounds { get; }
    public LodDescriptor<TVertex>[] Lods { get; }
    public SkeletonDescriptor? Skeleton { get; }
    public ISocketDescriptor?[] Sockets { get; }
    public MorphDescriptor? Morphs { get; }

    private PrimitiveDescriptor(PrimitiveDescriptor<TVertex> other)
    {
        Name = other.Name;
        Path = other.Path;
        Guid = other.Guid;
        ColorMode = other.ColorMode;
        Bounds = (CullingBounds) other.Bounds.Clone();
        Lods = [];
        Skeleton = null;
        Sockets = [];
        Morphs = other.Morphs;
    }

    public PrimitiveDescriptor(CullingBounds bounds, Func<TPrimitiveData<TVertex>> factory)
    {
        Guid = FGuid.Random();
        Bounds = bounds;
        Lods = [new LodDescriptor<TVertex>(factory())];
        Sockets = [];
    }

    private PrimitiveDescriptor(uint id, CullingBounds bounds, Func<uint, TPrimitiveData<TVertex>> factory)
    {
        Guid = new FGuid(id);
        Bounds = bounds;
        Lods = [new LodDescriptor<TVertex>(factory(id))];
        Sockets = [];
    }

    private PrimitiveDescriptor(UStaticMesh owner, Func<MeshVertex[], uint[], FColor[]?, FMeshUVFloat[]?, TPrimitiveData<TVertex>> factory)
    {
        Name = owner.Name;
        Path = owner.GetCleanPath();
        Guid = owner.LightingGuid;

        var colorRemap = CreateJunoColorRemap(owner.Owner?.Provider, Path);
        if (colorRemap != null) ColorMode = FragmentColorMode.VertexColor;

        using var dto = new StaticMeshDto(owner);
        Bounds = new CullingBounds(dto.Bounds);
        Lods = new LodDescriptor<TVertex>[dto.LODs.Count];
        for (var i = 0; i < Lods.Length; i++)
        {
            Lods[i] = LodDescriptor<TVertex>.FromLod(dto.LODs[i], factory, colorRemap);
        }

        Sockets = new ISocketDescriptor[dto.Sockets?.Length ?? 0];
        for (var i = 0; i < Sockets.Length; i++)
        {
            if (!dto.Sockets[i].TryLoad<UStaticMeshSocket>(out var socket)) continue;
            Sockets[i] = new StaticMeshSocketDescriptor(socket);
        }
    }

    private PrimitiveDescriptor(UGeometryCollection owner, Func<MeshVertex[], uint[], FColor[]?, FMeshUVFloat[]?, TPrimitiveData<TVertex>> factory)
    {
        Name = owner.Name;
        Path = owner.GetCleanPath();
        Guid = new FGuid((uint)owner.Name.GetHashCode());

        var colorRemap = CreateJunoColorRemap(owner.Owner?.Provider, Path);
        if (colorRemap != null) ColorMode = FragmentColorMode.VertexColor;

        using var dto = new StaticMeshDto(owner);
        Bounds = new CullingBounds(dto.Bounds);
        Lods = new LodDescriptor<TVertex>[dto.LODs.Count];
        for (var i = 0; i < Lods.Length; i++)
        {
            Lods[i] = LodDescriptor<TVertex>.FromLod(dto.LODs[i], factory, colorRemap);
        }

        Sockets = [];
    }

    private PrimitiveDescriptor(USkeletalMesh owner, Func<SkinnedMeshVertex[], uint[], FColor[]?, FMeshUVFloat[]?, TPrimitiveData<TVertex>> factory)
    {
        Name = owner.Name;
        Path = owner.GetCleanPath();
        Guid = new FGuid((uint)owner.Name.GetHashCode());

        var colorRemap = CreateJunoColorRemap(owner.Owner?.Provider, Path);
        if (colorRemap != null) ColorMode = FragmentColorMode.VertexColor;

        using var dto = new SkeletalMeshDto(owner);
        Bounds = new CullingBounds(dto.Bounds);
        Lods = new LodDescriptor<TVertex>[dto.LODs.Count];
        for (var i = 0; i < Lods.Length; i++)
        {
            Lods[i] = LodDescriptor<TVertex>.FromLod(dto.LODs[i], factory, colorRemap);
        }

        Skeleton = new SkeletonDescriptor(dto.Bones);
        if (owner.Skeleton.TryLoad<USkeleton>(out var skeleton))
        {
            Skeleton.SetOwner(skeleton);
        }

        Sockets = new ISocketDescriptor[dto.Sockets?.Length ?? 0];
        for (var i = 0; i < Sockets.Length; i++)
        {
            if (!dto.Sockets[i].TryLoad<USkeletalMeshSocket>(out var socket)) continue;
            Sockets[i] = new SkeletalMeshSocketDescriptor(socket);
        }

        if (dto.MorphTargets is { Length: > 0 } morphTargets)
        {
            owner.PopulateMorphTargetVerticesData();
            Morphs = MorphDescriptor.Create(morphTargets, Lods);
        }
    }

    private PrimitiveDescriptor(USkeleton owner, Func<SkeletonDescriptor, TPrimitiveData<TVertex>> factory)
    {
        Name = owner.Name;
        Path = owner.GetCleanPath();
        Guid = owner.Guid;

        using var dto = new SkeletonDto(owner);
        Bounds = new CullingBounds(dto.Bounds);

        Skeleton = new SkeletonDescriptor(dto.Bones);
        Skeleton.SetOwner(owner);

        Lods = [new LodDescriptor<TVertex>(factory(Skeleton), true)];

        Sockets = new ISocketDescriptor[dto.Sockets?.Length ?? 0];
        for (var i = 0; i < Sockets.Length; i++)
        {
            if (!dto.Sockets[i].TryLoad<USkeletalMeshSocket>(out var socket)) continue;
            Sockets[i] = new SkeletalMeshSocketDescriptor(socket);
        }
    }

    private Action<FColor[]>? CreateJunoColorRemap(IFileProvider? provider, string? path)
    {
        if (provider is null || path?.StartsWith("FortniteGame/Plugins/GameFeatures/Juno/", StringComparison.OrdinalIgnoreCase) != true)
            return null;

        return colors =>
        {
            for (var i = 0; i < colors.Length; i++)
            {
                colors[i] = JunoPaletteCache.Resolve(provider, colors[i]);
            }
        };
    }

    private ISocketDescriptor? FindSocket(string name) => Sockets.FirstOrDefault(x => x != null && x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    public bool HasSocket(string name)
    {
        var socket = FindSocket(name);
        if (socket != null) return true;

        return Skeleton?.BoneNameToIndex.ContainsKey(name) == true;
    }

    public Matrix4x4 GetSocketModelMatrix(string name)
    {
        var socket = FindSocket(name);

        var boneName = name;
        if (socket is SkeletalMeshSocketDescriptor sk)
        {
            boneName = sk.BoneName;
        }

        var matrix = socket?.LocalMatrix ?? Matrix4x4.Identity;
        if (Skeleton != null && Skeleton.BoneNameToIndex.TryGetValue(boneName, out var boneIndex))
        {
            matrix *= Skeleton.BoneMatrices[boneIndex];
        }

        return matrix;
    }

    public static PrimitiveDescriptor<TVertex> GetOrCreate(UStaticMesh owner, Func<MeshVertex[], uint[], FColor[]?, FMeshUVFloat[]?, TPrimitiveData<TVertex>> factory)
        => MeshCache.GetOrCreate(owner.LightingGuid, () => new PrimitiveDescriptor<TVertex>(owner, factory));

    public static PrimitiveDescriptor<TVertex> GetOrCreate(UGeometryCollection owner, Func<MeshVertex[], uint[], FColor[]?, FMeshUVFloat[]?, TPrimitiveData<TVertex>> factory)
        => MeshCache.GetOrCreate(new FGuid((uint)owner.Name.GetHashCode()), () => new PrimitiveDescriptor<TVertex>(owner, factory));

    public static PrimitiveDescriptor<TVertex> GetOrCreate(USkeletalMesh owner, Func<SkinnedMeshVertex[], uint[], FColor[]?, FMeshUVFloat[]?, TPrimitiveData<TVertex>> factory)
        => MeshCache.GetOrCreate(FGuid.Random(), () => new PrimitiveDescriptor<TVertex>(owner, factory));

    public static PrimitiveDescriptor<TVertex> GetOrCreate(USkeleton owner, Func<SkeletonDescriptor, TPrimitiveData<TVertex>> factory)
        => MeshCache.GetOrCreate(owner.Guid, () => new PrimitiveDescriptor<TVertex>(owner, factory));

    private int _selectedLod;
    public void DrawControls()
    {
        DrawHeader();

        ImGui.Spacing();
        ImGui.SeparatorText($"LODs ({Lods.Length})");
        DrawLodTable();

        ImGui.Spacing();
        ImGui.SeparatorText($"Sections  ({Lods[_selectedLod].Sections.Length})");
        DrawSectionTable();

        if (Skeleton != null)
        {
            ImGui.Spacing();
            ImGui.SeparatorText($"Bones  ({Skeleton.BoneCount})");
            Skeleton.DrawControls();
        }

        if (Sockets.Length > 0)
        {
            ImGui.Spacing();
            ImGui.SeparatorText($"Sockets ({Sockets.Length})");
            DrawSocketTable();
        }
    }

    private void DrawHeader()
    {
        ImGui.TextUnformatted(Name ?? Settings.NoName);
        ImGui.SameLine();
        ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetColorU32(ImGuiCol.TextDisabled));
        ImGui.TextUnformatted($"  ({Bounds.BoundsFormatted}) ({Guid})");

        ImGui.SetWindowFontScale(0.85f);
        ImGui.TextUnformatted($"Mesh: {Path}");
        if (Skeleton != null) ImGui.TextUnformatted($"Skeleton: {Skeleton.Path}");
        ImGui.SetWindowFontScale(1.0f);
        ImGui.PopStyleColor();
    }

    private void DrawLodTable()
    {
        var flags = ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.NoSavedSettings;

        if (ImGui.BeginTable("##DescriptorLodTable", 7, flags))
        {
            ImGui.TableSetupColumn("LOD", ImGuiTableColumnFlags.WidthFixed, 32f);
            ImGui.TableSetupColumn("Vertices", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Indices", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Sections", ImGuiTableColumnFlags.WidthFixed, 56f);
            ImGui.TableSetupColumn("Screen %", ImGuiTableColumnFlags.WidthFixed, 56f);
            ImGui.TableSetupColumn("Colored", ImGuiTableColumnFlags.WidthFixed, 52f);
            ImGui.TableSetupColumn("Skinned", ImGuiTableColumnFlags.WidthFixed, 52f);
            ImGui.TableHeadersRow();

            for (var i = 0; i < Lods.Length; i++)
            {
                var l = Lods[i]; ImGui.TableNextRow();
                ImGui.TableNextColumn(); ImGui.TextUnformatted($"{i}");
                ImGui.TableNextColumn(); ImGui.TextUnformatted($"{l.VertexCount:N0}");
                ImGui.TableNextColumn(); ImGui.TextUnformatted($"{l.IndexCount:N0}");
                ImGui.TableNextColumn(); ImGui.TextUnformatted($"{l.Sections.Length}");
                ImGui.TableNextColumn(); ImGui.TextUnformatted(l.ScreenSize >= 0f ? $"{l.ScreenSize * 100f:F1}%" : "\u0021");
                ImGui.TableNextColumn(); ImGui.TextUnformatted(l.HasColoredVertices ? "\uf00c" : "\uf00d");
                ImGui.TableNextColumn(); ImGui.TextUnformatted(l.HasSkinnedVertices ? "\uf00c" : "\uf00d");
            }
            ImGui.EndTable();
        }
    }

    private void DrawSectionTable()
    {
        if (Lods.Length > 1)
        {
            var spacing = ImGui.GetStyle().ItemSpacing.X;
            var maxLod = Lods.Length - 1;
            var btnW = MathF.Max(32f, (ImGui.GetContentRegionAvail().X - spacing * maxLod) / Lods.Length);
            var btnH = ImGui.GetFrameHeight();

            ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 3f);
            for (var i = 0; i <= maxLod; i++)
            {
                var isActive = _selectedLod == i;
                if (isActive)
                {
                    ImGui.PushStyleColor(ImGuiCol.Button, ImGui.GetColorU32(ImGuiCol.ButtonActive));
                    ImGui.PushStyleColor(ImGuiCol.ButtonHovered, ImGui.GetColorU32(ImGuiCol.ButtonActive));
                }
                if (ImGui.Button($"LOD {i}##SecLod{i}", new Vector2(btnW, btnH))) _selectedLod = i;
                if (isActive) ImGui.PopStyleColor(2);
                if (i < maxLod) ImGui.SameLine(0, spacing);
            }
            ImGui.PopStyleVar();
            ImGui.Spacing();
        }

        Lods[_selectedLod].DrawControls();
    }

    private void DrawSocketTable()
    {
        var rowH = ImGui.GetTextLineHeightWithSpacing();
        var tblH = Math.Min(Sockets.Length, 8) * rowH + ImGui.GetFrameHeightWithSpacing();
        var flags = ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.NoSavedSettings | ImGuiTableFlags.ScrollY;

        if (ImGui.BeginTable("##DescriptorSocketTable", 4, flags, new Vector2(0, tblH)))
        {
            ImGui.TableSetupScrollFreeze(0, 1);
            ImGui.TableSetupColumn("#", ImGuiTableColumnFlags.WidthFixed, 28f);
            ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch, 1.2f);
            ImGui.TableSetupColumn("Type", ImGuiTableColumnFlags.WidthFixed, 52f);
            ImGui.TableSetupColumn("Bone", ImGuiTableColumnFlags.WidthStretch, 1.0f);
            ImGui.TableHeadersRow();

            for (var i = 0; i < Sockets.Length; i++)
            {
                var socket = Sockets[i];
                if (socket == null) continue;

                ImGui.TableNextRow();
                ImGui.TableNextColumn(); ImGui.TextUnformatted($"{i}");
                ImGui.TableNextColumn(); ImGui.TextUnformatted(socket.Name);
                ImGui.TableNextColumn(); ImGui.TextUnformatted(socket is SkeletalMeshSocketDescriptor ? "Skeletal" : "Static");
                ImGui.TableNextColumn();
                if (socket is SkeletalMeshSocketDescriptor sk)
                {
                    ImGui.TextUnformatted(sk.BoneName);
                }
                else
                {
                    ImGui.TextDisabled("-");
                }
            }
            ImGui.EndTable();
        }
    }

    public object Clone() => new PrimitiveDescriptor<TVertex>(this);
}
