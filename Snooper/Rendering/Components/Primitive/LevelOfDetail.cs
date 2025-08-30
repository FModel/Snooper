using CUE4Parse.UE4.Objects.Core.Misc;
using Snooper.Rendering.Primitives;

namespace Snooper.Rendering.Components.Primitive;

public class LevelOfDetail<TVertex>(FGuid guid, TPrimitiveData<TVertex> primitive, float screenSize, PrimitiveSectionDescriptor[] sectionDescriptors) : IDisposable where TVertex : unmanaged
{
    public FGuid Guid { get; } = guid;
    public TPrimitiveData<TVertex> Primitive { get; } = primitive;
    public float ScreenSize { get; } = screenSize;
    public PrimitiveSectionDescriptor[] SectionDescriptors { get; } = sectionDescriptors;
    
    public LevelOfDetail(FGuid guid, TPrimitiveData<TVertex> primitive) : this(guid, primitive, 0f, [new PrimitiveSectionDescriptor(0, (uint)primitive.Indices.Length, 0)])
    {
        // if no section is provided, create one for the entire primitive
    }

    public void Dispose()
    {
        Primitive.Dispose();
    }
}