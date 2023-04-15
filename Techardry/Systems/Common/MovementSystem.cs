using System.Numerics;
using MintyCore;
using MintyCore.Components.Common;
using MintyCore.ECS;
using MintyCore.Registries;
using MintyCore.Utils;
using Techardry.Components.Client;
using Techardry.Identifications;

namespace Techardry.Systems.Common;

[RegisterSystem("movement")]
public partial class MovementSystem : ASystem
{
    [ComponentQuery]
    private readonly ComponentQuery<Position, InputComponent> _query = new();
    
    const float Speed = 10f;

    public override void Setup(SystemManager systemManager)
    {
        _query.Setup(this);
    }

    protected override void Execute()
    {
        foreach (var currentEntity in _query)
        {
            ref var pos = ref currentEntity.GetPosition();
            var input = currentEntity.GetInputComponent();

            var movement = input.Movement;
            if (movement != Vector3.Zero)
                movement = Vector3.Normalize(movement);

            pos.Value += movement * Speed * Engine.DeltaTime;
        }
    }

    public override Identification Identification => SystemIDs.Movement;
}