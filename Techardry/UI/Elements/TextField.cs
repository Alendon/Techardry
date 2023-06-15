using System.Drawing;
using MintyCore.Utils;
using SixLabors.Fonts;

namespace Techardry.UI.Elements;

/// <summary>
///     Ui element for a text input
/// </summary>
public class TextField : TextBox
{
    private readonly string _hint;
    private readonly TextInput _textInput;


    /// <summary>
    ///     Constructor
    /// </summary>
    /// <param name="relativeLayout"></param>
    /// ///
    /// <param name="fontFamilyId">The font family to use for rendering</param>
    /// <param name="desiredFontSize">The desired size of the font used.</param>
    /// <param name="borderActive">Whether or not a border should be drawn around the element</param>
    /// <param name="horizontalAlignment">Which horizontal alignment the text should use</param>
    /// <param name="hint">Text which will be displayed if empty</param>
    // ReSharper disable once NotNullMemberIsNotInitialized
    public TextField(RectangleF relativeLayout, ushort desiredFontSize = ushort.MaxValue,
        bool borderActive = true, HorizontalAlignment horizontalAlignment = HorizontalAlignment.Center,
        string hint = "", float borderWidth = 0.05f) : base(relativeLayout,
        "To Measure |", desiredFontSize, borderActive, horizontalAlignment, borderWidth: borderWidth)
    {
        _textInput = new TextInput(false);
        FillColor = Color.Gray;
        _hint = hint;
    }

    /// <summary>
    ///     The inputted text
    ///     Use this to access/set the inputted text
    ///     May differ to <see cref="TextBox.Content" />
    /// </summary>
    public string InputText
    {
        get => _textInput.ToString();
        set => _textInput.SetText(value);
    }

    /// <inheritdoc />
    public override void OnLeftClick()
    {
        var oldState = _textInput.IsActive;
        _textInput.IsActive = CursorHovering;
        if (_textInput.IsActive == oldState) return;
        FillColor = _textInput.IsActive ? Color.DarkGray : Color.Gray;
    }

    /// <inheritdoc />
    public override void Update(float deltaTime)
    {
        var currentInput = _textInput.ToString();
        if (!Content.Equals(currentInput) && currentInput.Length != 0)
        {
            Content = currentInput;
            DrawColor = Color.White;
        }

        if (currentInput.Length == 0)
        {
            Content = _hint;
            DrawColor = Color.DarkGray;
        }

        base.Update(deltaTime);
    }
}