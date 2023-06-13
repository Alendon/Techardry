using System.Drawing;
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
}