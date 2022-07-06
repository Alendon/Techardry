using MintyCore.Utils;
using SixLabors.ImageSharp.PixelFormats;

namespace Techardry.Blocks;

public class GenericBlock : IBlock
{
    private Rgba32 _color;
    private Identification _texture;

    public GenericBlock(Rgba32 color, Identification texture)
    {
        _color = color;
        _texture = texture;
    }


    public IBlock MakeCopy()
    {
        return new GenericBlock(_color, _texture);
    }

    public Rgba32 Color => _color;
    public Identification Texture => _texture;
    
    public bool IsRotatable => false;
    public bool IsSplittable => true;
}