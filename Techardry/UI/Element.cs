using System.Drawing;
using System.Numerics;
using JetBrains.Annotations;
using MintyCore.Utils;
using Silk.NET.Vulkan;
using Techardry.Render;
using Techardry.UI.Interfaces;

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

    public abstract void Draw(IUiRenderer renderer, Rect2D scissor, Viewport viewport);

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
    public RectangleF RelativeLayout { get; set; }

    /// <summary>
    ///     The absolute layout of the element
    ///     Values needs to be in Range 0f-1f
    ///     <remarks>The (0,0) coordinate is the lower left corner</remarks>
    /// </summary>
    public virtual RectangleF AbsoluteLayout
    {
        get
        {
            if (this is IRootElement) return new RectangleF(0, 0, 1, 1);
            Logger.AssertAndThrow(Parent is not null, "Cannot get absolute layout of element as parent is null", "UI");
            return new RectangleF(Parent.AbsoluteLayout.X + Parent.AbsoluteLayout.Width * RelativeLayout.X,
                Parent.AbsoluteLayout.Y + Parent.AbsoluteLayout.Height * RelativeLayout.Y,
                Parent.AbsoluteLayout.Width * RelativeLayout.Width, Parent.AbsoluteLayout.Height * RelativeLayout.Height);
        }
    }

    public Size RootPixelSize
    {
        get
        {
            var current = this;
            while (current is not IRootElement)
            {
                if (current.Parent is null)
                {
                    Logger.WriteLog("Cannot get root pixel size of element, as no root element was found", LogImportance.Error, "UI");
                    return new Size(0, 0);
                }
                
                current = current.Parent;
            }

            return (current as IRootElement)!.PixelSize;
        }
    }

    public Size ElementPixelSize
    {
        get
        {
            var rootSize = RootPixelSize;
            return new Size((int) (rootSize.Width * AbsoluteLayout.Width), (int) (rootSize.Height * AbsoluteLayout.Height));
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
    ///     Triggered when the window or the parent element is resized
    /// </summary>
    public virtual void OnResize()
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