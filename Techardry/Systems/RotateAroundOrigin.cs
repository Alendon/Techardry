using System.Numerics;
using MintyCore;
using MintyCore.Components.Client;
using MintyCore.Components.Common;
using MintyCore.ECS;
using MintyCore.Registries;
using MintyCore.Utils;
using Techardry.Identifications;

namespace Techardry.Systems;

[RegisterSystem("rotate_around_origin")]
public partial class RotateAroundOrigin : ASystem
{
    [ComponentQuery]
    private ComponentQuery<(Camera, Position)> _cameraQuery = new();
    
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

            pos.Value = Vector3.Transform(pos.Value,
                Quaternion.CreateFromAxisAngle(Vector3.UnitY, 1 * Engine.DeltaTime));
            cam.Forward = -Vector3.Normalize(pos.Value);
        }
    }

    public override Identification Identification => SystemIDs.RotateAroundOrigin;
}