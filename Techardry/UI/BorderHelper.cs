using Techardry.Identifications;

namespace Techardry.UI;

public static class BorderHelper
{
    public static BorderImages GetDefaultBorderImages() => new()
    {
        Bottom = TextureIDs.UiBorderBottom,
        Left = TextureIDs.UiBorderLeft,
        Right = TextureIDs.UiBorderRight,
        Top = TextureIDs.UiBorderTop,
        CornerLowerLeft = TextureIDs.UiCornerLowerLeft,
        CornerLowerRight = TextureIDs.UiCornerLowerRight,
        CornerUpperLeft = TextureIDs.UiCornerUpperLeft,
        CornerUpperRight = TextureIDs.UiCornerUpperRight
    };

}