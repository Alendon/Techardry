using System.Drawing;
using System.Numerics;
using System.Runtime.CompilerServices;
using MintyCore.Render;
using Silk.NET.Vulkan;

namespace Techardry.UI;

public static class UiHelper
{
    public static unsafe MemoryBuffer CreateVertexBuffer(RectangleF locationData, RectangleF uvData)
    {
        var queueSpan = (stackalloc uint[] { VulkanEngine.QueueFamilyIndexes.GraphicsFamily!.Value });
        var stagingBuffer = MemoryBuffer.Create(BufferUsageFlags.TransferSrcBit, (ulong)(sizeof(RectangleF) * 2),
            SharingMode.Exclusive, queueSpan, MemoryPropertyFlags.HostCoherentBit | MemoryPropertyFlags.HostVisibleBit,
            true);
        var data = (RectangleF*)MemoryManager.Map(stagingBuffer.Memory);
        data[0] = locationData;
        data[1] = uvData;
        MemoryManager.UnMap(stagingBuffer.Memory);

        var vertexBuffer = MemoryBuffer.Create(BufferUsageFlags.VertexBufferBit | BufferUsageFlags.TransferDstBit,
            (ulong)(sizeof(RectangleF) * 2),
            SharingMode.Exclusive, queueSpan, MemoryPropertyFlags.DeviceLocalBit, false);

        var cb = VulkanEngine.GetSingleTimeCommandBuffer();
        BufferCopy copy = new()
        {
            Size = stagingBuffer.Size,
        };

        VulkanEngine.Vk.CmdCopyBuffer(cb, stagingBuffer.Buffer, vertexBuffer.Buffer, 1, copy);
        VulkanEngine.ExecuteSingleTimeCommandBuffer(cb);
        stagingBuffer.Dispose();
        return vertexBuffer;
    }

    public static unsafe MemoryBuffer CreateVertexBuffer(RectangleF locationData, Color color)
    {
        var queueSpan = (stackalloc uint[] { VulkanEngine.QueueFamilyIndexes.GraphicsFamily!.Value });
        var stagingBuffer = MemoryBuffer.Create(BufferUsageFlags.TransferSrcBit, (ulong)(sizeof(RectangleF) * 2),
            SharingMode.Exclusive, queueSpan, MemoryPropertyFlags.HostCoherentBit | MemoryPropertyFlags.HostVisibleBit,
            true);
        var data = MemoryManager.Map(stagingBuffer.Memory);
        Unsafe.AsRef<RectangleF>(data.ToPointer()) = locationData;
        Unsafe.AsRef<Vector4>((data + sizeof(RectangleF)).ToPointer()) =
            new Vector4(color.R / 255f, color.G / 255f, color.B / 255f, color.A / 255f);
        MemoryManager.UnMap(stagingBuffer.Memory);

        var vertexBuffer = MemoryBuffer.Create(BufferUsageFlags.VertexBufferBit | BufferUsageFlags.TransferDstBit,
            (ulong)(sizeof(RectangleF) * 2),
            SharingMode.Exclusive, queueSpan, MemoryPropertyFlags.DeviceLocalBit, false);

        var cb = VulkanEngine.GetSingleTimeCommandBuffer();
        BufferCopy copy = new()
        {
            Size = stagingBuffer.Size,
        };

        VulkanEngine.Vk.CmdCopyBuffer(cb, stagingBuffer.Buffer, vertexBuffer.Buffer, 1, copy);
        VulkanEngine.ExecuteSingleTimeCommandBuffer(cb);
        stagingBuffer.Dispose();
        return vertexBuffer;
    }
}