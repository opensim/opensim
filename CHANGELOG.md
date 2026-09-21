# Changelog

## Unreleased
### Fixed
- Fixed a memory leak in the BulletSim physics module (`BSShapes.cs`). Temporary native shapes (`BSShapeMesh`, `BSShapeHull`, `BSShapeConvexHull`, `BSShapeGImpact`) are now correctly freed when their reference count drops to 0 using `physicsScene.PE.DeleteCollisionShape`.
  - Updated BulletSim collision shape deletion (`BSShapes.cs`) to schedule `PE.DeleteCollisionShape` at post-taint time via `physicsScene.PostTaintObject()` to prevent potential race conditions during linkset disassembly.
