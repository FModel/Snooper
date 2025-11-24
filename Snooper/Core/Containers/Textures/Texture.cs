using System.Numerics;
using CUE4Parse.UE4.Assets.Exports.Texture;
using CUE4Parse.UE4.Objects.Core.Misc;
using ImGuiNET;
using OpenTK.Graphics.OpenGL4;
using Serilog;
using Snooper.Extensions;
using Snooper.UI;

namespace Snooper.Core.Containers.Textures;

public abstract class Texture(
    int width, int height, TextureTarget target,
    SizedInternalFormat internalFormat = SizedInternalFormat.Rgba8,
    PixelFormat format = PixelFormat.Rgba,
    PixelType type = PixelType.UnsignedByte,
    string? name = null) : HandledObject, IMemorySizeProvider, IControllable
{
    public string Name { get; } = name ?? Settings.NoName;
    public FGuid Guid { get; protected init; }
    public TextureTarget Target { get; } = target;

    public int Width { get; protected set; } = width;
    public int Height { get; protected set; } = height;
    public ITextureFormatInfo FormatInfo { get; protected set; } = new TextureFormatInfo(internalFormat, format, type);

    public int[] SwizzleMask { get; internal set; } =
    [
        (int) PixelFormat.Red,
        (int) PixelFormat.Green,
        (int) PixelFormat.Blue,
        (int) PixelFormat.Alpha
    ];

    public override void Generate()
    {
        GL.CreateTextures(Target, 1, out uint handle);
        Handle = handle;
    }

    public void Bind(uint unit)
    {
        GL.BindTextureUnit(unit, Handle);
    }

    protected void Resize<T8>(int newWidth, int newHeight, T8[] pixels, bool mipmapped = false) where T8 : unmanaged
    {
        if (Target != TextureTarget.Texture2D)
            throw new NotSupportedException("Resizing the texture storage is only supported for Texture2D targets.");

        Width = newWidth;
        Height = newHeight;

        var mipCount = mipmapped ? (int)Math.Floor(Math.Log2(Math.Max(Width, Height))) + 1 : 1;
        GL.TextureStorage2D(Handle, mipCount, FormatInfo.InternalFormat, Width, Height);

        if (mipCount > 1)
        {
            GL.TextureParameter(Handle, TextureParameterName.TextureBaseLevel, 0);
            GL.TextureParameter(Handle, TextureParameterName.TextureMaxLevel, mipCount - 1);
        }

        if (pixels.Length == 0) return;
        switch (FormatInfo)
        {
            case TextureFormatInfo info:
                GL.TextureSubImage2D(Handle, 0, 0, 0, Width, Height, info.Format, info.Type, pixels);
                break;
            case CompressedTextureFormatInfo compressed:
                GL.CompressedTextureSubImage2D(Handle, 0, 0, 0, Width, Height, (PixelFormat)compressed.InternalFormat, pixels.Length, pixels);
                break;
            default:
                throw new NotSupportedException("Unknown texture format info.");
        }
    }

    public void Swizzle()
    {
        GL.TextureParameter(Handle, TextureParameterName.TextureSwizzleRgba, SwizzleMask);
    }

    public event Action? TextureReadyForBindless;
    protected void OnTextureReadyForBindless()
    {
        TextureReadyForBindless?.Invoke();
    }

    public IntPtr GetPointer() => (IntPtr)Handle;

    public void DrawControls()
    {
        const float previewSize = 64.0f;

        ImGui.Image(GetPointer(), new Vector2(previewSize, previewSize), Vector2.Zero, Vector2.One, Vector4.One, Vector4.One / 2);
        if (ImGui.IsItemClicked(ImGuiMouseButton.Left))
        {
            TexturePreviewWindow.Open(
                Guid.ToString(EGuidFormats.UniqueObjectGuid),
                $"Diffuse - {Name}",
                GetPointer(),
                new Vector2(Width, Height)
            );
        }

        ImGui.SameLine();

        ImGui.BeginGroup();
        ImGui.TextUnformatted(Name);
        ImGui.PushStyleVar(ImGuiStyleVar.Alpha, 0.6f);
        ImGui.SetWindowFontScale(0.85f);
        ImGui.TextUnformatted($"{Guid.ToString(EGuidFormats.UniqueObjectGuid)}");
        ImGui.TextUnformatted($"{Width}x{Height} pixels ({GetFormattedSpace()})");
        ImGui.SetWindowFontScale(1.0f);
        ImGui.PopStyleVar();
        ImGui.EndGroup();
    }

    public override bool Equals(object? obj) => obj is Texture texture && Guid.Equals(texture.Guid);
    public override int GetHashCode() => Guid.GetHashCode();

    public override void Dispose()
    {
        GL.DeleteTexture(Handle);
    }

    public override long Allocated => FormatInfo.GetMemorySize(Width, Height);
    public override long Used => Allocated;
    public string GetFormattedSpace() => Allocated.GetReadableSize();
}
