using System.Numerics;
using System.Runtime.CompilerServices;
using FontStashSharp.Interfaces;
using MintyCore.Render;
using MintyCore.Utils;
using Silk.NET.Vulkan;
using Techardry.Identifications;

namespace Techardry.Render;

public class FontRenderer : IFontStashRenderer2
{
    private CommandBuffer _commandBuffer;
    private Viewport _viewport;
    private Rect2D _scissor;
    
    public List<Mesh> Meshes { get; } = new();
    
    public void PrepareNextDraw(CommandBuffer commandBuffer, Viewport viewport, Rect2D scissor)
    {
        _commandBuffer = commandBuffer;
        _viewport = viewport;
        _scissor = scissor;
    }

    public void EndDraw()
    {
        _commandBuffer = default;
        _viewport = default;
        _scissor = default;
    }

    public unsafe void DrawQuad(object textureObj, ref VertexPositionColorTexture topLeft,
        ref VertexPositionColorTexture topRight,
        ref VertexPositionColorTexture bottomLeft, ref VertexPositionColorTexture bottomRight)
    {
        Logger.AssertAndThrow(_commandBuffer.Handle != default, "CommandBuffer is not set", "FontRenderer");

        if (textureObj is not FontTextureWrapper textureWrapper)
            throw new ArgumentException("Texture is not a FontTextureWrapper", nameof(textureObj));

        var vertices = (stackalloc Vertex[]
        {
            new Vertex(bottomLeft.Position, bottomLeft.Color.ToVector3(), Vector3.UnitZ, bottomLeft.TextureCoordinate),
            new Vertex(topLeft.Position, topLeft.Color.ToVector3(), Vector3.UnitZ, topLeft.TextureCoordinate),
            new Vertex(topRight.Position, topRight.Color.ToVector3(), Vector3.UnitZ, topRight.TextureCoordinate),
            
            new Vertex(topRight.Position, topRight.Color.ToVector3(), Vector3.UnitZ, topRight.TextureCoordinate),
            new Vertex(bottomRight.Position, bottomRight.Color.ToVector3(), Vector3.UnitZ, bottomRight.TextureCoordinate),
            new Vertex(bottomLeft.Position, bottomLeft.Color.ToVector3(), Vector3.UnitZ, bottomLeft.TextureCoordinate),
        });

        var mesh = MeshHandler.CreateDynamicMesh(vertices, 6);
        Meshes.Add(mesh);

        var pipeline = PipelineHandler.GetPipeline(PipelineIDs.UiFontPipeline);
        var pipelineLayout = PipelineHandler.GetPipelineLayout(PipelineIDs.UiFontPipeline);
        
        VulkanEngine.Vk.CmdBindPipeline(_commandBuffer, PipelineBindPoint.Graphics, pipeline);
        VulkanEngine.Vk.CmdBindDescriptorSets(_commandBuffer, PipelineBindPoint.Graphics, pipelineLayout, 0, 1,
            textureWrapper.SampledImageDescriptorSet, 0, null);
        VulkanEngine.Vk.CmdBindVertexBuffers(_commandBuffer, 0, 1, mesh.MemoryBuffer.Buffer, 0);
        
        VulkanEngine.Vk.CmdSetScissor(_commandBuffer, 0, 1, _scissor);
        VulkanEngine.Vk.CmdSetViewport(_commandBuffer, 0, 1, _viewport);

        VulkanEngine.Vk.CmdDraw(_commandBuffer, 6, 1, 0, 0);
    }

    public ITexture2DManager TextureManager => FontTextureManager;
    public FontTextureManager FontTextureManager { get; } = new();
}