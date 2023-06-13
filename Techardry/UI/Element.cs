using System.Drawing;
using System.Numerics;
using FontStashSharp;
using JetBrains.Annotations;
using MintyCore.Utils;
using Silk.NET.Vulkan;

namespace Techardry.UI;

/// <summary>
///     Abstract base class for all Ui Elements
/// </summary>
[PublicAPI]
public abstract class Element : IDisposable
{
    private bool _redraw;

    /// <summary />
    protected Element(RectangleF relativeLayout)
    {
        RelativeLayout = relativeLayout;
    }


    /// <summary>
    ///     Indicator if the element needs to be redrawn
    /// </summary>
    //public virtual bool Redraw { get; protected set; } = true;

    public abstract void Draw(CommandBuffer commandBuffer, IList<IDisposable> resourcesToDispose, Rect2D scissor, Viewport viewport);

    public virtual void DrawString(string text, Identification font, CommandBuffer commandBuffer, Vector2 position,
        FSColor color, Vector2? scale = null, float rotation = 0f, Vector2 origin = default, float layerDepth = 0f,
        float characterSpacing = 0f, float lineSpacing = 0f, TextStyle textStyle = TextStyle.None,
        FontSystemEffect effect = FontSystemEffect.None, int effectAmount = 0)
    {
        /*SpriteFontBase fotn = FontSystem.GetFont(font);
        FontSystem a;
        IFontStashRenderer2 renderer2 = FontSystem.GetRenderer2(font);
        renderer2.prepareDraw(commandBuffer);
        
        fotn.DrawText(renderer2, text, position, color, scale, rotation, origin, layerDepth, characterSpacing,
            lineSpacing, textStyle, effect, effectAmount);
        
        //This line actually draws the text
        //the DrawText method only prepares the draw and records the needed draw information
        //This method combines those information to execute a single draw call
        renderer2.EndDraw(commandBuffer);    
            */

        Console.WriteLine("Oh no, drawing strings on ui elements is not implemented yet");
    }

    public virtual bool HasChanged { get; protected set; }

    /// <summary>
    ///     The parent of this Element
    /// </summary>
    public Element? Parent { get; set; }

    /// <summary>
    ///     The layout off the element relative to the parent
    ///     Values needs to be in Range 0f-1f
    ///     <remarks>The (0,0) coordinate is the lower left corner</remarks>
    /// </summary>
    public RectangleF RelativeLayout { get; }

    /// <summary>
    ///     The absolute layout of the element
    ///     Values needs to be in Range 0f-1f
    ///     <remarks>The (0,0) coordinate is the lower left corner</remarks>
    /// </summary>
    public virtual RectangleF AbsoluteLayout
    {
        get
        {
            if (this is RootElement) return new RectangleF(0, 0, 1, 1);
            Logger.AssertAndThrow(Parent is not null, "Cannot get absolute layout of element as parent is null", "UI");
            return new RectangleF(Parent.AbsoluteLayout.X + Parent.AbsoluteLayout.Width * RelativeLayout.X,
                Parent.AbsoluteLayout.Y + Parent.AbsoluteLayout.Height * RelativeLayout.Y,
                Parent.AbsoluteLayout.Width * RelativeLayout.Width, Parent.AbsoluteLayout.Height * RelativeLayout.Height);
        }
    }

    /// <summary>
    ///     Whether or not the cursor is hovering over the element
    /// </summary>
    public bool CursorHovering { get; set; }

    /// <summary>
    ///     The cursor position relative to the element
    /// </summary>
    public Vector2 CursorPosition { get; set; }

    /// <summary>
    ///     Get/set whether or not this component is active (will get updated)
    /// </summary>
    public virtual bool IsActive { get; set; }
    
    
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
    public virtual void Initialize()
    {
        
    }

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

    protected virtual void Dispose(bool disposing)
    {
        
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~Element()
    {
        Dispose(false);
    }
}