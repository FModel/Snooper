using System.Numerics;
using System.Runtime.InteropServices;
using OpenTK.Graphics.OpenGL4;
using Snooper.Core.Containers;
using Snooper.Core.Containers.Buffers;
using Snooper.Core.Containers.Programs;
using Snooper.Core.Containers.Textures;
using Snooper.Rendering.Components.Camera;
using Snooper.Rendering.Components.Light;
using Snooper.Rendering.Systems;
using Snooper.UI;

namespace Snooper.Rendering.Containers.Framebuffers;

public class ShadowFramebuffer(int resolution = Settings.ShadowResolution, int cascadeCount = Settings.MaxShadowCascades) : Framebuffer<EShadowTexture>, IControllable
{
    [StructLayout(LayoutKind.Sequential)]
    private readonly struct ShadowViewData(ShadowMapView view)
    {
        public readonly Matrix4x4 ViewProjection = view.ViewProjection;
        public readonly float TexelWorldSize = view.TexelWorldSize;
        public readonly float DepthScale = view.DepthScale;
        public readonly float SplitFar = view.SplitFar;
        public readonly uint Slot = (uint) view.Slot;
    }

    private const int MaxSlots = Settings.MaxShadowViews < 32 ? Settings.MaxShadowViews : 32;

    private static readonly int[] _resolutions = [512, 1024, 2048, 4096];

    public float Softness = 1.0f;
    public float NormalOffset = 1.0f;
    public float Blend = 0.1f;
    public float SlopeBias = 2.0f;
    public float ConstantBias = 1.0f;
    public bool StaggerUpdates = true;

    public override int Width => _atlas.Width;
    public override int Height => _atlas.Height;
    public int SlotCount => _atlas.Depth;
    public int CascadeCount => _sun.CascadeCount;

    private Texture2DArray _atlas = CreateAtlas(resolution, Math.Clamp(cascadeCount, 1, MaxSlots));
    private readonly SunCascades _sun = new(cascadeCount);
    private readonly ShaderStorageBuffer<ShadowViewData> _viewBuffer = new(BufferUsageHint.DynamicDraw);
    private readonly ShadowViewData[] _viewData = new ShadowViewData[MaxSlots];
    private BufferAllocation _viewAllocation;
    private uint _compareSampler;

    private uint _usedSlots;
    private uint _renderMask;
    private ulong _frame;

    private int _resolutionIndex = Math.Max(0, Array.IndexOf(_resolutions, resolution));
    private int? _pendingResolution;
    private int? _pendingCascadeCount;

    private static Texture2DArray CreateAtlas(int resolution, int slots) => new(resolution, resolution, slots, SizedInternalFormat.DepthComponent32f, PixelFormat.DepthComponent, PixelType.Float, "Shadow - Depth");

    public override void Generate()
    {
        GL.CreateSamplers(1, out _compareSampler);
        GL.SamplerParameter(_compareSampler, SamplerParameterName.TextureMinFilter, (int) TextureMinFilter.Linear);
        GL.SamplerParameter(_compareSampler, SamplerParameterName.TextureMagFilter, (int) TextureMagFilter.Linear);
        GL.SamplerParameter(_compareSampler, SamplerParameterName.TextureWrapS, (int) TextureWrapMode.ClampToBorder);
        GL.SamplerParameter(_compareSampler, SamplerParameterName.TextureWrapT, (int) TextureWrapMode.ClampToBorder);
        GL.SamplerParameter(_compareSampler, SamplerParameterName.TextureBorderColor, [1.0f, 1.0f, 1.0f, 1.0f]);
        GL.SamplerParameter(_compareSampler, SamplerParameterName.TextureCompareMode, (int) TextureCompareMode.CompareRefToTexture);
        GL.SamplerParameter(_compareSampler, SamplerParameterName.TextureCompareFunc, (int) DepthFunction.Less);

        _viewBuffer.Generate();
        _viewAllocation = _viewBuffer.AddRange(_viewData);

        base.Generate();

        GenerateAtlas();
        AllocateSunSlots();
    }

    private void GenerateAtlas()
    {
        _atlas.Generate();
        _atlas.Reset<int>(Width, Height, []);

        GL.TextureParameter(_atlas, TextureParameterName.TextureMinFilter, (int) TextureMinFilter.Nearest);
        GL.TextureParameter(_atlas, TextureParameterName.TextureMagFilter, (int) TextureMagFilter.Nearest);
        GL.TextureParameter(_atlas, TextureParameterName.TextureWrapS, (int) TextureWrapMode.ClampToBorder);
        GL.TextureParameter(_atlas, TextureParameterName.TextureWrapT, (int) TextureWrapMode.ClampToBorder);
        GL.TextureParameter(_atlas, TextureParameterName.TextureBorderColor, [1.0f, 1.0f, 1.0f, 1.0f]);

        var clearDepth = 1.0f;
        GL.ClearTexImage(_atlas, 0, PixelFormat.DepthComponent, PixelType.Float, ref clearDepth);

        BindSlot(0);
        GL.NamedFramebufferDrawBuffer(Handle, DrawBufferMode.None);
        GL.NamedFramebufferReadBuffer(Handle, ReadBufferMode.None);

        CheckStatus();
    }

    public void BindSlot(int slot) => GL.NamedFramebufferTextureLayer(Handle, FramebufferAttachment.DepthAttachment, _atlas, 0, slot);

    public void ApplyPendingChanges()
    {
        if (_pendingCascadeCount is { } cascadeCount)
        {
            _pendingCascadeCount = null;

            FreeSlots(_sun.FirstSlot, _sun.CascadeCount);
            _sun.SetCascadeCount(cascadeCount);
            AllocateSunSlots();
        }

        if (_pendingResolution is { } newResolution)
        {
            _pendingResolution = null;

            if (newResolution != Width)
            {
                RebuildAtlas(newResolution, SlotCount);
            }
        }
    }

    public ShadowMapView[] UpdateSun(CameraComponent camera, DirectionalLightComponent light)
    {
        var views = _sun.Update(camera, light, Width, StaggerRun(_sun.FirstSlot, _sun.CascadeCount));

        foreach (var view in views)
        {
            _viewData[view.Slot] = new ShadowViewData(view);
        }
        _viewBuffer.Update(_viewAllocation, _viewData);

        _frame++;
        return views;
    }

    public bool NeedsRender(int slot) => (_renderMask & (1u << slot)) != 0;

    internal void BindForRendering(ShaderProgram shader, uint unit)
    {
        _atlas.Bind(unit);
        GL.BindSampler(unit, _compareSampler);
        _viewBuffer.Bind(ClusteredLightSystem.LightBindings.ShadowViews);

        shader.SetUniform("shadowMap", (int) unit);
        shader.SetUniform("uShadowCascadeCount", _sun.CascadeCount);
        shader.SetUniform("uShadowSoftness", Softness);
        shader.SetUniform("uShadowNormalOffset", NormalOffset);
        shader.SetUniform("uShadowBlend", Blend);
    }

    public override void Bind(EShadowTexture texture, uint unit)
    {
        if (texture != EShadowTexture.Depth)
            throw new ArgumentOutOfRangeException(nameof(texture), texture, "Invalid shadow texture type");

        _atlas.Bind(unit);
    }

    private void AllocateSunSlots()
    {
        if (!TryAllocateSlots(_sun.CascadeCount, out var firstSlot))
            throw new InvalidOperationException($"Not enough shadow map slots for {_sun.CascadeCount} sun cascades.");

        _sun.FirstSlot = firstSlot;
    }

    private bool TryAllocateSlots(int count, out int firstSlot)
    {
        firstSlot = -1;
        if (count is <= 0 or > MaxSlots) return false;

        var mask = SlotMask(count);
        for (var start = 0; start + count <= MaxSlots; start++)
        {
            if ((_usedSlots & (mask << start)) != 0) continue;
            if (!TryGrow(start + count)) return false;

            _usedSlots |= mask << start;
            firstSlot = start;
            return true;
        }

        return false;
    }

    private void FreeSlots(int firstSlot, int count)
    {
        if (firstSlot < 0 || count <= 0) return;
        _usedSlots &= ~(SlotMask(count) << firstSlot);
    }

    private bool TryGrow(int slots)
    {
        if (slots <= SlotCount) return true;
        if (slots > MaxSlots) return false;

        RebuildAtlas(Width, slots);
        return true;
    }

    private void RebuildAtlas(int resolution, int slots)
    {
        _atlas.Dispose();
        _atlas = CreateAtlas(resolution, slots);

        GenerateAtlas();
        _renderMask = _usedSlots;
    }

    private uint SlotMask(int count) => (uint) ((1UL << count) - 1UL);

    private uint StaggerRun(int firstSlot, int count)
    {
        var local = 0u;
        for (var i = 0; i < count; i++)
        {
            if (!StaggerUpdates || i == 0 || _frame % (1UL << i) == 0)
            {
                local |= 1u << i;
            }
        }

        _renderMask = (_renderMask & ~(SlotMask(count) << firstSlot)) | (local << firstSlot);
        return local;
    }

    public override void Resize(int newWidth, int newHeight)
    {
        // shadow map size is fixed
    }

    public override Texture[] GetTextures() => [];

    public void DrawControls()
    {
        EditorUI.PropertyValueTable("Shadows", () =>
        {
            if (EditorUI.SliderInt("Resolution", ref _resolutionIndex, 0, _resolutions.Length - 1, $"{_resolutions[_resolutionIndex]} px"))
            {
                _pendingResolution = _resolutions[_resolutionIndex];
            }

            var cascades = _sun.CascadeCount;
            if (EditorUI.SliderInt("Cascades", ref cascades, 1, Settings.MaxShadowCascades, "%d"))
            {
                _pendingCascadeCount = cascades;
            }

            _sun.DrawControls();

            EditorUI.DragFloat("Softness", ref Softness, 0.05f, 0.0f, 4.0f, "%.2f texels");
            EditorUI.DragFloat("Normal Offset", ref NormalOffset, 0.05f, 0.0f, 4.0f, "%.2f");
            EditorUI.DragFloat("Blend", ref Blend, 0.01f, 0.0f, 0.5f, "%.2f");
            EditorUI.DragFloat("Slope Bias", ref SlopeBias, 0.05f, 0.0f, 16.0f, "%.2f");
            EditorUI.DragFloat("Constant Bias", ref ConstantBias, 0.05f, 0.0f, 16.0f, "%.2f");
            EditorUI.Checkbox("Stagger Updates", ref StaggerUpdates);

            EditorUI.Text("Slots", $"{BitOperations.PopCount(_usedSlots)}/{SlotCount}");
            EditorUI.Text("Splits", $"{string.Join(", ", _sun.Splits)} units");
            EditorUI.Text("Texel Size", $"{_sun.Views[0].TexelWorldSize:F3} .. {_sun.Views[^1].TexelWorldSize:F3} units");
        });
    }

    public override long Allocated
    {
        get
        {
            long total = 0;
            total += _atlas.Allocated;
            total += _viewBuffer.Allocated;
            return total;
        }
    }

    public override long Used
    {
        get
        {
            long total = 0;
            total += _atlas.Used;
            total += _viewBuffer.Used;
            return total;
        }
    }

    public override IEnumerable<MemoryDetail> GetMemoryDetails()
    {
        yield return new MemoryDetail("Shadow Atlas", _atlas);
        yield return new MemoryDetail("Shadow Views", _viewBuffer);
    }

    public override void Dispose()
    {
        base.Dispose();

        GL.DeleteSampler(_compareSampler);
        _viewBuffer.Dispose();
        _atlas.Dispose();
    }
}
