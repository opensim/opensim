## Scene Scheduler Modernization — Sector 1 Analysis

### Current Loop Snapshot
- `Scene.Heartbeat()` runs a single `while` loop that increments `Frame`, sequentially executing terrain maintenance, physics prep/sim, avatar updates, event queues, script engine ticks, backup checks, and outgoing packet assembly.
- `SceneGraph` keeps region entities inside dictionary-backed collections guarded by `ReaderWriterLockSlim` instances; every frame code repeatedly takes locks to iterate presences, parts, and update lists.
- Physics (`PhysicsScene.Simulate`) is invoked synchronously on the sim thread even when the Bullet back end is configured to maintain its own worker thread, forcing the scene loop to remain serialized.
- Deferred work (asset persistence, coarse location updates, temp object cleanup) is farmed out through `WorkManager.RunInThreadPool` but results are marshalled back into the main thread, so long jobs still cause frame stalls.

### Constraints Blocking Parallelism
1. **Global frame lock** — The sim thread owns mutable state for presences, scripts, and physics metadata; there is no frame fencing or immutable snapshots for readers, so any attempt to run physics or AI on a worker risks races.
2. **Coarse-grained collections** — Entity dictionaries store heterogeneous objects (prims, avatars, attachments) and require full locks for enumeration. This prevents read-only traversal from scaling and makes granular job partitioning difficult.
3. **Serial dependency chain** — The loop assumes strict ordering (terrain → physics → avatar move → scripts → backup → networking). Many steps only need the previous frame’s outputs, but there is no buffering/latching strategy to exploit that.
4. **Side-effect-heavy modules** — Subsystems invoked from the loop frequently call back into `Scene` setters/getters, so even code that could run concurrently (e.g. stats aggregation) must stay on the sim thread to avoid cross-thread access.
5. **Update queues coupled to networking thread** — `LLClientView` consumes `SceneObjectGroup` updates directly as they are scheduled, meaning serialization pressure feeds back into the core loop.

### Modernization Direction
We can evolve toward a job-based scheduler that breaks the monolithic loop into discrete tasks:
- Capture a per-frame world snapshot (`FrameContext`) with immutable transforms/physics proxies, then fan out worker jobs (physics step, AI logic, script VM, terrain streaming). Use double-buffering to hand results back to the main thread.
- Replace dictionary iteration with a spatial index and struct-of-arrays storage for prim transforms; expose iterators that support chunking work into task batches.
- Introduce a scheduler service (e.g. `ISceneScheduler`) responsible for dependency graphs and thread pools. `Scene.Update()` becomes orchestration: submit jobs, wait on frame fence, then apply mutations.
- Isolate networking by queuing serialized deltas produced by jobs; a dedicated I/O thread flushes packets, decoupling frame time from bandwidth spikes.

### Incremental Steps
1. **Instrumentation & metrics** — Add optional per-phase timers and counters to characterize hotspots in real deployments.
2. **Frame context prototype** — Implement a lightweight `SceneFrameState` snapshot (read-only structs for avatars/objects) and convert one subsystem (e.g. stats collection) to consume it off-thread.
3. **Scheduler scaffolding** — Introduce `SceneScheduler` with a configurable `TaskScheduler/ThreadPool`, initial job types, and a frame fence API; start by migrating existing thread-pool work (coarse locations, temp cleanup) into structured jobs.
4. **Physics decoupling** — Allow Bullet/ubOde to run autonomous ticks and publish results via the frame context interface rather than blocking the core loop.
5. **Lock refactor** — Replace `ReaderWriterLockSlim` usages with finer-grained lock-free collections or epoch-based reclamation to ensure snapshotting remains cheap.

### Proposed Delivery Plan
| Phase | Scope | Key Deliverables | Success Criteria |
|-------|-------|------------------|------------------|
| **0. Baseline** (week 1) | Instrument existing loop; capture frame metrics in a dedicated profiling build. | `SceneFrameDiagnostics` toggled via config; dashboard log output. | Real regions produce per-phase timing without >1% frame overhead. |
| **1. Snapshot MVP** (weeks 2-3) | Build `SceneFrameState` structs and migration of stats + coarse-location jobs. | Immutable frame data container, unit tests, documentation. | Stats + coarse locations run on worker threads; no race regressions in soak test. |
| **2. Scheduler Core** (weeks 4-6) | Introduce `SceneScheduler` orchestrating job graph, migrate terrain taints and backups. | Scheduler service, dependency DSL, configuration knobs. | Scene loop time drops ≥20% in heavy region benchmark; jobs show parallel execution in traces. |
| **3. Physics Integration** (weeks 7-10) | Run Bullet/ubOde stepping asynchronously, apply results through frame fence. | Physics job adapters, deterministic handoff, rollback handling. | Physics thread no longer blocks scene loop; avatar collision correctness validated. |
| **4. Networking decoupling** (weeks 11-12) | Separate object update serialization into dedicated pipeline. | Update delta queue, I/O worker, stress tests with packet flood. | Packet send jitter reduced; frame misses under network load eliminated. |
| **5. Lock & storage upgrade** (weeks 13-16) | Replace dictionary locks with SoA entity storage + spatial index. | New entity container, migration tooling, compatibility shim. | Parallel job throughput scales ~linearly up to 4 worker cores. |

Each phase keeps the simulator runnable; checkpoint after every phase with feature flag gating (`SceneSchedulerMode = Legacy|Hybrid|JobGraph`).

Document updated: 2025-11-05.
