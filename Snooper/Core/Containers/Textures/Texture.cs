using System.Numerics;
using System.Runtime.InteropServices;
using CUE4Parse.UE4.Objects.Core.Misc;
using ImGuiNET;
using OpenTK.Graphics.OpenGL4;
using Snooper.Extensions;
using Snooper.UI;

namespace Snooper.Core.Containers.Textures;

public abstract class Texture : HandledObject, IMemorySizeProvider, IControllable
{
    public string Name { get; }
    public FGuid Guid { get; }
    public TextureTarget Target { get; }

    public int Width { get; protected set; }
    public int Height { get; protected set; }

    protected ITextureFormatInfo FormatInfo
    {
        get;
        set
        {
            field = value;
            FormatName = value.ToString() ?? "";
        }
    }

    public bool IsSrgb => FormatInfo.IsSrgb;

    public string FormatName { get; private set; } = string.Empty;

    protected Texture(
        int width, int height, TextureTarget target,
        SizedInternalFormat internalFormat = SizedInternalFormat.Rgba8,
        PixelFormat format = PixelFormat.Rgba,
        PixelType type = PixelType.UnsignedByte,
        FGuid? guid = null,
        string? name = null)
    {
        Name = name ?? Settings.NoName;
        Guid = guid ?? FGuid.Random();
        Target = target;

        Width = width;
        Height = height;

        FormatInfo = new TextureFormatInfo(internalFormat, format, type);
    }

    public int[] SwizzleMask { get; internal set; } =
    [
        (int) PixelFormat.Red,
        (int) PixelFormat.Green,
        (int) PixelFormat.Blue,
        (int) PixelFormat.Alpha
    ];

    public override void Generate()
    {
        if (Handle > 0)
            throw new InvalidOperationException("Texture already generated.");

        GL.CreateTextures(Target, 1, out uint handle);
        Handle = handle;
    }

    public void Bind(uint unit)
    {
        GL.BindTextureUnit(unit, Handle);
    }

    protected abstract void SetStorage(int levels);
    protected abstract void SetPixels<T8>(T8[] pixels) where T8 : unmanaged;

    protected internal void Reset<T8>(int newWidth, int newHeight, T8[] pixels, bool mipmapped = false) where T8 : unmanaged
    {
        Width = newWidth;
        Height = newHeight;

        var mipCount = mipmapped ? (int) Math.Floor(Math.Log2(Math.Max(Width, Height))) + 1 : 1;
        SetStorage(mipCount);

        if (mipCount > 1)
        {
            GL.TextureParameter(Handle, TextureParameterName.TextureBaseLevel, 0);
            GL.TextureParameter(Handle, TextureParameterName.TextureMaxLevel, mipCount - 1);
        }

        if (pixels.Length == 0) return;
        SetPixels(pixels);
    }

    public void Swizzle()
    {
        GL.TextureParameter(Handle, TextureParameterName.TextureSwizzleRgba, SwizzleMask);
    }

    public T GetPixel<T>(int x, int y) where T : unmanaged
    {
        var pixel = default(T);
        var stride = Marshal.SizeOf<T>();
        x = Math.Clamp(x, 0, Width - 1);
        y = Math.Clamp(y, 0, Height - 1);

        switch (FormatInfo)
        {
            case TextureFormatInfo info:
                GL.GetTextureSubImage(Handle, 0, x, y, 0, 1, 1, 1, info.Format, info.Type, stride, ref pixel);
                break;
            case CompressedTextureFormatInfo:
                GL.GetCompressedTextureSubImage(Handle, 0, x, y, 0, 1, 1, 1, stride, ref pixel);
                break;
            default:
                throw new NotSupportedException("Unknown texture format info.");
        }
        return pixel;
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
            WindowRequests.Request(Settings.TextureInspectorWindow, this);
        }

        ImGui.SameLine();

        ImGui.BeginGroup();
        ImGui.TextUnformatted(Name);
        EditorUI.Caption($"{Guid.ToString(EGuidFormats.UniqueObjectGuid)}", $"{Width}x{Height} pixels ({GetFormattedSpace()})");
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
