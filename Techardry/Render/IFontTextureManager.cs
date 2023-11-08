using FontStashSharp.Interfaces;

namespace Techardry.Render;

public interface IFontTextureManager : ITexture2DManager
{
    public IReadOnlyList<FontTextureWrapper> ManagedTextures { get; }
}