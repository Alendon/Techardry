using MintyCore;
using MintyCore.Input;
using MintyCore.Registries;
using Silk.NET.GLFW;

namespace Techardry.Utils;

internal static class KeyActions
{
    [RegisterInputAction("back_to_main_menu")]
    internal static InputActionDescription BackToMainMenu => new()
    {
        DefaultInput = Keys.Escape,
        ActionCallback = parameters =>
        {
            if (parameters.InputAction is InputAction.Press)
                Engine.ShouldStop = true;
            
            return InputActionResult.Stop;
        }
    };
}