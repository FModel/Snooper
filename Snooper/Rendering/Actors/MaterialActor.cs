using CUE4Parse_Conversion;
using CUE4Parse.UE4.Assets.Exports.Material;
using CUE4Parse.UE4.Assets.Exports.StaticMesh;
using CUE4Parse.UE4.Objects.UObject;
using Snooper.Rendering.Components.Mesh;
using Snooper.Rendering.Components.Transforms;
using Snooper.UI;

namespace Snooper.Rendering.Actors;

public class MaterialActor : Actor
{
    private readonly UMaterialInterface _material;

    public MaterialActor(UMaterialInterface material, Transform? transform = null) : base(material)
    {
        _material = material;

        if (_material.Owner?.Provider == null || !_material.Owner.Provider.TryLoadPackageObject<UStaticMesh>("Engine/Content/BasicShapes/Sphere.Sphere", out var sphere))
            return;

        var exportIndex = _material.Owner.GetExportIndex(material.Name);
        if (exportIndex < 0)
            return;

        Components.Add(new StaticMeshComponent(sphere, transform, [new FPackageIndex(_material.Owner, exportIndex + 1)]));
        WindowRequests.Request(Settings.MaterialInspectorWindow);
    }

    public override void Export(ExportSession session, CancellationToken ct = default)
    {
        session.Add(_material);
    }
}
