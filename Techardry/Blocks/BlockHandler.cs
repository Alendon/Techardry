using MintyCore.Utils;
using SixLabors.ImageSharp.PixelFormats;

namespace Techardry.Blocks;

public static class BlockHandler
{
    private static Dictionary<Identification, IBlock> _blocks = new();

    public static void Remove(Identification objectId)
    {
        _blocks.Remove(objectId);
    }

    public static void Clear()
    {
        _blocks.Clear();
    }

    public static void Add(Identification blockId, IBlock block)
    {
        _blocks[blockId] = block;
    }

    public static Rgba32 GetBlockColor(Identification id)
    {
        return _blocks[id].Color;
    }
    
    public static Identification GetBlockTexture(Identification id)
    {
        return _blocks[id].Texture;
    }

    public static bool IsBlockSplittable(Identification blockId)
    {
        return _blocks[blockId].IsSplittable;
    }

    public static bool IsBlockRotatable(Identification blockId)
    {
        return _blocks[blockId].IsRotatable;
    }

    public static bool DoesBlockExist(Identification blockId)
    {
        return _blocks.ContainsKey(blockId);
    }
}