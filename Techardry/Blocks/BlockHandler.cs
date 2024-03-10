using MintyCore.Utils;
using SixLabors.ImageSharp.PixelFormats;

namespace Techardry.Blocks;

[Singleton<IBlockHandler>]
public class BlockHandler : IBlockHandler
{
    private readonly Dictionary<Identification, IBlock> _blocks = new();

    public void Remove(Identification objectId)
    {
        _blocks.Remove(objectId);
    }

    public void Clear()
    {
        _blocks.Clear();
    }

    public void Add(Identification blockId, IBlock block)
    {
        _blocks[blockId] = block;
    }

    public Rgba32 GetBlockColor(Identification id)
    {
        return _blocks[id].Color;
    }

    public Identification GetBlockTexture(Identification id)
    {
        return _blocks[id].Texture;
    }

    public bool IsBlockSplittable(Identification blockId)
    {
        return _blocks[blockId].IsSplittable;
    }

    public bool IsBlockRotatable(Identification blockId)
    {
        return _blocks[blockId].IsRotatable;
    }

    public bool DoesBlockExist(Identification blockId)
    {
        return _blocks.ContainsKey(blockId);
    }
}