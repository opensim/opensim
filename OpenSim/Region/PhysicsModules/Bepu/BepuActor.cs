using System;
using System.Collections.Generic;
using OpenSim.Framework;
using OpenSim.Region.PhysicsModules.SharedBase;
using OpenMetaverse;
using BepuPhysics;
using BepuPhysics.Collidables;
using log4net;

namespace OpenSim.Region.PhysicsModule.Bepu
{
    public sealed class BepuActor : PhysicsActor
    {
        private static readonly ILog m_log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private readonly BepuScene _scene;
        private BodyHandle _bodyHandle;
        private TypedIndex _shapeIndex;
        private bool _hasBody;
        private bool _initialized;

        // Cached state (read from Bepu on demand, written locally pending next simulation step)
        private Vector3 _position;
        private Quaternion _orientation;
        private Vector3 _velocity;
        private Vector3 _rotationalVelocity;
        private Vector3 _size;
        private float _mass;
        private bool _isPhysical;
        private bool _isFlying;
        private bool _isColliding;
        private bool _collidingGround;
        private bool _collidingObj;
        private bool _grabbed;
        private bool _selected;
        private bool _kinematic;
        private bool _stopped = true;
        private float _buoyancy;
        private bool _floatOnWater;
        private Vector3 _force;
        private Vector3 _torque;
        private Vector3 _acceleration;
        private float _collisionScore;
        private int _physicsActorType;
        private int _vehicleType;
        private bool _throttleUpdates;
        private bool _setAlwaysRun;
        private bool _phantom;
        private byte _physicsShapeType;
        private PrimitiveBaseShape _shape;
        private float _density = 10f;
        private float _gravModifier = 1f;
        private float _friction = 0.5f;
        private float _restitution = 0.1f;
        private float _simulationSuspended;
        private int _angularLockAxes; // bitmask: 1=X, 2=Y, 4=Z

        // PID / MoveTo
        private Vector3 _pidTarget;
        private bool _pidActive;
        private float _pidTau;
        private float _pidHoverHeight;
        private bool _pidHoverActive;
        private PIDHoverType _pidHoverType;
        private float _pidHoverTau;

        // APID / RotLookAt
        private Quaternion _apidTarget;
        private bool _apidActive;
        private float _apidStrength;
        private float _apidDamping;

        // Events subscription
        private int _subscribeIntervalMs;
        private bool _subscribed;

        // Collision event subscription (separate from general SubscribeEvents)
        private bool _collisionSubscribed;
        private long _lastCollisionTick;
        private const int CollisionThrottleMs = 100;

        // Linkset
        private BepuActor _linksetParent;
        private readonly List<BepuActor> _linksetChildren = new();

        public BodyHandle BodyHandle => _bodyHandle;
        public bool HasBody => _hasBody;
        public bool Initialized => _initialized;
        public TypedIndex ShapeIndex => _shapeIndex;

        public BepuActor(BepuScene scene, uint localId, string name,
                         Vector3 position, Vector3 velocity, Vector3 size,
                         Quaternion orientation, float mass, bool isPhysical, bool isAvatar)
        {
            _scene = scene ?? throw new ArgumentNullException(nameof(scene));
            m_baseLocalID = localId;
            Name = name;

            _position = position;
            _velocity = velocity;
            _size = size;
            _orientation = orientation;
            _mass = mass;
            _isPhysical = isPhysical;
            _physicsActorType = isAvatar ? (int)ActorTypes.Agent : (int)ActorTypes.Prim;
            _bodyHandle = default;
            _hasBody = false;
            _initialized = true;
        }

        #region PhysicsActor abstract members

        public override bool Stopped
        {
            get => _stopped;
        }

        public override Vector3 Size
        {
            get => _size;
            set
            {
                _size = value;
                if (_hasBody)
                    _scene.ScheduleShapeUpdate(this);
            }
        }

        public override bool Phantom
        {
            get => _phantom;
            set => _phantom = value;
        }

        public override byte PhysicsShapeType
        {
            get => _physicsShapeType;
            set => _physicsShapeType = value;
        }

        public override PrimitiveBaseShape Shape
        {
            set => _shape = value;
        }

        public override uint LocalID
        {
            get => m_baseLocalID;
            set => m_baseLocalID = value;
        }

        public override bool Grabbed
        {
            set => _grabbed = value;
        }

        public override bool Selected
        {
            set => _selected = value;
        }

        public override Vector3 Position
        {
            get => _position;
            set
            {
                _position = value;
                if (_hasBody)
                    _scene.SchedulePositionUpdate(this, value);
            }
        }

        public override float Mass => _mass;

        public override Vector3 Force
        {
            get => _force;
            set => _force = value;
        }

        public override int VehicleType
        {
            get => _vehicleType;
            set => _vehicleType = value;
        }

        public override void VehicleFloatParam(int param, float value) { } // Vehicle physics implemented in Phase 7
        public override void VehicleVectorParam(int param, Vector3 value) { } // Vehicle physics implemented in Phase 7
        public override void VehicleRotationParam(int param, Quaternion rotation) { } // Vehicle physics implemented in Phase 7
        public override void VehicleFlags(int param, bool remove) { } // Vehicle physics implemented in Phase 7

        public override Vector3 Velocity
        {
            get => _velocity;
            set
            {
                _velocity = value;
                if (_hasBody)
                    _scene.ScheduleVelocityUpdate(this, value);
            }
        }

        public override Vector3 Torque
        {
            get => _torque;
            set => _torque = value;
        }

        public override float CollisionScore
        {
            get => _collisionScore;
            set => _collisionScore = value;
        }

        public override Vector3 Acceleration
        {
            get => _acceleration;
            set => _acceleration = value;
        }

        public override Quaternion Orientation
        {
            get => _orientation;
            set
            {
                _orientation = value;
                if (_hasBody)
                    _scene.ScheduleOrientationUpdate(this, value);
            }
        }

        public override int PhysicsActorType
        {
            get => _physicsActorType;
            set => _physicsActorType = value;
        }

        public override bool IsPhysical
        {
            get => _isPhysical;
            set
            {
                _isPhysical = value;
                if (_hasBody)
                    _scene.SchedulePhysicalFlagUpdate(this, value);
            }
        }

        public override bool Flying
        {
            get => _isFlying;
            set => _isFlying = value;
        }

        public override bool SetAlwaysRun
        {
            get => _setAlwaysRun;
            set => _setAlwaysRun = value;
        }

        public override bool ThrottleUpdates
        {
            get => _throttleUpdates;
            set => _throttleUpdates = value;
        }

        public override bool IsColliding
        {
            get => _isColliding;
            set => _isColliding = value;
        }

        public override bool CollidingGround
        {
            get => _collidingGround;
            set => _collidingGround = value;
        }

        public override bool CollidingObj
        {
            get => _collidingObj;
            set => _collidingObj = value;
        }

        public override Vector3 RotationalVelocity
        {
            get => _rotationalVelocity;
            set
            {
                _rotationalVelocity = value;
                if (_hasBody)
                    _scene.ScheduleAngularVelocityUpdate(this, value);
            }
        }

        public override bool Kinematic
        {
            get => _kinematic;
            set
            {
                _kinematic = value;
                if (_hasBody)
                    _scene.ScheduleKinematicUpdate(this, value);
            }
        }

        public override float Buoyancy
        {
            get => _buoyancy;
            set => _buoyancy = value;
        }

        public new bool FloatOnWater
        {
            get => _floatOnWater;
            set => _floatOnWater = value;
        }

        public override Vector3 PIDTarget
        {
            set
            {
                _pidTarget = value;
                SyncPidToScene();
            }
        }

        public override bool PIDActive
        {
            get => _pidActive;
            set
            {
                _pidActive = value;
                SyncPidToScene();
            }
        }

        public override float PIDTau
        {
            set
            {
                _pidTau = value;
                SyncPidToScene();
            }
        }

        public override bool PIDHoverActive
        {
            get => _pidHoverActive;
            set => _pidHoverActive = value;
        }

        public override float PIDHoverHeight
        {
            set => _pidHoverHeight = value;
        }

        public override PIDHoverType PIDHoverType
        {
            set => _pidHoverType = value;
        }

        public override float PIDHoverTau
        {
            set => _pidHoverTau = value;
        }

        public override Quaternion APIDTarget
        {
            set => _apidTarget = value;
        }

        public override bool APIDActive
        {
            set => _apidActive = value;
        }

        public override float APIDStrength
        {
            set => _apidStrength = value;
        }

        public override float APIDDamping
        {
            set => _apidDamping = value;
        }

        public override float Density
        {
            get => _density;
            set => _density = value;
        }

        public override float GravModifier
        {
            get => _gravModifier;
            set => _gravModifier = value;
        }

        public override float Friction
        {
            get => _friction;
            set => _friction = value;
        }

        public override float Restitution
        {
            get => _restitution;
            set => _restitution = value;
        }

        public override float SimulationSuspended
        {
            get => _simulationSuspended;
            set => _simulationSuspended = value;
        }

        public override Vector3 GeometricCenter => _size * 0.5f;
        public override Vector3 CenterOfMass => _size * 0.5f;

        #endregion

        #region Methods

        public override void AddForce(Vector3 force, bool pushforce)
        {
            if (!_hasBody) return;

            var snForce = BepuUtil.ToSN(force);
            _scene.EnqueueBodyAction(_bodyHandle, ref snForce, BodyActionType.AddForce);
        }

        public override void AddAngularForce(Vector3 force, bool pushforce)
        {
            if (!_hasBody) return;

            var snForce = BepuUtil.ToSN(force);
            _scene.EnqueueBodyAction(_bodyHandle, ref snForce, BodyActionType.AddAngularForce);
        }

        public override void SetMomentum(Vector3 momentum)
        {
            if (!_hasBody) return;

            var snMomentum = BepuUtil.ToSN(momentum);
            _scene.EnqueueBodyAction(_bodyHandle, ref snMomentum, BodyActionType.SetMomentum);
        }

        public override void AvatarJump(float forceZ)
        {
            AddForce(new Vector3(0, 0, forceZ), true);
        }

        public override void link(PhysicsActor obj)
        {
            if (obj is BepuActor bepuChild && _hasBody)
            {
                _linksetChildren.Add(bepuChild);
                bepuChild._linksetParent = this;
                _scene.ScheduleLinksetUpdate(this);
            }
        }

        public override void delink()
        {
            if (_linksetParent != null)
            {
                _linksetParent._linksetChildren.Remove(this);
                _linksetParent = null;
                _scene.ScheduleLinksetUpdate(this);
            }
        }

        public override void LockAngularMotion(byte axislocks)
        {
            _angularLockAxes = axislocks;
            if (_hasBody)
                _scene.ScheduleAngularLockUpdate(this, axislocks);
        }

        public override void CrossingFailure()
        {
            m_log.WarnFormat("[BEPU] CrossingFailure for {0} ({1})", Name, LocalID);
        }

        public override void SetVolumeDetect(int param)
        {
            // VolumeDetect via Bepu's ContinuousDetection — implement when needed
        }

        public override void SetMaterial(int material)
        {
            // Material properties via Bepu's MaterialProperties — implement when needed
        }

        public override void SubscribeEvents(int ms)
        {
            _subscribeIntervalMs = ms;
            _subscribed = true;
        }

        public override void UnSubscribeEvents()
        {
            _subscribed = false;
        }

        public override bool SubscribedEvents() => _subscribed;

        /// <summary>
        /// Subscribe to collision events with the given throttle interval.
        /// Separate from SubscribeEvents which handles position/orientation updates.
        /// </summary>
        internal void SubscribeCollisionEvents(int ms)
        {
            _collisionSubscribed = true;
        }

        /// <summary>
        /// Get per-actor material properties for collision response.
        /// Called from BepuScene.ResolveCollidableMaterial during narrow phase.
        /// </summary>
        internal (float friction, float restitution) GetMaterialProperties()
            => (_friction, _restitution);

        /// <summary>
        /// Build a PidState snapshot from current PID values.
        /// Called before syncing to BepuScene's PID tracking dictionary.
        /// </summary>
        internal PidState GetPidState() => new()
        {
            Target = _pidTarget,
            Tau = _pidTau,
            Active = _pidActive,
            HoverHeight = _pidHoverHeight,
            HoverType = _pidHoverType,
            HoverTau = _pidHoverTau
        };

        /// <summary>
        /// Sync the current PID state to BepuScene's tracking dictionary.
        /// Called from PID property setters to keep the scene informed.
        /// </summary>
        private void SyncPidToScene()
        {
            if (_hasBody && _scene != null)
                _scene.SyncPidState(LocalID, GetPidState());
        }

        /// <summary>
        /// Unsubscribe from collision events.
        /// </summary>
        internal void UnSubscribeCollisionEvents()
        {
            _collisionSubscribed = false;
        }

        #endregion

        #region Internal (called by BepuScene on simulation results)

        /// <summary>
        /// Update cached position/velocity/orientation from Bepu simulation result.
        /// Called from BepuScene after each Simulate() step.
        /// </summary>
        internal void ApplySimulationResult(Vector3 position, Quaternion orientation, Vector3 velocity, Vector3 rotationalVelocity)
        {
            bool posChanged = Vector3.DistanceSquared(_position, position) > 0.0001f;
            bool rotChanged = !_orientation.ApproxEquals(orientation, 0.0001f);

            _position = position;
            _orientation = orientation;
            _velocity = velocity;
            _rotationalVelocity = rotationalVelocity;
            _stopped = velocity.LengthSquared() < 0.0001f;

            if (posChanged)
                RaisePositionUpdate(position);
            if (rotChanged)
                RaiseOrientationUpdate(orientation);

            if ((posChanged || rotChanged) && _subscribed)
                RequestPhysicsterseUpdate();
        }

        internal void SetBody(BodyHandle handle, TypedIndex shapeIndex, float mass)
        {
            _bodyHandle = handle;
            _shapeIndex = shapeIndex;
            _mass = mass;
            _hasBody = true;
        }

        internal void RemoveBody()
        {
            _hasBody = false;
            _bodyHandle = default;
        }

        /// <summary>
        /// Called by BepuScene collision system.
        /// Fires collision events if subscribed and throttle allows.
        /// An empty dictionary signals collision-end to the consumer.
        /// </summary>
        internal bool FireCollisionEvents(Dictionary<uint, ContactPoint> collisions)
        {
            if (!_collisionSubscribed)
                return false;

            // Apply throttle: skip if not enough time has passed since last event
            long now = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond;
            if (collisions.Count > 0 && (now - _lastCollisionTick) < CollisionThrottleMs)
                return false;

            _lastCollisionTick = now;

            var update = new CollisionEventUpdate(collisions);
            SendCollisionUpdate(update);
            return true;
        }

        #endregion
    }

    /// <summary>
    /// Types of deferred body actions queued for the next simulation step.
    /// </summary>
    internal enum BodyActionType
    {
        AddForce,
        AddAngularForce,
        SetMomentum
    }

    /// <summary>
    /// A deferred action to be applied to a Bepu body during the next simulation step.
    /// </summary>
    internal struct BodyAction
    {
        public BodyHandle Handle;
        public System.Numerics.Vector3 Value;
        public BodyActionType Type;
    }
}
