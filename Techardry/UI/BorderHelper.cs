using MintyCore.UI;
using Techardry.Identifications;

namespace Techardry.UI;

public static class BorderHelper
{
    public static BorderImages GetDefaultBorderImages() => new BorderImages()
    {
        Bottom = ImageIDs.UiBorderBottom,
        Left = ImageIDs.UiBorderLeft,
        Right = ImageIDs.UiBorderRight,
        Top = ImageIDs.UiBorderTop,
        CornerLowerLeft = ImageIDs.UiCornerLowerLeft,
        CornerLowerRight = ImageIDs.UiCornerLowerRight,
        CornerUpperLeft = ImageIDs.UiCornerUpperLeft,
        CornerUpperRight = ImageIDs.UiCornerUpperRight
    };

}