# OpenSim Physics Engine Implementation Guide
## Complete Technical Documentation for Physics Modernization

**Document Version**: 1.0  
**Last Updated**: December 2024  
**Focus**: Physics Engine Transformation and Optimization  

---

## Table of Contents

1. [Current Physics Architecture Analysis](#current-physics-architecture-analysis)
2. [Bullet Physics Optimization](#bullet-physics-optimization)
3. [Jolt Physics Integration](#jolt-physics-integration)
4. [Advanced Physics Features](#advanced-physics-features)
5. [Performance Optimization](#performance-optimization)
6. [Testing and Validation](#testing-and-validation)
7. [Migration Strategy](#migration-strategy)

---

## Current Physics Architecture Analysis

### Existing Physics Engines in OpenSim

#### 1. BulletS Physics Engine (Primary)

**File Locations**:
- Core: `/OpenSim/Region/PhysicsModules/BulletS/`
- Configuration: `/bin/config-include/PhysicsEnhancements.ini.example`

**Current Implementation Analysis**:

```csharp
// Current BSScene implementation
public sealed class BSScene : PhysicsScene
{
    // Recent improvements implemented:
    private PhysicsProfiler m_physicsProfiler;  // ✅ Added performance monitoring
    private bool m_useImprovedCollisionMargins; // ⚠️ Assigned but not used
    
    public override float Simulate(float timeStep)
    {
        using (m_physicsProfiler?.StartTiming("PhysicsSimulation"))
        {
            // Core simulation loop
            TaintedObject.ProcessTaints();
            
            // ✅ Enhanced error handling added
            try
            {
                m_simTime = BulletSimAPI.PhysicsStep(World.ptr, timeStep, 
                                                   PhysicsMaxSubSteps, PhysicsFixedTimeStep);
            }
            catch (Exception e)
            {
                DetailLog("{0},BSScene.Simulate,exception,msg={1}", DetailLogZero, e.Message);
                // Use fallback time step
                m_simTime = timeStep;
            }
            
            return m_simTime;
        }
    }
}
```

**Critical Issues Identified** (from BulletSimTODO.txt):

1. **Hull Accuracy Problems**:
```csharp
// Current issue: Hulls are not as detailed as meshes
// Problem: Hulled vehicles have different interior shapes than mesh vehicles
// Impact: Vehicle physics behave differently based on collision shape type

// TODO: Implement adaptive hull generation
public class ImprovedHullGenerator
{
    public BulletShape CreateDetailedHull(PrimitiveBaseShape shape, Vector3 size)
    {
        // Current: Uses basic convex hull
        // Needed: Multi-hull decomposition for complex shapes
        
        var meshData = ExtractMeshData(shape, size);
        var hullGroups = DecomposeIntoConvexHulls(meshData);
        
        return CreateCompoundHull(hullGroups);
    }
}
```

2. **Avatar Contact Issues**:
```csharp
// Current issue: Avatar legs fold when standing
// Problem: No contact debouncing system
// Solution needed: Contact filtering and debouncing

public class AvatarContactManager
{
    private Dictionary<uint, ContactDebouncer> m_avatarContacts = new();
    
    public bool ShouldProcessContact(uint avatarId, ContactPoint contact)
    {
        if (!m_avatarContacts.TryGetValue(avatarId, out var debouncer))
        {
            debouncer = new ContactDebouncer();
            m_avatarContacts[avatarId] = debouncer;
        }
        
        return debouncer.ShouldProcess(contact, Util.GetTimeStamp());
    }
}
```

3. **Performance with Large Crowds**:
```csharp
// Current issue: Performance degradation with 1000+ avatars
// Problem: No spatial optimization for avatar-avatar interactions
// Solution needed: Spatial partitioning and LOD system

public class AvatarSpatialManager
{
    private OctreeNode<BSCharacter> m_avatarOctree;
    private const int MAX_AVATARS_PER_NODE = 20;
    
    public void UpdateAvatarSpatialData(List<BSCharacter> avatars)
    {
        m_avatarOctree.Clear();
        
        foreach (var avatar in avatars)
        {
            m_avatarOctree.Insert(avatar, avatar.RawPosition);
        }
        
        // Optimize collision checking using spatial queries
        OptimizeAvatarCollisions();
    }
}
```

#### 2. Vehicle Physics Issues and Solutions

**Current Implementation Problems**:
```csharp
// In BSDynamics.cs - Current vehicle implementation
public class BSDynamics
{
    // ✅ FIXED: Buoyancy calculation issue
    private void ComputeLinearVelocity(float pTimestep)
    {
        // Previous issue: Buoyancy could cause runaway motion
        // Fixed with proper clamping:
        VDetailLog("{0}, MoveLinear,buoyancy,vel={1},buoy={2}", 
                  Prim.LocalID, m_lastLinearVelocityVector, buoyancy);
        
        // ✅ Proper clamping implemented
        buoyancy = Math.Max(-1.0f, Math.Min(1.0f, buoyancy));
        
        // ✅ Enhanced error handling
        if (float.IsNaN(buoyancy) || float.IsInfinity(buoyancy))
        {
            buoyancy = 0.0f;
            VDetailLog("{0}, MoveLinear,buoyancy,invalid,reset", Prim.LocalID);
        }
    }
    
    // ✅ FIXED: Quaternion normalization issue
    public Quaternion VehicleFrameOrientation
    {
        get { return m_vehicleFrameOrientation; }
        set 
        { 
            m_vehicleFrameOrientation = value;
            // ✅ Auto-normalization added to prevent drift
            m_vehicleFrameOrientation.Normalize();
        }
    }
}
```

**Remaining Vehicle Physics Improvements Needed**:
```csharp
// Advanced vehicle suspension system
public class AdvancedVehicleSuspension
{
    public struct SuspensionSettings
    {
        public float SpringStiffness;      // N/m
        public float DamperStrength;       // Ns/m
        public float MaxCompressionRatio;  // 0.0 to 1.0
        public float MinCompressionRatio;  // 0.0 to 1.0
        public Vector3 SuspensionDirection; // Usually -Y
        public float WheelRadius;
        public float RestLength;
    }
    
    public void UpdateSuspension(float deltaTime, SuspensionSettings settings)
    {
        // Ray cast to find ground contact
        var rayStart = GetWheelPosition();
        var rayEnd = rayStart + settings.SuspensionDirection * 
                    (settings.RestLength + settings.WheelRadius);
        
        if (PhysicsRaycast(rayStart, rayEnd, out var hit))
        {
            var compressionDistance = settings.RestLength - hit.distance + settings.WheelRadius;
            var compressionRatio = compressionDistance / settings.RestLength;
            
            // Clamp compression
            compressionRatio = Math.Max(settings.MinCompressionRatio, 
                                      Math.Min(settings.MaxCompressionRatio, compressionRatio));
            
            // Calculate spring force
            var springForce = settings.SpringStiffness * (compressionRatio - settings.MinCompressionRatio);
            
            // Calculate damper force
            var velocity = GetWheelVelocity();
            var relativeVelocity = Vector3.Dot(velocity, settings.SuspensionDirection);
            var damperForce = settings.DamperStrength * relativeVelocity;
            
            // Apply combined force
            var totalForce = (springForce - damperForce) * (-settings.SuspensionDirection);
            ApplyForceAtPosition(totalForce, GetWheelPosition());
        }
    }
}

// Tire friction model
public class TireFrictionModel
{
    public struct TireProperties
    {
        public float LongitudinalStiffness;  // Slip ratio stiffness
        public float LateralStiffness;       // Slip angle stiffness
        public float PeakFriction;           // Maximum friction coefficient
        public float SlidingFriction;        // Sliding friction coefficient
        public AnimationCurve FrictionCurve; // Friction vs slip curve
    }
    
    public Vector3 CalculateTireForce(TireProperties tire, float slipRatio, float slipAngle, float normalForce)
    {
        // Longitudinal force (acceleration/braking)
        var longitudinalForce = tire.LongitudinalStiffness * slipRatio;
        longitudinalForce = Math.Sign(longitudinalForce) * 
                           Math.Min(Math.Abs(longitudinalForce), tire.PeakFriction * normalForce);
        
        // Lateral force (steering)
        var lateralForce = tire.LateralStiffness * slipAngle;
        lateralForce = Math.Sign(lateralForce) * 
                      Math.Min(Math.Abs(lateralForce), tire.PeakFriction * normalForce);
        
        // Combine forces with friction circle
        var combinedForce = Math.Sqrt(longitudinalForce * longitudinalForce + lateralForce * lateralForce);
        if (combinedForce > tire.PeakFriction * normalForce)
        {
            var scale = (tire.PeakFriction * normalForce) / combinedForce;
            longitudinalForce *= scale;
            lateralForce *= scale;
        }
        
        return new Vector3(longitudinalForce, 0, lateralForce);
    }
}
```

---

## Bullet Physics Optimization

### Phase 1: Critical Bug Fixes (Immediate - Month 1)

#### 1.1 Collision Margin Optimization

**Problem**: Current collision margins are not optimized for object size, causing inaccurate collisions.

```csharp
// Enhanced collision margin management
public class CollisionMarginManager
{
    private const float DEFAULT_MARGIN = 0.04f;
    private const float MIN_MARGIN = 0.001f;
    private const float MAX_MARGIN = 0.2f;
    private const float MARGIN_SCALE_FACTOR = 0.02f; // 2% of smallest dimension
    
    public static float CalculateOptimalMargin(Vector3 objectSize)
    {
        var minDimension = Math.Min(objectSize.X, Math.Min(objectSize.Y, objectSize.Z));
        var calculatedMargin = minDimension * MARGIN_SCALE_FACTOR;
        
        return Math.Max(MIN_MARGIN, Math.Min(MAX_MARGIN, calculatedMargin));
    }
    
    public static void UpdateCollisionMargins(BSScene scene)
    {
        foreach (var prim in scene.PhysObjects.Values)
        {
            if (prim is BSPrim bsPrim)
            {
                var optimalMargin = CalculateOptimalMargin(bsPrim.Size);
                
                // Update the collision shape margin
                if (bsPrim.PhysShape != null && bsPrim.PhysShape.HasPhysicalShape)
                {
                    BulletSimAPI.SetMargin(bsPrim.PhysShape.ptr, optimalMargin);
                    
                    DetailLog("{0},CollisionMarginManager.Update,id={1},size={2},margin={3}",
                             BSScene.DetailLogZero, bsPrim.LocalID, bsPrim.Size, optimalMargin);
                }
            }
        }
    }
}

// Integration into BSScene
public sealed class BSScene : PhysicsScene
{
    private float m_lastMarginUpdate = 0;
    private const float MARGIN_UPDATE_INTERVAL = 10.0f; // 10 seconds
    
    public override float Simulate(float timeStep)
    {
        // ... existing simulation code ...
        
        // Periodically update collision margins
        m_lastMarginUpdate += timeStep;
        if (m_lastMarginUpdate >= MARGIN_UPDATE_INTERVAL)
        {
            CollisionMarginManager.UpdateCollisionMargins(this);
            m_lastMarginUpdate = 0;
        }
        
        return m_simTime;
    }
}
```

#### 1.2 Enhanced Spatial Partitioning

**Problem**: Poor performance with large numbers of objects due to broad-phase collision detection.

```csharp
// Advanced spatial partitioning system
public class AdvancedSpatialPartitioning
{
    private readonly BulletHashedOverlappingPairCache m_pairCache;
    private readonly List<SpatialRegion> m_regions = new();
    private bool m_useAdaptiveRegions = true;
    
    public class SpatialRegion
    {
        public BoundingBox Bounds { get; set; }
        public List<BSPhysObject> Objects { get; set; } = new();
        public int MaxObjects { get; set; } = 50;
        public bool RequiresSubdivision => Objects.Count > MaxObjects;
    }
    
    public void InitializeAdvancedBroadphase(BSScene scene)
    {
        // Use Multi-SAP (Multiple Sweep and Prune) for better performance
        var broadphaseInterface = BulletSimAPI.CreateMultiSapBroadphase();
        
        // Configure axis sweep settings
        var worldMin = new Vector3(-10000, -10000, -10000);
        var worldMax = new Vector3(10000, 10000, 10000);
        
        BulletSimAPI.SetBroadphaseWorldLimits(broadphaseInterface, worldMin, worldMax);
        
        // Create adaptive regions based on object density
        if (m_useAdaptiveRegions)
        {
            CreateAdaptiveRegions(scene);
        }
    }
    
    private void CreateAdaptiveRegions(BSScene scene)
    {
        var regionBounds = scene.TerrainManager.DefaultRegionSize;
        var regionSize = 64.0f; // Start with 64m regions
        
        for (float x = 0; x < regionBounds.X; x += regionSize)
        {
            for (float y = 0; y < regionBounds.Y; y += regionSize)
            {
                var region = new SpatialRegion
                {
                    Bounds = new BoundingBox(
                        new Vector3(x, y, 0),
                        new Vector3(x + regionSize, y + regionSize, 256)
                    )
                };
                
                m_regions.Add(region);
                
                // Register region with Bullet
                BulletSimAPI.AddBroadphaseRegion(scene.World.ptr, region.Bounds);
            }
        }
    }
    
    public void UpdateSpatialPartitioning(BSScene scene)
    {
        // Clear existing object assignments
        foreach (var region in m_regions)
        {
            region.Objects.Clear();
        }
        
        // Assign objects to regions
        foreach (var obj in scene.PhysObjects.Values)
        {
            var objectBounds = obj.PhysObjectName.Contains("Terrain") ? 
                              GetTerrainBounds(obj) : GetObjectBounds(obj);
            
            foreach (var region in m_regions)
            {
                if (region.Bounds.Intersects(objectBounds))
                {
                    region.Objects.Add(obj);
                }
            }
        }
        
        // Handle regions that need subdivision
        foreach (var region in m_regions.Where(r => r.RequiresSubdivision).ToList())
        {
            SubdivideRegion(region, scene);
        }
    }
    
    private void SubdivideRegion(SpatialRegion region, BSScene scene)
    {
        var bounds = region.Bounds;
        var center = bounds.Center;
        
        // Create 4 sub-regions (quadtree subdivision)
        var subRegions = new[]
        {
            new SpatialRegion 
            { 
                Bounds = new BoundingBox(bounds.Min, center),
                MaxObjects = region.MaxObjects / 2
            },
            new SpatialRegion 
            { 
                Bounds = new BoundingBox(new Vector3(center.X, bounds.Min.Y, bounds.Min.Z), 
                                       new Vector3(bounds.Max.X, center.Y, bounds.Max.Z)),
                MaxObjects = region.MaxObjects / 2
            },
            new SpatialRegion 
            { 
                Bounds = new BoundingBox(new Vector3(bounds.Min.X, center.Y, bounds.Min.Z), 
                                       new Vector3(center.X, bounds.Max.Y, bounds.Max.Z)),
                MaxObjects = region.MaxObjects / 2
            },
            new SpatialRegion 
            { 
                Bounds = new BoundingBox(center, bounds.Max),
                MaxObjects = region.MaxObjects / 2
            }
        };
        
        // Redistribute objects
        foreach (var obj in region.Objects)
        {
            var objectBounds = GetObjectBounds(obj);
            
            foreach (var subRegion in subRegions)
            {
                if (subRegion.Bounds.Intersects(objectBounds))
                {
                    subRegion.Objects.Add(obj);
                }
            }
        }
        
        // Replace original region with sub-regions
        m_regions.Remove(region);
        m_regions.AddRange(subRegions);
        
        // Update Bullet broadphase
        BulletSimAPI.RemoveBroadphaseRegion(scene.World.ptr, region.Bounds);
        foreach (var subRegion in subRegions)
        {
            BulletSimAPI.AddBroadphaseRegion(scene.World.ptr, subRegion.Bounds);
        }
    }
}
```

#### 1.3 Object Pooling for Physics Shapes

**Problem**: Frequent allocation/deallocation of physics shapes causes garbage collection pressure.

```csharp
// Physics shape object pooling system
public class PhysicsShapePool
{
    private readonly Dictionary<ShapeKey, Queue<BulletShape>> m_shapePool = new();
    private readonly Dictionary<BulletShape, ShapeKey> m_activeShapes = new();
    private readonly object m_poolLock = new object();
    
    public struct ShapeKey
    {
        public PhysicsShapeType Type;
        public Vector3 Size;
        public uint HashCode;
        
        public ShapeKey(PhysicsShapeType type, Vector3 size)
        {
            Type = type;
            Size = size;
            HashCode = (uint)(type.GetHashCode() ^ size.GetHashCode());
        }
        
        public override bool Equals(object obj)
        {
            if (obj is ShapeKey other)
            {
                return Type == other.Type && 
                       Math.Abs(Size.X - other.Size.X) < 0.001f &&
                       Math.Abs(Size.Y - other.Size.Y) < 0.001f &&
                       Math.Abs(Size.Z - other.Size.Z) < 0.001f;
            }
            return false;
        }
        
        public override int GetHashCode() => (int)HashCode;
    }
    
    public BulletShape GetShape(PhysicsShapeType type, Vector3 size, BSScene scene)
    {
        var key = new ShapeKey(type, size);
        
        lock (m_poolLock)
        {
            if (m_shapePool.TryGetValue(key, out var queue) && queue.Count > 0)
            {
                var shape = queue.Dequeue();
                m_activeShapes[shape] = key;
                
                BSScene.DetailLog("{0},PhysicsShapePool.GetShape,reused,type={1},size={2}",
                                 BSScene.DetailLogZero, type, size);
                
                return shape;
            }
        }
        
        // Create new shape if none available in pool
        var newShape = CreatePhysicsShape(type, size, scene);
        
        lock (m_poolLock)
        {
            m_activeShapes[newShape] = key;
        }
        
        BSScene.DetailLog("{0},PhysicsShapePool.GetShape,created,type={1},size={2}",
                         BSScene.DetailLogZero, type, size);
        
        return newShape;
    }
    
    public void ReturnShape(BulletShape shape)
    {
        lock (m_poolLock)
        {
            if (m_activeShapes.TryGetValue(shape, out var key))
            {
                m_activeShapes.Remove(shape);
                
                if (!m_shapePool.TryGetValue(key, out var queue))
                {
                    queue = new Queue<BulletShape>();
                    m_shapePool[key] = queue;
                }
                
                // Limit pool size to prevent memory bloat
                if (queue.Count < 10)
                {
                    queue.Enqueue(shape);
                    
                    BSScene.DetailLog("{0},PhysicsShapePool.ReturnShape,pooled,type={1}",
                                     BSScene.DetailLogZero, key.Type);
                }
                else
                {
                    // Destroy excess shapes
                    BulletSimAPI.DestroyShape(shape.ptr);
                    
                    BSScene.DetailLog("{0},PhysicsShapePool.ReturnShape,destroyed,type={1}",
                                     BSScene.DetailLogZero, key.Type);
                }
            }
        }
    }
    
    private BulletShape CreatePhysicsShape(PhysicsShapeType type, Vector3 size, BSScene scene)
    {
        BulletShape shape = new BulletShape();
        
        switch (type)
        {
            case PhysicsShapeType.SHAPE_BOX:
                shape = BulletSimAPI.CreateBoxShape(size * 0.5f);
                break;
            case PhysicsShapeType.SHAPE_SPHERE:
                shape = BulletSimAPI.CreateSphereShape(size.X * 0.5f);
                break;
            case PhysicsShapeType.SHAPE_CYLINDER:
                shape = BulletSimAPI.CreateCylinderShapeZ(size * 0.5f);
                break;
            case PhysicsShapeType.SHAPE_CAPSULE:
                shape = BulletSimAPI.CreateCapsuleShapeZ(size.X * 0.5f, size.Z);
                break;
            default:
                shape = BulletSimAPI.CreateBoxShape(size * 0.5f);
                break;
        }
        
        return shape;
    }
    
    public void Cleanup()
    {
        lock (m_poolLock)
        {
            foreach (var queue in m_shapePool.Values)
            {
                while (queue.Count > 0)
                {
                    var shape = queue.Dequeue();
                    BulletSimAPI.DestroyShape(shape.ptr);
                }
            }
            
            m_shapePool.Clear();
            m_activeShapes.Clear();
        }
    }
}
```

### Phase 2: Performance Enhancements (Month 2)

#### 2.1 Multi-Threading Support

**Implementation**: Thread-safe physics updates with parallel processing.

```csharp
// Thread-safe physics updates
public class ThreadSafePhysicsManager
{
    private readonly object m_simulationLock = new object();
    private readonly TaskScheduler m_physicsScheduler;
    private readonly ConcurrentQueue<Action> m_taints = new();
    private readonly SemaphoreSlim m_simulationSemaphore;
    private readonly ThreadLocal<BSScene> m_threadLocalScene;
    
    public ThreadSafePhysicsManager(BSScene scene)
    {
        // Create limited concurrency scheduler for physics
        var maxConcurrency = Math.Max(1, Environment.ProcessorCount / 2);
        m_physicsScheduler = new LimitedConcurrencyLevelTaskScheduler(maxConcurrency);
        m_simulationSemaphore = new SemaphoreSlim(1, 1);
        m_threadLocalScene = new ThreadLocal<BSScene>(() => scene);
    }
    
    public async Task<float> SimulateAsync(float timeStep)
    {
        await m_simulationSemaphore.WaitAsync();
        
        try
        {
            using (PhysicsProfiler.StartTiming("ThreadSafeSimulation"))
            {
                // Process taints in parallel where possible
                await ProcessTaintsAsync();
                
                // Run main simulation on dedicated thread
                var simulationTask = Task.Factory.StartNew(() =>
                {
                    return RunSimulationStep(timeStep);
                }, CancellationToken.None, TaskCreationOptions.None, m_physicsScheduler);
                
                var result = await simulationTask;
                
                // Update physics objects in parallel
                await UpdatePhysicsObjectsAsync();
                
                return result;
            }
        }
        finally
        {
            m_simulationSemaphore.Release();
        }
    }
    
    private async Task ProcessTaintsAsync()
    {
        var taintBatches = new List<List<Action>>();
        var currentBatch = new List<Action>();
        
        // Group taints into batches that can be processed in parallel
        while (m_taints.TryDequeue(out var taint))
        {
            if (IsParallelSafe(taint))
            {
                currentBatch.Add(taint);
                
                if (currentBatch.Count >= 10) // Batch size
                {
                    taintBatches.Add(currentBatch);
                    currentBatch = new List<Action>();
                }
            }
            else
            {
                // Process non-parallel-safe taints sequentially
                if (currentBatch.Count > 0)
                {
                    taintBatches.Add(currentBatch);
                    currentBatch = new List<Action>();
                }
                
                taint(); // Execute immediately
            }
        }
        
        if (currentBatch.Count > 0)
        {
            taintBatches.Add(currentBatch);
        }
        
        // Process parallel-safe batches concurrently
        var tasks = taintBatches.Select(batch => Task.Run(() =>
        {
            foreach (var taint in batch)
            {
                try
                {
                    taint();
                }
                catch (Exception e)
                {
                    BSScene.DetailLog("{0},ThreadSafePhysicsManager.ProcessTaint,exception={1}",
                                     BSScene.DetailLogZero, e.Message);
                }
            }
        }));
        
        await Task.WhenAll(tasks);
    }
    
    private bool IsParallelSafe(Action taint)
    {
        // Analyze taint method to determine if it's safe for parallel execution
        var method = taint.Method;
        var declaringType = method.DeclaringType;
        
        // Simple heuristics - in practice, would need more sophisticated analysis
        if (declaringType == typeof(BSPrim))
        {
            return method.Name.Contains("Update") || method.Name.Contains("Set");
        }
        
        return false; // Conservative approach - assume not parallel safe
    }
    
    private async Task UpdatePhysicsObjectsAsync()
    {
        var scene = m_threadLocalScene.Value;
        var physicsObjects = scene.PhysObjects.Values.ToList();
        
        // Update objects in parallel batches
        var batchSize = Math.Max(1, physicsObjects.Count / Environment.ProcessorCount);
        var batches = physicsObjects.Batch(batchSize);
        
        var tasks = batches.Select(batch => Task.Run(() =>
        {
            foreach (var obj in batch)
            {
                try
                {
                    obj.UpdateProperties();
                }
                catch (Exception e)
                {
                    BSScene.DetailLog("{0},ThreadSafePhysicsManager.UpdateObject,id={1},exception={2}",
                                     BSScene.DetailLogZero, obj.LocalID, e.Message);
                }
            }
        }));
        
        await Task.WhenAll(tasks);
    }
}

// Custom task scheduler for physics operations
public class LimitedConcurrencyLevelTaskScheduler : TaskScheduler
{
    private readonly LinkedList<Task> m_tasks = new LinkedList<Task>();
    private readonly int m_maxDegreeOfParallelism;
    private int m_delegatesQueuedOrRunning = 0;
    private readonly object m_tasksLock = new object();
    
    public LimitedConcurrencyLevelTaskScheduler(int maxDegreeOfParallelism)
    {
        if (maxDegreeOfParallelism < 1) throw new ArgumentOutOfRangeException("maxDegreeOfParallelism");
        m_maxDegreeOfParallelism = maxDegreeOfParallelism;
    }
    
    protected sealed override void QueueTask(Task task)
    {
        lock (m_tasksLock)
        {
            m_tasks.AddLast(task);
            if (m_delegatesQueuedOrRunning < m_maxDegreeOfParallelism)
            {
                ++m_delegatesQueuedOrRunning;
                NotifyThreadPoolOfPendingWork();
            }
        }
    }
    
    private void NotifyThreadPoolOfPendingWork()
    {
        ThreadPool.UnsafeQueueUserWorkItem(_ =>
        {
            try
            {
                while (true)
                {
                    Task item;
                    lock (m_tasksLock)
                    {
                        if (m_tasks.Count == 0)
                        {
                            --m_delegatesQueuedOrRunning;
                            break;
                        }
                        
                        item = m_tasks.First.Value;
                        m_tasks.RemoveFirst();
                    }
                    
                    base.TryExecuteTask(item);
                }
            }
            finally { }
        }, null);
    }
    
    protected sealed override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
    {
        if (!taskWasPreviouslyQueued) return base.TryExecuteTask(task);
        
        if (TryDequeue(task)) return base.TryExecuteTask(task);
        else return false;
    }
    
    protected sealed override bool TryDequeue(Task task)
    {
        lock (m_tasksLock) { return m_tasks.Remove(task); }
    }
    
    protected sealed override IEnumerable<Task> GetScheduledTasks()
    {
        bool lockTaken = false;
        try
        {
            Monitor.TryEnter(m_tasksLock, ref lockTaken);
            if (lockTaken) return m_tasks.ToArray();
            else throw new NotSupportedException();
        }
        finally
        {
            if (lockTaken) Monitor.Exit(m_tasksLock);
        }
    }
    
    public sealed override int MaximumConcurrencyLevel { get { return m_maxDegreeOfParallelism; } }
}
```

---

## Jolt Physics Integration

### Phase 1: Foundation (Months 3-4)

#### 3.1 Jolt Physics C# Bindings

**Objective**: Create comprehensive P/Invoke bindings for Jolt Physics.

```csharp
// Jolt Physics Native Bindings
public static class JoltNative
{
    private const string JoltLibrary = "JoltPhysics";
    
    #region Foundation and System Management
    
    [DllImport(JoltLibrary, CallingConvention = CallingConvention.Cdecl)]
    public static extern bool JPH_Init();
    
    [DllImport(JoltLibrary, CallingConvention = CallingConvention.Cdecl)]
    public static extern void JPH_Shutdown();
    
    [DllImport(JoltLibrary, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr JPH_PhysicsSystemCreate(in PhysicsSystemSettings settings);
    
    [DllImport(JoltLibrary, CallingConvention = CallingConvention.Cdecl)]
    public static extern void JPH_PhysicsSystemDestroy(IntPtr system);
    
    [DllImport(JoltLibrary, CallingConvention = CallingConvention.Cdecl)]
    public static extern void JPH_PhysicsSystemUpdate(IntPtr system, float deltaTime, 
        int collisionSteps, int integrationSubSteps, IntPtr tempAllocator, IntPtr jobSystem);
    
    #endregion
    
    #region Body Interface
    
    [DllImport(JoltLibrary, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr JPH_BodyInterfaceCreateBody(IntPtr bodyInterface, in BodyCreationSettings settings);
    
    [DllImport(JoltLibrary, CallingConvention = CallingConvention.Cdecl)]
    public static extern uint JPH_BodyInterfaceAddBody(IntPtr bodyInterface, IntPtr body, ActivationMode activationMode);
    
    [DllImport(JoltLibrary, CallingConvention = CallingConvention.Cdecl)]
    public static extern void JPH_BodyInterfaceRemoveBody(IntPtr bodyInterface, uint bodyId);
    
    [DllImport(JoltLibrary, CallingConvention = CallingConvention.Cdecl)]
    public static extern void JPH_BodyInterfaceDestroyBody(IntPtr bodyInterface, uint bodyId);
    
    [DllImport(JoltLibrary, CallingConvention = CallingConvention.Cdecl)]
    public static extern void JPH_BodyInterfaceSetPosition(IntPtr bodyInterface, uint bodyId, 
        in Vector3 position, ActivationMode activationMode);
    
    [DllImport(JoltLibrary, CallingConvention = CallingConvention.Cdecl)]
    public static extern Vector3 JPH_BodyInterfaceGetPosition(IntPtr bodyInterface, uint bodyId);
    
    [DllImport(JoltLibrary, CallingConvention = CallingConvention.Cdecl)]
    public static extern void JPH_BodyInterfaceSetRotation(IntPtr bodyInterface, uint bodyId, 
        in Quaternion rotation, ActivationMode activationMode);
    
    [DllImport(JoltLibrary, CallingConvention = CallingConvention.Cdecl)]
    public static extern Quaternion JPH_BodyInterfaceGetRotation(IntPtr bodyInterface, uint bodyId);
    
    [DllImport(JoltLibrary, CallingConvention = CallingConvention.Cdecl)]
    public static extern void JPH_BodyInterfaceAddForce(IntPtr bodyInterface, uint bodyId, in Vector3 force);
    
    [DllImport(JoltLibrary, CallingConvention = CallingConvention.Cdecl)]
    public static extern void JPH_BodyInterfaceAddForceAtPosition(IntPtr bodyInterface, uint bodyId, 
        in Vector3 force, in Vector3 position);
    
    [DllImport(JoltLibrary, CallingConvention = CallingConvention.Cdecl)]
    public static extern void JPH_BodyInterfaceAddTorque(IntPtr bodyInterface, uint bodyId, in Vector3 torque);
    
    #endregion
    
    #region Shape Creation
    
    [DllImport(JoltLibrary, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr JPH_BoxShapeCreate(in Vector3 halfExtent, float convexRadius);
    
    [DllImport(JoltLibrary, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr JPH_SphereShapeCreate(float radius);
    
    [DllImport(JoltLibrary, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr JPH_CapsuleShapeCreate(float halfHeight, float radius);
    
    [DllImport(JoltLibrary, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr JPH_CylinderShapeCreate(float halfHeight, float radius, float convexRadius);
    
    [DllImport(JoltLibrary, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr JPH_ConvexHullShapeCreate(IntPtr points, int numPoints, float maxConvexRadius);
    
    [DllImport(JoltLibrary, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr JPH_MeshShapeCreate(IntPtr vertices, int vertexCount, IntPtr indices, int indexCount);
    
    [DllImport(JoltLibrary, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr JPH_HeightFieldShapeCreate(IntPtr samples, in Vector3 offset, in Vector3 scale, 
        int sampleCount, IntPtr materialIndices);
    
    #endregion
    
    #region Constraints
    
    [DllImport(JoltLibrary, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr JPH_PointConstraintCreate(uint bodyId1, uint bodyId2, in Vector3 point1, in Vector3 point2);
    
    [DllImport(JoltLibrary, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr JPH_HingeConstraintCreate(uint bodyId1, uint bodyId2, 
        in Vector3 point1, in Vector3 hingeAxis1, in Vector3 point2, in Vector3 hingeAxis2);
    
    [DllImport(JoltLibrary, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr JPH_SliderConstraintCreate(uint bodyId1, uint bodyId2, 
        in Vector3 point1, in Vector3 sliderAxis1, in Vector3 point2, in Vector3 sliderAxis2);
    
    [DllImport(JoltLibrary, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr JPH_DistanceConstraintCreate(uint bodyId1, uint bodyId2, 
        in Vector3 point1, in Vector3 point2, float minDistance, float maxDistance);
    
    #endregion
    
    #region Vehicle System
    
    [DllImport(JoltLibrary, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr JPH_VehicleConstraintCreate(uint vehicleBodyId, in VehicleConstraintSettings settings);
    
    [DllImport(JoltLibrary, CallingConvention = CallingConvention.Cdecl)]
    public static extern void JPH_VehicleConstraintSetDriverInput(IntPtr vehicleConstraint, 
        float forward, float right, float brake, float handBrake);
    
    [DllImport(JoltLibrary, CallingConvention = CallingConvention.Cdecl)]
    public static extern void JPH_VehicleConstraintGetWheelLocalTransform(IntPtr vehicleConstraint, 
        int wheelIndex, out Vector3 position, out Quaternion rotation);
    
    #endregion
    
    #region Character Controller
    
    [DllImport(JoltLibrary, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr JPH_CharacterCreate(in CharacterSettings settings, in Vector3 position, 
        in Quaternion rotation, ulong userData, IntPtr physicsSystem);
    
    [DllImport(JoltLibrary, CallingConvention = CallingConvention.Cdecl)]
    public static extern void JPH_CharacterUpdate(IntPtr character, float deltaTime, 
        in Vector3 gravity, IntPtr tempAllocator);
    
    [DllImport(JoltLibrary, CallingConvention = CallingConvention.Cdecl)]
    public static extern void JPH_CharacterSetLinearVelocity(IntPtr character, in Vector3 velocity);
    
    [DllImport(JoltLibrary, CallingConvention = CallingConvention.Cdecl)]
    public static extern Vector3 JPH_CharacterGetLinearVelocity(IntPtr character);
    
    [DllImport(JoltLibrary, CallingConvention = CallingConvention.Cdecl)]
    public static extern GroundState JPH_CharacterGetGroundState(IntPtr character);
    
    #endregion
}

// Native structure definitions
[StructLayout(LayoutKind.Sequential)]
public struct PhysicsSystemSettings
{
    public int MaxBodies;
    public int NumBodyMutexes;
    public int MaxBodyPairs;
    public int MaxContactConstraints;
    public BroadPhaseLayerInterface BroadPhaseLayerInterface;
    public ObjectVsBroadPhaseLayerFilter ObjectVsBroadPhaseLayerFilter;
    public ObjectLayerPairFilter ObjectLayerPairFilter;
}

[StructLayout(LayoutKind.Sequential)]
public struct BodyCreationSettings
{
    public Vector3 Position;
    public Quaternion Rotation;
    public Vector3 LinearVelocity;
    public Vector3 AngularVelocity;
    public ulong UserData;
    public ushort ObjectLayer;
    public MotionType MotionType;
    public IntPtr Shape;
    public float Friction;
    public float Restitution;
    public float LinearDamping;
    public float AngularDamping;
    public float MaxLinearVelocity;
    public float MaxAngularVelocity;
    public float GravityFactor;
    public OverrideMassProperties OverrideMassProperties;
    public bool IsSensor;
    public bool UseManifoldReduction;
}

[StructLayout(LayoutKind.Sequential)]
public struct VehicleConstraintSettings
{
    public Vector3 Up;
    public Vector3 Forward;
    public int MaxPitchRollAngle;
    public IntPtr VehicleController;
    public IntPtr[] Wheels;
    public int WheelCount;
}

[StructLayout(LayoutKind.Sequential)]
public struct CharacterSettings
{
    public Vector3 Up;
    public IntPtr SupportingVolume;
    public float MaxSlopeAngle;
    public IntPtr Shape;
    public float Mass;
    public float Friction;
    public float GravityFactor;
}

// Enumerations
public enum MotionType : byte
{
    Static,
    Kinematic,
    Dynamic
}

public enum ActivationMode : byte
{
    Activate,
    DontActivate
}

public enum GroundState : byte
{
    OnGround,
    OnSteepGround,
    NotSupported,
    InAir
}
```

#### 3.2 High-Level Jolt Physics Wrapper

```csharp
// High-level C# wrapper for Jolt Physics
public class JoltPhysicsSystem : IPhysicsEngine
{
    private IntPtr m_nativeSystem;
    private IntPtr m_bodyInterface;
    private readonly Dictionary<uint, JoltPhysicsActor> m_actors = new();
    private readonly JoltTempAllocator m_tempAllocator;
    private readonly JoltJobSystem m_jobSystem;
    private bool m_disposed = false;
    
    public JoltPhysicsSystem()
    {
        if (!JoltNative.JPH_Init())
        {
            throw new InvalidOperationException("Failed to initialize Jolt Physics");
        }
        
        m_tempAllocator = new JoltTempAllocator(10 * 1024 * 1024); // 10MB temp allocator
        m_jobSystem = new JoltJobSystem(Environment.ProcessorCount);
        
        var settings = new PhysicsSystemSettings
        {
            MaxBodies = 10000,
            NumBodyMutexes = 0, // Auto-detect
            MaxBodyPairs = 10000,
            MaxContactConstraints = 10000,
            BroadPhaseLayerInterface = CreateBroadPhaseLayerInterface(),
            ObjectVsBroadPhaseLayerFilter = CreateObjectVsBroadPhaseLayerFilter(),
            ObjectLayerPairFilter = CreateObjectLayerPairFilter()
        };
        
        m_nativeSystem = JoltNative.JPH_PhysicsSystemCreate(settings);
        if (m_nativeSystem == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to create Jolt Physics system");
        }
        
        m_bodyInterface = JoltNative.JPH_PhysicsSystemGetBodyInterface(m_nativeSystem);
    }
    
    public void Simulate(float timeStep)
    {
        if (m_disposed) return;
        
        try
        {
            // Update Jolt physics simulation
            JoltNative.JPH_PhysicsSystemUpdate(m_nativeSystem, timeStep, 1, 1, 
                                             m_tempAllocator.NativePtr, m_jobSystem.NativePtr);
            
            // Update managed actor data
            UpdateManagedActors();
        }
        catch (Exception e)
        {
            DetailLog("JoltPhysicsSystem.Simulate,exception={0}", e.Message);
        }
    }
    
    public PhysicsActor CreateActor(PhysicsActorType type, uint localID, PrimitiveBaseShape shape, 
        Vector3 position, Vector3 size, Quaternion rotation)
    {
        var actor = new JoltPhysicsActor(this, type, localID, shape, position, size, rotation);
        m_actors[localID] = actor;
        return actor;
    }
    
    public void RemoveActor(PhysicsActor actor)
    {
        if (actor is JoltPhysicsActor joltActor && m_actors.ContainsKey(actor.LocalID))
        {
            joltActor.Destroy();
            m_actors.Remove(actor.LocalID);
        }
    }
    
    private void UpdateManagedActors()
    {
        foreach (var actor in m_actors.Values)
        {
            actor.UpdateFromNative();
        }
    }
    
    internal IntPtr BodyInterface => m_bodyInterface;
    internal IntPtr NativeSystem => m_nativeSystem;
    
    public void Dispose()
    {
        if (!m_disposed)
        {
            // Clean up all actors
            foreach (var actor in m_actors.Values)
            {
                actor.Destroy();
            }
            m_actors.Clear();
            
            // Destroy native system
            if (m_nativeSystem != IntPtr.Zero)
            {
                JoltNative.JPH_PhysicsSystemDestroy(m_nativeSystem);
                m_nativeSystem = IntPtr.Zero;
            }
            
            // Clean up allocators
            m_tempAllocator?.Dispose();
            m_jobSystem?.Dispose();
            
            JoltNative.JPH_Shutdown();
            
            m_disposed = true;
        }
    }
}

// Jolt physics actor implementation
public class JoltPhysicsActor : PhysicsActor
{
    private readonly JoltPhysicsSystem m_parentScene;
    private uint m_bodyId = uint.MaxValue;
    private IntPtr m_shape = IntPtr.Zero;
    private Vector3 m_position;
    private Quaternion m_orientation;
    private Vector3 m_velocity;
    private Vector3 m_angularVelocity;
    private bool m_disposed = false;
    
    public JoltPhysicsActor(JoltPhysicsSystem parentScene, PhysicsActorType type, uint localID, 
        PrimitiveBaseShape shape, Vector3 position, Vector3 size, Quaternion rotation)
    {
        m_parentScene = parentScene;
        ActorType = type;
        LocalID = localID;
        m_position = position;
        m_orientation = rotation;
        Size = size;
        
        CreatePhysicsShape(shape, size);
        CreatePhysicsBody(position, rotation);
    }
    
    private void CreatePhysicsShape(PrimitiveBaseShape shape, Vector3 size)
    {
        switch (shape.ProfileShape)
        {
            case ProfileShape.Square:
                m_shape = JoltNative.JPH_BoxShapeCreate(size * 0.5f, 0.05f);
                break;
            case ProfileShape.Circle:
                m_shape = JoltNative.JPH_SphereShapeCreate(size.X * 0.5f);
                break;
            case ProfileShape.HalfCircle:
                m_shape = JoltNative.JPH_CapsuleShapeCreate(size.Z * 0.5f, size.X * 0.5f);
                break;
            default:
                m_shape = JoltNative.JPH_BoxShapeCreate(size * 0.5f, 0.05f);
                break;
        }
    }
    
    private void CreatePhysicsBody(Vector3 position, Quaternion rotation)
    {
        var bodySettings = new BodyCreationSettings
        {
            Position = position,
            Rotation = rotation,
            LinearVelocity = Vector3.Zero,
            AngularVelocity = Vector3.Zero,
            UserData = (ulong)LocalID,
            ObjectLayer = 0, // Default layer
            MotionType = ActorType == PhysicsActorType.Prim ? MotionType.Dynamic : MotionType.Kinematic,
            Shape = m_shape,
            Friction = 0.5f,
            Restitution = 0.1f,
            LinearDamping = 0.05f,
            AngularDamping = 0.05f,
            MaxLinearVelocity = 100.0f,
            MaxAngularVelocity = 25.0f,
            GravityFactor = 1.0f,
            IsSensor = false,
            UseManifoldReduction = true
        };
        
        var body = JoltNative.JPH_BodyInterfaceCreateBody(m_parentScene.BodyInterface, bodySettings);
        m_bodyId = JoltNative.JPH_BodyInterfaceAddBody(m_parentScene.BodyInterface, body, ActivationMode.Activate);
    }
    
    public override Vector3 Position
    {
        get => m_position;
        set
        {
            m_position = value;
            if (m_bodyId != uint.MaxValue)
            {
                JoltNative.JPH_BodyInterfaceSetPosition(m_parentScene.BodyInterface, m_bodyId, 
                                                       value, ActivationMode.Activate);
            }
        }
    }
    
    public override Quaternion Orientation
    {
        get => m_orientation;
        set
        {
            m_orientation = value;
            if (m_bodyId != uint.MaxValue)
            {
                JoltNative.JPH_BodyInterfaceSetRotation(m_parentScene.BodyInterface, m_bodyId, 
                                                       value, ActivationMode.Activate);
            }
        }
    }
    
    public override Vector3 Velocity
    {
        get => m_velocity;
        set
        {
            m_velocity = value;
            if (m_bodyId != uint.MaxValue)
            {
                JoltNative.JPH_BodyInterfaceSetLinearVelocity(m_parentScene.BodyInterface, m_bodyId, value);
            }
        }
    }
    
    public override void AddForce(Vector3 force, bool pushforce)
    {
        if (m_bodyId != uint.MaxValue)
        {
            JoltNative.JPH_BodyInterfaceAddForce(m_parentScene.BodyInterface, m_bodyId, force);
        }
    }
    
    public override void AddForceImpulse(Vector3 impulse)
    {
        if (m_bodyId != uint.MaxValue)
        {
            JoltNative.JPH_BodyInterfaceAddImpulse(m_parentScene.BodyInterface, m_bodyId, impulse);
        }
    }
    
    public override void AddAngularForce(Vector3 force, bool pushforce)
    {
        if (m_bodyId != uint.MaxValue)
        {
            JoltNative.JPH_BodyInterfaceAddTorque(m_parentScene.BodyInterface, m_bodyId, force);
        }
    }
    
    public void UpdateFromNative()
    {
        if (m_bodyId != uint.MaxValue)
        {
            m_position = JoltNative.JPH_BodyInterfaceGetPosition(m_parentScene.BodyInterface, m_bodyId);
            m_orientation = JoltNative.JPH_BodyInterfaceGetRotation(m_parentScene.BodyInterface, m_bodyId);
            m_velocity = JoltNative.JPH_BodyInterfaceGetLinearVelocity(m_parentScene.BodyInterface, m_bodyId);
            m_angularVelocity = JoltNative.JPH_BodyInterfaceGetAngularVelocity(m_parentScene.BodyInterface, m_bodyId);
        }
    }
    
    public void Destroy()
    {
        if (!m_disposed && m_bodyId != uint.MaxValue)
        {
            JoltNative.JPH_BodyInterfaceRemoveBody(m_parentScene.BodyInterface, m_bodyId);
            JoltNative.JPH_BodyInterfaceDestroyBody(m_parentScene.BodyInterface, m_bodyId);
            m_bodyId = uint.MaxValue;
        }
        
        if (m_shape != IntPtr.Zero)
        {
            JoltNative.JPH_ShapeDestroy(m_shape);
            m_shape = IntPtr.Zero;
        }
        
        m_disposed = true;
    }
}
```

This comprehensive documentation provides detailed implementation guidance for modernizing OpenSim's physics engine. The documentation covers current state analysis, optimization strategies, and complete implementation details for integrating modern physics engines while maintaining backwards compatibility.