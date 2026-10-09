using System.Numerics;

namespace Snooper.Rendering.Components.Transforms;

public sealed class InstancedTransforms
{
    private readonly Transform[] _locals;
    private readonly Transform[] _originals;
    private readonly bool[] _dirty;

    public int Count => _locals.Length;

    public InstancedTransforms(Transform[] transforms)
    {
        // add a dummy instance to avoid issues with empty instance arrays
        // this also makes it possible to toggle visibility on/off without having to add/remove instances
        _locals = transforms.Length > 0 ? transforms : [Transform.Identity];
        _originals = (Transform[]) _locals.Clone();
        _dirty = new bool[_locals.Length];
    }

    public void Set(int index, Transform transform)
    {
        _locals[index] = transform;
        _dirty[index] = true;
    }

    public void Reset(int index)
    {
        var original = _originals[index];
        _locals[index].Position = original.Position;
        _locals[index].Rotation = original.Rotation;
        _locals[index].Scale = original.Scale;
        _dirty[index] = false;
    }

    public bool IsDirty(int index) => _dirty[index];

    public Matrix4x4[] GetWorldMatrices(Matrix4x4 world, int index = -1)
    {
        if (index >= 0 && index < _locals.Length)
            return [_locals[index].ToMatrix() * world];

        var matrices = new Matrix4x4[_locals.Length];
        for (var i = 0; i < matrices.Length; i++)
        {
            matrices[i] = _locals[i].ToMatrix() * world;
        }
        return matrices;
    }

    public Transform this[int index] => _locals[index];
}
