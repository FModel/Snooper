using OpenTK.Graphics.OpenGL4;
using SkiaSharp;

namespace Snooper.Core.Containers.Textures;

public class FontAtlasTexture : Texture2D
{
    private static FontAtlasTexture? _instance;
    public static FontAtlasTexture Instance => _instance ??= new FontAtlasTexture();

    public readonly float FontSize;
    public readonly float LineHeight;
    
    private readonly SKTypeface _typeface;
    private readonly List<(char c, SKRect bounds, float advance)> _charInfos;
    private readonly int _padding;
    private readonly int _charsPerRow;
    private readonly int _cellWidth;
    private readonly int _cellHeight;
    private readonly int _atlasWidth;
    private readonly int _atlasHeight;
    
    public Dictionary<char, CharacterInfo> Characters { get; } = new();
    
    public struct CharacterInfo
    {
        public float U0, V0, U1, V1; // UV coordinates in atlas
        public float Width, Height;   // Character size in pixels
        public float CellWidth, CellHeight; // Cell size in pixels
        public float OffsetX, OffsetY; // Offset from baseline
        public float AdvanceX;         // How far to move cursor

        public override string ToString() => $"UV: ({U0},{V0})-({U1},{V1}), Size: ({Width}x{Height}), CellSize: ({CellWidth}x{CellHeight}), Offset: ({OffsetX},{OffsetY}), Advance: {AdvanceX}";
    }

    private FontAtlasTexture(string fontFamily = "Segoe UI", float fontSize = 48, bool bold = true) : base(1, 1, PixelInternalFormat.Rgba8, PixelFormat.Rgba, PixelType.UnsignedByte, $"FontAtlas_{fontFamily}_{fontSize}")
    {
        const string chars = " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~";
        
        _typeface = SKTypeface.FromFamilyName(fontFamily, bold ? SKFontStyle.Bold : SKFontStyle.Normal);

        using var paint = new SKPaint();
        paint.Typeface = _typeface;
        paint.TextSize = fontSize;
        paint.IsAntialias = true;
        paint.Color = SKColors.White;
        paint.Style = SKPaintStyle.Fill;


        _charInfos = [];
        float maxHeight = 0;
        foreach (var c in chars)
        {
            var bounds = new SKRect();
            var advance = paint.MeasureText(c.ToString(), ref bounds);
            _charInfos.Add((c, bounds, advance));
            maxHeight = Math.Max(maxHeight, bounds.Height);
        }
        
        // Calculate atlas dimensions
        _padding = 2;
        _charsPerRow = 16;
        var rows = (int)Math.Ceiling(_charInfos.Count / (float)_charsPerRow);
        
        _cellWidth = (int)Math.Ceiling(_charInfos.Max(info => info.bounds.Width)) + _padding * 2;
        _cellHeight = (int)Math.Ceiling(maxHeight) + _padding * 2;
        
        _atlasWidth = _cellWidth * _charsPerRow;
        _atlasHeight = _cellHeight * rows;
        
        // Build Characters dictionary with UV coordinates
        for (var i = 0; i < _charInfos.Count; i++)
        {
            var (c, bounds, advance) = _charInfos[i];
            
            var col = i % _charsPerRow;
            var row = i / _charsPerRow;
            
            // UV coordinates cover the character bounds
            var u0 = (col * _cellWidth + _padding) / (float)_atlasWidth;
            var v0 = (row * _cellHeight + _padding) / (float)_atlasHeight;
            var u1 = (col * _cellWidth + _padding + bounds.Width) / _atlasWidth;
            var v1 = (row * _cellHeight + _padding + bounds.Height) / _atlasHeight;
            
            Characters[c] = new CharacterInfo
            {
                U0 = u0,
                V0 = v0,
                U1 = u1,
                V1 = v1,
                Width = bounds.Width,
                Height = bounds.Height,
                CellWidth = _cellWidth,
                CellHeight = _cellHeight,
                OffsetX = bounds.Left,
                OffsetY = bounds.Top,
                AdvanceX = advance
            };
        }
        
        FontSize = paint.TextSize;
        LineHeight = maxHeight;
    }
    
    public override void Generate()
    {
        using var paint = new SKPaint();
        paint.Typeface = _typeface;
        paint.TextSize = FontSize;
        paint.IsAntialias = true;
        paint.Color = SKColors.White;
        paint.Style = SKPaintStyle.Fill;

        using var bitmap = new SKBitmap(_atlasWidth, _atlasHeight, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        
        for (var i = 0; i < _charInfos.Count; i++)
        {
            var (c, bounds, _) = _charInfos[i];
            
            var col = i % _charsPerRow;
            var row = i / _charsPerRow;
            
            var x = col * _cellWidth + _padding - bounds.Left;
            var y = row * _cellHeight + _padding - bounds.Top;
            
            canvas.DrawText(c.ToString(), x, y, paint);
        }
        
        canvas.Flush();
        
        base.Generate();
        Resize(_atlasWidth, _atlasHeight, bitmap.GetPixelSpan().ToArray());
        
        GL.TexParameter(Target, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapLinear);
        GL.TexParameter(Target, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        GL.TexParameter(Target, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        GL.TexParameter(Target, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
        GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
        
        Unbind();
    }
}
