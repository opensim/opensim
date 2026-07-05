using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using OpenSim.Framework;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.PhysicsModules.SharedBase;
using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.CollisionDetection;
using BepuPhysics.Constraints;
using BepuUtilities;
using BepuUtilities.Memory;
using log4net;
using Nini.Config;
using Mono.Addins;
using Quaternion = OpenMetaverse.Quaternion;
using Vector3 = OpenMetaverse.Vector3;

namespace OpenSim.Region.PhysicsModule.Bepu
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "BepuPhysicsScene")]
    public sealed class BepuScene : PhysicsScene
    {
        private static readonly ILog m_log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private const string LogHeader = "[BEPU SCENE]";

        private bool _initialized;
        private bool _enabled;
        private string _regionName;

        // Bepu core types
        private BufferPool _bufferPool;
        private Simulation _simulation;
        private ThreadDispatcher _threadDispatcher;

        // Actor tracking
        private readonly Dictionary<uint, BepuActor> _actors = new();
        private readonly Dictionary<BodyHandle, BepuActor> _bodyHandleToActor = new();
        private readonly object _actorsLock = new();

        // Deferred body actions (queued by BepuActor, applied before Simulate)
        private readonly ConcurrentQueue<BodyAction> _bodyActions = new();

        // Scheduled updates (queued by BepuActor property setters)
        private readonly ConcurrentQueue<Action> _scheduledUpdates = new();

        // Collision tracking
        private readonly Dictionary<uint, Dictionary<uint, ContactPoint>> _activeCollisions = new();
        private readonly List<BufferedContact> _contactBuffer = new();

        // Terrain
        private float[] _terrainHeightMap;
        private float _waterLevel;
        private StaticHandle _terrainStaticHandle;
        private bool _hasTerrain;

        // World constants
        private const float DefaultGravityZ = -9.81f;
        private const int MaxBodies = 4096;
        private const int MaxConstraints = 1024;
        private const int DefaultAllocatorPoolSize = 128;

        // Time dilation tracking
        private float _timeDilation = 1.0f;

        #region Constructor

        public BepuScene()
        {
        }

        private void InitializePhysics(Vector3 regionExtent)
        {
            _bufferPool = new BufferPool(DefaultAllocatorPoolSize);

            var pose = new RigidPose(System.Numerics.Vector3.Zero, System.Numerics.Quaternion.Identity);

            var narrowPhase = new BepuNarrowPhaseCallbacks();
            narrowPhase.Contacts = _contactBuffer;

            _simulation = Simulation.Create(
                _bufferPool,
                narrowPhase,
                new BepuPoseIntegratorCallbacks(new System.Numerics.Vector3(0, 0, DefaultGravityZ)),
                new BepuSolveDescription()
            );

            _threadDispatcher = new ThreadDispatcher(Environment.ProcessorCount > 1 ? Environment.ProcessorCount - 1 : 1);

            _initialized = true;
            m_log.InfoFormat("{0} BepuPhysics initialized. Gravity={1}, Threads={2}",
                LogHeader, DefaultGravityZ, _threadDispatcher.ThreadCount);
        }

        #endregion

        #region PhysicsScene abstract overrides

        public override PhysicsActor AddAvatar(
            string avName, Vector3 position, Vector3 velocity, Vector3 size, bool isFlying)
        {
            if (!_initialized) return null;

            var actor = new BepuActor(this, 0, avName, position, velocity, size,
                                       Quaternion.Identity, 70f, true, true)
            {
                Flying = isFlying
            };

            lock (_actorsLock)
                _actors[0] = actor; // LocalID assigned by caller via overload

            return actor;
        }

        public override PhysicsActor AddAvatar(
            uint localID, string avName, Vector3 position, Vector3 velocity, Vector3 size, bool isFlying)
        {
            if (!_initialized) return null;

            var actor = new BepuActor(this, localID, avName, position, velocity, size,
                                       Quaternion.Identity, 70f, true, true)
            {
                Flying = isFlying
            };

            lock (_actorsLock)
                _actors[localID] = actor;

            return actor;
        }

        public override void RemoveAvatar(PhysicsActor actor)
        {
            if (!_initialized || actor == null) return;

            if (actor is BepuActor bepuActor)
            {
                lock (_actorsLock)
                    _actors.Remove(bepuActor.LocalID);

                if (bepuActor.HasBody)
                {
                    _bodyHandleToActor.Remove(bepuActor.BodyHandle);
                    _simulation.Bodies.Remove(bepuActor.BodyHandle);
                    bepuActor.RemoveBody();
                }
            }
        }

        public override void RemovePrim(PhysicsActor prim)
        {
            if (!_initialized || prim == null) return;

            if (prim is BepuActor bepuPrim)
            {
                lock (_actorsLock)
                    _actors.Remove(bepuPrim.LocalID);

                if (bepuPrim.HasBody)
                {
                    _bodyHandleToActor.Remove(bepuPrim.BodyHandle);
                    _simulation.Bodies.Remove(bepuPrim.BodyHandle);
                    bepuPrim.RemoveBody();
                }
            }
        }

        public override PhysicsActor AddPrimShape(
            string primName, PrimitiveBaseShape pbs, Vector3 position,
            Vector3 size, Quaternion rotation, bool isPhysical, uint localID)
        {
            if (!_initialized) return null;

            var actor = new BepuActor(this, localID, primName, position, Vector3.Zero,
                                       size, rotation, CalculateMass(size, isPhysical),
                                       isPhysical, false);

            // Create the Bepu body for this prim
            var snPosition = BepuUtil.ToSN(position);
            var snRotation = BepuUtil.ToSN(rotation);
            var snSize = BepuUtil.ToSN(size);

            var bodyDescription = CreateBodyDescription(
                ref snPosition, ref snRotation, ref snSize,
                actor.Mass, isPhysical, localID);

            var handle = _simulation.Bodies.Add(bodyDescription);
            actor.SetBody(handle, bodyDescription.Collidable.Shape, actor.Mass);

            lock (_actorsLock)
            {
                _actors[localID] = actor;
                _bodyHandleToActor[handle] = actor;
            }

            return actor;
        }

        public override float Simulate(float timeStep)
        {
            if (!_initialized) return 5.0f;

            // Apply all queued body actions and scheduled updates
            ProcessScheduledUpdates();

            // Run Bepu simulation
            _simulation.Timestep(timeStep, _threadDispatcher);

            // Collect results and push to actors
            PushSimulationResults();

            // Process collisions
            ProcessCollisions();

            // Return simulated frames (always 1 for Bepu)
            return 1.0f;
        }

        public override void SetTerrain(float[] heightMap)
        {
            _terrainHeightMap = heightMap;

            if (!_initialized) return;

            // Remove previous terrain if any
            if (_hasTerrain)
            {
                _simulation.Statics.Remove(_terrainStaticHandle);
                _hasTerrain = false;
            }

            if (heightMap == null || heightMap.Length == 0)
                return;

            // Bepu terrain: use a Mesh from the heightmap
            // For simplicity in Phase 2, use a ground plane + basic mesh
            // Full heightmap mesh implementation comes in Phase 4
            var quad = new Box(256f, 256f, 1f);
            var shapeIndex = _simulation.Shapes.Add(quad);
            var staticPose = new RigidPose(
                new System.Numerics.Vector3(128f, 128f, -0.5f),
                System.Numerics.Quaternion.Identity);
            _terrainStaticHandle = _simulation.Statics.Add(new StaticDescription(staticPose, shapeIndex));
            _hasTerrain = true;
        }

        public override void SetWaterLevel(float baseheight)
        {
            _waterLevel = baseheight;
        }

        public override void DeleteTerrain()
        {
            if (_hasTerrain && _initialized)
            {
                _simulation.Statics.Remove(_terrainStaticHandle);
                _hasTerrain = false;
            }
            _terrainHeightMap = null;
        }

        public override void Dispose()
        {
            if (!_initialized) return;

            lock (_actorsLock)
            {
                foreach (var actor in _actors.Values)
                {
                    if (actor.HasBody)
                    {
                        _simulation.Bodies.Remove(actor.BodyHandle);
                        actor.RemoveBody();
                    }
                }
                _actors.Clear();
            }

            _simulation.Dispose();
            _bufferPool.Clear();
            _threadDispatcher.Dispose();
            _initialized = false;
        }

        public override Dictionary<uint, float> GetTopColliders()
        {
            var topColliders = new List<KeyValuePair<uint, float>>();
            lock (_actorsLock)
            {
                foreach (var kvp in _actors)
                {
                    float score = kvp.Value.CollisionScore;
                    if (score > 0f)
                        topColliders.Add(new KeyValuePair<uint, float>(kvp.Key, score));
                }
            }

            topColliders.Sort((a, b) => b.Value.CompareTo(a.Value));

            var result = new Dictionary<uint, float>();
            int count = Math.Min(topColliders.Count, 25);
            for (int i = 0; i < count; i++)
                result[topColliders[i].Key] = topColliders[i].Value;

            return result;
        }

        #endregion

        #region Bepu-specific API (called by BepuActor)

        internal void EnqueueBodyAction(BodyHandle handle, ref System.Numerics.Vector3 value, BodyActionType type)
        {
            _bodyActions.Enqueue(new BodyAction { Handle = handle, Value = value, Type = type });
        }

        internal void SchedulePositionUpdate(BepuActor actor, Vector3 position)
        {
            var snPos = BepuUtil.ToSN(position);
            _scheduledUpdates.Enqueue(() =>
            {
                if (actor.HasBody)
                {
                    ref var body = ref _simulation.Bodies.GetBodyReference(actor.BodyHandle);
                    var pose = body.Pose;
                    pose.Position = snPos;
                    body.Pose = pose;
                }
            });
        }

        internal void ScheduleOrientationUpdate(BepuActor actor, OpenMetaverse.Quaternion orientation)
        {
            var snRot = BepuUtil.ToSN(orientation);
            _scheduledUpdates.Enqueue(() =>
            {
                if (actor.HasBody)
                {
                    ref var body = ref _simulation.Bodies.GetBodyReference(actor.BodyHandle);
                    var pose = body.Pose;
                    pose.Orientation = snRot;
                    body.Pose = pose;
                }
            });
        }

        internal void ScheduleVelocityUpdate(BepuActor actor, Vector3 velocity)
        {
            var snVel = BepuUtil.ToSN(velocity);
            _scheduledUpdates.Enqueue(() =>
            {
                if (actor.HasBody)
                {
                    ref var body = ref _simulation.Bodies.GetBodyReference(actor.BodyHandle);
                    body.Velocity.Linear = snVel;
                }
            });
        }

        internal void ScheduleAngularVelocityUpdate(BepuActor actor, Vector3 angularVelocity)
        {
            var snAngVel = BepuUtil.ToSN(angularVelocity);
            _scheduledUpdates.Enqueue(() =>
            {
                if (actor.HasBody)
                {
                    ref var body = ref _simulation.Bodies.GetBodyReference(actor.BodyHandle);
                    body.Velocity.Angular = snAngVel;
                }
            });
        }

        internal void SchedulePhysicalFlagUpdate(BepuActor actor, bool isPhysical)
        {
            _scheduledUpdates.Enqueue(() =>
            {
                if (actor.HasBody)
                {
                    ref var body = ref _simulation.Bodies.GetBodyReference(actor.BodyHandle);
                    if (isPhysical)
                    {
                        body.Activity = new BodyActivityDescription { SleepThreshold = 0.01f };
                    }
                    else
                    {
                        // Kinematic body for non-physical
                        body.SetLocalInertia(default);
                    }
                }
            });
        }

        internal void ScheduleKinematicUpdate(BepuActor actor, bool kinematic)
        {
            _scheduledUpdates.Enqueue(() =>
            {
                if (actor.HasBody)
                {
                    ref var body = ref _simulation.Bodies.GetBodyReference(actor.BodyHandle);
                    if (kinematic)
                    {
                        body.SetLocalInertia(default);
                    }
                    else
                    {
                        // TODO: restore dynamic inertia when making body dynamic
                        // Requires mass and shape info to compute proper inertia
                    }
                }
            });
        }

        internal void ScheduleShapeUpdate(BepuActor actor)
        {
            _scheduledUpdates.Enqueue(() =>
            {
                if (!actor.HasBody) return;

                var snSize = BepuUtil.ToSN(actor.Size);
                ref var body = ref _simulation.Bodies.GetBodyReference(actor.BodyHandle);

                // Remove old shape, add new box
                // TODO: support arbitrary shapes in Phase 4
                _simulation.Shapes.Remove(body.Collidable.Shape);
                var newBox = new Box(snSize.X, snSize.Y, snSize.Z);
                body.Collidable.Shape = _simulation.Shapes.Add(newBox);
            });
        }

        internal void ScheduleLinksetUpdate(BepuActor actor)
        {
            // Linkset constraints will be implemented in Phase 4
        }

        /// <summary>
        /// Subscribe an actor to collision events at the given throttle interval.
        /// </summary>
        internal void SubscribeCollisionEvents(uint localID, int ms)
        {
            if (_actors.TryGetValue(localID, out var actor))
                actor.SubscribeCollisionEvents(ms);
        }

        #endregion

        #region Internal simulation plumbing

        private void ProcessScheduledUpdates()
        {
            // Process body actions (forces)
            while (_bodyActions.TryDequeue(out var action))
            {
                if (!_simulation.Bodies.BodyExists(action.Handle)) continue;
                var bodyRef = _simulation.Bodies.GetBodyReference(action.Handle);

                switch (action.Type)
                {
                    case BodyActionType.AddForce:
                        bodyRef.ApplyImpulse(action.Value, System.Numerics.Vector3.Zero);
                        break;
                    case BodyActionType.AddAngularForce:
                        bodyRef.ApplyAngularImpulse(action.Value);
                        break;
                    case BodyActionType.SetMomentum:
                        // Velocity change instead of momentum
                        bodyRef.Velocity.Linear = action.Value;
                        break;
                }
            }

            // Process scheduled updates
            while (_scheduledUpdates.TryDequeue(out var update))
            {
                try { update(); }
                catch (Exception ex)
                {
                    m_log.WarnFormat("{0} Scheduled update failed: {1}", LogHeader, ex.Message);
                }
            }
        }

        private void PushSimulationResults()
        {
            lock (_actorsLock)
            {
                foreach (var kvp in _actors)
                {
                    var actor = kvp.Value;
                    if (!actor.HasBody) continue;

                    ref var body = ref _simulation.Bodies.GetBodyReference(actor.BodyHandle);

                    var pos = BepuUtil.ToOM(body.Pose.Position);
                    var rot = BepuUtil.ToOM(body.Pose.Orientation);
                    var vel = BepuUtil.ToOM(body.Velocity.Linear);
                    var angVel = BepuUtil.ToOM(body.Velocity.Angular);

                    actor.ApplySimulationResult(pos, rot, vel, angVel);
                }
            }
        }

        private void ProcessCollisions()
        {
            if (_contactBuffer.Count == 0)
                return;

            // Build this frame's collision map: actorLocalID → { colliderLocalID → ContactPoint }
            var thisFrameCollisions = new Dictionary<uint, Dictionary<uint, ContactPoint>>();

            // Track collision scores per actor this frame
            var collisionScores = new Dictionary<uint, float>();

            foreach (var contact in _contactBuffer)
            {
                if (!TryGetLocalID(contact.CollidableA, out var localA) ||
                    !TryGetLocalID(contact.CollidableB, out var localB))
                    continue;

                // Skip terrain collisions
                if (IsTerrainHandle(localA) || IsTerrainHandle(localB))
                    continue;

                // Compute world contact position — the Offset is from CollidableA's position
                var worldPos = GetContactWorldPosition(contact);
                var omPos = BepuUtil.ToOM(worldPos);
                var omNormal = BepuUtil.ToOM(contact.Normal);
                var cp = new ContactPoint(omPos, omNormal, contact.Depth);

                // Add contact from A's perspective (A collided with B)
                AddCollisionEntry(thisFrameCollisions, localA, localB, cp);

                // Add contact from B's perspective (B collided with A) with inverted normal
                var cpInverted = new ContactPoint(omPos, -omNormal, contact.Depth);
                AddCollisionEntry(thisFrameCollisions, localB, localA, cpInverted);

                // Accumulate collision scores
                float absDepth = Math.Abs(contact.Depth);
                collisionScores[localA] = collisionScores.GetValueOrDefault(localA) + absDepth;
                collisionScores[localB] = collisionScores.GetValueOrDefault(localB) + absDepth;
            }

            // Send collision events for actors with collisions this frame
            foreach (var kvp in thisFrameCollisions)
            {
                uint actorLocalID = kvp.Key;
                var colliderMap = kvp.Value;

                if (_actors.TryGetValue(actorLocalID, out var actor))
                {
                    // Set collision score for this actor
                    if (collisionScores.TryGetValue(actorLocalID, out float score))
                        actor.CollisionScore = score;

                    // Set collision state flags
                    actor.IsColliding = true;
                    actor.CollidingObj = true;

                    // Fire collision events (with throttle)
                    actor.FireCollisionEvents(colliderMap);
                }
            }

            // Detect collision END: actors that had collisions last frame but not this frame
            foreach (var kvp in _activeCollisions)
            {
                uint actorLocalID = kvp.Key;
                if (!thisFrameCollisions.ContainsKey(actorLocalID))
                {
                    // Send empty CollisionEventUpdate to signal collision end
                    if (_actors.TryGetValue(actorLocalID, out var actor))
                    {
                        actor.IsColliding = false;
                        actor.CollidingObj = false;
                        actor.FireCollisionEvents(new Dictionary<uint, ContactPoint>());
                    }
                }
            }

            // Update active collisions for next frame
            _activeCollisions.Clear();
            foreach (var kvp in thisFrameCollisions)
                _activeCollisions[kvp.Key] = kvp.Value;

            // Clear the contact buffer for the next step
            _contactBuffer.Clear();
        }

        /// <summary>
        /// Map a CollidableReference to an actor's local ID.
        /// </summary>
        private bool TryGetLocalID(CollidableReference collidable, out uint localID)
        {
            if (collidable.Mobility == CollidableMobility.Static)
            {
                // Static handle — could be terrain or other static
                localID = uint.MaxValue - 1; // Sentinel for terrain
                return true;
            }

            // For dynamic/kinematic bodies, look up by BodyHandle
            if (_bodyHandleToActor.TryGetValue(new BodyHandle(collidable.Handle), out var actor))
            {
                localID = actor.LocalID;
                return true;
            }

            localID = 0;
            return false;
        }

        /// <summary>
        /// Check if a localID is one of the terrain sentinel values.
        /// </summary>
        private static bool IsTerrainHandle(uint localID)
        {
            return localID == uint.MaxValue - 1;
        }

        /// <summary>
        /// Compute the world-space contact position from a BufferedContact.
        /// The Offset is relative to CollidableA's position.
        /// </summary>
        private System.Numerics.Vector3 GetContactWorldPosition(in BufferedContact contact)
        {
            if (contact.CollidableA.Mobility == CollidableMobility.Static)
            {
                ref var staticRef = ref _simulation.Statics.GetStaticReference(
                    new StaticHandle(contact.CollidableA.Handle));
                return staticRef.Pose.Position + contact.Offset;
            }
            else
            {
                ref var bodyRef = ref _simulation.Bodies.GetBodyReference(
                    new BodyHandle(contact.CollidableA.Handle));
                return bodyRef.Pose.Position + contact.Offset;
            }
        }

        /// <summary>
        /// Add a collision entry to the per-frame collision map.
        /// </summary>
        private static void AddCollisionEntry(
            Dictionary<uint, Dictionary<uint, ContactPoint>> frameCollisions,
            uint actorLocalID, uint colliderLocalID, ContactPoint cp)
        {
            if (!frameCollisions.TryGetValue(actorLocalID, out var colliderMap))
            {
                colliderMap = new Dictionary<uint, ContactPoint>();
                frameCollisions[actorLocalID] = colliderMap;
            }

            // Keep the deepest penetration entry for each collider pair
            if (!colliderMap.TryGetValue(colliderLocalID, out var existing) ||
                Math.Abs(cp.PenetrationDepth) > Math.Abs(existing.PenetrationDepth))
            {
                colliderMap[colliderLocalID] = cp;
            }
        }

        private BodyDescription CreateBodyDescription(
            ref System.Numerics.Vector3 position,
            ref System.Numerics.Vector3 rotation,
            ref System.Numerics.Vector3 size,
            float mass, bool isPhysical, uint localID)
        {
            var box = new Box(size.X, size.Y, size.Z);
            var shapeIndex = _simulation.Shapes.Add(box);

            var pose = new RigidPose(position, System.Numerics.Quaternion.Identity);

            if (isPhysical && mass > 0)
            {
                var inertia = box.ComputeInertia(mass);
                return BodyDescription.CreateDynamic(pose, inertia, shapeIndex, 0.01f);
            }
            else
            {
                return BodyDescription.CreateKinematic(pose, shapeIndex, 0.01f);
            }
        }

        private static float CalculateMass(Vector3 size, bool isPhysical)
        {
            if (!isPhysical) return 0f;
            float volume = size.X * size.Y * size.Z;
            return Math.Max(volume * 10f, 1f); // density ~10
        }

        #endregion

        #region Raycasting

        public override bool SupportsRayCast() => true;

        public override void RaycastWorld(Vector3 position, Vector3 direction, float length, RaycastCallback retMethod)
        {
            if (!_initialized)
            {
                retMethod?.Invoke(false, Vector3.Zero, 0, 999999f, Vector3.Zero);
                return;
            }

            var rayOrigin = BepuUtil.ToSN(position);
            var rayDir = BepuUtil.ToSN(direction);

            var hitHandler = new RayHitHandler();
            _simulation.RayCast(rayOrigin, rayDir, length, ref hitHandler);

            if (retMethod != null)
            {
                if (hitHandler.Hit)
                {
                    var hitPoint = BepuUtil.ToOM(rayOrigin + rayDir * hitHandler.T);
                    var hitNormal = BepuUtil.ToOM(hitHandler.Normal);
                    retMethod(true, hitPoint, 0, hitHandler.T, hitNormal);
                }
                else
                {
                    retMethod(false, Vector3.Zero, 0, 999999f, Vector3.Zero);
                }
            }
        }

        public override void RaycastWorld(Vector3 position, Vector3 direction, float length, int count, RayCallback retMethod)
        {
            if (!_initialized)
            {
                retMethod?.Invoke(new List<ContactResult>());
                return;
            }

            var results = CollectRayHits(position, direction, length, count, RayFilterFlags.All);
            retMethod?.Invoke(results);
        }

        public override List<ContactResult> RaycastWorld(Vector3 position, Vector3 direction, float length, int count)
        {
            if (!_initialized)
                return new List<ContactResult>();

            return CollectRayHits(position, direction, length, count, RayFilterFlags.All);
        }

        public override object RaycastWorld(Vector3 position, Vector3 direction, float length, int count, RayFilterFlags filter)
        {
            if (!_initialized)
                return new List<ContactResult>();

            return CollectRayHits(position, direction, length, count, filter);
        }

        private List<ContactResult> CollectRayHits(Vector3 position, Vector3 direction, float length, int count, RayFilterFlags filter)
        {
            var rayOrigin = BepuUtil.ToSN(position);
            var rayDir = BepuUtil.ToSN(direction);

            var hitHandler = new MultiHitHandler
            {
                Results = new List<ContactResult>(),
                MaxHits = count,
                Origin = rayOrigin,
                Direction = rayDir,
                Filter = filter
            };

            _simulation.RayCast(rayOrigin, rayDir, length, ref hitHandler);
            return hitHandler.Results;
        }

        #endregion

        #region Probes

        public override List<ContactResult> SphereProbe(Vector3 position, float radius, int count, RayFilterFlags flags)
        {
            // Implement in Phase 4 with Bepu's sweep tests
            return new List<ContactResult>();
        }

        public override List<ContactResult> BoxProbe(Vector3 position, Vector3 size, Quaternion orientation, int count, RayFilterFlags flags)
        {
            return new List<ContactResult>();
        }

        #endregion

        #region Time and stats

        public override float TimeDilation
        {
            get => _timeDilation;
        }

        public override Dictionary<string, float> GetStats()
        {
            var stats = new Dictionary<string, float>
            {
                ["BepuBodyCount"] = _simulation.Bodies.CountBodies(),
                ["BepuActiveBodyCount"] = _simulation.Bodies.ActiveSet.Count,
                ["BepuStaticCount"] = _simulation.Statics.Count,
                ["BepuConstraintCount"] = _simulation.Solver.CountConstraints(),
            };
            return stats;
        }

        #endregion
    }

    #region Bepu callbacks

    /// <summary>
    /// A single collision contact captured from Bepu's narrow phase.
    /// </summary>
    internal struct BufferedContact
    {
        public CollidableReference CollidableA;
        public CollidableReference CollidableB;
        public System.Numerics.Vector3 Offset; // Offset from CollidableA's position to contact
        public System.Numerics.Vector3 Normal;
        public float Depth;
    }

    /// <summary>
    /// Narrow phase callbacks that collect contacts for the collision pipeline.
    /// Contacts are extracted from manifolds and buffered for batch processing
    /// after the simulation step.
    /// </summary>
    internal struct BepuNarrowPhaseCallbacks : INarrowPhaseCallbacks
    {
        /// <summary>
        /// Contact buffer populated during the narrow phase.
        /// Set before passing this struct to Simulation.Create.
        /// Shared by reference across struct copies since List is a reference type.
        /// </summary>
        public List<BufferedContact> Contacts;

        public void Initialize(Simulation simulation) { }

        public bool AllowContactGeneration(int workerIndex, CollidableReference a, CollidableReference b, ref float speculativeMargin)
            => true;

        public bool AllowContactGeneration(int workerIndex, CollidablePair pair, int childIndexA, int childIndexB)
            => true;

        public bool ConfigureContactManifold<TManifold>(int workerIndex, CollidablePair pair, ref TManifold manifold, out PairMaterialProperties pairMaterial)
            where TManifold : unmanaged, IContactManifold<TManifold>
        {
            pairMaterial = new PairMaterialProperties
            {
                FrictionCoefficient = 0.5f,
                MaximumRecoveryVelocity = 2f,
                SpringSettings = new SpringSettings(30f, 1f)
            };

            if (Contacts == null)
                return true;

            int count = manifold.Count;
            if (count <= 0)
                return true;

            // Runtime type dispatch to access concrete manifold's GetContact method.
            // Both ConvexContactManifold and NonconvexContactManifold share the same
            // contact access pattern. Handle the common convex case here.
            if (typeof(TManifold) == typeof(ConvexContactManifold))
            {
                ref var convex = ref Unsafe.As<TManifold, ConvexContactManifold>(ref manifold);
                ReadContactsFromConvex(pair, ref convex);
            }

            return true;
        }

        public bool ConfigureContactManifold(int workerIndex, CollidablePair pair, int childIndexA, int childIndexB, ref ConvexContactManifold manifold)
        {
            if (Contacts == null)
                return true;

            ReadContactsFromConvex(pair, ref manifold);
            return true;
        }

        public void Dispose() { }

        private void ReadContactsFromConvex(CollidablePair pair, ref ConvexContactManifold manifold)
        {
            for (int i = 0; i < manifold.Count; i++)
            {
                manifold.GetContact(i, out var offset, out var normal, out var depth, out _);
                Contacts.Add(new BufferedContact
                {
                    CollidableA = pair.A,
                    CollidableB = pair.B,
                    Offset = offset,
                    Normal = normal,
                    Depth = depth
                });
            }
        }
    }

    internal struct BepuPoseIntegratorCallbacks : IPoseIntegratorCallbacks
    {
        private Vector3Wide _gravityWide;

        public AngularIntegrationMode AngularIntegrationMode => AngularIntegrationMode.Nonconserving;

        public bool AllowSubstepsForUnconstrainedBodies => true;

        public bool IntegrateVelocityForKinematics => true;

        public BepuPoseIntegratorCallbacks(System.Numerics.Vector3 gravity) : this()
        {
            Vector3Wide.Broadcast(gravity, out _gravityWide);
        }

        public void Initialize(Simulation simulation) { }

        public void PrepareForIntegration(float dt) { }

        public void IntegrateVelocity(
            Vector<int> bodyIndices, Vector3Wide position, QuaternionWide orientation, BodyInertiaWide localInertia,
            Vector<int> integrationMask, int workerIndex, Vector<float> dt, ref BodyVelocityWide velocity)
        {
            velocity.Linear += _gravityWide * dt;
        }
    }

    internal struct RayHitHandler : IRayHitHandler
    {
        public bool Hit;
        public float T;
        public System.Numerics.Vector3 Normal;
        public CollidableReference HitCollidable;

        public bool AllowTest(CollidableReference collidable)
        {
            return true;
        }

        public bool AllowHit(CollidableReference collidable, ref float t)
        {
            return true;
        }

        public void OnRayHit(in RayData ray, ref float t, in System.Numerics.Vector3 normal, CollidableReference collidable)
        {
            Hit = true;
            T = t;
            Normal = normal;
            HitCollidable = collidable;
        }
    }

    internal struct MultiHitHandler : IRayHitHandler
    {
        public List<ContactResult> Results;
        public int MaxHits;
        public System.Numerics.Vector3 Origin;
        public System.Numerics.Vector3 Direction;
        public RayFilterFlags Filter;

        public bool AllowTest(CollidableReference collidable)
        {
            switch (collidable.Mobility)
            {
                case CollidableMobility.Static:
                    return (Filter & RayFilterFlags.land) != 0;
                case CollidableMobility.Dynamic:
                    return (Filter & (RayFilterFlags.agent | RayFilterFlags.physical)) != 0;
                case CollidableMobility.Kinematic:
                    return (Filter & RayFilterFlags.nonphysical) != 0;
                default:
                    return true;
            }
        }

        public bool AllowHit(CollidableReference collidable, ref float t)
        {
            // Reject every hit in AllowHit so Bepu continues returning
            // all hits through OnRayHit, where MultiHitHandler collects
            // up to MaxHits results.
            return false;
        }

        public void OnRayHit(in RayData ray, ref float t, in System.Numerics.Vector3 normal, CollidableReference collidable)
        {
            if (Results.Count >= MaxHits) return;

            var hitPoint = Origin + Direction * t;
            Results.Add(new ContactResult
            {
                Pos = new Vector3(hitPoint.X, hitPoint.Y, hitPoint.Z),
                Normal = new Vector3(normal.X, normal.Y, normal.Z),
                Depth = 0,
                ConsumerID = 0
            });
        }
    }

    #endregion
}
