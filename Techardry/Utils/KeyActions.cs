using MintyCore;
using MintyCore.GameStates;
using MintyCore.Input;
using MintyCore.Registries;
using Silk.NET.GLFW;

namespace Techardry.Utils;

internal static class KeyActions
{
    [RegisterInputAction("back_to_main_menu")]
    internal static InputActionDescription BackToMainMenu(IGameStateMachine stateMachine) => new()
    {
        DefaultInput = Keys.Escape,
        ActionCallback = parameters =>
        {
            //TODO replace this by a ui option
            if (parameters.InputAction is InputAction.Press)
                stateMachine.PopGameState();
            
            return InputActionResult.Stop;
        }
    };
}