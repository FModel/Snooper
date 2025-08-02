using Snooper.Rendering.Primitives;

namespace Snooper.Rendering.Components.Primitive;

public class LevelOfDetail<TVertex>(TPrimitiveData<TVertex> primitive, PrimitiveSectionDescriptor[] sectionDescriptors) where TVertex : unmanaged
{
    public TPrimitiveData<TVertex> Primitive { get; } = primitive;
    public PrimitiveSectionDescriptor[] SectionDescriptors { get; } = sectionDescriptors;
    
    public LevelOfDetail(TPrimitiveData<TVertex> primitive) : this(primitive, [new PrimitiveSectionDescriptor(0, (uint)primitive.Indices.Length, 0)])
    {
        // if no section is provided, create one for the entire primitive
    }
}