using System.Numerics;
using FontStashSharp;
using FontStashSharp.Interfaces;
using JetBrains.Annotations;
using MintyCore.Utils;
using Silk.NET.Vulkan;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Techardry.UI;

/// <summary>
///     Abstract base class for all Ui Elements
/// </summary>
[PublicAPI]
public abstract class Element : IDisposable
{
    private bool _redraw;

    /// <summary />
    protected Element(RectangleF layout)
    {
        Layout = layout;
    }


    /// <summary>
    ///     Indicator if the element needs to be redrawn
    /// </summary>
    public bool Redraw
    {
        get => _redraw || GetChildElements().Any(element => element.Redraw);
        protected set => _redraw = value;
    }

    public virtual void Draw(CommandBuffer commandBuffer)
    {
        InternalDraw(commandBuffer);
        Redraw = false;
        
        foreach (var element in GetChildElements())
        {
            element.Draw(commandBuffer);
        }
    }

    public abstract void InternalDraw(CommandBuffer commandBuffer);

    public virtual void DrawString(string text, Identification font, CommandBuffer commandBuffer, Vector2 position,
        FSColor color, Vector2? scale = null, float rotation = 0f, Vector2 origin = default, float layerDepth = 0f,
        float characterSpacing = 0f, float lineSpacing = 0f, TextStyle textStyle = TextStyle.None,
        FontSystemEffect effect = FontSystemEffect.None, int effectAmount = 0)
    {
        /*SpriteFontBase fotn = FontSystem.GetFont(font);
        FontSystem a;
        IFontStashRenderer2 renderer2 = FontSystem.GetRenderer2(font);
        renderer2.UseCommandBuffer(commandBuffer);
        
        fotn.DrawText(renderer2, text, position, color, scale, rotation, origin, layerDepth, characterSpacing,
            lineSpacing, textStyle, effect, effectAmount);*/
        
        Console.WriteLine("Oh no, drawing strings on ui elements is not implemented yet");
    }
    
    public virtual bool HasChanged { get; protected set; }

    /// <summary>
    ///     The parent of this Element
    /// </summary>
    public Element? Parent { get; set; }

    /// <summary>
    ///     Whether or not this element is a root element
    /// </summary>
    public bool IsRootElement { get; init; }

    /// <summary>
    ///     The layout off the element relative to the parent
    ///     Values needs to be in Range 0f-1f
    ///     <remarks>The (0,0) coordinate is the upper left corner</remarks>
    /// </summary>
    public RectangleF Layout { get; }

    /// <summary>
    ///     Whether or not the cursor is hovering over the element
    /// </summary>
    public bool CursorHovering { get; set; }

    /// <summary>
    ///     The cursor position relative to the element
    /// </summary>
    public Vector2 CursorPosition { get; set; }

    /// <summary>
    ///     The absolute pixel size of the element
    /// </summary>
    public virtual SizeF PixelSize
    {
        get
        {
            Logger.AssertAndThrow(!IsRootElement, $"RootElements have to override {nameof(PixelSize)}", "UI");
            Logger.AssertAndThrow(Parent != null, "Cannot get pixel size of element as parent is null", "UI");
            return new SizeF(Parent!.PixelSize.Width * Layout.Width, Parent!.PixelSize.Height * Layout.Height);
        }
    }

    /// <summary>
    ///     Get/set whether or not this component is active (will get updated)
    /// </summary>
    public virtual bool IsActive { get; set; }

    /// <inheritdoc />
    public virtual void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     Get the children of this element
    /// </summary>
    /// <returns></returns>
    public virtual IEnumerable<Element> GetChildElements()
    {
        return Enumerable.Empty<Element>();
    }

    /// <summary>
    ///     Update the element
    /// </summary>
    /// <param name="deltaTime">Time since last tick</param>
    public virtual void Update(float deltaTime)
    {
    }

    /// <summary>
    ///     Initialize the element
    /// </summary>
    public abstract void Initialize();

    /// <summary>
    ///     Resize the element
    /// </summary>
    public abstract void Resize();

    /// <summary>
    ///     Triggered when the cursor enters the element
    /// </summary>
    public virtual void OnCursorEnter()
    {
    }

    /// <summary>
    ///     Triggered when the cursor leaves the element
    /// </summary>
    public virtual void OnCursorLeave()
    {
    }

    /// <summary>
    ///     A left click is performed (gets called even when cursor not inside of element)
    /// </summary>
    public virtual void OnLeftClick()
    {
    }

    /// <summary>
    ///     A right click is performed (gets called even when cursor not inside of element)
    /// </summary>
    public virtual void OnRightClick()
    {
    }

    /// <summary>
    ///     A scroll is performed (gets called even when cursor not inside of element)
    /// </summary>
    public virtual void OnScroll(Vector2 movement)
    {
    }
}