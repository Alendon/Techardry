using System.Diagnostics.CodeAnalysis;
using BepuUtilities;
using MintyCore.Physics;
using MintyCore.Registries;

namespace Techardry.World;

public class TechardryWorld : MintyCore.ECS.World
{
    private Dictionary<Int3, Chunk> _chunks = new();
    
    internal void CreateChunk(Int3 chunkPos)
    {
        if (_chunks.ContainsKey(chunkPos))
        {
            return;
        }
        
        var chunk = new Chunk(chunkPos);
        _chunks.Add(chunkPos, chunk);
    }
    
    public bool TryGetChunk(Int3 chunkPos, [MaybeNullWhen(false)] out Chunk chunk)
    {
        return _chunks.TryGetValue(chunkPos, out chunk);
    }

    public TechardryWorld(bool isServerWorld) : base(isServerWorld)
    {
        SystemManager.SetSystemActive(MintyCore.Identifications.SystemIDs.RenderInstanced, false);
    }

    public TechardryWorld(bool isServerWorld, PhysicsWorld physicsWorld) : base(isServerWorld, physicsWorld)
    {
        SystemManager.SetSystemActive(MintyCore.Identifications.SystemIDs.RenderInstanced, false);
    }
}