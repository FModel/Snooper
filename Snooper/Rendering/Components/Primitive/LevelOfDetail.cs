using CUE4Parse.UE4.Objects.Core.Misc;
using Snooper.Rendering.Primitives;

namespace Snooper.Rendering.Components.Primitive;

public class LevelOfDetail<TVertex> where TVertex : unmanaged
{
    public FGuid Guid { get; }
    public float ScreenSize { get; }
    public uint IndexCount { get; }
    public uint VertexCount { get; }
    public PrimitiveSectionDescriptor[] SectionDescriptors { get; }
    
    private readonly Func<TPrimitiveData<TVertex>> _primitiveFactory;
    private TPrimitiveData<TVertex>? _primitive;
    
    public LevelOfDetail(FGuid guid, float screenSize, int indexCount, int vertexCount, PrimitiveSectionDescriptor[] descriptors, Func<TPrimitiveData<TVertex>> factory)
    {
        Guid = guid;
        ScreenSize = screenSize;
        IndexCount = (uint)indexCount;
        VertexCount = (uint)vertexCount;

        SectionDescriptors = descriptors;
        _primitiveFactory = factory;
    }
    
    public LevelOfDetail(FGuid guid, int indexCount, int vertexCount, Func<TPrimitiveData<TVertex>> factory) : this(guid, 0f, indexCount, vertexCount, [new PrimitiveSectionDescriptor(0, (uint)indexCount, 0)], factory)
    {
        // if no section is provided, create one for the entire primitive
    }

    /// <summary>
    /// Creates the primitive data. This should only be called once by the GeometryPool.
    /// </summary>
    internal TPrimitiveData<TVertex> CreatePrimitive()
    {
        if (_primitive != null)
            return _primitive;
            
        _primitive = _primitiveFactory();
        return _primitive;
    }
}