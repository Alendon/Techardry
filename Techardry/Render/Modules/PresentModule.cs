using MintyCore.Render;
using Silk.NET.Vulkan;

namespace Techardry.Render.Modules;

public sealed class PresentModule : IRenderModule
{
    /// <inheritdoc />
    public void Dispose()
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public void Process(CommandBuffer cb)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public void Initialize(IRenderWorker renderWorker)
    {
        throw new NotImplementedException();
    }
}