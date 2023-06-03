using MintyCore.Registries;
using Silk.NET.Vulkan;

namespace Techardry.Render;

public class Shaders
{
    
    [RegisterShader("triangle_vert", "triangle_vert.spv")]
    internal static ShaderInfo TriangleVertShaderInfo => new(ShaderStageFlags.VertexBit);

    [RegisterShader("color_frag", "color_frag.spv")]
    internal static ShaderInfo ColorFragShaderInfo => new(ShaderStageFlags.FragmentBit);

    [RegisterShader("common_vert", "common_vert.spv")]
    internal static ShaderInfo CommonVertShaderInfo => new(ShaderStageFlags.VertexBit);

    [RegisterShader("wireframe_frag", "wireframe_frag.spv")]
    internal static ShaderInfo WireframeFragShaderInfo => new(ShaderStageFlags.FragmentBit);

    [RegisterShader("texture_frag", "texture_frag.spv")]
    internal static ShaderInfo TextureFragShaderInfo => new(ShaderStageFlags.FragmentBit);

    [RegisterShader("ui_overlay_vert", "ui_overlay_vert.spv")]
    internal static ShaderInfo UiOverlayVertShaderInfo => new(ShaderStageFlags.VertexBit);

    [RegisterShader("ui_overlay_frag", "ui_overlay_frag.spv")]
    internal static ShaderInfo UiOverlayFragShaderInfo => new(ShaderStageFlags.FragmentBit);
}