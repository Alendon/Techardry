using System.Drawing;
using System.Numerics;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;
using MintyCore.Registries;
using MintyCore.Render;
using MintyCore.Utils;
using Silk.NET.Vulkan;
using Techardry.Identifications;
using Techardry.Render;

namespace Techardry.UI;

/// <summary>
///     Helper class to build a border image
/// </summary>
public static class BorderBuilder
{
    public static void DrawBorder(CommandBuffer cb, [ValueRange(0, 1)] float borderWidth,
        Color? fillColor, BorderImages borderImages, UiRenderer renderer,
        Rect2D scissor, Viewport viewport)
    {
        Logger.AssertAndThrow(borderWidth is >= 0 and <= 1, "Border width must be in range 0-1", "UI");

        BorderResources borderResources = new();
        renderer.Disposables.Add(borderResources);

        var heightModifier = viewport.Width / viewport.Height;

        var absolutBorderWidth = borderWidth;
        var absolutBorderHeight = borderWidth * heightModifier;

        if (fillColor is not null)
        {
            FillWithColor(cb, fillColor.Value, borderResources, scissor, viewport);
        }

        DrawBorderTexture(cb, borderImages.Left,
            new RectangleF(new PointF(0, 0),
                new SizeF(absolutBorderWidth, 1)), scissor, viewport);
        DrawBorderTexture(cb, borderImages.Right,
            new RectangleF(new PointF(1 - absolutBorderWidth, 0),
                new SizeF(absolutBorderWidth, 1)), scissor, viewport);
        DrawBorderTexture(cb, borderImages.Bottom,
            new RectangleF(new PointF(0, 0),
                new SizeF(1, absolutBorderHeight)), scissor, viewport);
        DrawBorderTexture(cb, borderImages.Top,
            new RectangleF(new PointF(0, 1 - absolutBorderHeight),
                new SizeF(1, absolutBorderHeight)), scissor, viewport);

        DrawBorderTexture(cb, borderImages.CornerLowerLeft,
            new RectangleF(new PointF(0, 0), new SizeF(absolutBorderWidth, absolutBorderHeight)), scissor,
            viewport);
        DrawBorderTexture(cb, borderImages.CornerLowerRight,
            new RectangleF(new PointF(1 - absolutBorderWidth, 0),
                new SizeF(absolutBorderWidth, absolutBorderHeight)), scissor, viewport);
        DrawBorderTexture(cb, borderImages.CornerUpperLeft,
            new RectangleF(new PointF(0, 1 - absolutBorderHeight),
                new SizeF(absolutBorderWidth, absolutBorderHeight)), scissor, viewport);
        DrawBorderTexture(cb, borderImages.CornerUpperRight,
            new RectangleF(new PointF(1 - absolutBorderWidth, 1 - absolutBorderHeight),
                new SizeF(absolutBorderWidth, absolutBorderHeight)), scissor, viewport);
    }


    private static unsafe void DrawBorderTexture(CommandBuffer cb, Identification borderTextureId,
        RectangleF drawingRect, Rect2D scissor, Viewport viewport)
    {
        var textureDescriptor = TextureHandler.GetTextureBindResourceSet(borderTextureId);
        var pipeline = PipelineHandler.GetPipeline(PipelineIDs.UiTexturePipeline);
        var pipelineLayout = PipelineHandler.GetPipelineLayout(PipelineIDs.UiTexturePipeline);

        VulkanEngine.Vk.CmdBindPipeline(cb, PipelineBindPoint.Graphics, pipeline);
        VulkanEngine.Vk.CmdBindDescriptorSets(cb, PipelineBindPoint.Graphics, pipelineLayout, 0, 1, textureDescriptor,
            0, null);
        VulkanEngine.Vk.CmdSetScissor(cb, 0, 1, scissor);
        VulkanEngine.Vk.CmdSetViewport(cb, 0, 1, viewport);

        var pushConstantValues = (stackalloc RectangleF[]
        {
            drawingRect,
            new RectangleF(0, 0, 1, 1)
        });
        VulkanEngine.Vk.CmdPushConstants(cb, pipelineLayout, ShaderStageFlags.VertexBit, 0,
            (uint)(sizeof(RectangleF) * 2), pushConstantValues);

        VulkanEngine.Vk.CmdDraw(cb, 6, 1, 0, 0);
    }

    private static unsafe void FillWithColor(CommandBuffer cb, Color color, BorderResources borderResources,
        Rect2D scissor, Viewport viewport)
    {
        var pipeline = PipelineHandler.GetPipeline(PipelineIDs.UiColorPipeline);
        var pipelineLayout = PipelineHandler.GetPipelineLayout(PipelineIDs.UiColorPipeline);

        var pushConstants = stackalloc float[4 * 2];
        Unsafe.AsRef<RectangleF>(pushConstants) = new RectangleF(0, 0, 1, 1);
        Unsafe.AsRef<Vector4>(pushConstants + 4) =
            new Vector4(color.R / 255f, color.G / 255f, color.B / 255f, color.A / 255f);

        VulkanEngine.Vk.CmdBindPipeline(cb, PipelineBindPoint.Graphics, pipeline);
        VulkanEngine.Vk.CmdPushConstants(cb, pipelineLayout, ShaderStageFlags.VertexBit, 0,
            sizeof(float) * 4 * 2, pushConstants);
        VulkanEngine.Vk.CmdSetScissor(cb, 0, 1, scissor);
        VulkanEngine.Vk.CmdSetViewport(cb, 0, 1, viewport);

        VulkanEngine.Vk.CmdDraw(cb, 6, 1, 0, 0);
    }


    class BorderResources : IDisposable
    {
        public List<MemoryBuffer> Buffers = new();

        public void Dispose()
        {
            foreach (var buffer in Buffers)
            {
                buffer.Dispose();
            }
        }
    }
}

/// <summary>
/// 
/// </summary>
public struct BorderImages
{
    /// <summary>
    /// 
    /// </summary>
    public Identification Left;

    /// <summary>
    /// 
    /// </summary>
    public Identification Right;

    /// <summary>
    /// 
    /// </summary>
    public Identification Top;

    /// <summary>
    /// 
    /// </summary>
    public Identification Bottom;

    /// <summary>
    /// 
    /// </summary>
    public Identification CornerUpperLeft;

    /// <summary>
    /// 
    /// </summary>
    public Identification CornerUpperRight;

    /// <summary>
    /// 
    /// </summary>
    public Identification CornerLowerLeft;

    /// <summary>
    /// 
    /// </summary>
    public Identification CornerLowerRight;
}