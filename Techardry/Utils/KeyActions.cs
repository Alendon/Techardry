using MintyCore;
using MintyCore.Registries;
using Silk.NET.Input;

namespace Techardry.Utils;

internal static class KeyActions
{
    public static int RenderMode = 3;
    [RegisterKeyAction("switch_render_mode")]
    public static KeyActionInfo SwitchRenderMode => new()
    {
        Action = (state, _) =>
        {
            if (state is KeyStatus.KeyDown)
            {
                RenderMode %= 3;
                RenderMode++;
            }
        },
        Key = Key.K,
        MouseButton = null
    };
    
    [RegisterKeyAction("back_to_main_menu")]
    internal static KeyActionInfo BackToMainMenu => new()
    {
        Key = Key.Escape,
        Action = (state, _) =>
        {
            if (state == KeyStatus.KeyDown)
                Engine.ShouldStop = true;
        }
    };
}