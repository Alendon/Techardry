using System.Drawing;
using JetBrains.Annotations;
using Silk.NET.Vulkan;
using Techardry.Render;
using Techardry.UI.Interfaces;
using static Techardry.UI.UiHelper;

namespace Techardry.UI.Elements;

/// <summary>
///     Simple button ui element
/// </summary>
[PublicAPI]
public class Button : Element, IBorderElement
{
    private readonly string _content;
    private readonly ushort _desiredFontSize;
    private RectangleF _innerLayout;
    private bool _lastHoveredState;
    private float _borderWidth;
    private bool _borderActive = true;

    /// <summary>
    ///     Create a new button
    /// </summary>
    /// <param name="layout">Layout of the button</param>
    /// <param name="content">Optional string to display inside of the button</param>
    /// <param name="desiredFontSize">Font size of the optional string</param>
    // ReSharper disable once NotNullMemberIsNotInitialized
    public Button(RectangleF layout, string content = "", ushort desiredFontSize = ushort.MaxValue, float borderWidth = 0.05f) : base(layout)
    {
        _content = content;
        _desiredFontSize = desiredFontSize;
        BorderWidth = borderWidth;
    }

    /// <summary>
    ///     Text box which lives inside the button if a string button content is provided
    /// </summary>
    public TextBox? TextBox { get; private set; }


    /// <summary>
    ///     Callback if the button is clicked
    /// </summary>
    public event Action OnLeftClickCb = delegate { };

    /// <inheritdoc />
    public override void Initialize()
    {
        if (_content.Length != 0)
        {
            var offsetX = BorderActive ? BorderWidth : 0;
            var offsetY = BorderActive ? GetRelativeBorderHeightByWidth(BorderWidth, this) : 0;
            TextBox = new TextBox(
                new RectangleF(offsetX, offsetY, 1 - offsetX * 2, 1 - offsetY * 2),
                _content, desiredFontSize: _desiredFontSize, borderActive: false)
            {
                Parent = this
            };
        }

        HasChanged = true;
        TextBox?.Initialize();
    }

    public override void OnResize()
    {
        if(TextBox is not null)
        {
            var offsetX = BorderActive ? BorderWidth : 0;
            var offsetY = BorderActive ? GetRelativeBorderHeightByWidth(BorderWidth, this) : 0;
            TextBox.RelativeLayout = new RectangleF(offsetX, offsetY, 1 - offsetX * 2, 1 - offsetY * 2);
            TextBox.OnResize();
        }
        base.OnResize();
    }

    public override void Draw(IUiRenderer renderer, Rect2D scissor, Viewport viewport)
    {
        if(BorderActive)
        {
            var borderTextures = GetDefaultBorderImages();
            var baseColor = CursorHovering ? Color.Gray : Color.DarkGray;
            BorderBuilder.DrawBorder(renderer, BorderWidth, baseColor, borderTextures, scissor, viewport);
        }

        if (TextBox is null) return;
        
        var childViewport = viewport;
        childViewport.Width *= TextBox.RelativeLayout.Width;
        childViewport.Height *= TextBox.RelativeLayout.Height;
        childViewport.X += (int)(viewport.Width * TextBox.RelativeLayout.X);
        childViewport.Y += (int)(viewport.Height * TextBox.RelativeLayout.Y);
        
        
        TextBox.Draw(renderer, scissor, childViewport);
    }

    /// <inheritdoc />
    public override void Update(float deltaTime)
    {
        _lastHoveredState = CursorHovering;
    }

    /// <inheritdoc />
    public override void OnLeftClick()
    {
        if (!CursorHovering) return;
        OnLeftClickCb();
    }

    protected override void Dispose(bool disposing)
    {
        TextBox?.Dispose();
        base.Dispose(disposing);
    }

    public bool BorderActive
    {
        get => _borderActive;
        set
        {
            _borderActive = value;
            OnResize();
        }
    }

    public float BorderWidth
    {
        get => _borderWidth;
        set
        {
            _borderWidth = value; 
            OnResize();
        }
    }
}