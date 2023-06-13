using FontStashSharp.Interfaces;
using MintyCore.Identifications;
using MintyCore.Render;
using Silk.NET.Vulkan;
using Techardry.UI;

namespace Techardry.Render;

/// <summary>
///     The main UI renderer
/// </summary>
public class UiRenderer
{
    private Element? _rootElement;

    private uint FrameIndex => VulkanEngine.ImageIndex;

    private List<IDisposable>?[] _disposables = new List<IDisposable>?[VulkanEngine.SwapchainImageCount];

    /// <summary>
    ///     Set the root ui element
    /// </summary>
    public void SetUiContext(Element? rootUiElement)
    {
        _rootElement = rootUiElement;
    }

    public void DrawUi()
    {
        if (_rootElement is null) return;
        VulkanEngine.SetActiveRenderPass(RenderPassHandler.GetRenderPass(RenderPassIDs.Main),
            SubpassContents.SecondaryCommandBuffers);
        DrawToScreen();
    }

    private void DrawToScreen()
    {
        _disposables[FrameIndex]?.ForEach(x => x.Dispose());
        if (_rootElement is null || _rootElement is not RootElement root) return;

        var cb = VulkanEngine.GetSecondaryCommandBuffer();
        List<IDisposable> disposables = new();

        var viewport = new Viewport()
        {
            X = 0,
            Y = 0,
            MaxDepth = 1f,
            Height = root.PixelSize.Height,
            Width = root.PixelSize.Width
        };
        
        var scissor = new Rect2D()
        {
            Offset = new Offset2D(),
            Extent = new Extent2D()
            {
                Height = (uint)root.PixelSize.Height,
                Width = (uint)root.PixelSize.Width
            }
        };

        _rootElement.Draw(cb, disposables, scissor, viewport);
        VulkanEngine.ExecuteSecondary(cb);
        _disposables[FrameIndex] = disposables;
    }
}