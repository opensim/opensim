/*
 * Copyright (c) Contributors, http://opensimulator.org/
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyrightD
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the OpenSimulator Project nor the
 *       names of its contributors may be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;

using OpenSim.Framework;

using OpenMetaverse;

namespace OpenSim.Region.PhysicsModule.BulletS
{
public sealed class BSAPIUnman : BSAPITemplate
{

private sealed class BulletWorldUnman : BulletWorld
{
    public IntPtr ptr;
    public BulletWorldUnman(uint id, BSScene physScene, IntPtr xx)
        : base(id, physScene)
    {
        ptr = xx;
    }
}

private sealed class BulletBodyUnman : BulletBody
{
    public IntPtr ptr;
    public BulletBodyUnman(uint id, IntPtr xx)
        : base(id)
    {
        ptr = xx;
    }
    public override bool HasPhysicalBody
    {
        get { return ptr != IntPtr.Zero; }
    }
    public override void Clear()
    {
        ptr = IntPtr.Zero;
    }
    public override string AddrString
    {
        get { return ptr.ToString("X"); }
    }
}

private sealed class BulletShapeUnman : BulletShape
{
    public IntPtr ptr;
    public BulletShapeUnman(IntPtr xx, BSPhysicsShapeType typ)
        : base()
    {
        ptr = xx;
        shapeType = typ;
    }
    public override bool HasPhysicalShape
    {
        get { return ptr != IntPtr.Zero; }
    }
    public override void Clear()
    {
        ptr = IntPtr.Zero;
    }
    public override BulletShape Clone()
    {
        return new BulletShapeUnman(ptr, shapeType);
    }
    public override bool ReferenceSame(BulletShape other)
    {
        BulletShapeUnman otheru = other as BulletShapeUnman;
        return (otheru != null) && (this.ptr == otheru.ptr);

    }
    public override string AddrString
    {
        get { return ptr.ToString("X"); }
    }
}
private sealed class BulletConstraintUnman : BulletConstraint
{
    public BulletConstraintUnman(IntPtr xx) : base()
    {
        ptr = xx;
    }
    public IntPtr ptr;

    public override void Clear()
    {
        ptr = IntPtr.Zero;
    }
    public override bool HasPhysicalConstraint { get { return ptr != IntPtr.Zero; } }

    // Used for log messages for a unique display of the memory/object allocated to this instance
    public override string AddrString
    {
        get { return ptr.ToString("X"); }
    }
}

// We pin the memory passed between the managed and unmanaged code.
GCHandle m_paramsHandle;
private GCHandle m_collisionArrayPinnedHandle;
private GCHandle m_updateArrayPinnedHandle;

// Handle to the callback used by the unmanaged code to call into the managed code.
// Used for debug logging.
// Need to store the handle in a persistant variable so it won't be freed.
private BSAPICPP.DebugLogCallback m_DebugLogCallbackHandle;

private BSScene PhysicsScene { get; set; }

public override string BulletEngineName { get { return "BulletUnmanaged"; } }
public override string BulletEngineVersion { get; protected set; }

public BSAPIUnman(string paramName, BSScene physScene)
{
    PhysicsScene = physScene;

    // Do something fancy with the paramName to get the right DLL implementation
    //     like "Bullet-2.80-OpenCL-Intel" loading the version for Intel based OpenCL implementation, etc.
    if (Util.IsWindows())
        Util.LoadArchSpecificWindowsDll("BulletSim.dll");
    // If not Windows, loading is performed by the
    // Mono loader as specified in
    // "bin/Physics/OpenSim.Region.Physics.BulletSPlugin.dll.config".
}

// Initialization and simulation
public override BulletWorld Initialize(Vector3 maxPosition, ConfigurationParameters parms,
											int maxCollisions,  ref CollisionDesc[] collisionArray,
											int maxUpdates, ref EntityProperties[] updateArray
                                            )
{
    // Pin down the memory that will be used to pass object collisions and updates back from unmanaged code
    m_paramsHandle = GCHandle.Alloc(parms, GCHandleType.Pinned);
    m_collisionArrayPinnedHandle = GCHandle.Alloc(collisionArray, GCHandleType.Pinned);
    m_updateArrayPinnedHandle = GCHandle.Alloc(updateArray, GCHandleType.Pinned);

    // If Debug logging level, enable logging from the unmanaged code
    m_DebugLogCallbackHandle = null;
    if (BSScene.m_log.IsDebugEnabled && PhysicsScene.PhysicsLogging.Enabled)
    {
        BSScene.m_log.DebugFormat("{0}: Initialize: Setting debug callback for unmanaged code", BSScene.LogHeader);
        if (PhysicsScene.PhysicsLogging.Enabled)
            // The handle is saved in a variable to make sure it doesn't get freed after this call
            m_DebugLogCallbackHandle = new BSAPICPP.DebugLogCallback(BulletLoggerPhysLog);
        else
            m_DebugLogCallbackHandle = new BSAPICPP.DebugLogCallback(BulletLogger);
    }

    // Get the version of the DLL
    // TODO: this doesn't work yet. Something wrong with marshaling the returned string.
    // BulletEngineVersion = BulletSimAPI.GetVersion2();
    BulletEngineVersion = "";

    // Call the unmanaged code with the buffers and other information
    return new BulletWorldUnman(0, PhysicsScene, BSAPICPP.Initialize2(maxPosition, m_paramsHandle.AddrOfPinnedObject(),
                                    maxCollisions, m_collisionArrayPinnedHandle.AddrOfPinnedObject(),
                                    maxUpdates, m_updateArrayPinnedHandle.AddrOfPinnedObject(),
                                    m_DebugLogCallbackHandle));

}

// Called directly from unmanaged code so don't do much
private void BulletLogger(string msg)
{
    BSScene.m_log.Debug("[BULLETS UNMANAGED]:" + msg);
}

// Called directly from unmanaged code so don't do much
private void BulletLoggerPhysLog(string msg)
{
    PhysicsScene.DetailLog("[BULLETS UNMANAGED]:" + msg);
}

public override int PhysicsStep(BulletWorld world, float timeStep, int maxSubSteps, float fixedTimeStep,
                                        out int updatedEntityCount, out int collidersCount)
{
    BulletWorldUnman worldu = world as BulletWorldUnman;
    return BSAPICPP.PhysicsStep2(worldu.ptr, timeStep, maxSubSteps, fixedTimeStep, out updatedEntityCount, out collidersCount);
}

public override void Shutdown(BulletWorld world)
{
    BulletWorldUnman worldu = world as BulletWorldUnman;
    BSAPICPP.Shutdown2(worldu.ptr);

    if (m_paramsHandle.IsAllocated)
    {
        m_paramsHandle.Free();
    }
    if (m_collisionArrayPinnedHandle.IsAllocated)
    {
        m_collisionArrayPinnedHandle.Free();
    }
    if (m_updateArrayPinnedHandle.IsAllocated)
    {
        m_updateArrayPinnedHandle.Free();
    }
}

public override bool PushUpdate(BulletBody obj)
{
    BulletBodyUnman bodyu = obj as BulletBodyUnman;
    return BSAPICPP.PushUpdate2(bodyu.ptr);
}

public override bool UpdateParameter(BulletWorld world, uint localID, String parm, float value)
{
    BulletWorldUnman worldu = world as BulletWorldUnman;
    return BSAPICPP.UpdateParameter2(worldu.ptr, localID, parm, value);
}

// =====================================================================================
// Mesh, hull, shape and body creation helper routines
public override BulletShape CreateMeshShape(BulletWorld world,
                int indicesCount, int[] indices,
                int verticesCount, float[] vertices)
{
    BulletWorldUnman worldu = world as BulletWorldUnman;
    return new BulletShapeUnman(
                    BSAPICPP.CreateMeshShape2(worldu.ptr, indicesCount, indices, verticesCount, vertices),
                    BSPhysicsShapeType.SHAPE_MESH);
}

public override BulletShape CreateGImpactShape(BulletWorld world,
                int indicesCount, int[] indices,
                int verticesCount, float[] vertices)
{
    BulletWorldUnman worldu = world as BulletWorldUnman;
    return new BulletShapeUnman(
                    BSAPICPP.CreateGImpactShape2(worldu.ptr, indicesCount, indices, verticesCount, vertices),
                    BSPhysicsShapeType.SHAPE_GIMPACT);
}

public override BulletShape CreateHullShape(BulletWorld world, int hullCount, float[] hulls)
{
    BulletWorldUnman worldu = world as BulletWorldUnman;
    return new BulletShapeUnman(
                    BSAPICPP.CreateHullShape2(worldu.ptr, hullCount, hulls),
                    BSPhysicsShapeType.SHAPE_HULL);
}

public override BulletShape BuildHullShapeFromMesh(BulletWorld world, BulletShape meshShape, HACDParams parms)
{
    BulletWorldUnman worldu = world as BulletWorldUnman;
    BulletShapeUnman shapeu = meshShape as BulletShapeUnman;
    return new BulletShapeUnman(
                    BSAPICPP.BuildHullShapeFromMesh2(worldu.ptr, shapeu.ptr, parms),
                    BSPhysicsShapeType.SHAPE_HULL);
}

public override BulletShape BuildConvexHullShapeFromMesh(BulletWorld world, BulletShape meshShape)
{
    BulletWorldUnman worldu = world as BulletWorldUnman;
    BulletShapeUnman shapeu = meshShape as BulletShapeUnman;
    return new BulletShapeUnman(
                    BSAPICPP.BuildConvexHullShapeFromMesh2(worldu.ptr, shapeu.ptr),
                    BSPhysicsShapeType.SHAPE_CONVEXHULL);
}

public override BulletShape CreateConvexHullShape(BulletWorld world,
                int indicesCount, int[] indices,
                int verticesCount, float[] vertices)
{
    BulletWorldUnman worldu = world as BulletWorldUnman;
    return new BulletShapeUnman(
                    BSAPICPP.CreateConvexHullShape2(worldu.ptr, indicesCount, indices, verticesCount, vertices),
                    BSPhysicsShapeType.SHAPE_CONVEXHULL);
}

public override BulletShape BuildNativeShape(BulletWorld world, ShapeData shapeData)
{
    BulletWorldUnman worldu = world as BulletWorldUnman;
    return new BulletShapeUnman(BSAPICPP.BuildNativeShape2(worldu.ptr, shapeData), shapeData.Type);
}

public override bool IsNativeShape(BulletShape shape)
{
    BulletShapeUnman shapeu = shape as BulletShapeUnman;
    if (shapeu != null && shapeu.HasPhysicalShape)
        return BSAPICPP.IsNativeShape2(shapeu.ptr);
    return false;
}

public override void SetShapeCollisionMargin(BulletShape shape, float margin)
{
    BulletShapeUnman shapeu = shape as BulletShapeUnman;
    if (shapeu != null && shapeu.HasPhysicalShape)
        BSAPICPP.SetShapeCollisionMargin(shapeu.ptr, margin);
}

public override BulletShape BuildCapsuleShape(BulletWorld world, float radius, float height, Vector3 scale)
{
    BulletWorldUnman worldu = world as BulletWorldUnman;
    return new BulletShapeUnman(
                   BSAPICPP.BuildCapsuleShape2(worldu.ptr, radius, height, scale),
                   BSPhysicsShapeType.SHAPE_CAPSULE);
}

public override BulletShape CreateCompoundShape(BulletWorld world, bool enableDynamicAabbTree)
{
    BulletWorldUnman worldu = world as BulletWorldUnman;
    return new BulletShapeUnman(
                    BSAPICPP.CreateCompoundShape2(worldu.ptr, enableDynamicAabbTree),
                    BSPhysicsShapeType.SHAPE_COMPOUND);

}

public override int GetNumberOfCompoundChildren(BulletShape shape)
{
    BulletShapeUnman shapeu = shape as BulletShapeUnman;
    if (shapeu != null && shapeu.HasPhysicalShape)
        return BSAPICPP.GetNumberOfCompoundChildren2(shapeu.ptr);
    return 0;
}

public override void AddChildShapeToCompoundShape(BulletShape shape, BulletShape addShape, Vector3 pos, Quaternion rot)
{
    BulletShapeUnman shapeu = shape as BulletShapeUnman;
    BulletShapeUnman addShapeu = addShape as BulletShapeUnman;
    BSAPICPP.AddChildShapeToCompoundShape2(shapeu.ptr, addShapeu.ptr, pos, rot);
}

public override BulletShape GetChildShapeFromCompoundShapeIndex(BulletShape shape, int indx)
{
    BulletShapeUnman shapeu = shape as BulletShapeUnman;
    return new BulletShapeUnman(BSAPICPP.GetChildShapeFromCompoundShapeIndex2(shapeu.ptr, indx), BSPhysicsShapeType.SHAPE_UNKNOWN);
}

public override BulletShape RemoveChildShapeFromCompoundShapeIndex(BulletShape shape, int indx)
{
    BulletShapeUnman shapeu = shape as BulletShapeUnman;
    return new BulletShapeUnman(BSAPICPP.RemoveChildShapeFromCompoundShapeIndex2(shapeu.ptr, indx), BSPhysicsShapeType.SHAPE_UNKNOWN);
}

public override void RemoveChildShapeFromCompoundShape(BulletShape shape, BulletShape removeShape)
{
    BulletShapeUnman shapeu = shape as BulletShapeUnman;
    BulletShapeUnman removeShapeu = removeShape as BulletShapeUnman;
    BSAPICPP.RemoveChildShapeFromCompoundShape2(shapeu.ptr, removeShapeu.ptr);
}

public override void UpdateChildTransform(BulletShape pShape, int childIndex, Vector3 pos, Quaternion rot, bool shouldRecalculateLocalAabb)
{
    BulletShapeUnman shapeu = pShape as BulletShapeUnman;
    BSAPICPP.UpdateChildTransform2(shapeu.ptr, childIndex, pos, rot, shouldRecalculateLocalAabb);
}

public override void RecalculateCompoundShapeLocalAabb(BulletShape shape)
{
    BulletShapeUnman shapeu = shape as BulletShapeUnman;
    BSAPICPP.RecalculateCompoundShapeLocalAabb2(shapeu.ptr);
}

public override BulletShape DuplicateCollisionShape(BulletWorld world, BulletShape srcShape, uint id)
{
    BulletWorldUnman worldu = world as BulletWorldUnman;
    BulletShapeUnman srcShapeu = srcShape as BulletShapeUnman;
    return new BulletShapeUnman(BSAPICPP.DuplicateCollisionShape2(worldu.ptr, srcShapeu.ptr, id), srcShape.shapeType);
}

public override bool DeleteCollisionShape(BulletWorld world, BulletShape shape)
{
    BulletWorldUnman worldu = world as BulletWorldUnman;
    BulletShapeUnman shapeu = shape as BulletShapeUnman;
    return BSAPICPP.DeleteCollisionShape2(worldu.ptr, shapeu.ptr);
}

public override CollisionObjectTypes GetBodyType(BulletBody obj)
{
    BulletBodyUnman bodyu = obj as BulletBodyUnman;
    return (CollisionObjectTypes)BSAPICPP.GetBodyType2(bodyu.ptr);
}

public override BulletBody CreateBodyFromShape(BulletWorld world, BulletShape shape, uint id, Vector3 pos, Quaternion rot)
{
    BulletWorldUnman worldu = world as BulletWorldUnman;
    BulletShapeUnman shapeu = shape as BulletShapeUnman;
    return new BulletBodyUnman(id, BSAPICPP.CreateBodyFromShape2(worldu.ptr, shapeu.ptr, id, pos, rot));
}

public override BulletBody CreateBodyWithDefaultMotionState(BulletShape shape, uint id, Vector3 pos, Quaternion rot)
{
    BulletShapeUnman shapeu = shape as BulletShapeUnman;
    return new BulletBodyUnman(id, BSAPICPP.CreateBodyWithDefaultMotionState2(shapeu.ptr, id, pos, rot));
}

public override BulletBody CreateGhostFromShape(BulletWorld world, BulletShape shape, uint id, Vector3 pos, Quaternion rot)
{
    BulletWorldUnman worldu = world as BulletWorldUnman;
    BulletShapeUnman shapeu = shape as BulletShapeUnman;
    return new BulletBodyUnman(id, BSAPICPP.CreateGhostFromShape2(worldu.ptr, shapeu.ptr, id, pos, rot));
}

public override void DestroyObject(BulletWorld world, BulletBody obj)
{
    BulletWorldUnman worldu = world as BulletWorldUnman;
    BulletBodyUnman bodyu = obj as BulletBodyUnman;
    BSAPICPP.DestroyObject2(worldu.ptr, bodyu.ptr);
}

// =====================================================================================
// Terrain creation and helper routines
public override BulletShape CreateGroundPlaneShape(uint id, float height, float collisionMargin)
{
    return new BulletShapeUnman(BSAPICPP.CreateGroundPlaneShape2(id, height, collisionMargin), BSPhysicsShapeType.SHAPE_GROUNDPLANE);
}

public override BulletShape CreateTerrainShape(uint id, Vector3 size, float minHeight, float maxHeight, float[] heightMap,
                                float scaleFactor, float collisionMargin)
{
    return new BulletShapeUnman(BSAPICPP.CreateTerrainShape2(id, size, minHeight, maxHeight, heightMap, scaleFactor, collisionMargin),
                                                BSPhysicsShapeType.SHAPE_TERRAIN);
}

// =====================================================================================
// Constraint creation and helper routines
public override BulletConstraint Create6DofConstraint(BulletWorld world, BulletBody obj1, BulletBody obj2,
                    Vector3 frame1loc, Quaternion frame1rot,
                    Vector3 frame2loc, Quaternion frame2rot,
                    bool useLinearReferenceFrameA, bool disableCollisionsBetweenLinkedBodies)
{
    BulletWorldUnman worldu = world as BulletWorldUnman;
    BulletBodyUnman bodyu1 = obj1 as BulletBodyUnman;
    BulletBodyUnman bodyu2 = obj2 as BulletBodyUnman;
    return new BulletConstraintUnman(BSAPICPP.Create6DofConstraint2(worldu.ptr, bodyu1.ptr, bodyu2.ptr, frame1loc, frame1rot,
                    frame2loc, frame2rot, useLinearReferenceFrameA, disableCollisionsBetweenLinkedBodies));
}

public override BulletConstraint Create6DofConstraintToPoint(BulletWorld world, BulletBody obj1, BulletBody obj2,
                    Vector3 joinPoint,
                    bool useLinearReferenceFrameA, bool disableCollisionsBetweenLinkedBodies)
{
    BulletWorldUnman worldu = world as BulletWorldUnman;
    BulletBodyUnman bodyu1 = obj1 as BulletBodyUnman;
    BulletBodyUnman bodyu2 = obj2 as BulletBodyUnman;
    return new BulletConstraintUnman(BSAPICPP.Create6DofConstraintToPoint2(worldu.ptr, bodyu1.ptr, bodyu2.ptr,
                    joinPoint, useLinearReferenceFrameA, disableCollisionsBetweenLinkedBodies));
}

public override BulletConstraint Create6DofConstraintFixed(BulletWorld world, BulletBody obj1,
                    Vector3 frameInBloc, Quaternion frameInBrot,
                    bool useLinearReferenceFrameB, bool disableCollisionsBetweenLinkedBodies)
{
    BulletWorldUnman worldu = world as BulletWorldUnman;
    BulletBodyUnman bodyu1 = obj1 as BulletBodyUnman;
    return new BulletConstraintUnman(BSAPICPP.Create6DofConstraintFixed2(worldu.ptr, bodyu1.ptr,
                    frameInBloc, frameInBrot, useLinearReferenceFrameB, disableCollisionsBetweenLinkedBodies));
}

public override BulletConstraint Create6DofSpringConstraint(BulletWorld world, BulletBody obj1, BulletBody obj2,
                    Vector3 frame1loc, Quaternion frame1rot,
                    Vector3 frame2loc, Quaternion frame2rot,
                    bool useLinearReferenceFrameA, bool disableCollisionsBetweenLinkedBodies)
{
    BulletWorldUnman worldu = world as BulletWorldUnman;
    BulletBodyUnman bodyu1 = obj1 as BulletBodyUnman;
    BulletBodyUnman bodyu2 = obj2 as BulletBodyUnman;
    return new BulletConstraintUnman(BSAPICPP.Create6DofSpringConstraint2(worldu.ptr, bodyu1.ptr, bodyu2.ptr, frame1loc, frame1rot,
                    frame2loc, frame2rot, useLinearReferenceFrameA, disableCollisionsBetweenLinkedBodies));
}

public override BulletConstraint CreateHingeConstraint(BulletWorld world, BulletBody obj1, BulletBody obj2,
                    Vector3 pivotinA, Vector3 pivotinB,
                    Vector3 axisInA, Vector3 axisInB,
                    bool useLinearReferenceFrameA, bool disableCollisionsBetweenLinkedBodies)
{
    BulletWorldUnman worldu = world as BulletWorldUnman;
    BulletBodyUnman bodyu1 = obj1 as BulletBodyUnman;
    BulletBodyUnman bodyu2 = obj2 as BulletBodyUnman;
    return new BulletConstraintUnman(BSAPICPP.CreateHingeConstraint2(worldu.ptr, bodyu1.ptr, bodyu2.ptr,
                    pivotinA, pivotinB, axisInA, axisInB, useLinearReferenceFrameA, disableCollisionsBetweenLinkedBodies));
}

public override BulletConstraint CreateSliderConstraint(BulletWorld world, BulletBody obj1, BulletBody obj2,
                    Vector3 frame1loc, Quaternion frame1rot,
                    Vector3 frame2loc, Quaternion frame2rot,
                    bool useLinearReferenceFrameA, bool disableCollisionsBetweenLinkedBodies)
{
    BulletWorldUnman worldu = world as BulletWorldUnman;
    BulletBodyUnman bodyu1 = obj1 as BulletBodyUnman;
    BulletBodyUnman bodyu2 = obj2 as BulletBodyUnman;
    return new BulletConstraintUnman(BSAPICPP.CreateSliderConstraint2(worldu.ptr, bodyu1.ptr, bodyu2.ptr, frame1loc, frame1rot,
                    frame2loc, frame2rot, useLinearReferenceFrameA, disableCollisionsBetweenLinkedBodies));
}

public override BulletConstraint CreateConeTwistConstraint(BulletWorld world, BulletBody obj1, BulletBody obj2,
                    Vector3 frame1loc, Quaternion frame1rot,
                    Vector3 frame2loc, Quaternion frame2rot,
                    bool disableCollisionsBetweenLinkedBodies)
{
    BulletWorldUnman worldu = world as BulletWorldUnman;
    BulletBodyUnman bodyu1 = obj1 as BulletBodyUnman;
    BulletBodyUnman bodyu2 = obj2 as BulletBodyUnman;
    return new BulletConstraintUnman(BSAPICPP.CreateConeTwistConstraint2(worldu.ptr, bodyu1.ptr, bodyu2.ptr, frame1loc, frame1rot,
                                        frame2loc, frame2rot, disableCollisionsBetweenLinkedBodies));
}

public override BulletConstraint CreateGearConstraint(BulletWorld world, BulletBody obj1, BulletBody obj2,
                    Vector3 axisInA, Vector3 axisInB,
                    float ratio, bool disableCollisionsBetweenLinkedBodies)
{
    BulletWorldUnman worldu = world as BulletWorldUnman;
    BulletBodyUnman bodyu1 = obj1 as BulletBodyUnman;
    BulletBodyUnman bodyu2 = obj2 as BulletBodyUnman;
    return new BulletConstraintUnman(BSAPICPP.CreateGearConstraint2(worldu.ptr, bodyu1.ptr, bodyu2.ptr, axisInA, axisInB,
                                        ratio, disableCollisionsBetweenLinkedBodies));
}

public override BulletConstraint CreatePoint2PointConstraint(BulletWorld world, BulletBody obj1, BulletBody obj2,
                    Vector3 pivotInA, Vector3 pivotInB,
                    bool disableCollisionsBetweenLinkedBodies)
{
    BulletWorldUnman worldu = world as BulletWorldUnman;
    BulletBodyUnman bodyu1 = obj1 as BulletBodyUnman;
    BulletBodyUnman bodyu2 = obj2 as BulletBodyUnman;
    return new BulletConstraintUnman(BSAPICPP.CreatePoint2PointConstraint2(worldu.ptr, bodyu1.ptr, bodyu2.ptr, pivotInA, pivotInB,
                                        disableCollisionsBetweenLinkedBodies));
}

public override void SetConstraintEnable(BulletConstraint constrain, float numericTrueFalse)
{
    BulletConstraintUnman constrainu = constrain as BulletConstraintUnman;
    BSAPICPP.SetConstraintEnable2(constrainu.ptr, numericTrueFalse);
}

public override void SetConstraintNumSolverIterations(BulletConstraint constrain, float iterations)
{
    BulletConstraintUnman constrainu = constrain as BulletConstraintUnman;
    BSAPICPP.SetConstraintNumSolverIterations2(constrainu.ptr, iterations);
}

public override bool SetFrames(BulletConstraint constrain,
                Vector3 frameA, Quaternion frameArot, Vector3 frameB, Quaternion frameBrot)
{
    BulletConstraintUnman constrainu = constrain as BulletConstraintUnman;
    return BSAPICPP.SetFrames2(constrainu.ptr, frameA, frameArot, frameB, frameBrot);
}

public override bool SetLinearLimits(BulletConstraint constrain, Vector3 low, Vector3 hi)
{
    BulletConstraintUnman constrainu = constrain as BulletConstraintUnman;
    return BSAPICPP.SetLinearLimits2(constrainu.ptr, low, hi);
}

public override bool SetAngularLimits(BulletConstraint constrain, Vector3 low, Vector3 hi)
{
    BulletConstraintUnman constrainu = constrain as BulletConstraintUnman;
    return BSAPICPP.SetAngularLimits2(constrainu.ptr, low, hi);
}

public override bool UseFrameOffset(BulletConstraint constrain, float enable)
{
    BulletConstraintUnman constrainu = constrain as BulletConstraintUnman;
    return BSAPICPP.UseFrameOffset2(constrainu.ptr, enable);
}

public override bool TranslationalLimitMotor(BulletConstraint constrain, float enable, float targetVel, float maxMotorForce)
{
    BulletConstraintUnman constrainu = constrain as BulletConstraintUnman;
    return BSAPICPP.TranslationalLimitMotor2(constrainu.ptr, enable, targetVel, maxMotorForce);
}

public override bool SetBreakingImpulseThreshold(BulletConstraint constrain, float threshold)
{
    BulletConstraintUnman constrainu = constrain as BulletConstraintUnman;
    return BSAPICPP.SetBreakingImpulseThreshold2(constrainu.ptr, threshold);
}

public override bool HingeSetLimits(BulletConstraint constrain, float low, float high, float softness, float bias, float relaxation)
{
    BulletConstraintUnman constrainu = constrain as BulletConstraintUnman;
    return BSAPICPP.HingeSetLimits2(constrainu.ptr, low, high, softness, bias, relaxation);
}

public override bool SpringEnable(BulletConstraint constrain, int index, float numericTrueFalse)
{
    BulletConstraintUnman constrainu = constrain as BulletConstraintUnman;
    return BSAPICPP.ConstraintSpringEnable2(constrainu.ptr, index, numericTrueFalse);
}

public override bool SpringSetEquilibriumPoint(BulletConstraint constrain, int index, float equilibriumPoint)
{
    BulletConstraintUnman constrainu = constrain as BulletConstraintUnman;
    return BSAPICPP.ConstraintSpringSetEquilibriumPoint2(constrainu.ptr, index, equilibriumPoint);
}

public override bool SpringSetStiffness(BulletConstraint constrain, int index, float stiffnesss)
{
    BulletConstraintUnman constrainu = constrain as BulletConstraintUnman;
    return BSAPICPP.ConstraintSpringSetStiffness2(constrainu.ptr, index, stiffnesss);
}

public override bool SpringSetDamping(BulletConstraint constrain, int index, float damping)
{
    BulletConstraintUnman constrainu = constrain as BulletConstraintUnman;
    return BSAPICPP.ConstraintSpringSetDamping2(constrainu.ptr, index, damping);
}

public override bool SliderSetLimits(BulletConstraint constrain, int lowerUpper, int linAng, float val)
{
    BulletConstraintUnman constrainu = constrain as BulletConstraintUnman;
    return BSAPICPP.SliderSetLimits2(constrainu.ptr, lowerUpper, linAng, val);
}

public override bool SliderSet(BulletConstraint constrain, int softRestDamp, int dirLimOrtho, int linAng, float val)
{
    BulletConstraintUnman constrainu = constrain as BulletConstraintUnman;
    return BSAPICPP.SliderSet2(constrainu.ptr, softRestDamp, dirLimOrtho, linAng, val);
}

public override bool SliderMotorEnable(BulletConstraint constrain, int linAng, float numericTrueFalse)
{
    BulletConstraintUnman constrainu = constrain as BulletConstraintUnman;
    return BSAPICPP.SliderMotorEnable2(constrainu.ptr, linAng, numericTrueFalse);
}

public override bool SliderMotor(BulletConstraint constrain, int forceVel, int linAng, float val)
{
    BulletConstraintUnman constrainu = constrain as BulletConstraintUnman;
    return BSAPICPP.SliderMotor2(constrainu.ptr, forceVel, linAng, val);
}

public override bool CalculateTransforms(BulletConstraint constrain)
{
    BulletConstraintUnman constrainu = constrain as BulletConstraintUnman;
    return BSAPICPP.CalculateTransforms2(constrainu.ptr);
}

public override bool SetConstraintParam(BulletConstraint constrain, ConstraintParams paramIndex, float value, ConstraintParamAxis axis)
{
    BulletConstraintUnman constrainu = constrain as BulletConstraintUnman;
    return BSAPICPP.SetConstraintParam2(constrainu.ptr, paramIndex, value, axis);
}

public override bool DestroyConstraint(BulletWorld world, BulletConstraint constrain)
{
    BulletWorldUnman worldu = world as BulletWorldUnman;
    BulletConstraintUnman constrainu = constrain as BulletConstraintUnman;
    return BSAPICPP.DestroyConstraint2(worldu.ptr, constrainu.ptr);
}

// =====================================================================================
// btCollisionWorld entries
public override void UpdateSingleAabb(BulletWorld world, BulletBody obj)
{
    BulletWorldUnman worldu = world as BulletWorldUnman;
    BulletBodyUnman bodyu = obj as BulletBodyUnman;
    BSAPICPP.UpdateSingleAabb2(worldu.ptr, bodyu.ptr);
}

public override void UpdateAabbs(BulletWorld world)
{
    BulletWorldUnman worldu = world as BulletWorldUnman;
    BSAPICPP.UpdateAabbs2(worldu.ptr);
}

public override bool GetForceUpdateAllAabbs(BulletWorld world)
{
    BulletWorldUnman worldu = world as BulletWorldUnman;
    return BSAPICPP.GetForceUpdateAllAabbs2(worldu.ptr);
}

public override void SetForceUpdateAllAabbs(BulletWorld world, bool force)
{
    BulletWorldUnman worldu = world as BulletWorldUnman;
    BSAPICPP.SetForceUpdateAllAabbs2(worldu.ptr, force);
}

// =====================================================================================
// btDynamicsWorld entries
public override bool AddObjectToWorld(BulletWorld world, BulletBody obj)
{
    BulletWorldUnman worldu = world as BulletWorldUnman;
    BulletBodyUnman bodyu = obj as BulletBodyUnman;

    // Bullet resets several variables when an object is added to the world.
    //   Gravity is reset to world default depending on the static/dynamic
    //   type. Of course, the collision flags in the broadphase proxy are initialized to default.
    Vector3 origGrav = BSAPICPP.GetGravity2(bodyu.ptr);

    bool ret = BSAPICPP.AddObjectToWorld2(worldu.ptr, bodyu.ptr);

    if (ret)
    {
        BSAPICPP.SetGravity2(bodyu.ptr, origGrav);
        obj.ApplyCollisionMask(world.physicsScene);
    }
    return ret;
}

public override bool RemoveObjectFromWorld(BulletWorld world, BulletBody obj)
{
    BulletWorldUnman worldu = world as BulletWorldUnman;
    BulletBodyUnman bodyu = obj as BulletBodyUnman;
    return BSAPICPP.RemoveObjectFromWorld2(worldu.ptr, bodyu.ptr);
}

public override bool ClearCollisionProxyCache(BulletWorld world, BulletBody obj)
{
    BulletWorldUnman worldu = world as BulletWorldUnman;
    BulletBodyUnman bodyu = obj as BulletBodyUnman;
    return BSAPICPP.ClearCollisionProxyCache2(worldu.ptr, bodyu.ptr);
}

public override bool AddConstraintToWorld(BulletWorld world, BulletConstraint constrain, bool disableCollisionsBetweenLinkedObjects)
{
    BulletWorldUnman worldu = world as BulletWorldUnman;
    BulletConstraintUnman constrainu = constrain as BulletConstraintUnman;
    return BSAPICPP.AddConstraintToWorld2(worldu.ptr, constrainu.ptr, disableCollisionsBetweenLinkedObjects);
}

public override bool RemoveConstraintFromWorld(BulletWorld world, BulletConstraint constrain)
{
    BulletWorldUnman worldu = world as BulletWorldUnman;
    BulletConstraintUnman constrainu = constrain as BulletConstraintUnman;
    return BSAPICPP.RemoveConstraintFromWorld2(worldu.ptr, constrainu.ptr);
}
// =====================================================================================
// btCollisionObject entries
public override Vector3 GetAnisotripicFriction(BulletConstraint constrain)
{
    BulletConstraintUnman constrainu = constrain as BulletConstraintUnman;
    return BSAPICPP.GetAnisotripicFriction2(constrainu.ptr);
}

public override Vector3 SetAnisotripicFriction(BulletConstraint constrain, Vector3 frict)
{
    BulletConstraintUnman constrainu = constrain as BulletConstraintUnman;
    return BSAPICPP.SetAnisotripicFriction2(constrainu.ptr, frict);
}

public override bool HasAnisotripicFriction(BulletConstraint constrain)
{
    BulletConstraintUnman constrainu = constrain as BulletConstraintUnman;
    return BSAPICPP.HasAnisotripicFriction2(constrainu.ptr);
}

public override void SetContactProcessingThreshold(BulletBody obj, float val)
{
    BulletBodyUnman bodyu = obj as BulletBodyUnman;
    BSAPICPP.SetContactProcessingThreshold2(bodyu.ptr, val);
}

public override float GetContactProcessingThreshold(BulletBody obj)
{
    BulletBodyUnman bodyu = obj as BulletBodyUnman;
    return BSAPICPP.GetContactProcessingThreshold2(bodyu.ptr);
}

public override bool IsStaticObject(BulletBody obj)
{
    BulletBodyUnman bodyu = obj as BulletBodyUnman;
    return BSAPICPP.IsStaticObject2(bodyu.ptr);
}

public override bool IsKinematicObject(BulletBody obj)
{
    BulletBodyUnman bodyu = obj as BulletBodyUnman;
    return BSAPICPP.IsKinematicObject2(bodyu.ptr);
}

public override bool IsStaticOrKinematicObject(BulletBody obj)
{
    BulletBodyUnman bodyu = obj as BulletBodyUnman;
    return BSAPICPP.IsStaticOrKinematicObject2(bodyu.ptr);
}

public override bool HasContactResponse(BulletBody obj)
{
    BulletBodyUnman bodyu = obj as BulletBodyUnman;
    return BSAPICPP.HasContactResponse2(bodyu.ptr);
}

public override void SetCollisionShape(BulletWorld world, BulletBody obj, BulletShape shape)
{
    BulletWorldUnman worldu = world as BulletWorldUnman;
    BulletBodyUnman bodyu = obj as BulletBodyUnman;
    BulletShapeUnman shapeu = shape as BulletShapeUnman;
    if (worldu != null && bodyu != null)
    {
        // Special case to allow the caller to zero out the reference to any physical shape
        if (shapeu != null)
            BSAPICPP.SetCollisionShape2(worldu.ptr, bodyu.ptr, shapeu.ptr);
        else
            BSAPICPP.SetCollisionShape2(worldu.ptr, bodyu.ptr, IntPtr.Zero);
    }
}

public override BulletShape GetCollisionShape(BulletBody obj)
{
    BulletBodyUnman bodyu = obj as BulletBodyUnman;
    return new BulletShapeUnman(BSAPICPP.GetCollisionShape2(bodyu.ptr), BSPhysicsShapeType.SHAPE_UNKNOWN);
}

public override int GetActivationState(BulletBody obj)
{
    BulletBodyUnman bodyu = obj as BulletBodyUnman;
    return BSAPICPP.GetActivationState2(bodyu.ptr);
}

public override void SetActivationState(BulletBody obj, int state)
{
    BulletBodyUnman bodyu = obj as BulletBodyUnman;
    BSAPICPP.SetActivationState2(bodyu.ptr, state);
}

public override void SetDeactivationTime(BulletBody obj, float dtime)
{
    BulletBodyUnman bodyu = obj as BulletBodyUnman;
    BSAPICPP.SetDeactivationTime2(bodyu.ptr, dtime);
}

public override float GetDeactivationTime(BulletBody obj)
{
    BulletBodyUnman bodyu = obj as BulletBodyUnman;
    return BSAPICPP.GetDeactivationTime2(bodyu.ptr);
}

public override void ForceActivationState(BulletBody obj, ActivationState state)
{
    BulletBodyUnman bodyu = obj as BulletBodyUnman;
    BSAPICPP.ForceActivationState2(bodyu.ptr, state);
}

public override void Activate(BulletBody obj, bool forceActivation)
{
    BulletBodyUnman bodyu = obj as BulletBodyUnman;
    BSAPICPP.Activate2(bodyu.ptr, forceActivation);
}

public override bool IsActive(BulletBody obj)
{
    BulletBodyUnman bodyu = obj as BulletBodyUnman;
    return BSAPICPP.IsActive2(bodyu.ptr);
}

public override void SetRestitution(BulletBody obj, float val)
{
    BulletBodyUnman bodyu = obj as BulletBodyUnman;
    BSAPICPP.SetRestitution2(bodyu.ptr, val);
}

public override float GetRestitution(BulletBody obj)
{
    BulletBodyUnman bodyu = obj as BulletBodyUnman;
    return BSAPICPP.GetRestitution2(bodyu.ptr);
}

public override void SetFriction(BulletBody obj, float val)
{
    BulletBodyUnman bodyu = obj as BulletBodyUnman;
    BSAPICPP.SetFriction2(bodyu.ptr, val);
}

public override float GetFriction(BulletBody obj)
{
    BulletBodyUnman bodyu = obj as BulletBodyUnman;
    return BSAPICPP.GetFriction2(bodyu.ptr);
}

public override Vector3 GetPosition(BulletBody obj)
{
    BulletBodyUnman bodyu = obj as BulletBodyUnman;
    return BSAPICPP.GetPosition2(bodyu.ptr);
}

public override Quaternion GetOrientation(BulletBody obj)
{
    BulletBodyUnman bodyu = obj as BulletBodyUnman;
    return BSAPICPP.GetOrientation2(bodyu.ptr);
}

public override void SetTranslation(BulletBody obj, Vector3 position, Quaternion rotation)
{
    BulletBodyUnman bodyu = obj as BulletBodyUnman;
    BSAPICPP.SetTranslation2(bodyu.ptr, position, rotation);
}

    /*
public override IntPtr GetBroadphaseHandle(BulletBody obj)
{
    BulletBodyUnman bodyu = obj as BulletBodyUnman;
    return BSAPICPP.GetBroadphaseHandle2(bodyu.ptr);
}

public override void SetBroadphaseHandle(BulletBody obj, IntPtr handle)
{
    BulletBodyUnman bodyu = obj as BulletBodyUnman;
    BSAPICPP.SetUserPointer2(bodyu.ptr, handle);
}
     */

public override void SetInterpolationLinearVelocity(BulletBody obj, Vector3 vel)
{
    BulletBodyUnman bodyu = obj as BulletBodyUnman;
    BSAPICPP.SetInterpolationLinearVelocity2(bodyu.ptr, vel);
}

public override void SetInterpolationAngularVelocity(BulletBody obj, Vector3 vel)
{
    BulletBodyUnman bodyu = obj as BulletBodyUnman;
    BSAPICPP.SetInterpolationAngularVelocity2(bodyu.ptr, vel);
}

public override void SetInterpolationVelocity(BulletBody obj, Vector3 linearVel, Vector3 angularVel)
{
    BulletBodyUnman bodyu = obj as BulletBodyUnman;
    BSAPICPP.SetInterpolationVelocity2(bodyu.ptr, linearVel, angularVel);
}

public override float GetHitFraction(BulletBody obj)
{
    BulletBodyUnman bodyu = obj as BulletBodyUnman;
    return BSAPICPP.GetHitFraction2(bodyu.ptr);
}

public override void SetHitFraction(BulletBody obj, float val)
{
    BulletBodyUnman bodyu = obj as BulletBodyUnman;
    BSAPICPP.SetHitFraction2(bodyu.ptr, val);
}

public override CollisionFlags GetCollisionFlags(BulletBody obj)
{
    BulletBodyUnman bodyu = obj as BulletBodyUnman;
    return BSAPICPP.GetCollisionFlags2(bodyu.ptr);
}

public override CollisionFlags SetCollisionFlags(BulletBody obj, CollisionFlags flags)
{
    BulletBodyUnman bodyu = obj as BulletBodyUnman;
    return BSAPICPP.SetCollisionFlags2(bodyu.ptr, flags);
}

public override CollisionFlags AddToCollisionFlags(BulletBody obj, CollisionFlags flags)
{
    BulletBodyUnman bodyu = obj as BulletBodyUnman;
    return BSAPICPP.AddToCollisionFlags2(bodyu.ptr, flags);
}

public override CollisionFlags RemoveFromCollisionFlags(BulletBody obj, CollisionFlags flags)
{
    BulletBodyUnman bodyu = obj as BulletBodyUnman;
    return BSAPICPP.RemoveFromCollisionFlags2(bodyu.ptr, flags);
}

public override float GetCcdMotionThreshold(BulletBody obj)
{
    BulletBodyUnman bodyu = obj as BulletBodyUnman;
    return BSAPICPP.GetCcdMotionThreshold2(bodyu.ptr);
}


public override void SetCcdMotionThreshold(BulletBody obj, float val)
{
    BulletBodyUnman bodyu = obj as BulletBodyUnman;
    BSAPICPP.SetCcdMotionThreshold2(bodyu.ptr, val);
}

public override float GetCcdSweptSphereRadius(BulletBody obj)
{
    BulletBodyUnman bodyu = obj as BulletBodyUnman;
    return BSAPICPP.GetCcdSweptSphereRadius2(bodyu.ptr);
}

public override void SetCcdSweptSphereRadius(BulletBody obj, float val)
{
    BulletBodyUnman bodyu = obj as BulletBodyUnman;
    BSAPICPP.SetCcdSweptSphereRadius2(bodyu.ptr, val);
}

public override IntPtr GetUserPointer(BulletBody obj)
{
    BulletBodyUnman bodyu = obj as BulletBodyUnman;
    return BSAPICPP.GetUserPointer2(bodyu.ptr);
}

public override void SetUserPointer(BulletBody obj, IntPtr val)
{
    BulletBodyUnman bodyu = obj as BulletBodyUnman;
    BSAPICPP.SetUserPointer2(bodyu.ptr, val);
}

// =====================================================================================
// btRigidBody entries
public override void ApplyGravity(BulletBody obj)
{
    BulletBodyUnman bodyu = obj as BulletBodyUnman;
    BSAPICPP.ApplyGravity2(bodyu.ptr);
}

public override void SetGravity(BulletBody obj, Vector3 val)
{
    BulletBodyUnman bodyu = obj as BulletBodyUnman;
    BSAPICPP.SetGravity2(bodyu.ptr, val);
}

public override Vector3 GetGravity(BulletBody obj)
{
    BulletBodyUnman bodyu = obj as BulletBodyUnman;
    return BSAPICPP.GetGravity2(bodyu.ptr);
}

public override void SetDamping(BulletBody obj, float lin_damping, float ang_damping)
{
    BulletBodyUnman bodyu = obj as BulletBodyUnman;
    BSAPICPP.SetDamping2(bodyu.ptr, lin_damping, ang_damping);
}

public override void SetLinearDamping(BulletBody obj, float lin_damping)
{
    BulletBodyUnman bodyu = obj as BulletBodyUnman;
    BSAPICPP.SetLinearDamping2(bodyu.ptr, lin_damping);
}

public override void SetAngularDamping(BulletBody obj, float ang_damping)
{
    BulletBodyUnman bodyu = obj as BulletBodyUnman;
    BSAPICPP.SetAngularDamping2(bodyu.ptr, ang_damping);
}

public override float GetLinearDamping(BulletBody obj)
{
    BulletBodyUnman bodyu = obj as BulletBodyUnman;
    return BSAPICPP.GetLinearDamping2(bodyu.ptr);
}

public override float GetAngularDamping(BulletBody obj)
{
    BulletBodyUnman bodyu = obj as BulletBodyUnman;
    return BSAPICPP.GetAngularDamping2(bodyu.ptr);
}

public override float GetLinearSleepingThreshold(BulletBody obj)
{
    BulletBodyUnman bodyu = obj as BulletBodyUnman;
    return BSAPICPP.GetLinearSleepingThreshold2(bodyu.ptr);
}

public override void ApplyDamping(BulletBody obj, float timeStep)
{
    BulletBodyUnman bodyu = obj as BulletBodyUnman;
    BSAPICPP.ApplyDamping2(bodyu.ptr, timeStep);
}

public override void SetMassProps(BulletBody obj, float mass, Vector3 inertia)
{
    BulletBodyUnman bodyu = obj as BulletBodyUnman;
    BSAPICPP.SetMassProps2(bodyu.ptr, mass, inertia);
}

public override Vector3 GetLinearFactor(BulletBody obj)
{
    BulletBodyUnman bodyu = obj as BulletBodyUnman;
    return BSAPICPP.GetLinearFactor2(bodyu.ptr);
}

public override void SetLinearFactor(BulletBody obj, Vector3 factor)
{
    BulletBodyUnman bodyu = obj as BulletBodyUnman;
    BSAPICPP.SetLinearFactor2(bodyu.ptr, factor);
}

public override void SetCenterOfMassByPosRot(BulletBody obj, Vector3 pos, Quaternion rot)
{
    BulletBodyUnman bodyu = obj as BulletBodyUnman;
    BSAPICPP.SetCenterOfMassByPosRot2(bodyu.ptr, pos, rot);
}

// Add a force to the object as if its mass is one.
// Deep down in Bullet: m_totalForce += force*m_linearFactor;
public override void ApplyCentralForce(BulletBody obj, Vector3 force)
{
    BulletBodyUnman bodyu = obj as BulletBodyUnman;
    BSAPICPP.ApplyCentralForce2(bodyu.ptr, force);
}

// Set the force being applied to the object as if its mass is one.
public override void SetObjectForce(BulletBody obj, Vector3 force)
{
    BulletBodyUnman bodyu = obj as BulletBodyUnman;
    BSAPICPP.SetObjectForce2(bodyu.ptr, force);
}

public override Vector3 GetTotalForce(BulletBody obj)
{
    BulletBodyUnman bodyu = obj as BulletBodyUnman;
    return BSAPICPP.GetTotalForce2(bodyu.ptr);
}

public override Vector3 GetTotalTorque(BulletBody obj)
{
    BulletBodyUnman bodyu = obj as BulletBodyUnman;
    return BSAPICPP.GetTotalTorque2(bodyu.ptr);
}

public override Vector3 GetInvInertiaDiagLocal(BulletBody obj)
{
    BulletBodyUnman bodyu = obj as BulletBodyUnman;
    return BSAPICPP.GetInvInertiaDiagLocal2(bodyu.ptr);
}

public override void SetInvInertiaDiagLocal(BulletBody obj, Vector3 inert)
{
    BulletBodyUnman bodyu = obj as BulletBodyUnman;
    BSAPICPP.SetInvInertiaDiagLocal2(bodyu.ptr, inert);
}

public override void SetSleepingThresholds(BulletBody obj, float lin_threshold, float ang_threshold)
{
    BulletBodyUnman bodyu = obj as BulletBodyUnman;
    BSAPICPP.SetSleepingThresholds2(bodyu.ptr, lin_threshold, ang_threshold);
}

// Deep down in Bullet: m_totalTorque += torque*m_angularFactor;
public override void ApplyTorque(BulletBody obj, Vector3 torque)
{
    BulletBodyUnman bodyu = obj as BulletBodyUnman;
    BSAPICPP.ApplyTorque2(bodyu.ptr, torque);
}

// Apply force at the given point. Will add torque to the object.
// Deep down in Bullet: applyCentralForce(force);
//              		applyTorque(rel_pos.cross(force*m_linearFactor));
public override void ApplyForce(BulletBody obj, Vector3 force, Vector3 pos)
{
    BulletBodyUnman bodyu = obj as BulletBodyUnman;
    BSAPICPP.ApplyForce2(bodyu.ptr, force, pos);
}

// Apply impulse to the object. Same as "ApplycentralForce" but force scaled by object's mass.
// Deep down in Bullet: m_linearVelocity += impulse *m_linearFactor * m_inverseMass;
public override void ApplyCentralImpulse(BulletBody obj, Vector3 imp)
{
    BulletBodyUnman bodyu = obj as BulletBodyUnman;
    BSAPICPP.ApplyCentralImpulse2(bodyu.ptr, imp);
}

// Apply impulse to the object's torque. Force is scaled by object's mass.
// Deep down in Bullet: m_angularVelocity += m_invInertiaTensorWorld * torque * m_angularFactor;
public override void ApplyTorqueImpulse(BulletBody obj, Vector3 imp)
{
    BulletBodyUnman bodyu = obj as BulletBodyUnman;
    BSAPICPP.ApplyTorqueImpulse2(bodyu.ptr, imp);
}

// Apply impulse at the point given. For is scaled by object's mass and effects both linear and angular forces.
// Deep down in Bullet: applyCentralImpulse(impulse);
//          			applyTorqueImpulse(rel_pos.cross(impulse*m_linearFactor));
public override void ApplyImpulse(BulletBody obj, Vector3 imp, Vector3 pos)
{
    BulletBodyUnman bodyu = obj as BulletBodyUnman;
    BSAPICPP.ApplyImpulse2(bodyu.ptr, imp, pos);
}

public override void ClearForces(BulletBody obj)
{
    BulletBodyUnman bodyu = obj as BulletBodyUnman;
    BSAPICPP.ClearForces2(bodyu.ptr);
}

public override void ClearAllForces(BulletBody obj)
{
    BulletBodyUnman bodyu = obj as BulletBodyUnman;
    BSAPICPP.ClearAllForces2(bodyu.ptr);
}

public override void UpdateInertiaTensor(BulletBody obj)
{
    BulletBodyUnman bodyu = obj as BulletBodyUnman;
    BSAPICPP.UpdateInertiaTensor2(bodyu.ptr);
}

public override Vector3 GetLinearVelocity(BulletBody obj)
{
    BulletBodyUnman bodyu = obj as BulletBodyUnman;
    return BSAPICPP.GetLinearVelocity2(bodyu.ptr);
}

public override Vector3 GetAngularVelocity(BulletBody obj)
{
    BulletBodyUnman bodyu = obj as BulletBodyUnman;
    return BSAPICPP.GetAngularVelocity2(bodyu.ptr);
}

public override void SetLinearVelocity(BulletBody obj, Vector3 vel)
{
    BulletBodyUnman bodyu = obj as BulletBodyUnman;
    BSAPICPP.SetLinearVelocity2(bodyu.ptr, vel);
}

public override void SetAngularVelocity(BulletBody obj, Vector3 angularVelocity)
{
    BulletBodyUnman bodyu = obj as BulletBodyUnman;
    BSAPICPP.SetAngularVelocity2(bodyu.ptr, angularVelocity);
}

public override Vector3 GetVelocityInLocalPoint(BulletBody obj, Vector3 pos)
{
    BulletBodyUnman bodyu = obj as BulletBodyUnman;
    return BSAPICPP.GetVelocityInLocalPoint2(bodyu.ptr, pos);
}

public override void Translate(BulletBody obj, Vector3 trans)
{
    BulletBodyUnman bodyu = obj as BulletBodyUnman;
    BSAPICPP.Translate2(bodyu.ptr, trans);
}

public override void UpdateDeactivation(BulletBody obj, float timeStep)
{
    BulletBodyUnman bodyu = obj as BulletBodyUnman;
    BSAPICPP.UpdateDeactivation2(bodyu.ptr, timeStep);
}

public override bool WantsSleeping(BulletBody obj)
{
    BulletBodyUnman bodyu = obj as BulletBodyUnman;
    return BSAPICPP.WantsSleeping2(bodyu.ptr);
}

public override void SetAngularFactor(BulletBody obj, float factor)
{
    BulletBodyUnman bodyu = obj as BulletBodyUnman;
    BSAPICPP.SetAngularFactor2(bodyu.ptr, factor);
}

public override void SetAngularFactorV(BulletBody obj, Vector3 factor)
{
    BulletBodyUnman bodyu = obj as BulletBodyUnman;
    BSAPICPP.SetAngularFactorV2(bodyu.ptr, factor);
}

public override Vector3 GetAngularFactor(BulletBody obj)
{
    BulletBodyUnman bodyu = obj as BulletBodyUnman;
    return BSAPICPP.GetAngularFactor2(bodyu.ptr);
}

public override bool IsInWorld(BulletWorld world, BulletBody obj)
{
    BulletBodyUnman bodyu = obj as BulletBodyUnman;
    return BSAPICPP.IsInWorld2(bodyu.ptr);
}

public override void AddConstraintRef(BulletBody obj, BulletConstraint constrain)
{
    BulletBodyUnman bodyu = obj as BulletBodyUnman;
    BulletConstraintUnman constrainu = constrain as BulletConstraintUnman;
    BSAPICPP.AddConstraintRef2(bodyu.ptr, constrainu.ptr);
}

public override void RemoveConstraintRef(BulletBody obj, BulletConstraint constrain)
{
    BulletBodyUnman bodyu = obj as BulletBodyUnman;
    BulletConstraintUnman constrainu = constrain as BulletConstraintUnman;
    BSAPICPP.RemoveConstraintRef2(bodyu.ptr, constrainu.ptr);
}

public override BulletConstraint GetConstraintRef(BulletBody obj, int index)
{
    BulletBodyUnman bodyu = obj as BulletBodyUnman;
    return new BulletConstraintUnman(BSAPICPP.GetConstraintRef2(bodyu.ptr, index));
}

public override int GetNumConstraintRefs(BulletBody obj)
{
    BulletBodyUnman bodyu = obj as BulletBodyUnman;
    return BSAPICPP.GetNumConstraintRefs2(bodyu.ptr);
}

public override bool SetCollisionGroupMask(BulletBody body, uint filter, uint mask)
{
    BulletBodyUnman bodyu = body as BulletBodyUnman;
    return BSAPICPP.SetCollisionGroupMask2(bodyu.ptr, filter, mask);
}

// =====================================================================================
// btCollisionShape entries

public override float GetAngularMotionDisc(BulletShape shape)
{
    BulletShapeUnman shapeu = shape as BulletShapeUnman;
    return BSAPICPP.GetAngularMotionDisc2(shapeu.ptr);
}

public override float GetContactBreakingThreshold(BulletShape shape, float defaultFactor)
{
    BulletShapeUnman shapeu = shape as BulletShapeUnman;
    return BSAPICPP.GetContactBreakingThreshold2(shapeu.ptr, defaultFactor);
}

public override bool IsPolyhedral(BulletShape shape)
{
    BulletShapeUnman shapeu = shape as BulletShapeUnman;
    return BSAPICPP.IsPolyhedral2(shapeu.ptr);
}

public override bool IsConvex2d(BulletShape shape)
{
    BulletShapeUnman shapeu = shape as BulletShapeUnman;
    return BSAPICPP.IsConvex2d2(shapeu.ptr);
}

public override bool IsConvex(BulletShape shape)
{
    BulletShapeUnman shapeu = shape as BulletShapeUnman;
    return BSAPICPP.IsConvex2(shapeu.ptr);
}

public override bool IsNonMoving(BulletShape shape)
{
    BulletShapeUnman shapeu = shape as BulletShapeUnman;
    return BSAPICPP.IsNonMoving2(shapeu.ptr);
}

public override bool IsConcave(BulletShape shape)
{
    BulletShapeUnman shapeu = shape as BulletShapeUnman;
    return BSAPICPP.IsConcave2(shapeu.ptr);
}

public override bool IsCompound(BulletShape shape)
{
    BulletShapeUnman shapeu = shape as BulletShapeUnman;
    return BSAPICPP.IsCompound2(shapeu.ptr);
}

public override bool IsSoftBody(BulletShape shape)
{
    BulletShapeUnman shapeu = shape as BulletShapeUnman;
    return BSAPICPP.IsSoftBody2(shapeu.ptr);
}

public override bool IsInfinite(BulletShape shape)
{
    BulletShapeUnman shapeu = shape as BulletShapeUnman;
    return BSAPICPP.IsInfinite2(shapeu.ptr);
}

public override void SetLocalScaling(BulletShape shape, Vector3 scale)
{
    BulletShapeUnman shapeu = shape as BulletShapeUnman;
    BSAPICPP.SetLocalScaling2(shapeu.ptr, scale);
}

public override Vector3 GetLocalScaling(BulletShape shape)
{
    BulletShapeUnman shapeu = shape as BulletShapeUnman;
    return BSAPICPP.GetLocalScaling2(shapeu.ptr);
}

public override Vector3 CalculateLocalInertia(BulletShape shape, float mass)
{
    BulletShapeUnman shapeu = shape as BulletShapeUnman;
    return BSAPICPP.CalculateLocalInertia2(shapeu.ptr, mass);
}

public override int GetShapeType(BulletShape shape)
{
    BulletShapeUnman shapeu = shape as BulletShapeUnman;
    return BSAPICPP.GetShapeType2(shapeu.ptr);
}

public override void SetMargin(BulletShape shape, float val)
{
    BulletShapeUnman shapeu = shape as BulletShapeUnman;
    BSAPICPP.SetMargin2(shapeu.ptr, val);
}

public override float GetMargin(BulletShape shape)
{
    BulletShapeUnman shapeu = shape as BulletShapeUnman;
    return BSAPICPP.GetMargin2(shapeu.ptr);
}

// =====================================================================================
// Debugging
public override void DumpRigidBody(BulletWorld world, BulletBody collisionObject)
{
    BulletWorldUnman worldu = world as BulletWorldUnman;
    BulletBodyUnman bodyu = collisionObject as BulletBodyUnman;
    BSAPICPP.DumpRigidBody2(worldu.ptr, bodyu.ptr);
}

public override void DumpCollisionShape(BulletWorld world, BulletShape collisionShape)
{
    BulletWorldUnman worldu = world as BulletWorldUnman;
    BulletShapeUnman shapeu = collisionShape as BulletShapeUnman;
    BSAPICPP.DumpCollisionShape2(worldu.ptr, shapeu.ptr);
}

public override void DumpConstraint(BulletWorld world, BulletConstraint constrain)
{
    BulletWorldUnman worldu = world as BulletWorldUnman;
    BulletConstraintUnman constrainu = constrain as BulletConstraintUnman;
    BSAPICPP.DumpConstraint2(worldu.ptr, constrainu.ptr);
}

public override void DumpActivationInfo(BulletWorld world)
{
    BulletWorldUnman worldu = world as BulletWorldUnman;
    BSAPICPP.DumpActivationInfo2(worldu.ptr);
}

public override void DumpAllInfo(BulletWorld world)
{
    BulletWorldUnman worldu = world as BulletWorldUnman;
    BSAPICPP.DumpAllInfo2(worldu.ptr);
}

public override void DumpPhysicsStatistics(BulletWorld world)
{
    BulletWorldUnman worldu = world as BulletWorldUnman;
    BSAPICPP.DumpPhysicsStatistics2(worldu.ptr);
}
public override void ResetBroadphasePool(BulletWorld world)
{
    BulletWorldUnman worldu = world as BulletWorldUnman;
    BSAPICPP.ResetBroadphasePool(worldu.ptr);
}
public override void ResetConstraintSolver(BulletWorld world)
{
    BulletWorldUnman worldu = world as BulletWorldUnman;
    BSAPICPP.ResetConstraintSolver(worldu.ptr);
}

// =====================================================================================
// =====================================================================================
// =====================================================================================
// =====================================================================================
// =====================================================================================
// The actual interface to the unmanaged code
static class BSAPICPP
{
// ===============================================================================
// Link back to the managed code for outputting log messages
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
public delegate void DebugLogCallback([MarshalAs(UnmanagedType.LPStr)]string msg);

// ===============================================================================
// Initialization and simulation
[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern IntPtr Initialize2(Vector3 maxPosition, IntPtr parms,
											int maxCollisions,  IntPtr collisionArray,
											int maxUpdates, IntPtr updateArray,
                                            DebugLogCallback logRoutine);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern int PhysicsStep2(IntPtr world, float timeStep, int maxSubSteps, float fixedTimeStep,
                        out int updatedEntityCount, out int collidersCount);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern void Shutdown2(IntPtr sim);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern bool PushUpdate2(IntPtr obj);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern bool UpdateParameter2(IntPtr world, uint localID, String parm, float value);

// =====================================================================================
// Mesh, hull, shape and body creation helper routines
[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern IntPtr CreateMeshShape2(IntPtr world,
                int indicesCount, [MarshalAs(UnmanagedType.LPArray)] int[] indices,
                int verticesCount, [MarshalAs(UnmanagedType.LPArray)] float[] vertices );

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern IntPtr CreateGImpactShape2(IntPtr world,
                int indicesCount, [MarshalAs(UnmanagedType.LPArray)] int[] indices,
                int verticesCount, [MarshalAs(UnmanagedType.LPArray)] float[] vertices );

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern IntPtr CreateHullShape2(IntPtr world,
                int hullCount, [MarshalAs(UnmanagedType.LPArray)] float[] hulls);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern IntPtr BuildHullShapeFromMesh2(IntPtr world, IntPtr meshShape, HACDParams parms);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern IntPtr BuildConvexHullShapeFromMesh2(IntPtr world, IntPtr meshShape);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern IntPtr CreateConvexHullShape2(IntPtr world,
                int indicesCount, [MarshalAs(UnmanagedType.LPArray)] int[] indices,
                int verticesCount, [MarshalAs(UnmanagedType.LPArray)] float[] vertices );

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern IntPtr BuildNativeShape2(IntPtr world, ShapeData shapeData);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern bool IsNativeShape2(IntPtr shape);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern void SetShapeCollisionMargin(IntPtr shape, float margin);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern IntPtr BuildCapsuleShape2(IntPtr world, float radius, float height, Vector3 scale);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern IntPtr CreateCompoundShape2(IntPtr sim, bool enableDynamicAabbTree);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern int GetNumberOfCompoundChildren2(IntPtr cShape);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern void AddChildShapeToCompoundShape2(IntPtr cShape, IntPtr addShape, Vector3 pos, Quaternion rot);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern IntPtr GetChildShapeFromCompoundShapeIndex2(IntPtr cShape, int indx);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern IntPtr RemoveChildShapeFromCompoundShapeIndex2(IntPtr cShape, int indx);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern void RemoveChildShapeFromCompoundShape2(IntPtr cShape, IntPtr removeShape);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern void UpdateChildTransform2(IntPtr pShape, int childIndex, Vector3 pos, Quaternion rot, bool shouldRecalculateLocalAabb);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern void RecalculateCompoundShapeLocalAabb2(IntPtr cShape);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern IntPtr DuplicateCollisionShape2(IntPtr sim, IntPtr srcShape, uint id);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern bool DeleteCollisionShape2(IntPtr world, IntPtr shape);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern int GetBodyType2(IntPtr obj);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern IntPtr CreateBodyFromShape2(IntPtr sim, IntPtr shape, uint id, Vector3 pos, Quaternion rot);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern IntPtr CreateBodyWithDefaultMotionState2(IntPtr shape, uint id, Vector3 pos, Quaternion rot);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern IntPtr CreateGhostFromShape2(IntPtr sim, IntPtr shape, uint id, Vector3 pos, Quaternion rot);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern void DestroyObject2(IntPtr sim, IntPtr obj);

// =====================================================================================
// Terrain creation and helper routines
[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern IntPtr CreateGroundPlaneShape2(uint id, float height, float collisionMargin);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern IntPtr CreateTerrainShape2(uint id, Vector3 size, float minHeight, float maxHeight,
                                            [MarshalAs(UnmanagedType.LPArray)] float[] heightMap,
                                            float scaleFactor, float collisionMargin);

// =====================================================================================
// Constraint creation and helper routines
[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern IntPtr Create6DofConstraint2(IntPtr world, IntPtr obj1, IntPtr obj2,
                    Vector3 frame1loc, Quaternion frame1rot,
                    Vector3 frame2loc, Quaternion frame2rot,
                    bool useLinearReferenceFrameA, bool disableCollisionsBetweenLinkedBodies);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern IntPtr Create6DofConstraintToPoint2(IntPtr world, IntPtr obj1, IntPtr obj2,
                    Vector3 joinPoint,
                    bool useLinearReferenceFrameA, bool disableCollisionsBetweenLinkedBodies);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern IntPtr Create6DofConstraintFixed2(IntPtr world, IntPtr obj1,
                    Vector3 frameInBloc, Quaternion frameInBrot,
                    bool useLinearReferenceFrameB, bool disableCollisionsBetweenLinkedBodies);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern IntPtr Create6DofSpringConstraint2(IntPtr world, IntPtr obj1, IntPtr obj2,
                    Vector3 frame1loc, Quaternion frame1rot,
                    Vector3 frame2loc, Quaternion frame2rot,
                    bool useLinearReferenceFrameA, bool disableCollisionsBetweenLinkedBodies);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern IntPtr CreateHingeConstraint2(IntPtr world, IntPtr obj1, IntPtr obj2,
                    Vector3 pivotinA, Vector3 pivotinB,
                    Vector3 axisInA, Vector3 axisInB,
                    bool useLinearReferenceFrameA, bool disableCollisionsBetweenLinkedBodies);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern IntPtr CreateSliderConstraint2(IntPtr world, IntPtr obj1, IntPtr obj2,
                    Vector3 frameInAloc, Quaternion frameInArot,
                    Vector3 frameInBloc, Quaternion frameInBrot,
                    bool useLinearReferenceFrameA, bool disableCollisionsBetweenLinkedBodies);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern IntPtr CreateConeTwistConstraint2(IntPtr world, IntPtr obj1, IntPtr obj2,
                    Vector3 frameInAloc, Quaternion frameInArot,
                    Vector3 frameInBloc, Quaternion frameInBrot,
                    bool disableCollisionsBetweenLinkedBodies);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern IntPtr CreateGearConstraint2(IntPtr world, IntPtr obj1, IntPtr obj2,
                    Vector3 axisInA, Vector3 axisInB,
                    float ratio, bool disableCollisionsBetweenLinkedBodies);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern IntPtr CreatePoint2PointConstraint2(IntPtr world, IntPtr obj1, IntPtr obj2,
                    Vector3 pivotInA, Vector3 pivotInB,
                    bool disableCollisionsBetweenLinkedBodies);


[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern void SetConstraintEnable2(IntPtr constrain, float numericTrueFalse);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern void SetConstraintNumSolverIterations2(IntPtr constrain, float iterations);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern bool SetFrames2(IntPtr constrain,
                Vector3 frameA, Quaternion frameArot, Vector3 frameB, Quaternion frameBrot);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern bool SetLinearLimits2(IntPtr constrain, Vector3 low, Vector3 hi);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern bool SetAngularLimits2(IntPtr constrain, Vector3 low, Vector3 hi);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern bool UseFrameOffset2(IntPtr constrain, float enable);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern bool TranslationalLimitMotor2(IntPtr constrain, float enable, float targetVel, float maxMotorForce);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern bool SetBreakingImpulseThreshold2(IntPtr constrain, float threshold);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern bool HingeSetLimits2(IntPtr constrain, float low, float high, float softness, float bias, float relaxation);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern bool ConstraintSpringEnable2(IntPtr constrain, int index, float numericTrueFalse);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern bool ConstraintSpringSetEquilibriumPoint2(IntPtr constrain, int index, float equilibriumPoint);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern bool ConstraintSpringSetStiffness2(IntPtr constrain, int index, float stiffness);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern bool ConstraintSpringSetDamping2(IntPtr constrain, int index, float damping);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern bool SliderSetLimits2(IntPtr constrain, int lowerUpper, int linAng, float val);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern bool SliderSet2(IntPtr constrain, int softRestDamp, int dirLimOrtho, int linAng, float val);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern bool SliderMotorEnable2(IntPtr constrain, int linAng, float numericTrueFalse);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern bool SliderMotor2(IntPtr constrain, int forceVel, int linAng, float val);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern bool CalculateTransforms2(IntPtr constrain);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern bool SetConstraintParam2(IntPtr constrain, ConstraintParams paramIndex, float value, ConstraintParamAxis axis);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern bool DestroyConstraint2(IntPtr world, IntPtr constrain);

// =====================================================================================
// btCollisionWorld entries
[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern void UpdateSingleAabb2(IntPtr world, IntPtr obj);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern void UpdateAabbs2(IntPtr world);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern bool GetForceUpdateAllAabbs2(IntPtr world);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern void SetForceUpdateAllAabbs2(IntPtr world, bool force);

// =====================================================================================
// btDynamicsWorld entries
[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern bool AddObjectToWorld2(IntPtr world, IntPtr obj);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern bool RemoveObjectFromWorld2(IntPtr world, IntPtr obj);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern bool ClearCollisionProxyCache2(IntPtr world, IntPtr obj);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern bool AddConstraintToWorld2(IntPtr world, IntPtr constrain, bool disableCollisionsBetweenLinkedObjects);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern bool RemoveConstraintFromWorld2(IntPtr world, IntPtr constrain);
// =====================================================================================
// btCollisionObject entries
[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern Vector3 GetAnisotripicFriction2(IntPtr constrain);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern Vector3 SetAnisotripicFriction2(IntPtr constrain, Vector3 frict);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern bool HasAnisotripicFriction2(IntPtr constrain);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern void SetContactProcessingThreshold2(IntPtr obj, float val);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern float GetContactProcessingThreshold2(IntPtr obj);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern bool IsStaticObject2(IntPtr obj);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern bool IsKinematicObject2(IntPtr obj);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern bool IsStaticOrKinematicObject2(IntPtr obj);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern bool HasContactResponse2(IntPtr obj);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern void SetCollisionShape2(IntPtr sim, IntPtr obj, IntPtr shape);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern IntPtr GetCollisionShape2(IntPtr obj);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern int GetActivationState2(IntPtr obj);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern void SetActivationState2(IntPtr obj, int state);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern void SetDeactivationTime2(IntPtr obj, float dtime);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern float GetDeactivationTime2(IntPtr obj);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern void ForceActivationState2(IntPtr obj, ActivationState state);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern void Activate2(IntPtr obj, bool forceActivation);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern bool IsActive2(IntPtr obj);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern void SetRestitution2(IntPtr obj, float val);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern float GetRestitution2(IntPtr obj);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern void SetFriction2(IntPtr obj, float val);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern float GetFriction2(IntPtr obj);

    /* Haven't defined the type 'Transform'
[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern Transform GetWorldTransform2(IntPtr obj);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern void setWorldTransform2(IntPtr obj, Transform trans);
     */

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern Vector3 GetPosition2(IntPtr obj);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern Quaternion GetOrientation2(IntPtr obj);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern void SetTranslation2(IntPtr obj, Vector3 position, Quaternion rotation);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern IntPtr GetBroadphaseHandle2(IntPtr obj);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern void SetBroadphaseHandle2(IntPtr obj, IntPtr handle);

    /*
[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern Transform GetInterpolationWorldTransform2(IntPtr obj);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern void SetInterpolationWorldTransform2(IntPtr obj, Transform trans);
     */

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern void SetInterpolationLinearVelocity2(IntPtr obj, Vector3 vel);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern void SetInterpolationAngularVelocity2(IntPtr obj, Vector3 vel);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern void SetInterpolationVelocity2(IntPtr obj, Vector3 linearVel, Vector3 angularVel);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern float GetHitFraction2(IntPtr obj);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern void SetHitFraction2(IntPtr obj, float val);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern CollisionFlags GetCollisionFlags2(IntPtr obj);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern CollisionFlags SetCollisionFlags2(IntPtr obj, CollisionFlags flags);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern CollisionFlags AddToCollisionFlags2(IntPtr obj, CollisionFlags flags);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern CollisionFlags RemoveFromCollisionFlags2(IntPtr obj, CollisionFlags flags);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern float GetCcdMotionThreshold2(IntPtr obj);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern void SetCcdMotionThreshold2(IntPtr obj, float val);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern float GetCcdSweptSphereRadius2(IntPtr obj);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern void SetCcdSweptSphereRadius2(IntPtr obj, float val);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern IntPtr GetUserPointer2(IntPtr obj);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern void SetUserPointer2(IntPtr obj, IntPtr val);

// =====================================================================================
// btRigidBody entries
[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern void ApplyGravity2(IntPtr obj);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern void SetGravity2(IntPtr obj, Vector3 val);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern Vector3 GetGravity2(IntPtr obj);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern void SetDamping2(IntPtr obj, float lin_damping, float ang_damping);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern void SetLinearDamping2(IntPtr obj, float lin_damping);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern void SetAngularDamping2(IntPtr obj, float ang_damping);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern float GetLinearDamping2(IntPtr obj);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern float GetAngularDamping2(IntPtr obj);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern float GetLinearSleepingThreshold2(IntPtr obj);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern float GetAngularSleepingThreshold2(IntPtr obj);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern void ApplyDamping2(IntPtr obj, float timeStep);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern void SetMassProps2(IntPtr obj, float mass, Vector3 inertia);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern Vector3 GetLinearFactor2(IntPtr obj);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern void SetLinearFactor2(IntPtr obj, Vector3 factor);

    /*
[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern void SetCenterOfMassTransform2(IntPtr obj, Transform trans);
     */

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern void SetCenterOfMassByPosRot2(IntPtr obj, Vector3 pos, Quaternion rot);

// Add a force to the object as if its mass is one.
[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern void ApplyCentralForce2(IntPtr obj, Vector3 force);

// Set the force being applied to the object as if its mass is one.
[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern void SetObjectForce2(IntPtr obj, Vector3 force);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern Vector3 GetTotalForce2(IntPtr obj);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern Vector3 GetTotalTorque2(IntPtr obj);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern Vector3 GetInvInertiaDiagLocal2(IntPtr obj);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern void SetInvInertiaDiagLocal2(IntPtr obj, Vector3 inert);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern void SetSleepingThresholds2(IntPtr obj, float lin_threshold, float ang_threshold);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern void ApplyTorque2(IntPtr obj, Vector3 torque);

// Apply force at the given point. Will add torque to the object.
[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern void ApplyForce2(IntPtr obj, Vector3 force, Vector3 pos);

// Apply impulse to the object. Same as "ApplycentralForce" but force scaled by object's mass.
[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern void ApplyCentralImpulse2(IntPtr obj, Vector3 imp);

// Apply impulse to the object's torque. Force is scaled by object's mass.
[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern void ApplyTorqueImpulse2(IntPtr obj, Vector3 imp);

// Apply impulse at the point given. For is scaled by object's mass and effects both linear and angular forces.
[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern void ApplyImpulse2(IntPtr obj, Vector3 imp, Vector3 pos);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern void ClearForces2(IntPtr obj);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern void ClearAllForces2(IntPtr obj);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern void UpdateInertiaTensor2(IntPtr obj);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern Vector3 GetCenterOfMassPosition2(IntPtr obj);

    /*
[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern Transform GetCenterOfMassTransform2(IntPtr obj);
     */

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern Vector3 GetLinearVelocity2(IntPtr obj);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern Vector3 GetAngularVelocity2(IntPtr obj);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern void SetLinearVelocity2(IntPtr obj, Vector3 val);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern void SetAngularVelocity2(IntPtr obj, Vector3 angularVelocity);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern Vector3 GetVelocityInLocalPoint2(IntPtr obj, Vector3 pos);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern void Translate2(IntPtr obj, Vector3 trans);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern void UpdateDeactivation2(IntPtr obj, float timeStep);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern bool WantsSleeping2(IntPtr obj);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern void SetAngularFactor2(IntPtr obj, float factor);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern void SetAngularFactorV2(IntPtr obj, Vector3 factor);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern Vector3 GetAngularFactor2(IntPtr obj);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern bool IsInWorld2(IntPtr obj);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern void AddConstraintRef2(IntPtr obj, IntPtr constrain);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern void RemoveConstraintRef2(IntPtr obj, IntPtr constrain);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern IntPtr GetConstraintRef2(IntPtr obj, int index);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern int GetNumConstraintRefs2(IntPtr obj);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern bool SetCollisionGroupMask2(IntPtr body, uint filter, uint mask);

// =====================================================================================
// btCollisionShape entries

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern float GetAngularMotionDisc2(IntPtr shape);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern float GetContactBreakingThreshold2(IntPtr shape, float defaultFactor);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern bool IsPolyhedral2(IntPtr shape);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern bool IsConvex2d2(IntPtr shape);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern bool IsConvex2(IntPtr shape);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern bool IsNonMoving2(IntPtr shape);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern bool IsConcave2(IntPtr shape);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern bool IsCompound2(IntPtr shape);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern bool IsSoftBody2(IntPtr shape);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern bool IsInfinite2(IntPtr shape);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern void SetLocalScaling2(IntPtr shape, Vector3 scale);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern Vector3 GetLocalScaling2(IntPtr shape);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern Vector3 CalculateLocalInertia2(IntPtr shape, float mass);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern int GetShapeType2(IntPtr shape);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern void SetMargin2(IntPtr shape, float val);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern float GetMargin2(IntPtr shape);

// =====================================================================================
// Debugging
[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern void DumpRigidBody2(IntPtr sim, IntPtr collisionObject);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern void DumpCollisionShape2(IntPtr sim, IntPtr collisionShape);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern void DumpMapInfo2(IntPtr sim, IntPtr manInfo);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern void DumpConstraint2(IntPtr sim, IntPtr constrain);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern void DumpActivationInfo2(IntPtr sim);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern void DumpAllInfo2(IntPtr sim);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern void DumpPhysicsStatistics2(IntPtr sim);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern void ResetBroadphasePool(IntPtr sim);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern void ResetConstraintSolver(IntPtr sim);

}

}

}
