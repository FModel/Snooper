using System.Numerics;
using System.Reflection;
using System.Text;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Component;
using ImGuiNET;
using Serilog;
using Snooper;
using Snooper.Hosting;
using Snooper.Rendering.Actors;
using Snooper.Rendering.Components;
using Snooper.Rendering.Components.Transforms;
using Snooper.UI;

namespace Editor.Modals;

/// <summary>
/// Add a component to an actor
/// </summary>
public sealed class AddComponentModal
{
    public static AddComponentModal Instance { get; } = new();

    private const string Title = "New Component";

    private sealed record Parameter(string Label, Type Type, Type? Nullable, object? Default, bool Optional);
    private sealed record Constructor(ConstructorInfo Info, string Signature, Parameter[] Parameters);
    private sealed record Entry(string Group, string Name, Constructor[] Constructors);

    private static readonly Lazy<Entry[]> _catalogue = new(Reflect);

    private Actor? _actor;
    private Entry? _entry;
    private Constructor? _constructor;
    private object?[] _values = [];
    private bool _openPopup;

    public void DrawMenu(Actor actor)
    {
        string? group = null;
        var open = false;
        foreach (var entry in _catalogue.Value)
        {
            if (entry.Group != group)
            {
                if (open) ImGui.EndMenu();
                group = entry.Group;
                open = ImGui.BeginMenu(group);
            }

            if (open && ImGui.MenuItem(entry.Name)) Open(actor, entry);
        }

        if (open) ImGui.EndMenu();
    }

    private void Open(Actor actor, Entry entry)
    {
        _actor = actor;
        _entry = entry;
        _openPopup = true;
        Select(entry.Constructors[0]);
    }

    private void Select(Constructor constructor)
    {
        _constructor = constructor;
        _values = new object?[constructor.Parameters.Length];
        for (var i = 0; i < _values.Length; i++)
        {
            _values[i] = constructor.Parameters[i].Default;
        }
    }

    public void Draw()
    {
        if (_openPopup)
        {
            ImGui.OpenPopup(Title);
            _openPopup = false;
        }

        if (_entry is not { } entry || _actor is not { } actor || _constructor is not { } constructor) return;

        var viewport = ImGui.GetMainViewport();
        ImGui.SetNextWindowSize(new Vector2(viewport.WorkSize.X * 0.25f, 0), ImGuiCond.Always);
        ImGui.SetNextWindowPos(viewport.GetCenter(), ImGuiCond.Always, new Vector2(0.5f, 0.5f));
        if (!ImGui.BeginPopupModal(Title, ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove)) return;

        ImGui.TextDisabled($"{entry.Name} of {actor.Name}");

        if (entry.Constructors.Length > 1)
        {
            ImGui.SetNextItemWidth(-1);
            if (ImGui.BeginCombo("##Constructor", constructor.Signature))
            {
                foreach (var candidate in entry.Constructors)
                {
                    if (ImGui.Selectable(candidate.Signature, candidate == constructor)) Select(candidate);
                }
                ImGui.EndCombo();
            }
        }

        var parameters = constructor.Parameters;
        if (parameters.Length > 0)
        {
            EditorUI.PropertyValueTable("NewComponent", () =>
            {
                for (var i = 0; i < parameters.Length; i++)
                {
                    ImGui.PushID(i);
                    DrawParameter(entry, parameters[i], i);
                    ImGui.PopID();
                }
            }, false);
        }

        var complete = true;
        for (var i = 0; i < parameters.Length; i++)
        {
            complete &= _values[i] is not null || parameters[i].Optional;
        }

        ImGui.Spacing(); ImGui.Separator(); ImGui.Spacing();

        var size = new Vector2((ImGui.GetContentRegionAvail().X - ImGui.GetStyle().ItemSpacing.X) * 0.5f, 0);
        ImGui.BeginDisabled(!complete);
        var close = ImGui.Button("OK", size);
        if (close)
        {
            try
            {
                actor.Components.Add((ActorComponent) constructor.Info.Invoke(_values));
            }
            catch (Exception e)
            {
                Log.Error(e.InnerException ?? e, "Could not create a {Component}", entry.Name);
            }
        }
        ImGui.EndDisabled();

        ImGui.SameLine();
        close |= ImGui.Button("Cancel", size);

        if (close)
        {
            if (Bridge.PendingRequest is { Kind: AssetRequestKind.Parameter }) Bridge.CancelRequest();
            _entry = null;
            ImGui.CloseCurrentPopup();
        }

        ImGui.EndPopup();
    }

    private void DrawParameter(Entry entry, Parameter parameter, int index)
    {
        EditorUI.Property(parameter.Label);
        ref var value = ref _values[index];

        if (typeof(UObject).IsAssignableFrom(parameter.Type))
        {
            var pending = Bridge.PendingRequest is { } request && request.IsFor(parameter);
            var caption = value is UObject asset ? asset.Name : pending ? $"{Settings.SpinnerIcon}  Pick one in {Bridge.Host.Name}" : $"{Settings.FolderOpenIcon}  Pick in {Bridge.Host.Name}";

            ImGui.BeginDisabled(!Bridge.Host.CanBrowseAssets);
            if (ImGui.Button(caption, new Vector2(-1f, 0f)))
            {
                if (pending) Bridge.CancelRequest();
                else Bridge.RequestParameter(parameter.Type, parameter, $"Requesting a {parameter.Type.Name.TrimStart('U')} for the new {entry.Name}", picked => _values[index] = picked);
            }
            ImGui.EndDisabled();
            return;
        }

        if (parameter.Type == typeof(Transform))
        {
            ImGui.TextDisabled("Identity");
            return;
        }

        // optional by type: a box to give it a value at all, then the value
        if (parameter.Nullable is { } underlying)
        {
            var set = value is not null;
            if (ImGui.Checkbox("##Set", ref set)) value = set ? Activator.CreateInstance(underlying) : null;
            if (value is null) return;

            ImGui.SameLine();
            ImGui.SetNextItemWidth(-1);
        }

        switch (value)
        {
            case float f:
                if (ImGui.DragFloat("##Value", ref f, 0.01f)) value = f;
                break;
            case int i:
                if (ImGui.DragInt("##Value", ref i)) value = i;
                break;
            case uint u:
                var signed = (int) u;
                if (ImGui.DragInt("##Value", ref signed, 1f, 0, int.MaxValue)) value = (uint) signed;
                break;
            case bool b:
                if (ImGui.Checkbox("##Value", ref b)) value = b;
                break;
            case Vector3 color when parameter.Label.Contains("Color", StringComparison.OrdinalIgnoreCase):
                if (ImGui.ColorEdit3("##Value", ref color, ImGuiColorEditFlags.Float)) value = color;
                break;
            case Vector3 v3:
                if (ImGui.DragFloat3("##Value", ref v3, 0.01f)) value = v3;
                break;
            case Vector4 v4:
                if (ImGui.DragFloat4("##Value", ref v4, 0.01f)) value = v4;
                break;
            case Quaternion q:
                var vec = new Vector4(q.X, q.Y, q.Z, q.W);
                if (ImGui.DragFloat4("##Value", ref vec, 0.01f)) value = new Quaternion(vec.X, vec.Y, vec.Z, vec.W);
                break;
            case Enum e:
                if (ImGui.BeginCombo("##Value", e.ToString()))
                {
                    foreach (var option in Enum.GetValues(e.GetType()))
                    {
                        if (ImGui.Selectable(option.ToString(), option.Equals(e))) value = option;
                    }
                    ImGui.EndCombo();
                }
                break;
            case string or null when parameter.Type == typeof(string):
                var text = value as string ?? string.Empty;
                if (ImGui.InputText("##Value", ref text, 256)) value = text.Length > 0 ? text : null;
                break;
            default:
                ImGui.TextDisabled("Default"); // nothing here can edit it, so the constructor's own default is what it gets
                break;
        }
    }

    private static Entry[] Reflect()
    {
        var nullability = new NullabilityInfoContext();
        var entries = new List<Entry>();
        foreach (var type in typeof(ActorComponent).Assembly.GetTypes())
        {
            if (!type.IsPublic || type.IsAbstract || !type.IsSubclassOf(typeof(ActorComponent))) continue;

            var constructors = new List<Constructor>();
            foreach (var info in type.GetConstructors())
            {
                if (Describe(info) is { } constructor) constructors.Add(constructor);
            }
            if (constructors.Count == 0) continue;

            var group = type.Namespace?.Length > typeof(ActorComponent).Namespace!.Length ? type.Namespace[(typeof(ActorComponent).Namespace!.Length + 1)..] : "Other";
            entries.Add(new Entry(group, type.Name, [.. constructors]));
        }

        entries.Sort((a, b) => string.CompareOrdinal(a.Group, b.Group) is var order && order != 0 ? order : string.CompareOrdinal(a.Name, b.Name));
        return [.. entries];

        // null when a parameter can neither be filled here nor left at a default
        Constructor? Describe(ConstructorInfo info)
        {
            var infos = info.GetParameters();
            var parameters = new Parameter[infos.Length];
            var signature = new StringBuilder("(");
            for (var i = 0; i < infos.Length; i++)
            {
                var type = infos[i].ParameterType;
                if (typeof(UActorComponent).IsAssignableFrom(type) || typeof(ActorComponent).IsAssignableFrom(type)) return null;
                if (!CanFill(type) && !infos[i].HasDefaultValue) return null;

                var nullable = Nullable.GetUnderlyingType(type);
                var fallback = infos[i].HasDefaultValue ? infos[i].DefaultValue : type.IsValueType && nullable is null ? Activator.CreateInstance(type) : null;
                var optional = infos[i].HasDefaultValue || nullability.Create(infos[i]).WriteState == NullabilityState.Nullable;
                var label = Humanize(infos[i].Name ?? $"arg{i}");

                parameters[i] = new Parameter(label, type, nullable, fallback, optional);
                signature.Append(i > 0 ? ", " : string.Empty).Append(label);
            }

            return new Constructor(info, signature.Append(')').ToString(), parameters);
        }

        static bool CanFill(Type type)
        {
            if (Nullable.GetUnderlyingType(type) is { } underlying) type = underlying;

            return type == typeof(float) ||
                   type == typeof(int) ||
                   type == typeof(uint) ||
                   type == typeof(bool) ||
                   type == typeof(string) ||
                   type.IsEnum ||
                   type == typeof(Vector3) ||
                   type == typeof(Vector4) ||
                   type == typeof(Quaternion) ||
                   type == typeof(Transform) ||
                   typeof(UObject).IsAssignableFrom(type);
        }

        // "skeletalMesh" read as "Skeletal Mesh"
        static string Humanize(string name)
        {
            var builder = new StringBuilder(name.Length + 4);
            for (var i = 0; i < name.Length; i++)
            {
                var c = name[i];
                if (i == 0) builder.Append(char.ToUpperInvariant(c));
                else if (char.IsUpper(c)) builder.Append(' ').Append(c);
                else builder.Append(c);
            }
            return builder.ToString();
        }
    }
}
