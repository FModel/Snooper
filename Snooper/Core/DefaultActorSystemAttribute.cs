namespace Snooper.Core;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class DefaultActorSystemAttribute(Type type) : Attribute
{
    public Type Type { get; } = type;
}
