using MintyCore.Utils;
using SixLabors.ImageSharp.PixelFormats;

namespace Techardry.Blocks;

public interface IBlock
{
    IBlock MakeCopy();
    Rgba32 Color { get; }
    Identification Texture { get; }
    bool IsRotatable { get; }
    bool IsSplittable { get; }
}