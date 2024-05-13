using System.Collections.Concurrent;
using MintyCore.ECS;
using MintyCore.ECS.SystemGroups;
using MintyCore.Graphics.Render.Managers;
using MintyCore.Registries;
using MintyCore.Utils;
using MintyCore.Utils.Events;
using Serilog;
using Techardry.Identifications;
using Techardry.Utils;
using Techardry.World;

namespace Techardry.Systems.Client;

[RegisterSystem("chunk_input_data_update")]
[ExecuteInSystemGroup<InitializationSystemGroup>]
[ExecutionSide(GameType.Client)]
public class ChunkInputDataUpdateSystem(IEventBus eventBus, IInputDataManager inputDataManager) : ASystem
{
    private readonly ConcurrentQueue<(Int3 chunkPosition, bool set)> _chunkUpdates = new();

    public override Identification Identification => SystemIDs.ChunkInputDataUpdate;

    private EventBinding<AddChunkEvent>? _addChunkEventBinding;
    private EventBinding<RemoveChunkEvent>? _removeChunkEventBinding;
    private EventBinding<UpdateChunkEvent>? _updateChunkEventBinding;

    public override void Setup(SystemManager systemManager)
    {
        _addChunkEventBinding = new EventBinding<AddChunkEvent>(eventBus, OnAddChunk);
        _removeChunkEventBinding = new EventBinding<RemoveChunkEvent>(eventBus, OnRemoveChunk);
        _updateChunkEventBinding = new EventBinding<UpdateChunkEvent>(eventBus, OnUpdateChunk);
    }

    private EventResult OnUpdateChunk(UpdateChunkEvent e)
    {
        if (e.UpdateKind == UpdateChunkEvent.ChunkUpdateKind.Voxel) return EventResult.Continue;
        if (!ReferenceEquals(World, e.ContainingWorld)) return EventResult.Continue;
        
        _chunkUpdates.Enqueue((e.ChunkPosition, false));
        _chunkUpdates.Enqueue((e.ChunkPosition, true));

        return EventResult.Continue;
    }

    private EventResult OnRemoveChunk(RemoveChunkEvent e)
    {
        if (ReferenceEquals(World, e.ContainingWorld))
            _chunkUpdates.Enqueue((e.ChunkPosition, false));

        return EventResult.Continue;
    }

    private EventResult OnAddChunk(AddChunkEvent e)
    {
        if (ReferenceEquals(World, e.ContainingWorld))
            _chunkUpdates.Enqueue((e.ChunkPosition, true));

        return EventResult.Continue;
    }

    protected override void Execute()
    {
        if (World is not TechardryWorld world) return;


        while (_chunkUpdates.TryDequeue(out var value))
        {
            var (chunkPosition, set) = value;

            if (set)
            {
                if (!world.ChunkManager.TryGetChunk(chunkPosition, out var chunk))
                {
                    Log.Warning("Tried to set input data for non-existing chunk at {ChunkPosition}", chunkPosition);
                    continue;
                }
                
                inputDataManager.SetKeyIndexedInputData(RenderInputDataIDs.Voxel, chunkPosition, chunk.Octree);
            }
            else
            {
                inputDataManager.RemoveKeyIndexedInputData(RenderInputDataIDs.Voxel, chunkPosition);
            }
        }
    }
    
    protected override void Dispose(bool disposing)
    {
        _addChunkEventBinding?.UnregisterBinding(eventBus);
        _removeChunkEventBinding?.UnregisterBinding(eventBus);
        _updateChunkEventBinding?.UnregisterBinding(eventBus);
        
        base.Dispose(disposing);
    }
}