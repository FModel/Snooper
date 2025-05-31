using CUE4Parse.UE4.Objects.Core.Math;
using Snooper.Rendering.Components.Culling;
using Snooper.Rendering.Primitives;

namespace Snooper.Rendering.Components.Mesh;

public class MeshComponent(IPrimitiveData primitive, FBox box) : BoxCullingComponent(primitive, box);
