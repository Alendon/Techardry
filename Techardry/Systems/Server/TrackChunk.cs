using MintyCore.Components.Common;
using MintyCore.ECS;
using MintyCore.ECS.SystemGroups;
using MintyCore.Registries;
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

            if (lastChunk.HasValue && lastChunk.Value == currentChunk) continue;

            techardryWorld.ChunkManager.PlayerChunkChanged(lastChunk.HasValue ? lastChunk.Value : null, currentChunk,
                World.EntityManager.GetEntityOwner(currentEntity.Entity));

            lastChunk.Value = currentChunk;
            lastChunk.HasValue = true;
            lastChunk.Dirty = true;
        }
    }

    public override Identification Identification => SystemIDs.TrackChunk;
}