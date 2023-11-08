using System.Drawing;
using MintyCore.Utils;
using Techardry.UI.Interfaces;

namespace Techardry.UI;

public interface IUiHandler
{
    /// <summary>
    ///     Add a root element (an element which gets automatically updated)
    /// </summary>
    /// <param name="id"></param>
    /// <param name="element"></param>
    void AddRootElement<TRootElement>(Identification id) where TRootElement : Element, IRootElement;

    /// <summary>
    ///     Get a root element
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    IRootElement GetRootElement(Identification id);

    /// <summary>
    ///     Create a new element
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    Element CreateElement(Identification id);

    void Update();

    /// <summary>
    ///     Update a specific element
    /// </summary>
    /// <param name="element">Element to update</param>
    /// <param name="updateChildren">Whether or not the children should be updated</param>
    void UpdateElement(Element element, Size rootSize, bool updateChildren = true);

    void Clear();
    void RemoveElement(Identification objectId);
    void CreateRootElements();
}