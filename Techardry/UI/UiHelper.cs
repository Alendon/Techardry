using Techardry.Identifications;

namespace Techardry.UI;

public static class UiHelper
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
    
    public static float GetRelativeBorderWidth(float absoluteBorderWidth, Element element)
    {
        return GetRelativeBorderWidth(absoluteBorderWidth, element.AbsoluteLayout.Width);
    }
    
    public static float GetRelativeBorderWidth(float absoluteBorderWidth, float absoluteElementWidth)
    {
        return absoluteBorderWidth / absoluteElementWidth;
    }
    
    public static float GetRelativeBorderHeight(float absoluteBorderHeight, Element element)
    {
        return GetRelativeBorderHeight(absoluteBorderHeight, element.AbsoluteLayout.Height);
    }
    
    public static float GetRelativeBorderHeight(float absoluteBorderHeight, float absoluteElementHeight)
    {
        return absoluteBorderHeight / absoluteElementHeight;
    }

    public static float GetRelativeBorderHeightByWidth(float relativeBorderWidth, Element element)
    {
        var elementPixelSize = element.ElementPixelSize;
        return GetRelativeBorderHeightByWidth(relativeBorderWidth, (float)elementPixelSize.Width / elementPixelSize.Height);
    }
    
    public static float GetRelativeBorderHeightByWidth(float relativeBorderHeight, float heightWidthRatio)
    {
        return relativeBorderHeight * heightWidthRatio;
    }
    
    public static float GetAbsoluteBorderWidth(float relativeWidth, float relativeElementWidth)
    {
        return relativeWidth * relativeElementWidth;
    }
}