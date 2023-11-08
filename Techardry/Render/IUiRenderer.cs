using System.Drawing;
using System.Numerics;
using FontStashSharp;
using MintyCore.Utils;
using Silk.NET.Vulkan;
using Techardry.UI;

namespace Techardry.Render;

public interface IUiRenderer : IDisposable
{
    CommandBuffer CommandBuffer { get; set; }
    void DrawUi(Element rootElement);
    void UpdateInternalTextures(CommandBuffer commandBuffer);

    Bounds MeasureString(string text, int fontSize, Vector2 position, Vector2? scale = null,
        float characterSpacing = 0f, float lineSpacing = 0f, FontSystemEffect effect = FontSystemEffect.None,
        int effectAmount = 0);

    void DrawString(string text, int fontSize, Viewport viewport,
        Rect2D scissor, Vector2 position,
        FSColor color, Vector2? scale = null, float rotation = 0f, Vector2 origin = default, float layerDepth = 0f,
        float characterSpacing = 0f, float lineSpacing = 0f, TextStyle textStyle = TextStyle.None,
        FontSystemEffect effect = FontSystemEffect.None, int effectAmount = 0);

    void DrawTexture(Identification textureId,
        RectangleF drawingRect, RectangleF uvRect, Rect2D scissor, Viewport viewport);

    void FillColor(Color color,
        Rect2D scissor, Viewport viewport);
}