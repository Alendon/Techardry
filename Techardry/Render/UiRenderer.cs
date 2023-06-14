using System.Numerics;
using DotNext.Collections.Generic;
using FontStashSharp;
using FontStashSharp.Interfaces;
using FontStashSharp.Rasterizers.StbTrueTypeSharp;
using MintyCore.Modding;
using MintyCore.Render;
using MintyCore.Utils;
using Silk.NET.Vulkan;
using Techardry.Identifications;
using Techardry.UI;
using RenderPassIDs = MintyCore.Identifications.RenderPassIDs;

namespace Techardry.Render;

/// <summary>
///     The main UI renderer
/// </summary>
public class UiRenderer
{
    private Element? _rootElement;

    private uint FrameIndex => VulkanEngine.ImageIndex;

    private List<IDisposable>?[] _disposables = new List<IDisposable>?[VulkanEngine.SwapchainImageCount];

    private FontSystem[] _fontSystems;
    private FontRenderer[] _fontRenderers;

    public UiRenderer()
    {
        var fontSettings = new FontSystemSettings()
        {
            FontResolutionFactor = 2,
            KernelHeight = 2,
            KernelWidth = 2,
            TextureWidth = 4096,
            TextureHeight = 4096,
        };

        _fontSystems = new FontSystem[VulkanEngine.SwapchainImageCount];
        foreach (ref var fontSystem in _fontSystems.AsSpan())
        {
            fontSystem = new FontSystem(fontSettings);
            fontSystem.AddFont(ModManager.GetResourceFileStream(FontIDs.Akashi));
        }

        _fontRenderers = new FontRenderer[VulkanEngine.SwapchainImageCount];
        foreach (ref var fontTextureManager in _fontRenderers.AsSpan())
        {
            fontTextureManager = new FontRenderer();
        }
    }

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

        var cb = VulkanEngine.GetSecondaryCommandBuffer();
        DrawToScreen(cb);
        
        var singleTimeCb = VulkanEngine.GetSingleTimeCommandBuffer();
        _fontRenderers[FrameIndex].FontTextureManager.ManagedTextures.ForEach(x => x.ApplyChanges(singleTimeCb));
        VulkanEngine.ExecuteSingleTimeCommandBuffer(singleTimeCb);
        
        VulkanEngine.ExecuteSecondary(cb);
    }

    public List<IDisposable> Disposables { get; private set; } = new();

    private void DrawToScreen(CommandBuffer cb)
    {
        _disposables[FrameIndex]?.ForEach(x => x.Dispose());
        if (_rootElement is null || _rootElement is not RootElement root) return;

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

        _rootElement.Draw(cb, this, scissor, viewport);

        _disposables[FrameIndex] = Disposables;
        Disposables = new List<IDisposable>();
    }

    public virtual void DrawString(string text, int fontSize, CommandBuffer commandBuffer, Viewport viewport,
        Rect2D scissor, Vector2 position,
        FSColor color, Vector2? scale = null, float rotation = 0f, Vector2 origin = default, float layerDepth = 0f,
        float characterSpacing = 0f, float lineSpacing = 0f, TextStyle textStyle = TextStyle.None,
        FontSystemEffect effect = FontSystemEffect.None, int effectAmount = 0)
    {
        var font = _fontSystems[FrameIndex].GetFont(fontSize);
        var renderer = _fontRenderers[FrameIndex];

        renderer.PrepareNextDraw(commandBuffer, viewport, scissor);
        font.DrawText(renderer, text, position, color, scale, rotation, origin, layerDepth, characterSpacing,
            lineSpacing, textStyle, effect, effectAmount);
        renderer.EndDraw();
    }
}