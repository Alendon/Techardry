using System;
using System.Numerics;
using System.Runtime.InteropServices;
using MintyCore.Components.Common;
using MintyCore.Graphics;
using MintyCore.Graphics.Managers;
using MintyCore.Graphics.Render;
using MintyCore.Graphics.Render.Data;
using MintyCore.Graphics.Render.Data.RegistryWrapper;
using MintyCore.Graphics.VulkanObjects;
using MintyCore.Registries;
using MintyCore.Utils;
using Serilog;
using Silk.NET.Vulkan;
using Techardry.Components.Client;
using Techardry.Identifications;

namespace Techardry.Render;

[RegisterInputDataModule("camera")]
public class CameraInputModule(
    IMemoryManager memoryManager,
    IVulkanEngine vulkanEngine,
    IDescriptorSetManager descriptorSetManager) : InputModule
{
    private SingletonInputData<CameraInputData>? _cameraData;
    private Func<CameraIntermediateData>? _intermediateDataFunc;
    private MemoryBuffer? _stagingBuffer;

    public override void Setup()
    {
        _cameraData = ModuleDataAccessor.UseSingletonInputData<CameraInputData>(RenderInputDataIDs.Camera, this);
        _intermediateDataFunc =
            ModuleDataAccessor.ProvideIntermediateData<CameraIntermediateData>(IntermediateRenderDataIDs.Camera, this);

        Span<uint> queueFamilyIndices = [vulkanEngine.GraphicQueue.familyIndex];
        _stagingBuffer = memoryManager.CreateBuffer(BufferUsageFlags.TransferSrcBit,
            (ulong)Marshal.SizeOf<CameraData>(), queueFamilyIndices,
            MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit, true);
    }

    public override unsafe void Update(ManagedCommandBuffer commandBuffer)
    {
        if (_cameraData is null || _intermediateDataFunc is null)
        {
            Log.Error("Camera Input Module is not setup correctly");
            return;
        }
        
        var (camera, position) = _cameraData.AcquireData();
        var intermediateData = _intermediateDataFunc();

        ref var buffer = ref intermediateData.CameraBuffer;
        if (buffer is null)
        {
            Span<uint> queueFamilyIndices = [vulkanEngine.GraphicQueue.familyIndex];
            buffer = memoryManager.CreateBuffer(BufferUsageFlags.TransferDstBit | BufferUsageFlags.UniformBufferBit,
                (ulong)Marshal.SizeOf<CameraData>(), queueFamilyIndices,
                MemoryPropertyFlags.DeviceLocalBit, false);
        }

        ref var cameraData = ref _stagingBuffer!.MapAs<CameraData>()[0];
        cameraData.Forward = camera.Forward;
        cameraData.Upward = camera.Upward;
        cameraData.AspectRatio = vulkanEngine.SwapchainExtent.Width / (float)vulkanEngine.SwapchainExtent.Height;
        cameraData.HFov = camera.Fov;
        cameraData.Position = position.Value;
        cameraData.Near = camera.NearPlane;
        cameraData.Far = camera.FarPlane;
        _stagingBuffer!.Unmap();

        commandBuffer.CopyBuffer(_stagingBuffer, buffer);

        // Create and bind descriptor set
        if (intermediateData.CameraDescriptorSet.Handle != 0) return;
        intermediateData.CameraDescriptorSet =
            descriptorSetManager.AllocateDescriptorSet(DescriptorSetIDs.CameraData);

        var bufferInfo = new DescriptorBufferInfo { Buffer = buffer.Buffer, Offset = 0, Range = buffer.Size };
        var writeDescriptorSet = new WriteDescriptorSet
        {
            SType = StructureType.WriteDescriptorSet,
            DescriptorCount = 1,
            DescriptorType = DescriptorType.UniformBuffer,
            DstBinding = 0,
            DstArrayElement = 0,
            DstSet = intermediateData.CameraDescriptorSet,
            PBufferInfo = &bufferInfo
        };
            
        vulkanEngine.Vk.UpdateDescriptorSets(vulkanEngine.Device, 1, &writeDescriptorSet, 0, null);
    }

    public override void Dispose()
    {
        _stagingBuffer?.Dispose();
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    struct CameraData
    {
        [FieldOffset(sizeof(float) * 0)] public float HFov;
        [FieldOffset(sizeof(float) * 1)] public float AspectRatio;
        [FieldOffset(sizeof(float) * 2)] public Vector3 Forward;
        [FieldOffset(sizeof(float) * 5)] public Vector3 Upward;

        [FieldOffset(sizeof(float) * 8)] public Vector3 Position;
        [FieldOffset(sizeof(float) * 11)] public float Near;
        [FieldOffset(sizeof(float) * 12)] public float Far;
    }

    public override Identification Identification => RenderInputModuleIDs.Camera;

    [RegisterSingletonInputData("camera")]
    public static SingletonInputDataRegistryWrapper<CameraInputData> CameraInput => new();
}

public record struct CameraInputData(Camera Camera, Position Position);