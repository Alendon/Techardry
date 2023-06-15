using System.Diagnostics;
using System.Numerics;
using MintyCore;
using MintyCore.Components.Common;
using MintyCore.ECS;
using MintyCore.SystemGroups;
using MintyCore.Utils;
using Techardry.Components.Client;

namespace Techardry.Systems;

//[RegisterSystem("freecam")]
[ExecuteInSystemGroup<InitializationSystemGroup>]
public partial class Freecam : ASystem
{
    [ComponentQuery] private readonly ComponentQuery<(Camera, Position)> _cameraQuery = new();

    internal static Vector3 Input = Vector3.Zero;
    private static readonly float Speed = 10f;

    private static float _yaw = 0f;
    private static float _pitch = 0f;

    private static readonly float mouseSensitiveity = 0.5f;
    
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

   
    
    public override Identification Identification => throw new Exception();// SystemIDs.Freecam;
}