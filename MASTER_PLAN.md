# OpenSim Master Improvement Plan

This document outlines a roadmap to modernize OpenSimulator, bringing it to parity with (or advancing past) Second Life, introducing non-Euclidean world types, and significantly improving overall performance.

## 1. Performance Optimization Findings & Architectural Overhaul

Based on the static analysis of the codebase, OpenSim suffers from several architectural bottlenecks that severely limit scaling:

### A. Asset Pipeline & Database Concurrency
- **The Problem:** The `FlotsamAssetCache` heavily relies on aggressive locking (`weakAssetReferencesLock`, `timerLock`) on its internal dictionaries. Asset fetch routines blocking synchronously read from files via `File.Open` and deserialize using the extremely slow `BinaryFormatter`. Database queries (e.g., `MySQLXAssetData.GetAsset`) use blocking synchronous connections (`MySqlCommand.ExecuteReader`).
- **The Solution:**
  1. Refactor `FlotsamAssetCache` to use `ConcurrentDictionary` to eliminate broad lock contention.
  2. Deprecate `BinaryFormatter` in favor of zero-copy binary serialization or fast serializers like MessagePack.
  3. Overhaul the `IAssetService` and `IAssetDataPlugin` interfaces to return `Task<AssetBase>` (async/await paradigm). This will prevent the thread pool from being exhausted during high-concurrency asset fetching.

### B. Scene Update Loop
- **The Problem:** The core simulation loop in `Scene.Update()` is largely single-threaded. It processes physics, terrain, events, network sends, and scripts sequentially.
- **The Solution:**
  1. Decouple the monolithic `Scene.Update` loop.
  2. Move network serialization out of the main thread pool to a dedicated highly concurrent Task Queue.
  3. Introduce parallel processing for independent scene entities where state does not collide.

## 2. Second Life Parity Features

To achieve parity with modern Second Life, the following features must be prioritized in the region server:

*   **PBR Materials (Physically Based Rendering):**
    *   Update the `PrimitiveBaseShape` and asset parsing to support GLTF material overrides.
    *   Expose new LSL functions to script GLTF parameters.
*   **Bakes on Mesh (BoM):**
    *   Modify the Avatar appearance modules to process `BAKES_ON_MESH` flags and relay baked texture UUIDs directly to mesh attachments without requiring alpha-hiding tricks.
*   **Animesh:**
    *   Update physics engines (BulletS/ubOde) to ignore collision properties for rigged skeleton bones on Animesh objects.
    *   Pass the appropriate skeleton/animation packets to the viewer when an object is flagged as Animesh.
*   **Environment Enhancement Project (EEP):**
    *   Replace the legacy Windlight modules with the newer EEP asset structures.
    *   Support region-level, parcel-level, and user-level environment settings.

## 3. Non-Euclidean Worlds (Sphere/Ring Worlds)

Creating non-Euclidean geometries (like a Ringworld or spherical planet) presents a massive challenge, primarily because SL viewers assume a flat, Cartesian coordinate system (where Z is always up, gravity is -Z).

### Server-Side Requirements (Physics & Logic)
*   **Directional/Variable Gravity:**
    *   The `PhysicsScene` interface must be modified so that gravity is no longer a global property `Vector3(0, 0, -9.8f)`.
    *   Objects must calculate their local "down" vector relative to the center of the sphere or the surface of the ring.
*   **Coordinate Wrapping/Space Partitioning:**
    *   Instead of hard grid boundaries (X,Y from 0 to 256), a mathematical wrapper must seamlessly teleport physics objects and avatars across the geometry loop (e.g., leaving the east side of the sphere instantly enters the west).

### Viewer-Side Requirements (The Real Challenge)
*   Standard viewers *cannot* render a curved horizon natively.
*   **Illusion Approach (Server-Side Magic):** We can intercept ObjectUpdate packets before they reach the viewer, applying a spherical transform matrix to all object positions relative to the Avatar's current position. This creates an optical illusion of curvature without the viewer knowing.
*   **Custom Viewer Approach (True Non-Euclidean):** Develop a custom client that understands Spherical or Cylindrical coordinate systems natively.

---
*Drafted during deep architectural review session.*
