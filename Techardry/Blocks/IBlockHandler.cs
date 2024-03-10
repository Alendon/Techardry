using MintyCore.Utils;
using SixLabors.ImageSharp.PixelFormats;

namespace Techardry.Blocks;

public interface IBlockHandler
{
    void Remove(Identification objectId);
    void Clear();
    void Add(Identification blockId, IBlock block);
    Rgba32 GetBlockColor(Identification id);
    Identification GetBlockTexture(Identification id);
    bool IsBlockSplittable(Identification blockId);
    bool IsBlockRotatable(Identification blockId);
    bool DoesBlockExist(Identification blockId);
}