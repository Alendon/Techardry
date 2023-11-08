using JetBrains.Annotations;
using MintyCore.Registries;
using MintyCore.Render;
using MintyCore.Render.Managers.Interfaces;
using MintyCore.Render.Utils;
using MintyCore.Render.VulkanObjects;
using MintyCore.Utils;
using Silk.NET.Vulkan;
using Techardry.Identifications;
using Techardry.UI;

namespace Techardry.Render.Modules;

[RegisterRenderInput("ui_preprocessor")]
public class UiPreprocessor : IRenderInputConcreteResult<CommandBuffer>, IRenderInputKeyValue<object, Element>
{
    private IVulkanEngine VulkanEngine { get; }
    private IUiRenderer UiRenderer { get; }
    private IAllocationHandler AllocationHandler { get; }
    private IAsyncFenceAwaiter FenceAwaiter { get; }
    private IRenderPassManager RenderPassManager { get; }

    private (CommandPool pool, CommandBuffer buffer)[] _commandBuffers;
    private CommandPool _singleTimeCommandPool;

    private Element? _element;

    public unsafe UiPreprocessor(IVulkanEngine vulkanEngine, IUiRenderer uiRenderer,
        IAllocationHandler allocationHandler, IAsyncFenceAwaiter fenceAwaiter, IRenderPassManager renderPassManager)
    {
        VulkanEngine = vulkanEngine;
        UiRenderer = uiRenderer;
        AllocationHandler = allocationHandler;
        FenceAwaiter = fenceAwaiter;
        RenderPassManager = renderPassManager;

        _commandBuffers = new (CommandPool, CommandBuffer)[VulkanEngine.SwapchainImageCount];

        CommandPoolCreateInfo poolCreateInfo = new()
        {
            SType = StructureType.CommandPoolCreateInfo,
            QueueFamilyIndex = VulkanEngine.GraphicQueue.familyIndex,
            Flags = CommandPoolCreateFlags.TransientBit
        };
        for (var i = 0; i < _commandBuffers.Length; i++)
        {
            VulkanUtils.Assert(
                VulkanEngine.Vk.CreateCommandPool(VulkanEngine.Device, poolCreateInfo, null, out var pool));
            _commandBuffers[i] = (pool, default);
        }
    }

    /// <inheritdoc />
    public Task Process()
    {
        ResetCommandPools();
        if (_element is null) return Task.CompletedTask;

        CreateAndBeginCommandBuffer();
        var cb = GetConcreteResult();

        UiRenderer.CommandBuffer = cb;
        UiRenderer.DrawUi(_element);
        UiRenderer.CommandBuffer = default;

        var singleTimeCb = BeginSingleTimeCommandBuffer();
        UiRenderer.UpdateInternalTextures(singleTimeCb);
        return SubmitSingleTimeCommandBuffer(singleTimeCb);
    }

    private void ResetCommandPools()
    {
        VulkanUtils.Assert(VulkanEngine.Vk.ResetCommandPool(VulkanEngine.Device, _commandBuffers[VulkanEngine.ImageIndex].pool,
            CommandPoolResetFlags.ReleaseResourcesBit));
        
        VulkanUtils.Assert(VulkanEngine.Vk.ResetCommandPool(VulkanEngine.Device, _singleTimeCommandPool,
            CommandPoolResetFlags.ReleaseResourcesBit));
    }

    private CommandBuffer BeginSingleTimeCommandBuffer()
    {
        CommandBufferAllocateInfo allocateInfo = new()
        {
            SType = StructureType.CommandBufferAllocateInfo,
            CommandPool = _singleTimeCommandPool,
            CommandBufferCount = 1,
            Level = CommandBufferLevel.Primary
        };
        VulkanUtils.Assert(VulkanEngine.Vk.AllocateCommandBuffers(VulkanEngine.Device, allocateInfo, out var cb));

        CommandBufferBeginInfo beginInfo = new()
        {
            SType = StructureType.CommandBufferBeginInfo,
            Flags = CommandBufferUsageFlags.OneTimeSubmitBit
        };
        VulkanUtils.Assert(VulkanEngine.Vk.BeginCommandBuffer(cb, beginInfo));

        return cb;
    }

    private unsafe Task SubmitSingleTimeCommandBuffer(CommandBuffer singleTimeCb)
    {
        var fence = new ManagedFence(VulkanEngine, AllocationHandler);

        var submitInfo = new SubmitInfo()
        {
            SType = StructureType.SubmitInfo,
            CommandBufferCount = 1,
            PCommandBuffers = &singleTimeCb
        };

        VulkanUtils.Assert(VulkanEngine.Vk.QueueSubmit(VulkanEngine.GraphicQueue.queue, 1, submitInfo,
            fence.InternalFence));
        var task = FenceAwaiter.AwaitAsync(fence);
        task.ContinueWith(_ => fence.Dispose());

        return task;
    }

    private unsafe void CreateAndBeginCommandBuffer()
    {
        var pool = _commandBuffers[VulkanEngine.ImageIndex].pool;
        ref var cb = ref _commandBuffers[VulkanEngine.ImageIndex].buffer;
        
        CommandBufferAllocateInfo allocateInfo = new()
        {
            SType = StructureType.CommandBufferAllocateInfo,
            CommandPool = pool,
            CommandBufferCount = 1,
            Level = CommandBufferLevel.Secondary
        };
        
        VulkanUtils.Assert(VulkanEngine.Vk.AllocateCommandBuffers(VulkanEngine.Device, allocateInfo, out cb));
        
        CommandBufferInheritanceInfo inheritanceInfo = new()
        {
            SType = StructureType.CommandBufferInheritanceInfo,
            RenderPass = RenderPassManager.GetRenderPass(RenderPassIDs.ColorOnly),
            Subpass = 0,
            Framebuffer = default,
            OcclusionQueryEnable = false
        };
        
        CommandBufferBeginInfo beginInfo = new()
        {
            SType = StructureType.CommandBufferBeginInfo,
            Flags = CommandBufferUsageFlags.RenderPassContinueBit,
            PInheritanceInfo = &inheritanceInfo
        };
        
        VulkanUtils.Assert(VulkanEngine.Vk.BeginCommandBuffer(cb, beginInfo));
    }

    /// <inheritdoc />
    public CommandBuffer GetConcreteResult()
    {
        return _commandBuffers[VulkanEngine.ImageIndex].buffer;
    }

    /// <inheritdoc />
    public unsafe void Dispose()
    {
        foreach (var (pool, _) in _commandBuffers)
        {
            VulkanEngine.Vk.DestroyCommandPool(VulkanEngine.Device, pool, null);
        }
    }


    /// <inheritdoc />
    public object GetResult()
    {
        return GetConcreteResult();
    }

    /// <inheritdoc />
    public void RemoveData(object key)
    {
        //key is currently ignored. This will maybe be changed in the future to allow for more than one element to be processed at once
        _element = null;
    }

    /// <inheritdoc />
    public void SetData(object key, Element value)
    {
        //key is currently ignored. This will maybe be changed in the future to allow for more than one element to be processed at once
        _element = value;
    }
}