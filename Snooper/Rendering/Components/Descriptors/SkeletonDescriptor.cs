using System.Numerics;
using CUE4Parse_Conversion.Dto;
using CUE4Parse.UE4.Assets.Exports.Animation;
using CUE4Parse.UE4.Objects.Core.Misc;
using ImGuiNET;
using Snooper.Core.Containers.Buffers;
using Snooper.Rendering.Components.Transforms;
using Snooper.UI;

namespace Snooper.Rendering.Components.Descriptors;

public readonly struct BoneDescriptor
{
    public readonly string Name;
    public readonly int ParentIndex;
    public readonly Matrix4x4 BindPoseLocalMatrix;

    public bool IsRoot => ParentIndex < 0;

    public BoneDescriptor(string name, int parentIndex, Matrix4x4 bindPoseLocalMatrix)
    {
        Name = name;
        ParentIndex = parentIndex;

        if (IsRoot && Matrix4x4.Decompose(bindPoseLocalMatrix, out _, out var rotation, out var position))
        {
            // some games scale their root bone for some reason which offsets all others (FarFarWest)
            bindPoseLocalMatrix = Matrix4x4.CreateFromQuaternion(rotation) * Matrix4x4.CreateTranslation(position);
        }
        BindPoseLocalMatrix = bindPoseLocalMatrix;
    }
}

public class SkeletonDescriptor : IControllable
{
    public string? Name { get; private set; }
    public string? Path { get; private set; }
    public FGuid Guid { get; private set; }

    internal BufferAllocation? _poseAllocation;

    /// <summary>
    /// local-space transform for each bone for the current frame. This is the single source of truth for bone transforms.
    /// </summary>
    public Matrix4x4[] BoneLocalMatrices { get; }

    /// <summary>
    /// this is never modified after construction.
    /// </summary>
    public BoneDescriptor[] BoneDescriptors { get; }

    /// <summary>
    /// model-space transform for each bone for the current frame. This is always recalculated from BoneLocalMatrices.
    /// Never set this array directly.
    /// </summary>
    public Matrix4x4[] BoneMatrices { get; }

    public IReadOnlyDictionary<string, uint> BoneNameToIndex => _boneNameToIndex;
    private readonly Dictionary<string, uint> _boneNameToIndex;

    public int BoneCount => BoneLocalMatrices.Length;

    public SkeletonDescriptor(FReferenceSkeleton reference)
    {
        BoneLocalMatrices = new Matrix4x4[reference.FinalRefBonePose.Length];
        BoneDescriptors = new BoneDescriptor[BoneCount];
        BoneMatrices = new Matrix4x4[BoneCount];
        _boneNameToIndex = new Dictionary<string, uint>(BoneCount, StringComparer.OrdinalIgnoreCase);

        for (var boneIndex = 0u; boneIndex < BoneCount; boneIndex++)
        {
            var info = reference.FinalRefBoneInfo[boneIndex];
            var matrix = new Transform(reference.FinalRefBonePose[boneIndex]).ToMatrix();
            var descriptor = new BoneDescriptor(info.Name.Text, info.ParentIndex, matrix);

            BoneLocalMatrices[boneIndex] = descriptor.BindPoseLocalMatrix;
            BoneDescriptors[boneIndex] = descriptor;
            _boneNameToIndex.Add(descriptor.Name, boneIndex);
        }

        RecalculateBoneMatrices();
    }

    public SkeletonDescriptor(IReadOnlyList<MeshBoneDto> bones)
    {
        BoneLocalMatrices = new Matrix4x4[bones.Count];
        BoneDescriptors = new BoneDescriptor[BoneCount];
        BoneMatrices = new Matrix4x4[BoneCount];
        _boneNameToIndex = new Dictionary<string, uint>(BoneCount, StringComparer.OrdinalIgnoreCase);

        for (var boneIndex = 0u; boneIndex < BoneCount; boneIndex++)
        {
            var bone = bones[(int) boneIndex];
            var matrix = new Transform(bone.Transform).ToMatrix();
            var descriptor = new BoneDescriptor(bone.Name, bone.ParentIndex, matrix);

            BoneLocalMatrices[boneIndex] = descriptor.BindPoseLocalMatrix;
            BoneDescriptors[boneIndex] = descriptor;
            _boneNameToIndex.Add(descriptor.Name, boneIndex);
        }

        RecalculateBoneMatrices();
    }

    internal void SetOwner(USkeleton owner)
    {
        Name = owner.Name;
        Path = owner.Owner?.Provider?.FixPath(owner.Owner?.Name ?? owner.GetPathName());
        Guid = owner.Guid;
    }

    public string GetBoneName(int index) => BoneDescriptors[index].Name;
    public int GetBoneParentIndex(int index) => BoneDescriptors[index].ParentIndex;

    public void MoveBone(int boneIndex, Matrix4x4 matrix)
    {
        var pi = BoneDescriptors[boneIndex].ParentIndex;
        if (pi >= 0 && Matrix4x4.Invert(BoneMatrices[pi], out var parentMatrix))
        {
            BoneLocalMatrices[boneIndex] = matrix * parentMatrix;
        }
        else
        {
            BoneLocalMatrices[boneIndex] = matrix;
        }

        RecalculateBoneMatrices(boneIndex);
    }

    public void ResetBone(int boneIndex)
    {
        BoneLocalMatrices[boneIndex] = BoneDescriptors[boneIndex].BindPoseLocalMatrix;
        RecalculateBoneMatrices(boneIndex);
    }

    public void ResetAllBones()
    {
        for (var i = 0; i < BoneCount; i++)
        {
            BoneLocalMatrices[i] = BoneDescriptors[i].BindPoseLocalMatrix;
        }
        RecalculateBoneMatrices();
    }

    public void RecalculateBoneMatrices(int start = -1, int end = -1)
    {
        var from = start >= 0 ? start : 0;
        var to = end >= 0 && end < BoneCount ? end : BoneCount - 1;
        for (var i = from; i <= to; i++)
        {
            var pi = BoneDescriptors[i].ParentIndex;
            BoneMatrices[i] = pi < 0 ? BoneLocalMatrices[i] : BoneLocalMatrices[i] * BoneMatrices[pi];
        }
    }

    public void DrawControls()
    {
        var rowH = ImGui.GetTextLineHeightWithSpacing();
        var tblH = 8 * rowH + ImGui.GetFrameHeightWithSpacing();
        var tableW = ImGui.GetContentRegionAvail().X - tblH - ImGui.GetStyle().ItemSpacing.X;
        var flags = ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.NoSavedSettings | ImGuiTableFlags.ScrollY;

        if (ImGui.BeginTable("##SkeletonBoneTable", 3, flags, new Vector2(tableW, tblH)))
        {
            ImGui.TableSetupScrollFreeze(0, 1);
            ImGui.TableSetupColumn("#", ImGuiTableColumnFlags.WidthFixed, 34f);
            ImGui.TableSetupColumn("Bone", ImGuiTableColumnFlags.WidthStretch, 1.0f);
            ImGui.TableSetupColumn("Parent", ImGuiTableColumnFlags.WidthStretch, 1.0f);
            ImGui.TableHeadersRow();

            for (var i = 0; i < BoneCount; i++)
            {
                var bone = BoneDescriptors[i];
                ImGui.TableNextRow();

                ImGui.TableNextColumn();
                ImGui.TextUnformatted($"{i}");

                ImGui.TableNextColumn();
                ImGui.TextUnformatted(bone.Name);

                ImGui.TableNextColumn();
                if (bone.IsRoot) ImGui.TextDisabled("-");
                else ImGui.TextUnformatted($"[{bone.ParentIndex}] {BoneDescriptors[bone.ParentIndex].Name}");
            }
            ImGui.EndTable();
        }

        ImGui.SameLine();
        DrawCanvas(tblH);
    }

    private void DrawCanvas(float size)
    {
        var canvasPos = ImGui.GetCursorScreenPos();
        var canvasSize = new Vector2(size, size);
        ImGui.InvisibleButton("##SkeletonCanvas", canvasSize);
        var isHovered = ImGui.IsItemHovered();
        var mousePos = ImGui.GetMousePos();

        var pts  = new Vector2[BoneCount];
        var minX = float.MaxValue; var maxX = float.MinValue;
        var minY = float.MaxValue; var maxY = float.MinValue;
        for (var i = 0; i < pts.Length; i++)
        {
            var m = BoneMatrices[i];
            var p = new Vector2(m.M41, -m.M42);
            pts[i] = p;

            if (p.X < minX) minX = p.X;
            if (p.X > maxX) maxX = p.X;
            if (p.Y < minY) minY = p.Y;
            if (p.Y > maxY) maxY = p.Y;
        }

        var range = MathF.Max(maxX - minX, maxY - minY);
        if (range < 0.0001f) range = 1f;

        const float canvasPad = 24f;
        var fitScale = (MathF.Min(canvasSize.X, canvasSize.Y) - canvasPad * 2f) / range;
        var cx = (minX + maxX) * 0.5f;
        var cy = (minY + maxY) * 0.5f;

        Vector2 ToScreen(Vector2 p) => new(canvasPos.X + canvasSize.X * 0.5f + (p.X - cx) * fitScale, canvasPos.Y + canvasSize.Y * 0.5f + (p.Y - cy) * fitScale);

        var dl = ImGui.GetWindowDrawList();
        dl.AddRectFilled(canvasPos, canvasPos + canvasSize, 0xFF_14_14_14);
        dl.AddRect(canvasPos, canvasPos + canvasSize, 0xFF_32_32_32);

        // bone connections
        for (var i = 0; i < BoneCount; i++)
        {
            var pi = BoneDescriptors[i].ParentIndex;
            if (pi < 0) continue;
            dl.AddLine(ToScreen(pts[pi]), ToScreen(pts[i]), 0xFF_50_50_50, 1f);
        }

        // joints + hover detection
        var hoveredBone = -1;
        var bestDist    = 9f;
        for (var i = 0; i < BoneCount; i++)
        {
            var sp = ToScreen(pts[i]);
            var isRoot = BoneDescriptors[i].IsRoot;
            dl.AddCircleFilled(sp, isRoot ? 4.5f : 2.5f, isRoot ? 0xFF_00_AA_FF : 0xFF_80_C0_FF);

            if (isHovered)
            {
                var d = Vector2.Distance(mousePos, sp);
                if (d < bestDist)
                {
                    bestDist = d;
                    hoveredBone = i;
                }
            }
        }

        if (hoveredBone >= 0)
        {
            var sp = ToScreen(pts[hoveredBone]);
            var bone = BoneDescriptors[hoveredBone];
            dl.AddCircle(sp, 6f, 0xFF_00_FF_CC, 0, 1.5f);

            ImGui.BeginTooltip();
            ImGui.TextUnformatted($"[{hoveredBone}] {bone.Name}");
            if (!bone.IsRoot)
            {
                ImGui.TextUnformatted($"Parent: [{bone.ParentIndex}] {BoneDescriptors[bone.ParentIndex].Name}");
            }
            ImGui.EndTooltip();
        }
    }
}
