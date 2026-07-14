using System.Numerics;
using OpenTK.Graphics.OpenGL4;
using Snooper.Core.Containers.Buffers;
using Snooper.Core.Containers.Programs;
using Snooper.Rendering.Components.Camera;
using Snooper.Rendering.Components.Descriptors;

namespace Snooper.Core.Containers.Resources;

public class CullingResources : IMemoryDetailsProvider, IDisposable
{
    private readonly ShaderStorageBuffer<PrimitiveOffsets> _primitives = new();
    private readonly ShaderStorageBuffer<SectionOffsets> _sections = new();
    private readonly ComputeShader _compute = new("culling.comp");

    public void Generate()
    {
        _primitives.Generate();
        _sections.Generate();

        _compute.Generate();
        _compute.Link();
    }

    public void Allocate(AllocationCounts counts)
    {
        if (counts.UniqueComponents > 0) _primitives.Allocate(counts.UniqueComponents);
        if (counts.Sections > 0) _sections.Allocate(counts.Sections);
    }

    public BufferAllocation Add(SectionDescriptor[] sections)
    {
        var offsets = new SectionOffsets[sections.Length];
        for (var i = 0; i < sections.Length; i++)
        {
            offsets[i] = new SectionOffsets(sections[i]);
        }

        return _sections.AddRange(offsets);
    }

    public BufferAllocation Add(PrimitiveOffsets offsets) => _primitives.Add(offsets);

    public void UpdateOverrideLod(BufferAllocation allocation, int overrideLod)
    {
        _primitives.UpdateCustom(allocation, overrideLod, 40);
    }

    public void Cull<TInstanceData>(IViewProjectionProvider camera, ShaderStorageBuffer<TInstanceData> instances, DrawIndirectBuffer commands, bool shadowPass = false) where TInstanceData : unmanaged, IPerInstanceData
    {
        var matrix = camera.ViewMatrix * camera.ProjectionMatrix;
        var planes = new Plane[6];
        planes[0] = new Plane(matrix.M14 + matrix.M11, matrix.M24 + matrix.M21, matrix.M34 + matrix.M31, matrix.M44 + matrix.M41); // Near
        planes[1] = new Plane(matrix.M14 - matrix.M11, matrix.M24 - matrix.M21, matrix.M34 - matrix.M31, matrix.M44 - matrix.M41); // Far
        planes[2] = new Plane(matrix.M14 + matrix.M12, matrix.M24 + matrix.M22, matrix.M34 + matrix.M32, matrix.M44 + matrix.M42); // Left
        planes[3] = new Plane(matrix.M14 - matrix.M12, matrix.M24 - matrix.M22, matrix.M34 - matrix.M32, matrix.M44 - matrix.M42); // Right
        planes[4] = new Plane(matrix.M14 + matrix.M13, matrix.M24 + matrix.M23, matrix.M34 + matrix.M33, matrix.M44 + matrix.M43); // Bottom
        planes[5] = new Plane(matrix.M14 - matrix.M13, matrix.M24 - matrix.M23, matrix.M34 - matrix.M33, matrix.M44 - matrix.M43); // Top

        _compute.Use();
        _compute.SetUniform("uFrustumPlanes", planes);
        _compute.SetUniform("uProjectionMatrix", camera.ProjectionMatrix);
        _compute.SetUniform("uCameraPosition", camera.InverseViewMatrix.Translation);
        _compute.SetUniform("uShadowPass", shadowPass);

        commands.Bind(0);
        instances.Bind(1);
        _primitives.Bind(2);
        _sections.Bind(3);

        GL.DispatchCompute(commands.Capacity, 1, 1);
        GL.MemoryBarrier(MemoryBarrierFlags.CommandBarrierBit | MemoryBarrierFlags.ShaderStorageBarrierBit);
        _compute.Unuse();
    }

    public void Remove(int index)
    {
        // _primitives.Bind();
        // _primitives.Remove();
        // _primitives.Unbind();
        //
        // _sections.Bind();
        // _sections.Remove();
        // _sections.Unbind();
    }

    public void Dispose()
    {
        _primitives.Dispose();
        _sections.Dispose();
        _compute.Dispose();
    }

    public long Allocated
    {
        get
        {
            long total = 0;
            total += _primitives.Allocated;
            total += _sections.Allocated;
            total += _compute.Allocated;
            return total;
        }
    }

    public long Used
    {
        get
        {
            long total = 0;
            total += _primitives.Used;
            total += _sections.Used;
            total += _compute.Used;
            return total;
        }
    }

    public IEnumerable<MemoryDetail> GetMemoryDetails()
    {
        yield return new MemoryDetail("Primitive Offsets", _primitives);
        yield return new MemoryDetail("Section Offsets", _sections);
        yield return new MemoryDetail("Culling Compute Shader", _compute);
    }
}
