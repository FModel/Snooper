using Snooper.Rendering.Actors;

namespace Snooper.Extensions;

public static class EnumExtensions
{
    public static bool Includes(this WorldActorType value, WorldActorType flag) => (value & flag) != 0;
}