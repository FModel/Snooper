using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Component;
using CUE4Parse.UE4.Assets.Exports.Engine;
using CUE4Parse.UE4.Objects.Core.Misc;
using CUE4Parse.UE4.Objects.Engine;
using CUE4Parse.UE4.Objects.UObject;
using Serilog;
using Snooper.Rendering.Components.Transforms;

namespace Snooper.Rendering.Actors;

public class BlueprintActor : UnrealActor
{
    private const int MaxChildActorDepth = 8;

    public BlueprintActor(UBlueprintGeneratedClass blueprint) : this(blueprint, [], 0)
    {

    }

    private BlueprintActor(UBlueprintGeneratedClass blueprint, HashSet<string> ancestry, int depth) : base(blueprint)
    {
        _ancestry = ancestry;
        _depth = depth;
        _classKey = blueprint.GetPathName();

        var chain = CollectClassChain(blueprint);
        var deferred = BuildNativeComponents(chain);
        BuildOverrideTable(chain);
        ExecuteConstructionScripts(chain);

        // non-scene native components (movement, audio, ...) carry no transform, so they must never become the root
        foreach (var component in deferred)
        {
            Components.Add(component);
        }

        _nativeByName.Clear();
        _byTemplatePtr.Clear();
        _scsByVariable.Clear();
        _scsByName.Clear();
        _visitedNodes.Clear();
        _overridesByGuid.Clear();
        _overridesByKey.Clear();
    }

    public override string Icon => Settings.ClipboardListIcon;

    private readonly HashSet<string> _ancestry;
    private readonly int _depth;
    private readonly string _classKey;

    private readonly Dictionary<string, SpatialComponent> _nativeByName = new(StringComparer.Ordinal);
    private readonly Dictionary<FPackageIndex, SpatialComponent> _byTemplatePtr = [];
    private readonly Dictionary<(string OwnerClass, string Variable), SpatialComponent> _scsByVariable = [];
    private readonly Dictionary<string, SpatialComponent> _scsByName = new(StringComparer.Ordinal);
    private readonly HashSet<FPackageIndex> _visitedNodes = [];
    private readonly Dictionary<FGuid, FPackageIndex> _overridesByGuid = [];
    private readonly Dictionary<(string OwnerClass, string Variable), FPackageIndex> _overridesByKey = [];

    private List<UBlueprintGeneratedClass> CollectClassChain(UBlueprintGeneratedClass blueprint)
    {
        var chain = new List<UBlueprintGeneratedClass>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        var current = blueprint;
        while (current != null && seen.Add(current.GetPathName()))
        {
            chain.Add(current);
            current = current.SuperStruct is { IsNull: false } super && super.TryLoad<UBlueprintGeneratedClass>(out var parent) ? parent : null;
        }

        return chain;
    }

    private List<SpatialComponent> BuildNativeComponents(List<UBlueprintGeneratedClass> chain)
    {
        // collect default subobjects of every class default object in the chain, the most-derived export of a given name wins
        var subobjects = new Dictionary<string, FPackageIndex>(StringComparer.Ordinal);
        UObject? mostDerivedCdo = null;
        foreach (var bp in chain)
        {
            var cdoPtr = bp.ClassDefaultObject;
            if (cdoPtr is not { IsNull: false } || cdoPtr.ResolvedObject is not { ExportIndex: >= 0 } cdo) continue;

            mostDerivedCdo ??= cdoPtr.Load();

            var package = cdo.Package;
            for (var i = 0; i < package.ExportMapLength; i++)
            {
                var ptr = new FPackageIndex(package, i + 1);
                var resolved = package.ResolvePackageIndex(ptr);
                if (resolved?.Outer is not { } outer || outer.ExportIndex != cdo.ExportIndex || !ReferenceEquals(outer.Package, package)) continue;

                subobjects.TryAdd(resolved.Name.Text, ptr);
            }
        }

        var created = new List<(ComponentPair Pair, bool IsScene)>();
        foreach (var (name, ptr) in subobjects)
        {
            if (!ptr.TryLoad<UActorComponent>(out var actorComponent)) continue;

            var pair = CreateComponentPair(ptr);
            pair.Component.Name = name;
            created.Add((pair, actorComponent is USceneComponent));

            _nativeByName[name] = pair.Component;
            _byTemplatePtr[ptr] = pair.Component;
        }

        // the attach parent pointer may target an ancestor package's export of the same subobject, so resolve it by name
        foreach (var (pair, _) in created)
        {
            if (pair.ParentPtr is { IsNull: false } parentPtr && _nativeByName.TryGetValue(parentPtr.Name, out var parent) && parent != pair.Component)
            {
                pair.Component.Relation = parent;
            }
        }

        SpatialComponent? root = null;
        if (mostDerivedCdo?.GetOrDefault<FPackageIndex?>("RootComponent") is { IsNull: false } rootPtr)
        {
            _nativeByName.TryGetValue(rootPtr.Name, out root);
        }
        root ??= created.FirstOrDefault(c => c.IsScene && c.Pair.Component.Relation is null).Pair.Component;

        // the first spatial component added becomes the actor's root
        var deferred = new List<SpatialComponent>();
        if (root != null) Components.Add(root);
        foreach (var (pair, isScene) in created)
        {
            if (pair.Component == root) continue;
            if (isScene) Components.Add(pair.Component);
            else deferred.Add(pair.Component);
        }
        return deferred;
    }

    private void BuildOverrideTable(List<UBlueprintGeneratedClass> chain)
    {
        // base first so that the most-derived override wins
        for (var i = chain.Count - 1; i >= 0; i--)
        {
            var handler = chain[i].InheritableComponentHandler?.Load<UInheritableComponentHandler>();
            if (handler == null) continue;

            foreach (var record in handler.Records)
            {
                if (record.ComponentTemplate is not { IsNull: false } template) continue;

                var key = record.ComponentKey;
                if (key.AssociatedGuid.IsValid())
                {
                    _overridesByGuid[key.AssociatedGuid] = template;
                }

                var ownerName = key.OwnerClass?.Name;
                if (ownerName != null && key.SCSVariableName is { IsNone: false } variable)
                {
                    _overridesByKey[(ownerName, variable.Text)] = template;
                }
            }
        }
    }

    private void ExecuteConstructionScripts(List<UBlueprintGeneratedClass> chain)
    {
        for (var i = chain.Count - 1; i >= 0; i--)
        {
            var owner = chain[i];
            var script = owner.SimpleConstructionScript?.Load<USimpleConstructionScript>();
            if (script == null) continue;

            var processedAny = false;
            foreach (var nodePtr in script.RootNodes)
            {
                if (nodePtr is not { IsNull: false }) continue;

                processedAny = true;
                ProcessNode(nodePtr, owner, null);
            }

            // the default scene root only exists when nothing else can be the root
            if (!processedAny && RootComponent is null && script.DefaultSceneRootNode is { IsNull: false } defaultRoot)
            {
                ProcessNode(defaultRoot, owner, null);
            }
        }
    }

    private void ProcessNode(FPackageIndex nodePtr, UBlueprintGeneratedClass owner, SpatialComponent? parentFromRecursion)
    {
        if (!_visitedNodes.Add(nodePtr) || !nodePtr.TryLoad<USCS_Node>(out var node)) return;

        var variableName = node.InternalVariableName.Text;
        var template = ResolveTemplate(node, owner.Name, variableName);
        if (template is not { IsNull: false })
        {
            Log.Warning("Node {NodeName} has no component template, skipping", variableName);
            foreach (var childPtr in node.ChildNodes)
            {
                if (childPtr is { IsNull: false }) ProcessNode(childPtr, owner, parentFromRecursion);
            }
            return;
        }

        if (!_byTemplatePtr.TryGetValue(template, out var component))
        {
            var pair = CreateComponentPair(template);
            component = pair.Component;
            component.Name = variableName;

            if (node.GetOrDefault<FName?>("AttachToName") is { IsNone: false } socket)
            {
                component.AttachSocketName = socket.Text;
            }

            var parent = ResolveNodeParent(node, parentFromRecursion);
            var isScene = template.ResolvedObject?.Object?.Value is USceneComponent;
            if (RootComponent is null && isScene && parent is null)
            {
                // becomes the root
            }
            else
            {
                var expected = parent ?? RootComponent;
                component.Relation = expected;
                if (expected != null && component.Relation != expected)
                {
                    Log.Warning("Node {NodeName} could not be attached to {Parent}", variableName, expected.Name);
                }
            }

            Components.Add(component);

            _byTemplatePtr[template] = component;
            _scsByVariable[(owner.Name, variableName)] = component;
            _scsByName.TryAdd(variableName, component);

            if (template.ResolvedObject?.Object?.Value is UChildActorComponent childActorComponent)
            {
                TryCreateChildActor(childActorComponent, component);
            }
        }

        foreach (var childPtr in node.ChildNodes)
        {
            if (childPtr is { IsNull: false }) ProcessNode(childPtr, owner, component);
        }
    }

    private FPackageIndex? ResolveTemplate(USCS_Node node, string ownerName, string variableName)
    {
        if (node.VariableGuid.IsValid() && _overridesByGuid.TryGetValue(node.VariableGuid, out var byGuid))
            return byGuid;
        if (_overridesByKey.TryGetValue((ownerName, variableName), out var byKey))
            return byKey;
        return node.ComponentTemplate;
    }

    private SpatialComponent? ResolveNodeParent(USCS_Node node, SpatialComponent? parentFromRecursion)
    {
        if (node.GetOrDefault<FName?>("ParentComponentOrVariableName") is not { IsNone: false } parentName)
            return parentFromRecursion;

        var name = parentName.Text;
        if (node.GetOrDefault("bIsParentComponentNative", false))
        {
            if (_nativeByName.TryGetValue(name, out var native)) return native;
        }
        else
        {
            if (node.GetOrDefault<FName?>("ParentComponentOwnerClassName") is { IsNone: false } ownerClassName &&
                _scsByVariable.TryGetValue((ownerClassName.Text, name), out var scoped))
                return scoped;
            if (_scsByName.TryGetValue(name, out var byName)) return byName;
            if (_nativeByName.TryGetValue(name, out var native)) return native;
        }

        Log.Warning("Node {NodeName} references unknown parent {Parent}, attaching to root", node.InternalVariableName.Text, name);
        return null;
    }

    private void TryCreateChildActor(UChildActorComponent childActorComponent, SpatialComponent component)
    {
        if (_depth >= MaxChildActorDepth)
        {
            Log.Warning("Child actor depth limit reached at {Component}", component.Name);
            return;
        }

        if (childActorComponent.GetOrDefault<FPackageIndex?>("ChildActorClass") is not { IsNull: false } classPtr ||
            !classPtr.TryLoad<UBlueprintGeneratedClass>(out var childClass))
            return;

        var childKey = childClass.GetPathName();
        if (childKey == _classKey || _ancestry.Contains(childKey))
        {
            Log.Warning("Child actor {Class} would recurse into itself, skipping", childClass.Name);
            return;
        }

        var ancestry = new HashSet<string>(_ancestry, StringComparer.Ordinal) { _classKey };
        var child = new BlueprintActor(childClass, ancestry, _depth + 1);
        Children.Add(child);

        // OnChildAdded parents the child to our root, re-target it to the spawning component
        if (child.RootComponent is { } childRoot)
        {
            childRoot.Relation = component;
            childRoot.SetLocalTransform(Transform.Identity);
        }
    }
}
