# BepuPhysics v2 Module — Project Status

## Goal
Replace BulletSim/ubOde with BepuPhysics v2 (Apache 2.0) as the physics engine for OpenSimulator.

## Progress

### ✅ Phase 1 — Exploration
- Analyzed BulletSim (BSScene, BSPhysObject) and ubOde implementations for reference patterns
- Studied BepuPhysics v2 API: `INarrowPhaseCallbacks`, `IPoseIntegratorCallbacks`, `Simulation`, body management

### ✅ Phase 2 — Core Skeleton
- `BepuScene.cs` — Simulation lifecycle, terrain, basic body management
- `BepuActor.cs` — PhysicsActor implementation skeleton
- `BepuUtil.cs` — Vector3/Quaternion conversion between System.Numerics and OpenMetaverse
- Fixed all API signatures after source analysis of actual Bepu v2 interfaces

### ✅ Phase 3 — Abstract Contract Mapping
- BepuActor implements all 48 abstract members of PhysicsActor
- BepuScene implements all abstract methods + useful overrides
- `RaycastWorld` overloads (single-hit, multi-hit, filtered)
- `FloatOnWater` support

### ✅ Phase 4 — Collision Pipeline
- `BepuNarrowPhaseCallbacks` with contact buffering via `Unsafe.As` + `GetContact()`
- `ProcessCollisions()` — full pipeline: group contacts, detect start/end/persisted, bidirectional dispatch
- `GetTopColliders()` — top 25 by collision score
- Collision subscription with throttle (100ms)
- Terrain collision filtering

### 🔲 Phase 5 — Body Management
- Complete remaining BepuActor methods
- Vehicle type support, AddForce, Locked, etc.

### 🔲 Phase 6 — Terrain Mesh
- Build real Mesh from heightmap instead of flat box
- Triangle content for accurate terrain collider

### 🔲 Phase 7 — Buoyancy / Vehicles
- Water plane collision for FloatOnWater
- Vehicle physics (ships/vehicles with Bepu constraints)

## Dependencies
- BepuPhysics v2 NuGet: `dotnet add package BepuPhysics`
- BepuUtilities v2 NuGet: `dotnet add package BepuUtilities`
- .NET 9 SDK

## How to Build
```bash
# After installing .NET 9 SDK and BepuPhysics NuGet:
./runprebuild.sh
dotnet build -c Release OpenSim.sln
```

## Files
| File | Lines | Role |
|------|-------|------|
| `BepuScene.cs` | 1006 | Main scene: simulation, terrain, raycasts, collisions |
| `BepuActor.cs` | 596 | Actor: all PhysicsActor members, collision events |
| `BepuUtil.cs` | 65 | Type conversion utilities |
