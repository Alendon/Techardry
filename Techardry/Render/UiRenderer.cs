using System.Drawing;
using System.Numerics;
using System.Runtime.CompilerServices;
using DotNext.Collections.Generic;
using FontStashSharp;
using JetBrains.Annotations;
using MintyCore.Modding;
using MintyCore.Render;
using MintyCore.Utils;
using Silk.NET.Vulkan;
using Techardry.Identifications;
using Techardry.UI;
using Techardry.UI.Interfaces;
using RenderPassIDs = MintyCore.Identifications.RenderPassIDs;

namespace Techardry.Render;

/// <summary>
///     The main UI renderer
/// </summary>
[PublicAPI]
public class UiRenderer : IDisposable
{
    private Element? _rootElement;

    private uint FrameIndex => VulkanEngine.ImageIndex;

    public Size RenderSize => (_rootElement as IRootElement)?.PixelSize ?? Size.Empty;
    public CommandBuffer CommandBuffer { get; private set; }

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

        CommandBuffer = VulkanEngine.GetSecondaryCommandBuffer();
        DrawToScreen();

        var singleTimeCb = VulkanEngine.GetSingleTimeCommandBuffer();
        _fontRenderers[FrameIndex].FontTextureManager.ManagedTextures.ForEach(x => x.ApplyChanges(singleTimeCb));
        VulkanEngine.ExecuteSingleTimeCommandBuffer(singleTimeCb);

        VulkanEngine.ExecuteSecondary(CommandBuffer);
        CommandBuffer = default;
    }

    public List<IDisposable> Disposables { get; private set; } = new();

    private void DrawToScreen()
    {
        _disposables[FrameIndex]?.ForEach(x => x.Dispose());
        if (_rootElement is null || _rootElement is not IRootElement root) return;

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

        _rootElement.Draw(this, scissor, viewport);

        _disposables[FrameIndex] = Disposables;
        Disposables = new List<IDisposable>();
    }

    public Bounds MeasureString(string text, int fontSize, Vector2 position, Vector2? scale = null,
        float characterSpacing = 0f, float lineSpacing = 0f, FontSystemEffect effect = FontSystemEffect.None,
        int effectAmount = 0)
    {
        var font = _fontSystems[FrameIndex].GetFont(fontSize);
        return font.TextBounds(text, position, scale, characterSpacing, lineSpacing, effect, effectAmount);
    }

    public void DrawString(string text, int fontSize, Viewport viewport,
        Rect2D scissor, Vector2 position,
        FSColor color, Vector2? scale = null, float rotation = 0f, Vector2 origin = default, float layerDepth = 0f,
        float characterSpacing = 0f, float lineSpacing = 0f, TextStyle textStyle = TextStyle.None,
        FontSystemEffect effect = FontSystemEffect.None, int effectAmount = 0)
    {
        var font = _fontSystems[FrameIndex].GetFont(fontSize);
        var renderer = _fontRenderers[FrameIndex];

        renderer.PrepareNextDraw(CommandBuffer, viewport, scissor);
        font.DrawText(renderer, text, position, color, scale, rotation, origin, layerDepth, characterSpacing,
            lineSpacing, textStyle, effect, effectAmount);
        renderer.EndDraw();
    }

    public unsafe void DrawTexture(Identification textureId,
        RectangleF drawingRect, RectangleF uvRect, Rect2D scissor, Viewport viewport)
    {
        var textureDescriptor = TextureHandler.GetTextureBindResourceSet(textureId);
        var pipeline = PipelineHandler.GetPipeline(PipelineIDs.UiTexturePipeline);
        var pipelineLayout = PipelineHandler.GetPipelineLayout(PipelineIDs.UiTexturePipeline);

        VulkanEngine.Vk.CmdBindPipeline(CommandBuffer, PipelineBindPoint.Graphics, pipeline);
        VulkanEngine.Vk.CmdBindDescriptorSets(CommandBuffer, PipelineBindPoint.Graphics, pipelineLayout, 0, 1,
            textureDescriptor,
            0, null);
        VulkanEngine.Vk.CmdSetScissor(CommandBuffer, 0, 1, scissor);
        VulkanEngine.Vk.CmdSetViewport(CommandBuffer, 0, 1, viewport);

        var pushConstantValues = (stackalloc RectangleF[]
        {
            drawingRect,
            uvRect
        });
        VulkanEngine.Vk.CmdPushConstants(CommandBuffer, pipelineLayout, ShaderStageFlags.VertexBit, 0,
            (uint)(sizeof(RectangleF) * 2), pushConstantValues);

        VulkanEngine.Vk.CmdDraw(CommandBuffer, 6, 1, 0, 0);
    }

    public unsafe void FillColor(Color color,
        Rect2D scissor, Viewport viewport)
    {
        var pipeline = PipelineHandler.GetPipeline(PipelineIDs.UiColorPipeline);
        var pipelineLayout = PipelineHandler.GetPipelineLayout(PipelineIDs.UiColorPipeline);

        var pushConstants = stackalloc float[4 * 2];
        Unsafe.AsRef<RectangleF>(pushConstants) = new RectangleF(0, 0, 1, 1);
        Unsafe.AsRef<Vector4>(pushConstants + 4) =
            new Vector4(color.R / 255f, color.G / 255f, color.B / 255f, color.A / 255f);

        VulkanEngine.Vk.CmdBindPipeline(CommandBuffer, PipelineBindPoint.Graphics, pipeline);
        VulkanEngine.Vk.CmdPushConstants(CommandBuffer, pipelineLayout, ShaderStageFlags.VertexBit, 0,
            sizeof(float) * 4 * 2, pushConstants);
        VulkanEngine.Vk.CmdSetScissor(CommandBuffer, 0, 1, scissor);
        VulkanEngine.Vk.CmdSetViewport(CommandBuffer, 0, 1, viewport);

        VulkanEngine.Vk.CmdDraw(CommandBuffer, 6, 1, 0, 0);
    }

    public void Dispose()
    {
        _rootElement?.Dispose();

        foreach (var fontSystem in _fontSystems.AsSpan())
        {
            foreach (var fontAtlas in fontSystem.Atlases)
            {
                FontTextureWrapper? wrapper = fontAtlas.Texture as FontTextureWrapper;
                wrapper?.Dispose();
            }

            fontSystem.Dispose();
        }

        foreach (var disposableList in _disposables)
        {
            foreach (var disposable in disposableList ?? Enumerable.Empty<IDisposable>())
            {
                disposable.Dispose();
            }

            disposableList?.Clear();
        }
    }
}