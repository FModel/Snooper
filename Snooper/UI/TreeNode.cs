using CUE4Parse_Conversion;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Actor;
using Newtonsoft.Json;

namespace Snooper.UI;

public abstract class TreeNode : IControllable, ICloneable, IEquatable<TreeNode>
{
    public string Name { get; internal set; }
    public string Type { get; }
    public string? Class { get; }
    public string? Path { get; }
    public string[]? JsonProperties { get; }

    public virtual string Icon { get; private set; } = "\uf550";

    public bool IsNodeOpen { get; set; }
    public bool IsNodeSelected { get; set; }
    public int NodeDepth { get; set; }
    public int NodeIndex { get; set; }

    protected TreeNode(TreeNode other)
    {
        Name = other.Name + " (Copy)";
        Type = other.Type;
        Class = other.Class;
        Path = other.Path;
        JsonProperties = other.JsonProperties;
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
        Path = owner.Owner?.Provider?.FixPath(owner.Owner?.Name ?? owner.GetPathName());

        var jsonProperties = new List<string> { JsonConvert.SerializeObject(owner, Formatting.Indented) };
        var templatePtr = owner.Template;
        while (templatePtr?.TryLoad(out var template) == true)
        {
            jsonProperties.Add(JsonConvert.SerializeObject(template, Formatting.Indented));
            templatePtr = template.Template;
        }
        JsonProperties = jsonProperties.ToArray();
    }

    public abstract void Export(ExportSession session, CancellationToken ct = default);

    protected void SetIcon(string icon) => Icon = icon;

    public abstract int Id { get; }
    public abstract void SetOutlined(bool state);
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
