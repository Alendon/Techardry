using System.Drawing;
using JetBrains.Annotations;
using MintyCore.Utils;
using Silk.NET.Vulkan;
using Techardry.Render;

namespace Techardry.UI;

/// <summary>
///     Helper class to build a border image
/// </summary>
public static class BorderBuilder
{
    public static void DrawBorder(UiRenderer renderer, [ValueRange(0, 1)] float borderWidth,
        Color? fillColor, BorderImages borderImages,
        Rect2D scissor, Viewport viewport)
    {
        Logger.AssertAndThrow(borderWidth is >= 0 and <= 1, "Border width must be in range 0-1", "UI");

        var heightModifier = viewport.Width / viewport.Height;

        var absolutBorderWidth = borderWidth;
        var absolutBorderHeight = borderWidth * heightModifier;

        if (fillColor is not null)
        {
            renderer.FillColor(fillColor.Value, scissor, viewport);
        }

        renderer.DrawTexture(borderImages.Left, new RectangleF(new PointF(0, 0), new SizeF(absolutBorderWidth, 1)),
            new RectangleF(0, 0, 1, 1), scissor, viewport);
        renderer.DrawTexture(borderImages.Right,
            new RectangleF(new PointF(1 - absolutBorderWidth, 0), new SizeF(absolutBorderWidth, 1)),
            new RectangleF(0, 0, 1, 1), scissor, viewport);
        renderer.DrawTexture(borderImages.Bottom, new RectangleF(new PointF(0, 0), new SizeF(1, absolutBorderHeight)),
            new RectangleF(0, 0, 1, 1), scissor, viewport);
        renderer.DrawTexture(borderImages.Top,
            new RectangleF(new PointF(0, 1 - absolutBorderHeight), new SizeF(1, absolutBorderHeight)),
            new RectangleF(0, 0, 1, 1), scissor, viewport);

        renderer.DrawTexture(borderImages.CornerLowerLeft,
            new RectangleF(new PointF(0, 0), new SizeF(absolutBorderWidth, absolutBorderHeight)),
            new RectangleF(0, 0, 1, 1), scissor, viewport);
        renderer.DrawTexture(borderImages.CornerLowerRight,
            new RectangleF(new PointF(1 - absolutBorderWidth, 0), new SizeF(absolutBorderWidth, absolutBorderHeight)),
            new RectangleF(0, 0, 1, 1), scissor, viewport);
        renderer.DrawTexture(borderImages.CornerUpperLeft,
            new RectangleF(new PointF(0, 1 - absolutBorderHeight), new SizeF(absolutBorderWidth, absolutBorderHeight)),
            new RectangleF(0, 0, 1, 1), scissor, viewport);
        renderer.DrawTexture(borderImages.CornerUpperRight,
            new RectangleF(new PointF(1 - absolutBorderWidth, 1 - absolutBorderHeight),
                new SizeF(absolutBorderWidth, absolutBorderHeight)), new RectangleF(0, 0, 1, 1), scissor, viewport);
    }

}

/// <summary>
/// 
/// </summary>
public struct BorderImages
{
    /// <summary>
    /// 
    /// </summary>
    public Identification Left;

    /// <summary>
    /// 
    /// </summary>
    public Identification Right;

    /// <summary>
    /// 
    /// </summary>
    public Identification Top;

    /// <summary>
    /// 
    /// </summary>
    public Identification Bottom;

    /// <summary>
    /// 
    /// </summary>
    public Identification CornerUpperLeft;

    /// <summary>
    /// 
    /// </summary>
    public Identification CornerUpperRight;

    /// <summary>
    /// 
    /// </summary>
    public Identification CornerLowerLeft;

    /// <summary>
    /// 
    /// </summary>
    public Identification CornerLowerRight;
}