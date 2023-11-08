using System.Drawing;
using System.Numerics;
using FontStashSharp;
using JetBrains.Annotations;
using MintyCore.Utils;
using Silk.NET.Vulkan;
using SixLabors.Fonts;
using Techardry.Render;
using Techardry.UI.Interfaces;

namespace Techardry.UI.Elements;

/// <summary>
///     Ui element to display a simple text
/// </summary>
[PublicAPI]
public class TextBox : Element, IBorderElement
{
    private readonly int _desiredFontSize;
    private string _content;
    private Color _drawColor;
    private Color _fillColor;
    private HorizontalAlignment _horizontalAlignment;

    private RectangleF _innerLayout;

    /// <summary>
    ///     Constructor
    /// </summary>
    /// <param name="relativeLayout">The layout to use for the text box</param>
    /// <param name="content">The string the text box will show</param>
    /// <param name="desiredFontSize">The desired size of the font used.</param>
    /// <param name="borderActive">Whether or not a border should be drawn around the element</param>
    /// <param name="horizontalAlignment">Which horizontal alignment the text should use</param>
    // ReSharper disable once NotNullMemberIsNotInitialized
    public TextBox(RectangleF relativeLayout, string content, ushort desiredFontSize = ushort.MaxValue,
        bool borderActive = true, HorizontalAlignment horizontalAlignment = HorizontalAlignment.Center,
        float borderWidth = 0.05f) : base(
        relativeLayout)
    {
        _content = content;
        _desiredFontSize = desiredFontSize;
        BorderActive = borderActive;
        _horizontalAlignment = horizontalAlignment;
        _drawColor = Color.White;
        _fillColor = Color.Transparent;
        BorderWidth = borderWidth;
    }

    /// <summary>
    ///     Get or set the content
    /// </summary>
    public string Content
    {
        get => _content;
        set
        {
            _content = value;
            HasChanged = true;
        }
    }

    /// <summary>
    ///     Get or set the horizontal alignment of the text
    /// </summary>
    public HorizontalAlignment HorizontalAlignment
    {
        get => _horizontalAlignment;
        set
        {
            _horizontalAlignment = value;
            HasChanged = true;
        }
    }

    /// <summary>
    ///     Get or set the fill / background color
    /// </summary>
    public Color FillColor
    {
        get => _fillColor;
        set
        {
            _fillColor = value;
            HasChanged = true;
        }
    }

    /// <summary>
    ///     Get or set the draw color
    /// </summary>
    public Color DrawColor
    {
        get => _drawColor;
        set
        {
            _drawColor = value;
            HasChanged = true;
        }
    }

    /// <inheritdoc />
    public override void Initialize()
    {
        HasChanged = true;
    }

    public override void Draw(IUiRenderer renderer, Rect2D scissors, Viewport viewports)
    {
        if (BorderActive)
        {
            var borderTextures = UiHelper.GetDefaultBorderImages();

            BorderBuilder.DrawBorder(renderer, BorderWidth, _fillColor, borderTextures, scissors, viewports);
        }

        Vector2 scale = Vector2.One / new Vector2(viewports.Width, viewports.Height);
        var fontSize = (int)(1 * viewports.Height);

        var bounds = renderer.MeasureString(Content, fontSize, new Vector2(0, 0), scale);
        var stringSize = new Vector2(bounds.X2 - bounds.X, bounds.Y2 - bounds.Y);
        var position = new Vector2(-stringSize.X / 2, -stringSize.Y / 2);

        renderer.DrawString(Content, fontSize, viewports, scissors, position, FSColor.Blue, scale: scale);
    }

    /// <inheritdoc />
    public override void Update(float deltaTime)
    {
    }

    public bool BorderActive { get; set; }
    public float BorderWidth { get; set; }
}