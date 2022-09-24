using System.Numerics;
using MintyCore;
using MintyCore.Components.Client;
using MintyCore.Components.Common;
using MintyCore.ECS;
using MintyCore.Registries;
using System.Numerics;
using MintyCore.Components.Client;
using MintyCore.Components.Common;
using MintyCore.ECS;
using MintyCore.Identifications;
using MintyCore.Registries;
using MintyCore.Utils;
using Techardry.Identifications;
using Silk.NET.Input;
using SystemIDs = Techardry.Identifications.SystemIDs;

namespace Techardry.Systems;

[RegisterSystem("freecam")]
public partial class Freecam : ASystem
{
    [ComponentQuery] 
    private readonly ComponentQuery<(Camera, Position)> _cameraQuery = new();

    internal static Vector3 Input = Vector3.Zero;
    private static readonly float Speed = 10f;

    private static float _yaw = 0f;
    private static float _pitch = 0f;

    private static readonly float mouseSensitiveity = 0.05f;

    public override void Setup(SystemManager systemManager)
    {
        _cameraQuery.Setup(this);
    }

    protected override void Execute()
    {
        foreach (var currentEntity in _cameraQuery)
        {
            ref var pos = ref currentEntity.GetPosition();
            ref var cam = ref currentEntity.GetCamera();

            pos.Value += Input * Engine.DeltaTime * Speed;

            _yaw -= (InputHandler.MouseDelta.X * mouseSensitiveity * Engine.DeltaTime);
            _pitch += (InputHandler.MouseDelta.Y * mouseSensitiveity * Engine.DeltaTime);
            if (_pitch < -90) _pitch = -90;
            if (_pitch > 90) _pitch = 90;
            Quaternion rotation = Quaternion.CreateFromYawPitchRoll(_yaw, _pitch, 0);

            if (Input.Length() != 0)
                Input = Vector3.Normalize(Input);
            Input = Vector3.Transform(Input, rotation);
            Input *= Speed;
            
            cam.Forward = Vector3.Transform(Vector3.One, rotation);
            
        }
    }

    [RegisterKeyAction("Move_Forward")]
    public static KeyActionInfo MoveForward => new KeyActionInfo()
    {
        Key = Key.W,
        Action = (state, _) =>
        {
            if (state is KeyStatus.KeyDown)
            {
                Input.Z += 1;
            }

            if (state is KeyStatus.KeyUp)
            {
                Input.Z -= 1;
            }
        }
    };

    [RegisterKeyAction("Move_Backward")]
    public static KeyActionInfo MoveBackwards => new KeyActionInfo()
    {
        Key = Key.S,
        Action = (state, _) =>
        {
            if (state is KeyStatus.KeyDown)
            {
                Input.Z -= 1;
            }

            if (state is KeyStatus.KeyUp)
            {
                Input.Z += 1;
            }
        }
    };

    [RegisterKeyAction("Move_Left")]
    public static KeyActionInfo MoveLeft => new KeyActionInfo()
    {
        Key = Key.A,
        Action = (state, _) =>
        {
            if (state is KeyStatus.KeyDown)
            {
                Input.X -= 1;
            }

            if (state is KeyStatus.KeyUp)
            {
                Input.X += 1;
            }
        }
    };

    [RegisterKeyAction("Move_Right")]
    public static KeyActionInfo MoveRight => new KeyActionInfo()
    {
        Key = Key.D,
        Action = (state, _) =>
        {
            if (state is KeyStatus.KeyDown)
            {
                Input.X += 1;
            }

            if (state is KeyStatus.KeyUp)
            {
                Input.X -= 1;
            }
        }
    };

    [RegisterKeyAction("Move_Up")]
    public static KeyActionInfo MoveUp => new KeyActionInfo()
    {
        Key = Key.Space,
        Action = (state, _) =>
        {
            if (state is KeyStatus.KeyDown)
            {
                Input.Y += 1;
            }

            if (state is KeyStatus.KeyUp)
            {
                Input.Y -= 1;
            }
        }
    };

    [RegisterKeyAction("Move_Down")]
    public static KeyActionInfo MoveDown => new KeyActionInfo()
    {
        Key = Key.ShiftLeft,
        Action = (state, _) =>
        {
            if (state is KeyStatus.KeyDown)
            {
                Input.Y -= 1;
            }

            if (state is KeyStatus.KeyUp)
            {
                Input.Y += 1;
            }
        }
    };

    [RegisterKeyAction("Mouse_Lock")]
    public static KeyActionInfo MouseLock => new KeyActionInfo()
    {
        Key = Key.F,
        Action = (state, _) =>
        {
            if (state is KeyStatus.KeyDown)
            {
                if (Engine.Window != null) Engine.Window.MouseLocked = !Engine.Window.MouseLocked;
            }
        }
    };

    public override Identification Identification => SystemIDs.Freecam;
}
