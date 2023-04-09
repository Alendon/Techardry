using MintyCore.Registries;

namespace Techardry.World;

internal static class WorldInfos
{
    [RegisterWorld("default")]
    public static WorldInfo TechardryWorldInfo => new()
    {
        WorldCreateFunction = serverWorld => new TechardryWorld(serverWorld),
    };
}