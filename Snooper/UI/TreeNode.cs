using CUE4Parse_Conversion;
using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Actor;
using CUE4Parse.UE4.Objects.UObject;
using Snooper.Extensions;
using Newtonsoft.Json;

namespace Snooper.UI;

public abstract class TreeNode : IControllable, ICloneable, IEquatable<TreeNode>
{
    public string Name { get; internal set; }
    public string Type { get; }
    public string? Class { get; }
    public string? Path { get; }

    private readonly FSoftObjectPath? _object;

    public virtual string Icon { get; private set; } = "\uf550";

    public bool IsNodeOpen { get; set; }
    public virtual bool IsNodeSelected { get; set; }
    public int NodeDepth { get; set; }
    public int NodeIndex { get; set; }

    protected TreeNode(TreeNode other)
    {
        Name = other.Name + " (Copy)";
        Type = other.Type;
        Class = other.Class;
        Path = other.Path;

        _object = other._object;
    }

    protected TreeNode(string name)
    {
        Name = name;
        Type = GetType().Name;
    }

    protected TreeNode(UObject owner) : this(owner.Name)
    {
        if (owner is AActor a && !string.IsNullOrEmpty(a.ActorLabel))
        {
            Name = a.ActorLabel;
        }

        Class = owner.ExportType;
        Path = owner.GetCleanPath();

        _object = owner.ToSoftObjectPath();
    }

    public abstract void Export(ExportSession session, CancellationToken ct = default);

    private Task<string[]>? _json;
    public Task<string[]>? GetJsonProperties(IFileProvider provider)
    {
        if (_object is not { } path) return null;
        return _json ??= Task.Run(Serialize);

        string[] Serialize()
        {
            if (!path.TryLoad<UObject>(provider, out var owner)) return [];

            var jsonProperties = new List<string> { JsonConvert.SerializeObject(owner, Formatting.Indented) };
            var templatePtr = owner.Template;
            while (templatePtr?.TryLoad(out var template) == true)
            {
                jsonProperties.Add(JsonConvert.SerializeObject(template, Formatting.Indented));
                templatePtr = template.Template;
            }
            return [.. jsonProperties];
        }
    }

    protected void SetIcon(string icon) => Icon = icon;

    private static int _nextId;
    public int Id { get; } = Interlocked.Increment(ref _nextId);
    public abstract bool ShouldScrollHere { get; set; }
    public abstract void DrawControls();

    public abstract object Clone();

    public static bool operator ==(TreeNode? left, TreeNode? right)
    {
        if (left is null && right is null) return true;
        if (left is null || right is null) return false;
        return left.Id == right.Id;
    }
    public static bool operator !=(TreeNode? left, TreeNode? right) => !(left == right);
    public bool Equals(TreeNode? other) => this == other;
    public override int GetHashCode() => Id.GetHashCode();
    public override bool Equals(object? obj) => obj is TreeNode node && this == node;
}
