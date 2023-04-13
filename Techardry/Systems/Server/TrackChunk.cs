using System.Diagnostics.CodeAnalysis;
using MintyCore.Components.Common;
using MintyCore.ECS;
using MintyCore.Registries;
using MintyCore.SystemGroups;
using MintyCore.Utils;
using Techardry.Components.Common;
using Techardry.Identifications;
using Techardry.Utils;
using Techardry.World;

namespace Techardry.Systems.Server;

[RegisterSystem("track_chunk")]
[ExecuteInSystemGroup<FinalizationSystemGroup>]
[ExecutionSide(GameType.Server)]
public partial class TrackChunk : ASystem
{
    [ComponentQuery] private ComponentQuery<LastChunk, Position> _componentQuery = new();


    public override void Setup(SystemManager systemManager)
    {
        _componentQuery.Setup(this);
    }

    protected override void Execute()
    {
        if (World is not TechardryWorld techardryWorld) return;

        foreach (var currentEntity in _componentQuery)
        {
            var position = currentEntity.GetPosition();
            var currentChunk = new Int3((int)(position.Value.X / Chunk.Size), (int)(position.Value.Y / Chunk.Size),
                (int)(position.Value.Z / Chunk.Size));
            ref var lastChunk = ref currentEntity.GetLastChunk();

            if (lastChunk.Value == currentChunk) continue;

            var oldArea = ChunkArea(lastChunk.Value, TechardryMod.Instance!.ServerRenderDistance).ToArray();
            var newArea = ChunkArea(currentChunk, TechardryMod.Instance!.ServerRenderDistance).ToArray();
            
            
            foreach (var chunk in oldArea.Except(newArea))
            {
                //techardryWorld.UntrackPlayerInChunk(World.EntityManager.GetEntityOwner(currentEntity.Entity), chunk);
            }

            foreach (var chunk in newArea.Except(oldArea))
            {
                //techardryWorld.TrackPlayerInChunk(World.EntityManager.GetEntityOwner(currentEntity.Entity), chunk);
            }
        }
    }

    private static IEnumerable<Int3> ChunkArea(Int3 center, int renderDistance)
    {
        for (var x = center.X - renderDistance; x <= center.X + renderDistance; x++)
        {
            for (var y = center.Y - renderDistance; y <= center.Y + renderDistance; y++)
            {
                for (var z = center.Z - renderDistance; z <= center.Z + renderDistance; z++)
                {
                    yield return new Int3(x, y, z);
                }
            }
        }
    }

    public override Identification Identification => SystemIDs.TrackChunk;
}