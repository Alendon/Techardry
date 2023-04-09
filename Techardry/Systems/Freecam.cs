using System.Diagnostics;
using System.Numerics;
using MintyCore;
using MintyCore.Components.Common;
using MintyCore.ECS;
using MintyCore.Registries;
using MintyCore.SystemGroups;
using MintyCore.Utils;
using Silk.NET.Input;
using Techardry.Components.Client;
using SystemIDs = Techardry.Identifications.SystemIDs;

namespace Techardry.Systems;

[RegisterSystem("freecam")]
[ExecuteInSystemGroup<InitializationSystemGroup>]
public partial class Freecam : ASystem
{
    [ComponentQuery] private readonly ComponentQuery<(Camera, Position)> _cameraQuery = new();

    internal static Vector3 Input = Vector3.Zero;
    private static readonly float Speed = 10f;

    private static float _yaw = 0f;
    private static float _pitch = 0f;

    private static readonly float mouseSensitiveity = 0.15f;
    
    private Stopwatch _stopwatch = new();

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

            var movement = Input;

            var movementLength = movement.Length();
            if (movementLength > 1f)
                movement /= movementLength;

            movement *= Speed * Engine.DeltaTime;

            if (Engine.Window is {MouseLocked: true})
            {
                float deltaTime = (float) _stopwatch.Elapsed.TotalSeconds;
                _stopwatch.Restart();
                _yaw += -InputHandler.MouseDelta.X * mouseSensitiveity * deltaTime;
                _pitch += -Math.Clamp(InputHandler.MouseDelta.Y * mouseSensitiveity * deltaTime, -85f, 85);
            }

            var rotation = Quaternion.CreateFromYawPitchRoll(_yaw, _pitch, 0f);
            cam.Forward = Vector3.Transform(Vector3.UnitZ, rotation);
            cam.Upward = Vector3.Transform(-Vector3.UnitY, rotation);

            pos.Value += Vector3.Transform(movement, rotation);

            cam.Dirty = true;
            pos.Dirty = true;
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
                Input.Z = Math.Clamp(Input.Z + 1, -1, 1);
            }

            if (state is KeyStatus.KeyUp)
            {
                Input.Z = Math.Clamp(Input.Z - 1, -1, 1);
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
                Input.Z = Math.Clamp(Input.Z - 1, -1, 1);
            }

            if (state is KeyStatus.KeyUp)
            {
                Input.Z = Math.Clamp(Input.Z + 1, -1, 1);
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
                Input.X = Math.Clamp(Input.X - 1, -1, 1);
            }

            if (state is KeyStatus.KeyUp)
            {
                Input.X = Math.Clamp(Input.X + 1, -1, 1);
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
                Input.X = Math.Clamp(Input.X + 1, -1, 1);
            }

            if (state is KeyStatus.KeyUp)
            {
                Input.X = Math.Clamp(Input.X - 1, -1, 1);
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
                Input.Y = Math.Clamp(Input.Y + 1, -1, 1);
            }

            if (state is KeyStatus.KeyUp)
            {
                Input.Y = Math.Clamp(Input.Y - 1, -1, 1);
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
                Input.Y = Math.Clamp(Input.Y - 1, -1, 1);
            }

            if (state is KeyStatus.KeyUp)
            {
                Input.Y = Math.Clamp(Input.Y + 1, -1, 1);
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