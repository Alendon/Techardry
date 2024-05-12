using BepuPhysics;
using BepuPhysics.Collidables;
using BepuUtilities;
using MintyCore.ECS;
using MintyCore.Registries;
using MintyCore.Utils;
using Techardry.Identifications;

namespace Techardry.Components.Common.Physic;

[RegisterComponent("body")]
public struct Body : IComponent
{
    /// <summary>Position and orientation of the body.</summary>
    private RigidPose _pose;

    /// <summary>Linear and angular velocity of the body.</summary>
    private BodyVelocity _velocity;

    /// <summary>Mass and inertia tensor of the body.</summary>
    private BodyInertia _localInertia;

    /// <summary>Shape and collision detection settings for the body.</summary>
    private CollidableDescription _collidable;

    /// <summary>Sleeping settings for the body.</summary>
    private BodyActivityDescription _activity;

    private DirtyFlag _bodyDirty;

    public void PopulateWithDefaultValues()
    {
    }

    public void Serialize(DataWriter writer, IWorld world, Entity entity)
    {
        if ((BodyDirty & DirtyFlag.Pose) != 0)
        {
            writer.Put(true);

            writer.Put(Pose.Position);
            writer.Put(Pose.Orientation);
        }
        else
        {
            writer.Put(false);
        }

        if ((BodyDirty & DirtyFlag.Velocity) != 0)
        {
            writer.Put(true);

            writer.Put(Velocity.Linear);
            writer.Put(Velocity.Angular);
        }
        else
        {
            writer.Put(false);
        }

        if ((BodyDirty & DirtyFlag.LocalInertia) != 0)
        {
            writer.Put(true);

            writer.Put(LocalInertia.InverseMass);
            writer.Put(LocalInertia.InverseInertiaTensor.XX);
            writer.Put(LocalInertia.InverseInertiaTensor.YY);
            writer.Put(LocalInertia.InverseInertiaTensor.ZZ);
            writer.Put(LocalInertia.InverseInertiaTensor.YX);
            writer.Put(LocalInertia.InverseInertiaTensor.ZX);
            writer.Put(LocalInertia.InverseInertiaTensor.ZY);
        }
        else
        {
            writer.Put(false);
        }

        //TODO: Sync Collidable description. Not trivial since it contains an index to the shape in the simulation

        if ((BodyDirty & DirtyFlag.Activity) != 0)
        {
            writer.Put(true);
            writer.Put(Activity.SleepThreshold);
            writer.Put(Activity.MinimumTimestepCountUnderThreshold);
        }
        else
        {
            writer.Put(false);
        }
        
        writer.Put(BodyShouldExists);

        BodyDirty = 0;
    }

    public bool Deserialize(DataReader reader, IWorld world, Entity entity)
    {
        if (!reader.TryGetBool(out var hasPose)) return false;
        if (hasPose)
        {
            if (!reader.TryGetVector3(out var position) || !reader.TryGetQuaternion(out var orientation)) return false;
            Pose = new RigidPose(position, orientation);
        }

        if (!reader.TryGetBool(out var hasVelocity)) return false;
        if (hasVelocity)
        {
            if (!reader.TryGetVector3(out var linear) || !reader.TryGetVector3(out var angular)) return false;
            Velocity = new BodyVelocity(linear, angular);
        }

        if (!reader.TryGetBool(out var hasInertia)) return false;
        if (hasInertia)
        {
            if (!reader.TryGetFloat(out var inverseMass) || !reader.TryGetFloat(out var inverseInertiaXx) ||
                !reader.TryGetFloat(out var inverseInertiaYy) || !reader.TryGetFloat(out var inverseInertiaZz) ||
                !reader.TryGetFloat(out var inverseInertiaYx) || !reader.TryGetFloat(out var inverseInertiaZx) ||
                !reader.TryGetFloat(out var inverseInertiaZy)) return false;

            LocalInertia = new BodyInertia()
            {
                InverseMass = inverseMass,
                InverseInertiaTensor = new Symmetric3x3()
                {
                    XX = inverseInertiaXx, YY = inverseInertiaYy, ZZ = inverseInertiaZz, YX = inverseInertiaYx,
                    ZX = inverseInertiaZx, ZY = inverseInertiaZy
                }
            };
        }

        if (!reader.TryGetBool(out var hasActivity)) return false;
        if (hasActivity)
        {
            if (!reader.TryGetFloat(out var sleepThreshold) || !reader.TryGetByte(out var minTimestepCount))
                return false;
            Activity = new BodyActivityDescription(sleepThreshold, minTimestepCount);
        }
        
        if (!reader.TryGetBool(out var bodyShouldExists)) return false;
        BodyShouldExists = bodyShouldExists;

        return true;
    }


    public bool Dirty { get; set; }
    public Identification Identification => ComponentIDs.Body;

    /// <summary>Position and orientation of the body.</summary>
    public RigidPose Pose
    {
        get => _pose;
        set
        {
            _pose = value;
            _bodyDirty &= DirtyFlag.Pose;
        }
    }

    /// <summary>Linear and angular velocity of the body.</summary>
    public BodyVelocity Velocity
    {
        get => _velocity;
        set
        {
            _velocity = value;
            _bodyDirty &= DirtyFlag.Velocity;
        }
    }

    /// <summary>Mass and inertia tensor of the body.</summary>
    public BodyInertia LocalInertia
    {
        get => _localInertia;
        set
        {
            _localInertia = value;
            _bodyDirty &= DirtyFlag.LocalInertia;
        }
    }

    /// <summary>Shape and collision detection settings for the body.</summary>
    public CollidableDescription Collidable
    {
        get => _collidable;
        set
        {
            _collidable = value;
            _bodyDirty &= DirtyFlag.Collidable;
        }
    }

    /// <summary>Sleeping settings for the body.</summary>
    public BodyActivityDescription Activity
    {
        get => _activity;
        set
        {
            _activity = value;
            _bodyDirty &= DirtyFlag.Activity;
        }
    }

    public BodyDescription GetBodyDescription()
    {
        return new BodyDescription
        {
            Pose = Pose,
            LocalInertia = LocalInertia,
            Collidable = Collidable,
            Activity = Activity,
            Velocity = Velocity
        };
    }

    public static Body FromBodyDescription(BodyDescription description)
    {
        return new Body()
        {
            Pose = description.Pose,
            LocalInertia = description.LocalInertia,
            Collidable = description.Collidable,
            Activity = description.Activity,
            Velocity = description.Velocity,
            BodyDirty = DirtyFlag.All
        };
    }

    public DirtyFlag BodyDirty
    {
        get => _bodyDirty;
        private set => _bodyDirty = value;
    }

    public bool BodyShouldExists;


    public void IncreaseRefCount()
    {
    }

    public void DecreaseRefCount()
    {
    }

    [Flags]
    public enum DirtyFlag : byte
    {
        Pose = 1 << 0,
        Velocity = 1 << 1,
        LocalInertia = 1 << 2,
        Collidable = 1 << 3,
        Activity = 1 << 4,
        NeedReplacement = Collidable | Activity,
        All = Pose | Velocity | LocalInertia | Collidable | Activity
    }
}