using System.Numerics;
using OpenTK.Graphics.OpenGL4;
using Snooper.Core.Containers.Buffers;
using Snooper.Core.Containers.Programs;
using Snooper.Rendering.Components.Camera;
using Snooper.Rendering.Systems;

namespace Snooper.Rendering.Containers;

public class LightClusterManager : IDisposable
{
    private readonly ShaderStorageBuffer<ClusterAABB> _clusterAABBBuffer = new(BufferUsageHint.DynamicDraw);
    private readonly ShaderStorageBuffer<ClusterData> _clusterDataBuffer = new(BufferUsageHint.DynamicDraw);
    private readonly ShaderStorageBuffer<uint> _lightIndexListBuffer = new(BufferUsageHint.DynamicDraw);
    private readonly ShaderStorageBuffer<uint> _globalIndexCountBuffer = new(BufferUsageHint.DynamicDraw);

    private readonly EmbeddedShaderProgram _clusterBuildProgram = new(string.Empty, string.Empty) { Compute = "Lighting/cluster_build.comp" };
    private readonly EmbeddedShaderProgram _lightCullingProgram = new(string.Empty, string.Empty) { Compute = "Lighting/light_culling.comp" };

    private int _screenWidth = 1;
    private int _screenHeight = 1;
    private int _gridDimX;
    private int _gridDimY;
    private int _gridDimZ = ClusteringConstants.ZSlices;
    private int _totalClusters;

    private BufferAllocation? _globalIndexCountAllocation;

    public void Generate()
    {
        _clusterAABBBuffer.Generate();
        _clusterDataBuffer.Generate();
        _lightIndexListBuffer.Generate();

        _globalIndexCountBuffer.Generate();
        _globalIndexCountAllocation = _globalIndexCountBuffer.Add(0u);

        _clusterBuildProgram.Generate();
        _clusterBuildProgram.Link();

        _lightCullingProgram.Generate();
        _lightCullingProgram.Link();
    }

    public void Resize(int width, int height)
    {
        if (_screenWidth == width && _screenHeight == height) return;

        _screenWidth = width;
        _screenHeight = height;

        // Calculate grid dimensions based on 32-pixel tiles
        _gridDimX = (width + ClusteringConstants.TileSize - 1) / ClusteringConstants.TileSize;
        _gridDimY = (height + ClusteringConstants.TileSize - 1) / ClusteringConstants.TileSize;
        _totalClusters = _gridDimX * _gridDimY * _gridDimZ;

        // Reallocate cluster buffers
        _clusterAABBBuffer.Reallocate(_totalClusters);
        _clusterDataBuffer.Reallocate(_totalClusters);
        _lightIndexListBuffer.Reallocate(_totalClusters * ClusteringConstants.MaxLightsPerCluster);
    }

    public void BuildClusters(CameraComponent camera)
    {
        if (_totalClusters == 0) return;

        _clusterBuildProgram.Use();
        _clusterBuildProgram.SetUniform("uScreenWidth", _screenWidth);
        _clusterBuildProgram.SetUniform("uScreenHeight", _screenHeight);
        _clusterBuildProgram.SetUniform("uGridDimX", _gridDimX);
        _clusterBuildProgram.SetUniform("uGridDimY", _gridDimY);
        _clusterBuildProgram.SetUniform("uGridDimZ", _gridDimZ);
        _clusterBuildProgram.SetUniform("uZNear", camera.NearPlaneDistance);
        _clusterBuildProgram.SetUniform("uZFar", camera.FarPlaneDistance);

        Matrix4x4.Invert(camera.ProjectionMatrix, out var invProj);
        _clusterBuildProgram.SetUniform("uInverseProjectionMatrix", invProj);

        _clusterAABBBuffer.Bind(0);

        // Dispatch one thread per cluster with work group size of 64
        int workGroupSize = 64;
        int numWorkGroups = (_totalClusters + workGroupSize - 1) / workGroupSize;

        GL.DispatchCompute(numWorkGroups, 1, 1);
        GL.MemoryBarrier(MemoryBarrierFlags.ShaderStorageBarrierBit);
    }

    public void CullLights(CameraComponent camera, ShaderStorageBuffer<LightData> lightDataBuffer)
    {
        if (_totalClusters == 0 || lightDataBuffer.Count == 0)
        {
            return;
        }

        // Reset global index counter
        if (_globalIndexCountAllocation.HasValue)
        {
            _globalIndexCountBuffer.Update(_globalIndexCountAllocation.Value, 0u);
        }

        _lightCullingProgram.Use();
        _lightCullingProgram.SetUniform("uLightCount", lightDataBuffer.Count);
        _lightCullingProgram.SetUniform("uGridDimX", _gridDimX);
        _lightCullingProgram.SetUniform("uGridDimY", _gridDimY);
        _lightCullingProgram.SetUniform("uGridDimZ", _gridDimZ);
        _lightCullingProgram.SetUniform("uViewMatrix", camera.ViewMatrix);

        lightDataBuffer.Bind(0);
        _clusterAABBBuffer.Bind(1);
        _clusterDataBuffer.Bind(2);
        _lightIndexListBuffer.Bind(3);
        _globalIndexCountBuffer.Bind(4);

        // Each workgroup processes ONE cluster with 8x8x8=512 threads cooperating
        // We need to dispatch one workgroup per cluster
        GL.DispatchCompute(_gridDimX, _gridDimY, _gridDimZ);
        GL.MemoryBarrier(MemoryBarrierFlags.ShaderStorageBarrierBit);
    }

    public void BindForRendering(ShaderStorageBuffer<LightData> lightDataBuffer)
    {
        lightDataBuffer.Bind(6);
        _clusterDataBuffer.Bind(7);
        _lightIndexListBuffer.Bind(8);
    }

    public int GetGridDimX() => _gridDimX;
    public int GetGridDimY() => _gridDimY;
    public int GetGridDimZ() => _gridDimZ;

    public void Dispose()
    {
        _clusterAABBBuffer?.Dispose();
        _clusterDataBuffer?.Dispose();
        _lightIndexListBuffer?.Dispose();
        _globalIndexCountBuffer?.Dispose();
        _clusterBuildProgram?.Dispose();
        _lightCullingProgram?.Dispose();
    }
}
