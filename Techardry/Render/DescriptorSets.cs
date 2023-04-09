using MintyCore.Registries;
using Silk.NET.Vulkan;

namespace Techardry.Render;

public class DescriptorSets
{
    
    
    [RegisterDescriptorSet("camera_buffer")]
    internal static DescriptorSetInfo CameraBufferInfo => new()
    {
        Bindings = new[]
        {
            new DescriptorSetLayoutBinding
            {
                Binding = 0,
                DescriptorCount = 1,
                DescriptorType = DescriptorType.UniformBuffer,
                StageFlags = ShaderStageFlags.VertexBit
            }
        }
    };
    
    
}