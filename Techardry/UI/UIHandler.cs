using System.Drawing;
using System.Numerics;
using JetBrains.Annotations;
using MintyCore;
using MintyCore.Utils;
using Silk.NET.Input;

namespace Techardry.UI;

/// <summary>
///     Class to handle the user interface
/// </summary>
[PublicAPI]
public static class UiHandler
{
    private static readonly Dictionary<Identification, Identification> _uiRootElementCreators = new();
    private static readonly Dictionary<Identification, RootElement> _uiRootElements = new();
    private static readonly Dictionary<Identification, Func<Element>> _elementPrefabs = new();

    private static bool _lastLeftMouseState;
    private static bool _lastRightMouseState;

    private static bool _currentLeftMouseState;
    private static bool _currentRightMouseState;

    /// <summary>
    ///     Add a root element (an element which gets automatically updated)
    /// </summary>
    /// <param name="id"></param>
    /// <param name="element"></param>
    public static void AddRootElement(Identification id, Identification element)
    {
        _uiRootElementCreators.Add(id, element);
    }

    internal static void AddElementPrefab(Identification id, Func<Element> prefab)
    {
        _elementPrefabs.Add(id, prefab);
    }

    internal static void SetElementPrefab(Identification prefabId, Func<Element> prefabCreator)
    {
        _elementPrefabs.Remove(prefabId);
        AddElementPrefab(prefabId, prefabCreator);
    }

    internal static void SetRootElement(Identification elementId, Identification rootElement)
    {
        _uiRootElements.Remove(elementId);
        AddRootElement(elementId, rootElement);
    }

    /// <summary>
    ///     Get a root element
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    public static RootElement GetRootElement(Identification id)
    {
        return _uiRootElements[id];
    }

    /// <summary>
    ///     Create a new element
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    public static Element CreateElement(Identification id)
    {
        return _elementPrefabs[id]();
    }


    public static void Update()
    {
        _lastLeftMouseState = _currentLeftMouseState;
        _lastRightMouseState = _currentRightMouseState;
        _currentLeftMouseState = InputHandler.GetMouseDown(MouseButton.Left);
        _currentRightMouseState = InputHandler.GetMouseDown(MouseButton.Right);

        try
        {
            foreach (var rootElement in _uiRootElements.Values)
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
    public static void UpdateElement(Element element, Size rootSize, bool updateChildren = true)
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

    private static PointF GetUiCursorPosition()
    {
        return new PointF(InputHandler.MousePosition with { Y = Engine.Window!.Size.Y - InputHandler.MousePosition.Y });
    }

    internal static void Clear()
    {
        foreach (var rootElement in _uiRootElements.Values)
        {
            if (rootElement is Element element)
            {
                element.Dispose();
            }
        }

        _uiRootElementCreators.Clear();
        _uiRootElements.Clear();
        _elementPrefabs.Clear();
    }

    internal static void RemoveElement(Identification objectId)
    {
        _elementPrefabs.Remove(objectId);
        if (_uiRootElements.Remove(objectId, out var rootElement) && rootElement is Element element)
        {
            element.Dispose();
        }
        _uiRootElementCreators.Remove(objectId);
    }

    internal static void CreateRootElements()
    {
        foreach (var (elementId, creatorId) in _uiRootElementCreators)
        {
            if (_uiRootElements.ContainsKey(elementId)) continue;
            var element = CreateElement(creatorId);
            if (element is not RootElement rootElement) throw new Exception("Root elements must be of type RootElement");
            _uiRootElements.Add(elementId, rootElement);
        }
    }
}