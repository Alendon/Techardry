using MintyCore.Utils;
using SixLabors.ImageSharp;
using Techardry.Identifications;
using Techardry.Registries;

namespace Techardry.Blocks;

public static class Blocks
{
    [RegisterBlock("air")]
    public static IBlock Air => new GenericBlock(Color.Transparent, Identification.Invalid);
    
    [RegisterBlock("stone")]
    public static IBlock Stone => new GenericBlock(Color.Gray, TextureIDs.Stone);
    
    [RegisterBlock("grass")]
    public static IBlock Grass => new GenericBlock(Color.Green, Identification.Invalid);
    
    [RegisterBlock("dirt")]
    public static IBlock Dirt => new GenericBlock(Color.SaddleBrown, TextureIDs.Dirt);
}