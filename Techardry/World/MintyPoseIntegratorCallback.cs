using System;
using System.Numerics;
using BepuPhysics;
using BepuUtilities;

namespace Techardry.World;

internal struct MintyPoseIntegratorCallback : IPoseIntegratorCallbacks
{
    private Vector<float> _dtAngularDamping;

    private Vector3Wide _dtGravity;
    private Vector<float> _dtLinearDamping;
    private readonly float _angularDamping;
    private readonly Vector3 _gravity;
    private readonly float _linearDamping;

    public MintyPoseIntegratorCallback(Vector3 gravity, float angularDamping, float linearDamping)
    {
        AngularIntegrationMode = AngularIntegrationMode.Nonconserving;
        IntegrateVelocityForKinematics = false;
        AllowSubstepsForUnconstrainedBodies = false;

        _gravity = gravity;
        _angularDamping = angularDamping;
        _linearDamping = linearDamping;

        _dtGravity = default;
        _dtAngularDamping = default;
        _dtLinearDamping = default;
    }


    public void Initialize(Simulation simulation)
    {
    }

    public void PrepareForIntegration(float dt)
    {
        _dtGravity = Vector3Wide.Broadcast(_gravity * dt);
        _dtLinearDamping = new Vector<float>(MathF.Pow(MathHelper.Clamp(1 - _linearDamping, 0, 1), dt));
        _dtAngularDamping = new Vector<float>(MathF.Pow(MathHelper.Clamp(1 - _angularDamping, 0, 1), dt));
    }

    public void IntegrateVelocity(Vector<int> bodyIndices, Vector3Wide position, QuaternionWide orientation,
        BodyInertiaWide localInertia, Vector<int> integrationMask, int workerIndex, Vector<float> dt,
        ref BodyVelocityWide velocity)
    {
        velocity.Linear = (velocity.Linear + _dtGravity) * _dtLinearDamping;
        velocity.Angular *= _dtAngularDamping;
    }

    public AngularIntegrationMode AngularIntegrationMode { get; }
    public bool AllowSubstepsForUnconstrainedBodies { get; }
    public bool IntegrateVelocityForKinematics { get; }
}