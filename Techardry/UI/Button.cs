using System.Drawing;
using JetBrains.Annotations;
using Silk.NET.Vulkan;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Techardry.Identifications;

namespace Techardry.UI;

/// <summary>
///     Simple button ui element
/// </summary>
[PublicAPI]
public class Button : Element
{
    private readonly string _content;
    private readonly ushort _desiredFontSize;
    private RectangleF _innerLayout;
    private bool _lastHoveredState;
    private float _borderWidth = 0.01f;

    /// <summary>
    ///     Create a new button
    /// </summary>
    /// <param name="layout">Layout of the button</param>
    /// <param name="content">Optional string to display inside of the button</param>
    /// <param name="desiredFontSize">Font size of the optional string</param>
    // ReSharper disable once NotNullMemberIsNotInitialized
    public Button(RectangleF layout, string content = "", ushort desiredFontSize = ushort.MaxValue) : base(layout)
    {
        _content = content;
        _desiredFontSize = desiredFontSize;
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
            TextBox = new TextBox(
                new RectangleF(_borderWidth, _borderWidth, 1 - _borderWidth * 2, 1 - _borderWidth * 2),
                _content, FontIDs.Akashi, useBorder: false,
                desiredFontSize: _desiredFontSize)
            {
                Parent = this
            };

        HasChanged = true;
        TextBox?.Initialize();
    }

    public override void Draw(CommandBuffer commandBuffer, IList<IDisposable> resourcesToDispose, Rect2D scissor, Viewport viewport)
    {
        var borderTextures = BorderHelper.GetDefaultBorderImages();
        BorderBuilder.DrawBorder(commandBuffer, _borderWidth, Color.Blue, borderTextures,
            resourcesToDispose,scissor, viewport);

        if (TextBox is null) return;
        
        var childViewport = viewport;
        childViewport.Width *= TextBox.RelativeLayout.Width;
        childViewport.Height *= TextBox.RelativeLayout.Height;
        childViewport.X += (int)(viewport.Width * TextBox.RelativeLayout.X);
        childViewport.Y += (int)(viewport.Height * TextBox.RelativeLayout.Y);
        
        
        TextBox.Draw(commandBuffer, resourcesToDispose, scissor, childViewport);
        //Redraw = false;
    }

    /// <inheritdoc />
    public override void Update(float deltaTime)
    {
        //Redraw = _lastHoveredState != CursorHovering;
        _lastHoveredState = CursorHovering;
    }

    /*public override bool Redraw
    {
        get => (TextBox?.Redraw ?? false) || base.Redraw;
        protected set => base.Redraw = value;
    }*/


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
}