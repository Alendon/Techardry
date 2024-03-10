using System.Numerics;
using MintyCore.ECS;
using MintyCore.Registries;
using MintyCore.Utils;
using Techardry.Identifications;

namespace Techardry.Components.Client;

/// <summary>
///     Component to track camera data
/// </summary>
[PlayerControlled]
[RegisterComponent("camera")]
public struct Camera : IComponent
{
    /// <inheritdoc />
    public void DecreaseRefCount()
    {
        
    }

    /// <inheritdoc />
    public bool Dirty { get; set; }

    /// <summary>
    ///     Stores the field of view
    /// </summary>
    public float Fov;

    /// <summary>
    ///     Position Offset from the entity Position
    /// </summary>
    public Vector3 PositionOffset;

    /// <summary>
    ///     The Forward Vector of the camera
    /// </summary>
    public Vector3 Forward;

    /// <summary>
    ///     The Upward Vector of the camera
    /// </summary>
    public Vector3 Upward;

    /// <summary>
    /// 
    /// </summary>
    public float NearPlane;

    /// <summary>
    /// 
    /// </summary>
    public float FarPlane;

    public float Yaw;
    public float Pitch;


    /// <summary>
    ///     <see cref="Identification" /> of the <see cref="Camera" /> Component
    /// </summary>
    public Identification Identification => ComponentIDs.Camera;


    /// <inheritdoc />
    public void PopulateWithDefaultValues()
    {
        Fov = 1.0f;
        PositionOffset = Vector3.Zero;
        Forward = new Vector3(0, 0, 1);
        Upward = new Vector3(0, -1, 0);
        NearPlane = 0.1f;
        FarPlane = 1000.0f;
    }
    
    /// <inheritdoc />
    public void Serialize(DataWriter writer, IWorld world, Entity entity)
    {
        writer.Put(Fov);
        writer.Put(PositionOffset);
        writer.Put(Forward);
        writer.Put(Upward);
        writer.Put(Yaw);
        writer.Put(Pitch);
    }

    /// <inheritdoc />
    public bool Deserialize(DataReader reader, IWorld world, Entity entity)
    {
        bool success = true;
        success &= reader.TryGetFloat(out Fov);
        success &= reader.TryGetVector3(out PositionOffset);
        success &= reader.TryGetVector3(out Forward);
        success &= reader.TryGetVector3(out Upward);
        success &= reader.TryGetFloat(out Yaw);
        success &= reader.TryGetFloat(out Pitch);
        
        return success;
    }

    /// <inheritdoc />
    public void IncreaseRefCount()
    {
    }
}