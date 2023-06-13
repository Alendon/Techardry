using System.Drawing;
using System.Numerics;
using JetBrains.Annotations;
using MintyCore.Registries;
using MintyCore.Render;
using MintyCore.Utils;
using Silk.NET.Vulkan;
using Techardry.Identifications;

namespace Techardry.UI;

/// <summary>
///     Helper class to build a border image
/// </summary>
public static class BorderBuilder
{
    public static void DrawBorder(CommandBuffer cb, [ValueRange(0, 1)] float borderWidth,
        Color? fillColor, BorderImages borderImages, IList<IDisposable> resourcesToDispose,
        Rect2D scissor, Viewport viewport)
    {
        Logger.AssertAndThrow(borderWidth is >= 0 and <= 1, "Border width must be in range 0-1", "UI");

        BorderResources borderResources = new();
        resourcesToDispose.Add(borderResources);
        
        var heightModifier = viewport.Width / viewport.Height;

        var absolutBorderWidth = borderWidth;
        var absolutBorderHeight = borderWidth * heightModifier;
        
        DrawBorderTexture(cb, borderImages.Left,
            new RectangleF(new PointF(0, 0),
                new SizeF(absolutBorderWidth, 1)), borderResources, scissor, viewport);
        DrawBorderTexture(cb, borderImages.Right,
            new RectangleF(new PointF(1 - absolutBorderWidth, 0),
                new SizeF(absolutBorderWidth, 1)), borderResources, scissor, viewport);
        DrawBorderTexture(cb, borderImages.Bottom,
            new RectangleF(new PointF(0, 0),
                new SizeF(1, absolutBorderHeight)), borderResources, scissor, viewport);
        DrawBorderTexture(cb, borderImages.Top,
            new RectangleF(new PointF(0, 1 - absolutBorderHeight),
                new SizeF(1, absolutBorderHeight)), borderResources, scissor, viewport);

        DrawBorderTexture(cb, borderImages.CornerLowerLeft,
            new RectangleF(new PointF(0,0), new SizeF(absolutBorderWidth, absolutBorderHeight)), borderResources, scissor,
            viewport);
        DrawBorderTexture(cb, borderImages.CornerLowerRight,
            new RectangleF(new PointF(1 - absolutBorderWidth, 0),
                new SizeF(absolutBorderWidth, absolutBorderHeight)), borderResources, scissor, viewport);
        DrawBorderTexture(cb, borderImages.CornerUpperLeft,
            new RectangleF(new PointF(0, 1 - absolutBorderHeight),
                new SizeF(absolutBorderWidth, absolutBorderHeight)), borderResources, scissor, viewport);
        DrawBorderTexture(cb, borderImages.CornerUpperRight,
            new RectangleF(new PointF(1 - absolutBorderWidth, 1 - absolutBorderHeight),
                new SizeF(absolutBorderWidth, absolutBorderHeight)), borderResources, scissor, viewport);

       
    }


    private static unsafe void DrawBorderTexture(CommandBuffer cb, Identification borderTextureId,
        RectangleF drawingRect, BorderResources borderResources, Rect2D scissor,
        Viewport viewport)
    {
        var textureDescriptor = TextureHandler.GetTextureBindResourceSet(borderTextureId);
        var vertexBuffer = UiHelper.CreateVertexBuffer(drawingRect, new RectangleF(0, 0, 1, 1));
        var pipeline = PipelineHandler.GetPipeline(PipelineIDs.UiTexturePipeline);
        var pipelineLayout = PipelineHandler.GetPipelineLayout(PipelineIDs.UiTexturePipeline);

        VulkanEngine.Vk.CmdBindPipeline(cb, PipelineBindPoint.Graphics, pipeline);
        VulkanEngine.Vk.CmdBindDescriptorSets(cb, PipelineBindPoint.Graphics, pipelineLayout, 0, 1, textureDescriptor,
            0, null);
        VulkanEngine.Vk.CmdBindVertexBuffers(cb, 0, 1, vertexBuffer.Buffer, 0);
        VulkanEngine.Vk.CmdSetScissor(cb, 0,1,scissor);
        VulkanEngine.Vk.CmdSetViewport(cb, 0, 1, viewport);
        
        VulkanEngine.Vk.CmdDraw(cb, 6, 1, 0, 0);

        borderResources.Buffers.Add(vertexBuffer);
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