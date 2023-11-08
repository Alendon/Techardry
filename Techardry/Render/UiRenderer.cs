using System.Drawing;
using System.Numerics;
using System.Runtime.CompilerServices;
using DotNext.Collections.Generic;
using FontStashSharp;
using JetBrains.Annotations;
using MintyCore.Modding;
using MintyCore.Render;
using MintyCore.Render.Managers.Interfaces;
using MintyCore.Utils;
using Silk.NET.Vulkan;
using Techardry.Identifications;
using Techardry.UI;
using Techardry.UI.Interfaces;

namespace Techardry.Render;

/// <summary>
///     The main UI renderer
/// </summary>
[PublicAPI]
[Singleton<IUiRenderer>(SingletonContextFlags.NoHeadless)]
internal sealed class UiRenderer : IUiRenderer
{
    private readonly IVulkanEngine _vulkanEngine;
    private readonly IModManager _modManager;
    private readonly IPipelineManager _pipelineManager;
    private readonly ITextureManager _textureManager;
    private readonly IFontTextureManager _fontTextureManager;
    
    private uint FrameIndex => _vulkanEngine.ImageIndex;

    public CommandBuffer CommandBuffer { get; set; }

    private List<IDisposable>?[] _disposables;

    private FontSystem[] _fontSystems;
    private FontRenderer[] _fontRenderers;

    public UiRenderer(IVulkanEngine vulkanEngine, IModManager modManager, IPipelineManager pipelineManager,
        ITextureManager textureManager, IFontTextureManager fontTextureManager)
    {
        _vulkanEngine = vulkanEngine;
        _modManager = modManager;
        _pipelineManager = pipelineManager;
        _textureManager = textureManager;
        _fontTextureManager = fontTextureManager;

        var fontSettings = new FontSystemSettings()
        {
            FontResolutionFactor = 2,
            KernelHeight = 2,
            KernelWidth = 2,
            TextureWidth = 4096,
            TextureHeight = 4096,
        };

        _disposables = new List<IDisposable>?[_vulkanEngine.SwapchainImageCount];
        _fontSystems = new FontSystem[_vulkanEngine.SwapchainImageCount];
        foreach (ref var fontSystem in _fontSystems.AsSpan())
        {
            fontSystem = new FontSystem(fontSettings);
            fontSystem.AddFont(_modManager.GetResourceFileStream(FontIDs.Akashi));
        }

        _fontRenderers = new FontRenderer[_vulkanEngine.SwapchainImageCount];
        foreach (ref var fontRenderer in _fontRenderers.AsSpan())
        {
            fontRenderer = new FontRenderer(fontTextureManager, vulkanEngine, pipelineManager);
        }
    }

    private List<IDisposable> Disposables { get; set; } = new();

    public void DrawUi(Element rootElement)
    {
        _disposables[FrameIndex]?.ForEach(x => x.Dispose());
        if (rootElement is null || rootElement is not IRootElement root) return;

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
                Height = (uint) root.PixelSize.Height,
                Width = (uint) root.PixelSize.Width
            }
        };

        rootElement.Draw(this, scissor, viewport);

        _disposables[FrameIndex] = Disposables;
        Disposables = new List<IDisposable>();
    }

    public void UpdateInternalTextures(CommandBuffer commandBuffer)
    {
        _fontRenderers[FrameIndex].FontTextureManager.ManagedTextures.ForEach(x => x.ApplyChanges(commandBuffer));
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
        var textureDescriptor = _textureManager.GetTextureBindResourceSet(textureId);
        var pipeline = _pipelineManager.GetPipeline(PipelineIDs.UiTexturePipeline);
        var pipelineLayout = _pipelineManager.GetPipelineLayout(PipelineIDs.UiTexturePipeline);

        _vulkanEngine.Vk.CmdBindPipeline(CommandBuffer, PipelineBindPoint.Graphics, pipeline);
        _vulkanEngine.Vk.CmdBindDescriptorSets(CommandBuffer, PipelineBindPoint.Graphics, pipelineLayout, 0, 1,
            textureDescriptor,
            0, null);
        _vulkanEngine.Vk.CmdSetScissor(CommandBuffer, 0, 1, scissor);
        _vulkanEngine.Vk.CmdSetViewport(CommandBuffer, 0, 1, viewport);

        var pushConstantValues = (stackalloc RectangleF[]
        {
            drawingRect,
            uvRect
        });
        _vulkanEngine.Vk.CmdPushConstants(CommandBuffer, pipelineLayout, ShaderStageFlags.VertexBit, 0,
            (uint) (sizeof(RectangleF) * 2), pushConstantValues);

        _vulkanEngine.Vk.CmdDraw(CommandBuffer, 6, 1, 0, 0);
    }

    public unsafe void FillColor(Color color,
        Rect2D scissor, Viewport viewport)
    {
        var pipeline = _pipelineManager.GetPipeline(PipelineIDs.UiColorPipeline);
        var pipelineLayout = _pipelineManager.GetPipelineLayout(PipelineIDs.UiColorPipeline);

        var pushConstants = stackalloc float[4 * 2];
        Unsafe.AsRef<RectangleF>(pushConstants) = new RectangleF(0, 0, 1, 1);
        Unsafe.AsRef<Vector4>(pushConstants + 4) =
            new Vector4(color.R / 255f, color.G / 255f, color.B / 255f, color.A / 255f);

        _vulkanEngine.Vk.CmdBindPipeline(CommandBuffer, PipelineBindPoint.Graphics, pipeline);
        _vulkanEngine.Vk.CmdPushConstants(CommandBuffer, pipelineLayout, ShaderStageFlags.VertexBit, 0,
            sizeof(float) * 4 * 2, pushConstants);
        _vulkanEngine.Vk.CmdSetScissor(CommandBuffer, 0, 1, scissor);
        _vulkanEngine.Vk.CmdSetViewport(CommandBuffer, 0, 1, viewport);

        _vulkanEngine.Vk.CmdDraw(CommandBuffer, 6, 1, 0, 0);
    }

    public void Dispose()
    {
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