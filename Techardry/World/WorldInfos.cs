using MintyCore.Registries;
using Techardry.Lib.FastNoseLite;

namespace Techardry.World;

internal static class WorldInfos
{
    [RegisterWorld("default")]
    public static WorldInfo TechardryWorldInfo => new()
    {
        WorldCreateFunction = serverWorld =>
        {
            var noise = new FastNoiseLite(5);
            noise.SetNoiseType(FastNoiseLite.NoiseType.Perlin);
            noise.SetFrequency(0.02f);
            
            return new TechardryWorld(serverWorld, new WorldGeneratorSettings()
            {
                Noise = noise
            });
        }
    };
}