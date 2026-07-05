# BepuPhysics v2 Module for OpenSimulator

A replacement physics engine for OpenSimulator using [BepuPhysics v2](https://github.com/bepu/bepuphysics2), a fast and deterministic physics simulation library. Licensed under Apache 2.0.

## Architecture

### Core Types

```mermaid
classDiagram
    class PhysicsScene {
        <<abstract>>
        +AddAvatar()
        +AddPrimShape()
        +RemoveAvatar()
        +RemovePrim()
        +Simulate()
        +SetTerrain()
        +SetWaterLevel()
        +DeleteTerrain()
        +RaycastWorld()
        +SupportsRayCast()
    }

    class BepuScene {
        -BufferPool _bufferPool
        -Simulation _simulation
        -ThreadDispatcher _threadDispatcher
        -Dictionary~uint, BepuActor~ _actors
        -Dictionary~BodyHandle, BepuActor~ _bodyHandleToActor
        -ConcurrentQueue~BodyAction~ _bodyActions
        -ConcurrentQueue~Action~ _scheduledUpdates
        -Dictionary~uint, Dictionary~uint, ContactPoint~~ _activeCollisions
        -List~BufferedContact~ _contactBuffer
        -Dictionary~uint, PidState~ _pidStates
        -StaticHandle _terrainStaticHandle
        -float[] _terrainHeightMap
        +Simulate(float timeStep)
        +RaycastWorld() multiple overloads
        +GetTerrainHeightAtXYZ()
        +GetStats()
    }

    class PhysicsActor {
        <<abstract>>
        +Vector3 Position
        +Vector3 Velocity
        +Quaternion Orientation
        +float Mass
        +bool IsPhysical
        +bool Kinematic
        +float Buoyancy
        +bool FloatOnWater
        +AddForce()
        +LockAngularMotion()
    }

    class BepuActor {
        -BepuScene _scene
        -BodyHandle _bodyHandle
        -TypedIndex _shapeIndex
        -bool _hasBody
        -Vector3 _pidTarget
        -bool _pidActive
        -int _angularLockAxes
        +BodyHandle BodyHandle
        +ApplySimulationResult()
        +GetMaterialProperties()
        +FireCollisionEvents()
        +SetBody()
        +RemoveBody()
    }

    class BepuNarrowPhaseCallbacks {
        <<struct>> INarrowPhaseCallbacks
        +List~BufferedContact~ Contacts
        +Func~CollidableReference, CollidableReference, PairMaterialProperties~ MaterialResolver
        +ConfigureContactManifold()
    }

    class BepuPoseIntegratorCallbacks {
        <<struct>> IPoseIntegratorCallbacks
        +Vector3 Gravity
        +IntegrateVelocity()
    }

    class RayHitHandler {
        <<struct>> IRayHitHandler
        +bool Hit
        +float T
        +OnRayHit()
    }

    class MultiHitHandler {
        <<struct>> IRayHitHandler
        +List~ContactResult~ Results
        +int MaxHits
        +RayFilterFlags Filter
        +OnRayHit()
    }

    class BepuUtil {
        <<static>>
        +ToSN(Vector3) System.Numerics.Vector3
        +ToOM(System.Numerics.Vector3) Vector3
        +ToRigidPose()
    }

    BepuScene --|> PhysicsScene : extends
    BepuActor --|> PhysicsActor : extends
    BepuScene *-- BepuActor : manages
    BepuScene *-- BepuNarrowPhaseCallbacks : owns
    BepuScene *-- BepuPoseIntegratorCallbacks : owns
    BepuScene *-- RayHitHandler : creates
    BepuScene *-- MultiHitHandler : creates
    BepuScene ..> BepuUtil : uses
    BepuActor --> BepuScene : references
    BepuActor ..> BepuUtil : uses
```

### Simulation Loop

```mermaid
flowchart TD
    A["OpenSim Simulate() called"] --> B["ProcessScheduledUpdates()"]
    B --> B1["Apply queued forces & torques"]
    B1 --> B2["Apply position/orientation changes"]
    B2 --> B3["Apply physics flag toggles"]
    B3 --> B4["Apply angular lock axis zeroing"]

    B4 --> C["_simulation.Timestep()"]
    C --> C1["Pose Integrator Callbacks<br/>(gravity integration)"]
    C1 --> C2["Narrow Phase Callbacks<br/>(contact generation + material resolution)"]
    C2 --> C3["Solver<br/>(constraints + contacts)"]
    C3 --> C4["Body integration"]

    C4 --> D["PushSimulationResults()"]
    D --> D1["Read body poses & velocities"]
    D1 --> D2["Update BepuActor cached state"]
    D2 --> D3["Fire position/orientation events"]

    D3 --> E["ProcessCollisions()"]
    E --> E1["Group BufferedContact by pair"]
    E1 --> E2["Detect start/end/persisted collisions"]
    E2 --> E3["Bidirectional dispatch to both actors"]
    E3 --> E4["Fire CollisionEventUpdate with throttle"]

    E4 --> F["ProcessPidControls()"]
    F --> F1["For each active PID actor"]
    F1 --> F2["Compute spring error: target - position"]
    F2 --> F3["Apply corrective force via ApplyDescription"]

    F3 --> G["ProcessBuoyancy()"]
    G --> G1["For each FloatOnWater actor"]
    G1 --> G2["Compute submerged depth"]
    G2 --> G3["Apply upward buoyancy force"]

    G --> H["Return simulated frames"]
```

### Collision Pipeline

```mermaid
flowchart LR
    subgraph NarrowPhase [Narrow Phase - Worker Threads]
        NP["BepuNarrowPhaseCallbacks.ConfigureContactManifold()"]
        NP --> MR["MaterialResolver( pairA, pairB )<br/>returns PairMaterialProperties"]
        MR --> RC["ReadContactsFromConvex()"]
        RC --> CB["Append BufferedContact to _contactBuffer"]
    end

    subgraph MainThread [Main Thread - After Timestep]
        PC["ProcessCollisions()"]
        PC --> GROUP["Group contacts by CollidablePair"]
        GROUP --> MAP["Map BodyHandle/StaticReference → BepuActor"]
        MAP --> DETECT["Detect start / end / persisted"]
        DETECT --> FIRE1["BepuActor.FireCollisionEvents()"]
        DETECT --> FIRE2["BepuActor.FireCollisionEvents()"]
        FIRE1 --> T1["Throttle check (100ms)"]
        FIRE2 --> T2["Throttle check (100ms)"]
        T1 --> SEND["SendCollisionEventUpdate()"]
        T2 --> SEND
    end

    CB --> PC
```

### Raycasting

```mermaid
flowchart TD
    RAY["RaycastWorld() called"] --> DECIDE{"Which overload?"}

    DECIDE -->|"Single-hit callback"| SH["Create RayHitHandler"]
    SH --> SHR["simulation.RayCast(ref RayHitHandler)"]
    SHR --> SHON["OnRayHit called for each hit<br/>Sets maximumT = t to narrow<br/>search to closest hit"]
    SHON --> SHRES["Return closest hit via callback"]

    DECIDE -->|"Multi-hit with count"| MH["Create MultiHitHandler"]
    MH --> MHR["simulation.RayCast(ref MultiHitHandler)"]
    MHR --> MHON["OnRayHit called for each hit<br/>Does NOT modify maximumT<br/>so traversal continues"]
    MHON --> MHRES["Collect up to MaxHits<br/>Return List~ContactResult~"]

    DECIDE -->|"Multi-hit with filter"| FH["Create MultiHitHandler(Filter)"]
    FH --> FHR["simulation.RayCast(ref MultiHitHandler)"]
    FHR --> FHON["AllowTest filters by CollidableMobility<br/>Static=land, Dynamic=agent/physical, Kinematic=nonphysical"]
    FHON --> FHRES["Return filtered List~ContactResult~"]
```

## Phases

| Phase | Status | Description |
|-------|--------|-------------|
| 1 - Exploration | ✅ Done | Analyzed BulletSim patterns, studied Bepu v2 API |
| 2 - Core Skeleton | ✅ Done | Simulation lifecycle, terrain, basic body management |
| 3 - Abstract Contract | ✅ Done | All 48 PhysicsActor members, all PhysicsScene overrides |
| 4 - Collision Pipeline | ✅ Done | Narrow phase callbacks, contact buffering, collision dispatch |
| 5 - Per-Actor Control | ✅ Done | Materials, PID/MoveTo, buoyancy, angular lock, gravity |
| 6 - Terrain Mesh | 🔲 Pending | Build real Mesh from heightmap instead of flat box |
| 7 - Buoyancy/Vehicles | 🔲 Pending | Water plane collision, vehicle constraints |

## Files

| File | Lines | Role |
|------|-------|------|
| `BepuScene.cs` | 1261 | Main scene: simulation, terrain, raycasts, collisions, PID, buoyancy |
| `BepuActor.cs` | 644 | Actor: all PhysicsActor members, collision events, material/PID helpers |
| `BepuUtil.cs` | 66 | Type conversion between OpenMetaverse and System.Numerics |
| `TRACKING.md` | 68 | Project tracker with progress and dependencies |

## Build

```bash
# Requires .NET 9 SDK and BepuPhysics NuGet
./runprebuild.sh
dotnet build -c Release OpenSim.sln
```
