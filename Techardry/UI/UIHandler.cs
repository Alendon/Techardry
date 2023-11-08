using System.Drawing;
using System.Numerics;
using JetBrains.Annotations;
using MintyCore;
using MintyCore.Utils;
using Silk.NET.Input;
using Techardry.UI.Elements;
using Techardry.UI.Interfaces;

namespace Techardry.UI;

/// <summary>
///     Class to handle the user interface
/// </summary>
public class UiHandler : IUiHandler
{
    private readonly Dictionary<Identification, Identification> UiRootElementCreators = new();
    private readonly Dictionary<Identification, IRootElement> UiRootElements = new();
    private readonly Dictionary<Identification, Func<Element>> ElementPrefabs = new();
    
    public required IInputHandler InputHandler { private get; init; }

    private bool _lastLeftMouseState;
    private bool _lastRightMouseState;

    private bool _currentLeftMouseState;
    private bool _currentRightMouseState;

    /// <summary>
    ///     Get a root element
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    public IRootElement GetRootElement(Identification id)
    {
        return UiRootElements[id];
    }

    /// <summary>
    ///     Create a new element
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    public Element CreateElement(Identification id)
    {
        return ElementPrefabs[id]();
    }


    public void Update()
    {
        _lastLeftMouseState = _currentLeftMouseState;
        _lastRightMouseState = _currentRightMouseState;
        _currentLeftMouseState = InputHandler.GetMouseDown(MouseButton.Left);
        _currentRightMouseState = InputHandler.GetMouseDown(MouseButton.Right);

        try
        {
            foreach (var rootElement in UiRootElements.Values)
            {
                if (rootElement is not Element element) throw new Exception();
                UpdateElement(element, rootElement.PixelSize);
            }
        }
        catch (InvalidOperationException)
        {
            //Ignore. Happens when Main Menu is updated, a game starts and end. At the end the root elements get cleared. The collection invalidated.
            //TODO fix this
        }
    }

    /// <summary>
    ///     Update a specific element
    /// </summary>
    /// <param name="element">Element to update</param>
    /// <param name="updateChildren">Whether or not the children should be updated</param>
    public void UpdateElement(Element element, Size rootSize, bool updateChildren = true)
    {
        if (!element.IsActive) return;

        var cursorPos = GetUiCursorPosition();

        var absoluteLayout = new RectangleF(element.RelativeLayout.X * rootSize.Width,
            element.RelativeLayout.Y * rootSize.Height,
            rootSize.Width * element.RelativeLayout.Width, rootSize.Height * element.RelativeLayout.Height);

        if (absoluteLayout.Contains(cursorPos))
        {
            if (!element.CursorHovering)
            {
                element.CursorHovering = true;
                element.OnCursorEnter();
            }

            //calculate the relative position of the cursor in the element from 0 to 1

            element.CursorPosition = new Vector2((cursorPos.X - absoluteLayout.X) / absoluteLayout.Width,
                (cursorPos.Y - absoluteLayout.Y) / absoluteLayout.Height);
        }
        else
        {
            if (element.CursorHovering)
            {
                element.CursorHovering = false;
                element.OnCursorLeave();
            }
        }

        if (!_lastLeftMouseState && _currentLeftMouseState) element.OnLeftClick();

        if (!_lastRightMouseState && _currentRightMouseState) element.OnRightClick();

        if (InputHandler.ScrollWheelDelta != Vector2.Zero) element.OnScroll(InputHandler.ScrollWheelDelta);

        element.Update(Engine.DeltaTime);
        if (!updateChildren || element is not ElementContainer elementContainer) return;

        foreach (var childElement in elementContainer.GetChildElements())
        {
            UpdateElement(childElement, rootSize);
        }
    }

    private PointF GetUiCursorPosition()
    {
        return new PointF(InputHandler.MousePosition with { Y = Engine.Window!.Size.Y - InputHandler.MousePosition.Y });
    }

    public void Clear()
    {
        foreach (var rootElement in UiRootElements.Values)
        {
            if (rootElement is Element element)
            {
                element.Dispose();
            }
        }

        UiRootElementCreators.Clear();
        UiRootElements.Clear();
        ElementPrefabs.Clear();
    }

    public void RemoveElement(Identification objectId)
    {
        ElementPrefabs.Remove(objectId);
        if (UiRootElements.Remove(objectId, out var rootElement) && rootElement is Element element)
        {
            element.Dispose();
        }
        UiRootElementCreators.Remove(objectId);
    }

    public void CreateRootElements()
    {
        foreach (var (elementId, creatorId) in UiRootElementCreators)
        {
            if (UiRootElements.ContainsKey(elementId)) continue;
            var element = CreateElement(creatorId);
            if (element is not IRootElement rootElement) throw new Exception("Root elements must be of type RootElement");
            UiRootElements.Add(elementId, rootElement);
        }
    }
}