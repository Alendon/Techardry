using SixLabors.ImageSharp;
using Techardry.Registries;

namespace Techardry.Blocks;

public static class Blocks
{
    [RegisterBlock("air")]
    public static IBlock Air => new GenericBlock(Color.Transparent);
    
    [RegisterBlock("stone")]
    public static IBlock Stone => new GenericBlock(Color.Gray);
    
    [RegisterBlock("grass")]
    public static IBlock Grass => new GenericBlock(Color.Green);
    
    [RegisterBlock("dirt")]
    public static IBlock Dirt => new GenericBlock(Color.SaddleBrown);
}