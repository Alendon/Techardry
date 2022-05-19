using SixLabors.ImageSharp.PixelFormats;

namespace Techardry.Blocks;

public interface IBlock
{
    IBlock MakeCopy();
    Rgba32 Color { get; }
    bool IsRotatable { get; }
    bool AllowSubVoxels { get; }
}