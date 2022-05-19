using SixLabors.ImageSharp.PixelFormats;

namespace Techardry.Blocks;

public class GenericBlock : IBlock
{
    private Rgba32 _color;

    public GenericBlock(Rgba32 color)
    {
        _color = color;
    }


    public IBlock MakeCopy()
    {
        return new GenericBlock(_color);
    }

    public Rgba32 Color => _color;
    public bool IsRotatable => false;
    public bool AllowSubVoxels => true;
}