using System.Drawing;
using JetBrains.Annotations;
using MintyCore.Utils;
using Silk.NET.Vulkan;
using Techardry.Render;
using Techardry.UI.Interfaces;

namespace Techardry.UI.Elements;

/// <summary>
///     A generic element which can contain multiple child elements
/// </summary>
public class ElementContainer : Element
{
    private readonly List<Element> _containingElements = new();

    /// <summary>
    ///     Constructor
    /// </summary>
    /// <param name="layout"></param>
    // ReSharper disable once NotNullMemberIsNotInitialized
    public ElementContainer(RectangleF layout) : base(layout)
    {
    }

    /// <inheritdoc />
    public override void Initialize()
    {
    }

    
    /*public override bool Redraw
    {
        get => _redraw || GetChildElements().Any(element => element.Redraw);
        protected set => _redraw = value;
    }*/

    public override void Draw(UiRenderer renderer, Rect2D scissor, Viewport viewport)
    {
        foreach (var childElement in GetChildElements())
        {
            var childViewport = viewport;
            childViewport.Width *= childElement.RelativeLayout.Width;
            childViewport.Height *= childElement.RelativeLayout.Height;
            childViewport.X += (int)(viewport.Width * childElement.RelativeLayout.X);
            childViewport.Y += (int)(viewport.Height * childElement.RelativeLayout.Y);
            childElement.Draw(renderer, scissor, childViewport);
        }
    }

    /// <summary>
    ///     Add a new child element
    /// </summary>
    /// <param name="element">Element to add as a child</param>
    [PublicAPI]
    public void AddElement(Element element)
    {
        if (element is IRootElement)
            Logger.WriteLog("Root element can not be added as a child", LogImportance.Exception, "UI");

        if (!RelativeLayout.Contains(element.RelativeLayout))
        {
            Logger.WriteLog("Element to add is not inside parent bounds", LogImportance.Warning, "UI");
        }

        if (_containingElements.Any(childElement => element.RelativeLayout.IntersectsWith(childElement.RelativeLayout)))
        {
            Logger.WriteLog("Element to add overlaps with existing element", LogImportance.Warning, "UI");
        }

        _containingElements.Add(element);
        element.Parent = this;
        element.Initialize();
    }

    [PublicAPI]
    public virtual IEnumerable<Element> GetChildElements()
    {
        return _containingElements;
    }

    protected override void Dispose(bool disposing)
    {
        foreach (var childElement in GetChildElements())
        {
            childElement.Dispose();
        }
        
        base.Dispose(disposing);
    }

    public override void OnResize()
    {
        base.OnResize();
        foreach (var childElement in GetChildElements())
        {
            childElement.OnResize();
        }
    }
}