using System.Text;
using OpenTK.Graphics.OpenGL4;
using Serilog;
using Snooper.Core.Containers.Buffers;
using Snooper.Rendering.Components;
using Snooper.Rendering.Components.Camera;
using Snooper.Rendering.Components.Primitive;

namespace Snooper.Core.Containers.Resources;

public class IndirectResources<TVertex, TInstanceData, TPerDrawData>(int initialDrawCapacity, PrimitiveType type)
    : IBind, IMemorySizeProvider, IDisposable
    where TVertex : unmanaged
    where TInstanceData : unmanaged, IPerInstanceData 
    where TPerDrawData : unmanaged, IPerDrawData
{
    private readonly DoubleBuffer<DrawIndirectBuffer> _commands = new(() => new DrawIndirectBuffer(initialDrawCapacity));
    private readonly ShaderStorageBuffer<TInstanceData> _instanceData = new(initialDrawCapacity);
    private readonly ShaderStorageBuffer<TPerDrawData> _drawData = new(initialDrawCapacity);
    
    private readonly CullingResources _culling = new(initialDrawCapacity);

    private readonly VertexArray _vao = new();
    public readonly ElementArrayBuffer<uint> EBO = new(initialDrawCapacity * 200);
    public readonly ArrayBuffer<TVertex> VBO = new(initialDrawCapacity * 100);
    
    public void Generate()
    {
        _commands.Generate();
        _instanceData.Generate();
        _drawData.Generate();
        
        _culling.Generate();
        
        _vao.Generate();
        EBO.Generate();
        VBO.Generate();
    }

    public void Bind()
    {
        _commands.Current.Bind();
        _instanceData.Bind();
        
        _vao.Bind();
        EBO.Bind();
        VBO.Bind();
    }
    
    public void Allocate(int componentCount, int drawCount, int indices, int vertices)
    {
        _culling.Allocate(componentCount, drawCount);
        
        _drawData.Bind();
        _drawData.Allocate(new TPerDrawData[drawCount]);
        _drawData.Unbind(); // instance ssbo is rebound here

        _commands.Current.Allocate(new DrawElementsIndirectCommand[drawCount]);
        _instanceData.Allocate(new TInstanceData[drawCount * 10]);
        
        EBO.Allocate(new uint[indices]);
        VBO.Allocate(new TVertex[vertices]);
    }

    public void Unbind()
    {
        _commands.Current.Unbind();
        _instanceData.Unbind();
        
        _vao.Unbind();
        EBO.Unbind();
        VBO.Unbind();
    }
    
    public void Add(LevelOfDetail<TVertex>[] levelOfDetails, MaterialSection[] materials, TInstanceData[] instanceData, CullingBounds bounds)
    {
        var (firstIndex, baseVertex, descriptor) = CreateDescriptor();
        var baseInstance = (uint)_instanceData.AddRange(instanceData);
        var modelId = (uint)_culling.Add(descriptor);
        var instanceCount = (uint)instanceData.Length;

        for (var i = 0u; i < materials.Length; i++)
        {
            materials[i].DrawMetadata.BaseInstance = (int)baseInstance;
            materials[i].DrawMetadata.DrawId = _commands.Current.Add(new DrawElementsIndirectCommand
            {
                IndexCount = levelOfDetails[0].SectionDescriptors[i].IndexCount,
                InstanceCount = instanceCount,
                FirstIndex = firstIndex + levelOfDetails[0].SectionDescriptors[i].FirstIndex,
                BaseVertex = baseVertex,
                BaseInstance = baseInstance,
                OriginalInstanceCount = instanceCount,
                OriginalBaseInstance = baseInstance,
                ModelId = modelId,
                SectionId = i,
            });
        }

        unsafe (uint, uint, PrimitiveDescriptor) CreateDescriptor()
        {
            var maxLod = 0u;
            var d = new PrimitiveDescriptor(bounds);
            for (var i = 0; i < levelOfDetails.Length && i < Settings.MaxNumberOfLods; i++)
            {
                if (!levelOfDetails[i].Primitive.IsValid)
                {
                    continue;
                    // throw new InvalidOperationException("Primitive data is not valid.");
                }
                
                d.LOD_FirstIndex[i] = (uint)EBO.AddRange(levelOfDetails[i].Primitive.Indices);
                d.LOD_BaseVertex[i] = (uint)VBO.AddRange(levelOfDetails[i].Primitive.Vertices);
                d.LOD_SectionCount[i] = (uint)levelOfDetails[i].SectionDescriptors.Length;
                d.LOD_SectionOffset[i] = (uint)_culling.Add(levelOfDetails[i].SectionDescriptors);
                maxLod++;
            }
            d.Bounds.MaxLevelOfDetail = Math.Min(maxLod, Settings.MaxNumberOfLods) - 1;
            return (d.LOD_FirstIndex[0], d.LOD_BaseVertex[0], d);
        }
    }

    public void Update(PrimitiveComponent<TVertex, TInstanceData, TPerDrawData> component)
    {
        if (!component.Actor.IsDirty || component.Materials.Length < 1) return;
        
        var metadata = component.Materials[0].DrawMetadata;
        _instanceData.Update(metadata.BaseInstance, component.GetPerInstanceData());
        component.Actor.MarkClean();
    }
    
    public void Update(int drawId, TPerDrawData drawData)
    {
        if (!drawData.IsReady) throw new InvalidOperationException("Draw data is not ready.");
        Log.Debug("Updating draw data for draw ID {DrawId}.", drawId);

        _drawData.Bind();
        _drawData.Update(drawId, drawData);
        _drawData.Unbind();
    }

    public void Update(int drawId, TVertex[] vertices)
    {
        var command = _commands.Current[drawId];
        VBO.Update((int) command.BaseVertex, vertices);
    }

    public void Update(int drawId, uint[] indices, TVertex[] vertices)
    {
        var command = _commands.Current[drawId];
        EBO.Update((int) command.FirstIndex, indices);
        VBO.Update((int) command.BaseVertex, vertices);

        _commands.Current.UpdateIndexCount(drawId, (uint) indices.Length);
    }

    public void Remove(IndirectDrawMetadata metadata)
    {
        Log.Debug("Removing draw data for draw ID {DrawId}.", metadata.DrawId);
        
        Bind();
        _commands.Current.Remove(metadata.DrawId);
        _instanceData.Remove(metadata.BaseInstance);
        // EBO.Remove();
        // VBO.Remove();
        Unbind();

        _drawData.Bind();
        _drawData.Remove(metadata.DrawId);
        _drawData.Unbind();
        
        // _culling.Remove();
    }

    public void Cull(CameraComponent camera) => _culling.Cull(camera, _instanceData, _commands.Current);

    public void Render()
    {
        _commands.Current.Bind();
        _instanceData.Bind(0);
        _drawData.Bind(1);
        _vao.Bind();

        GL.MultiDrawElementsIndirect(type, DrawElementsType.UnsignedInt, 0, _commands.Current.Count, _commands.Current.Stride);

        // _vao.Unbind();
        // EBO.Unbind();
        _commands.Current.Unbind();

        // _commands.Swap();
    }
    
    public void Dispose()
    {
        _commands.Dispose();
        _instanceData.Dispose();
        _culling.Dispose();
        _drawData.Dispose();
        
        _vao.Dispose();
        EBO.Dispose();
        VBO.Dispose();
    }

    public string GetFormattedSpace()
    {
        var builder = new StringBuilder();
        builder.AppendLine($"IndirectResources<{typeof(TVertex).Name}, {typeof(TInstanceData).Name}>:");
        builder.AppendLine($"    Commands:     {_commands.Current.GetFormattedSpace()}");
        builder.AppendLine($"    InstanceData: {_instanceData.GetFormattedSpace()}");
        builder.AppendLine($"    DrawData:     {_drawData.GetFormattedSpace()}");
        builder.AppendLine($"    Indices:      {EBO.GetFormattedSpace()}");
        builder.AppendLine($"    Vertices:     {VBO.GetFormattedSpace()}");
        return builder.ToString();
    }

    public GetPName Name => throw new NotImplementedException();
    public int PreviousHandle => throw new NotImplementedException();
}
