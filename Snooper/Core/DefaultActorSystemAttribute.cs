namespace Snooper.Core;

[AttributeUsage(AttributeTargets.Class)]
public sealed class DefaultActorSystemAttribute(Type type) : Attribute
{
    public Type Type { get; } = type;
}
