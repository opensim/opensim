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

using OpenMetaverse;

namespace OpenSim.Region.Physics.BulletSPlugin
{
public sealed class BSAPIUnman : BSAPITemplate
{

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
    if (BSScene.m_log.IsDebugEnabled || PhysicsScene.PhysicsLogging.Enabled)
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
    return new BulletWorld(0, PhysicsScene, BSAPICPP.Initialize2(maxPosition, m_paramsHandle.AddrOfPinnedObject(),
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
    return BSAPICPP.PhysicsStep2(world.ptr, timeStep, maxSubSteps, fixedTimeStep, out updatedEntityCount, out collidersCount);
}

public override void Shutdown(BulletWorld sim)
{
    BSAPICPP.Shutdown2(sim.ptr);
}

public override bool PushUpdate(BulletBody obj)
{
    return BSAPICPP.PushUpdate2(obj.ptr);
}

public override bool UpdateParameter(BulletWorld world, uint localID, String parm, float value)
{
    return BSAPICPP.UpdateParameter2(world.ptr, localID, parm, value);
}

// =====================================================================================
// Mesh, hull, shape and body creation helper routines
public override BulletShape CreateMeshShape(BulletWorld world,
                int indicesCount, int[] indices,
                int verticesCount, float[] vertices)
{
    return new BulletShape(
                    BSAPICPP.CreateMeshShape2(world.ptr, indicesCount, indices, verticesCount, vertices),
                    BSPhysicsShapeType.SHAPE_MESH);
}

public override BulletShape CreateHullShape(BulletWorld world, int hullCount, float[] hulls)
{
    return new BulletShape(
                    BSAPICPP.CreateHullShape2(world.ptr, hullCount, hulls), 
                    BSPhysicsShapeType.SHAPE_HULL);
}

public override BulletShape BuildHullShapeFromMesh(BulletWorld world, BulletShape meshShape)
{
    return new BulletShape(
                    BSAPICPP.BuildHullShapeFromMesh2(world.ptr, meshShape.ptr),
                    BSPhysicsShapeType.SHAPE_HULL);
}

public override BulletShape BuildNativeShape( BulletWorld world, ShapeData shapeData)
{
    return new BulletShape(
                    BSAPICPP.BuildNativeShape2(world.ptr, shapeData), 
                    shapeData.Type);
}

public override bool IsNativeShape(BulletShape shape)
{
    if (shape.HasPhysicalShape)
        return BSAPICPP.IsNativeShape2(shape.ptr);
    return false;
}

public override void SetShapeCollisionMargin(BulletShape shape, float margin)
{
    if (shape.HasPhysicalShape)
        BSAPICPP.SetShapeCollisionMargin2(shape.ptr, margin);
}

public override BulletShape BuildCapsuleShape(BulletWorld world, float radius, float height, Vector3 scale)
{
    return new BulletShape(
                   BSAPICPP.BuildCapsuleShape2(world.ptr, radius, height, scale),
                   BSPhysicsShapeType.SHAPE_CAPSULE);
}

public override BulletShape CreateCompoundShape(BulletWorld sim, bool enableDynamicAabbTree)
{
    return new BulletShape(
                    BSAPICPP.CreateCompoundShape2(sim.ptr, enableDynamicAabbTree),
                    BSPhysicsShapeType.SHAPE_COMPOUND);

}

public override int GetNumberOfCompoundChildren(BulletShape shape)
{
    if (shape.HasPhysicalShape)
        return BSAPICPP.GetNumberOfCompoundChildren2(shape.ptr);
    return 0;
}

public override void AddChildShapeToCompoundShape(BulletShape cShape, BulletShape addShape, Vector3 pos, Quaternion rot)
{
    BSAPICPP.AddChildShapeToCompoundShape2(cShape.ptr, addShape.ptr, pos, rot);
}

public override BulletShape GetChildShapeFromCompoundShapeIndex(BulletShape cShape, int indx)
{
    return new BulletShape(BSAPICPP.GetChildShapeFromCompoundShapeIndex2(cShape.ptr, indx));
}

public override BulletShape RemoveChildShapeFromCompoundShapeIndex(BulletShape cShape, int indx)
{
    return new BulletShape(BSAPICPP.RemoveChildShapeFromCompoundShapeIndex2(cShape.ptr, indx));
}

public override void RemoveChildShapeFromCompoundShape(BulletShape cShape, BulletShape removeShape)
{
    BSAPICPP.RemoveChildShapeFromCompoundShape2(cShape.ptr, removeShape.ptr);
}

public override void RecalculateCompoundShapeLocalAabb(BulletShape cShape)
{
    BSAPICPP.RecalculateCompoundShapeLocalAabb2(cShape.ptr);
}

public override BulletShape DuplicateCollisionShape(BulletWorld sim, BulletShape srcShape, uint id)
{
    return new BulletShape(BSAPICPP.DuplicateCollisionShape2(sim.ptr, srcShape.ptr, id), srcShape.type);
}

public override bool DeleteCollisionShape(BulletWorld world, BulletShape shape)
{
    return BSAPICPP.DeleteCollisionShape2(world.ptr, shape.ptr);
}

public override int GetBodyType(BulletBody obj)
{
    return BSAPICPP.GetBodyType2(obj.ptr);
}

public override BulletBody CreateBodyFromShape(BulletWorld sim, BulletShape shape, uint id, Vector3 pos, Quaternion rot)
{
    return new BulletBody(id, BSAPICPP.CreateBodyFromShape2(sim.ptr, shape.ptr, id, pos, rot));
}

public override BulletBody CreateBodyWithDefaultMotionState(BulletShape shape, uint id, Vector3 pos, Quaternion rot)
{
    return new BulletBody(id, BSAPICPP.CreateBodyWithDefaultMotionState2(shape.ptr, id, pos, rot));
}

public override BulletBody CreateGhostFromShape(BulletWorld sim, BulletShape shape, uint id, Vector3 pos, Quaternion rot)
{
    return new BulletBody(id, BSAPICPP.CreateGhostFromShape2(sim.ptr, shape.ptr, id, pos, rot));
}

public override void DestroyObject(BulletWorld sim, BulletBody obj)
{
    BSAPICPP.DestroyObject2(sim.ptr, obj.ptr);
}

// =====================================================================================
// Terrain creation and helper routines
public override BulletShape CreateGroundPlaneShape(uint id, float height, float collisionMargin)
{
    return new BulletShape(BSAPICPP.CreateGroundPlaneShape2(id, height, collisionMargin), BSPhysicsShapeType.SHAPE_GROUNDPLANE);
}

public override BulletShape CreateTerrainShape(uint id, Vector3 size, float minHeight, float maxHeight, float[] heightMap,
                                float scaleFactor, float collisionMargin)
{
    return new BulletShape(BSAPICPP.CreateTerrainShape2(id, size, minHeight, maxHeight, heightMap, scaleFactor, collisionMargin),
                                                BSPhysicsShapeType.SHAPE_TERRAIN);
}

// =====================================================================================
// Constraint creation and helper routines
public override BulletConstraint Create6DofConstraint(BulletWorld world, BulletBody obj1, BulletBody obj2,
                    Vector3 frame1loc, Quaternion frame1rot,
                    Vector3 frame2loc, Quaternion frame2rot,
                    bool useLinearReferenceFrameA, bool disableCollisionsBetweenLinkedBodies)
{
    return new BulletConstraint(BSAPICPP.Create6DofConstraint2(world.ptr, obj1.ptr, obj2.ptr, frame1loc, frame1rot,
                    frame2loc, frame2rot, useLinearReferenceFrameA, disableCollisionsBetweenLinkedBodies));
}

public override BulletConstraint Create6DofConstraintToPoint(BulletWorld world, BulletBody obj1, BulletBody obj2,
                    Vector3 joinPoint,
                    bool useLinearReferenceFrameA, bool disableCollisionsBetweenLinkedBodies)
{
    return new BulletConstraint(BSAPICPP.Create6DofConstraintToPoint2(world.ptr, obj1.ptr, obj2.ptr,
                    joinPoint, useLinearReferenceFrameA, disableCollisionsBetweenLinkedBodies));
}

public override BulletConstraint CreateHingeConstraint(BulletWorld world, BulletBody obj1, BulletBody obj2,
                    Vector3 pivotinA, Vector3 pivotinB,
                    Vector3 axisInA, Vector3 axisInB,
                    bool useLinearReferenceFrameA, bool disableCollisionsBetweenLinkedBodies)
{
    return new BulletConstraint(BSAPICPP.CreateHingeConstraint2(world.ptr, obj1.ptr, obj2.ptr,
                    pivotinA, pivotinB, axisInA, axisInB, useLinearReferenceFrameA, disableCollisionsBetweenLinkedBodies));
}

public override void SetConstraintEnable(BulletConstraint constrain, float numericTrueFalse)
{
    BSAPICPP.SetConstraintEnable2(constrain.ptr, numericTrueFalse);
}

public override void SetConstraintNumSolverIterations(BulletConstraint constrain, float iterations)
{
    BSAPICPP.SetConstraintNumSolverIterations2(constrain.ptr, iterations);
}

public override bool SetFrames(BulletConstraint constrain,
                Vector3 frameA, Quaternion frameArot, Vector3 frameB, Quaternion frameBrot)
{
    return BSAPICPP.SetFrames2(constrain.ptr, frameA, frameArot, frameB, frameBrot);
}

public override bool SetLinearLimits(BulletConstraint constrain, Vector3 low, Vector3 hi)
{
    return BSAPICPP.SetLinearLimits2(constrain.ptr, low, hi);
}

public override bool SetAngularLimits(BulletConstraint constrain, Vector3 low, Vector3 hi)
{
    return BSAPICPP.SetAngularLimits2(constrain.ptr, low, hi);
}

public override bool UseFrameOffset(BulletConstraint constrain, float enable)
{
    return BSAPICPP.UseFrameOffset2(constrain.ptr, enable);
}

public override bool TranslationalLimitMotor(BulletConstraint constrain, float enable, float targetVel, float maxMotorForce)
{
    return BSAPICPP.TranslationalLimitMotor2(constrain.ptr, enable, targetVel, maxMotorForce);
}

public override bool SetBreakingImpulseThreshold(BulletConstraint constrain, float threshold)
{
    return BSAPICPP.SetBreakingImpulseThreshold2(constrain.ptr, threshold);
}

public override bool CalculateTransforms(BulletConstraint constrain)
{
    return BSAPICPP.CalculateTransforms2(constrain.ptr);
}

public override bool SetConstraintParam(BulletConstraint constrain, ConstraintParams paramIndex, float value, ConstraintParamAxis axis)
{
    return BSAPICPP.SetConstraintParam2(constrain.ptr, paramIndex, value, axis);
}

public override bool DestroyConstraint(BulletWorld world, BulletConstraint constrain)
{
    return BSAPICPP.DestroyConstraint2(world.ptr, constrain.ptr);
}

// =====================================================================================
// btCollisionWorld entries
public override void UpdateSingleAabb(BulletWorld world, BulletBody obj)
{
    BSAPICPP.UpdateSingleAabb2(world.ptr, obj.ptr);
}

public override void UpdateAabbs(BulletWorld world)
{
    BSAPICPP.UpdateAabbs2(world.ptr);
}

public override bool GetForceUpdateAllAabbs(BulletWorld world)
{
    return BSAPICPP.GetForceUpdateAllAabbs2(world.ptr);
}

public override void SetForceUpdateAllAabbs(BulletWorld world, bool force)
{
    BSAPICPP.SetForceUpdateAllAabbs2(world.ptr, force);
}

// =====================================================================================
// btDynamicsWorld entries
public override bool AddObjectToWorld(BulletWorld world, BulletBody obj)
{
    return BSAPICPP.AddObjectToWorld2(world.ptr, obj.ptr);
}

public override bool RemoveObjectFromWorld(BulletWorld world, BulletBody obj)
{
    return BSAPICPP.RemoveObjectFromWorld2(world.ptr, obj.ptr);
}

public override bool AddConstraintToWorld(BulletWorld world, BulletConstraint constrain, bool disableCollisionsBetweenLinkedObjects)
{
    return BSAPICPP.AddConstraintToWorld2(world.ptr, constrain.ptr, disableCollisionsBetweenLinkedObjects);
}

public override bool RemoveConstraintFromWorld(BulletWorld world, BulletConstraint constrain)
{
    return BSAPICPP.RemoveConstraintFromWorld2(world.ptr, constrain.ptr);
}
// =====================================================================================
// btCollisionObject entries
public override Vector3 GetAnisotripicFriction(BulletConstraint constrain)
{
    return BSAPICPP.GetAnisotripicFriction2(constrain.ptr);
}

public override Vector3 SetAnisotripicFriction(BulletConstraint constrain, Vector3 frict)
{
    return BSAPICPP.SetAnisotripicFriction2(constrain.ptr, frict);
}

public override bool HasAnisotripicFriction(BulletConstraint constrain)
{
    return BSAPICPP.HasAnisotripicFriction2(constrain.ptr);
}

public override void SetContactProcessingThreshold(BulletBody obj, float val)
{
    BSAPICPP.SetContactProcessingThreshold2(obj.ptr, val);
}

public override float GetContactProcessingThreshold(BulletBody obj)
{
    return BSAPICPP.GetContactProcessingThreshold2(obj.ptr);
}

public override bool IsStaticObject(BulletBody obj)
{
    return BSAPICPP.IsStaticObject2(obj.ptr);
}

public override bool IsKinematicObject(BulletBody obj)
{
    return BSAPICPP.IsKinematicObject2(obj.ptr);
}

public override bool IsStaticOrKinematicObject(BulletBody obj)
{
    return BSAPICPP.IsStaticOrKinematicObject2(obj.ptr);
}

public override bool HasContactResponse(BulletBody obj)
{
    return BSAPICPP.HasContactResponse2(obj.ptr);
}

public override void SetCollisionShape(BulletWorld sim, BulletBody obj, BulletShape shape)
{
    BSAPICPP.SetCollisionShape2(sim.ptr, obj.ptr, shape.ptr);
}

public override BulletShape GetCollisionShape(BulletBody obj)
{
    return new BulletShape(BSAPICPP.GetCollisionShape2(obj.ptr));
}

public override int GetActivationState(BulletBody obj)
{
    return BSAPICPP.GetActivationState2(obj.ptr);
}

public override void SetActivationState(BulletBody obj, int state)
{
    BSAPICPP.SetActivationState2(obj.ptr, state);
}

public override void SetDeactivationTime(BulletBody obj, float dtime)
{
    BSAPICPP.SetDeactivationTime2(obj.ptr, dtime);
}

public override float GetDeactivationTime(BulletBody obj)
{
    return BSAPICPP.GetDeactivationTime2(obj.ptr);
}

public override void ForceActivationState(BulletBody obj, ActivationState state)
{
    BSAPICPP.ForceActivationState2(obj.ptr, state);
}

public override void Activate(BulletBody obj, bool forceActivation)
{
    BSAPICPP.Activate2(obj.ptr, forceActivation);
}

public override bool IsActive(BulletBody obj)
{
    return BSAPICPP.IsActive2(obj.ptr);
}

public override void SetRestitution(BulletBody obj, float val)
{
    BSAPICPP.SetRestitution2(obj.ptr, val);
}

public override float GetRestitution(BulletBody obj)
{
    return BSAPICPP.GetRestitution2(obj.ptr);
}

public override void SetFriction(BulletBody obj, float val)
{
    BSAPICPP.SetFriction2(obj.ptr, val);
}

public override float GetFriction(BulletBody obj)
{
    return BSAPICPP.GetFriction2(obj.ptr);
}

public override Vector3 GetPosition(BulletBody obj)
{
    return BSAPICPP.GetPosition2(obj.ptr);
}

public override Quaternion GetOrientation(BulletBody obj)
{
    return BSAPICPP.GetOrientation2(obj.ptr);
}

public override void SetTranslation(BulletBody obj, Vector3 position, Quaternion rotation)
{
    BSAPICPP.SetTranslation2(obj.ptr, position, rotation);
}

    /*
public override IntPtr GetBroadphaseHandle(BulletBody obj)
{
    return BSAPICPP.GetBroadphaseHandle2(obj.ptr);
}

public override void SetBroadphaseHandle(BulletBody obj, IntPtr handle)
{
    BSAPICPP.SetUserPointer2(obj.ptr, handle);
}
     */

public override void SetInterpolationLinearVelocity(BulletBody obj, Vector3 vel)
{
    BSAPICPP.SetInterpolationLinearVelocity2(obj.ptr, vel);
}

public override void SetInterpolationAngularVelocity(BulletBody obj, Vector3 vel)
{
    BSAPICPP.SetInterpolationAngularVelocity2(obj.ptr, vel);
}

public override void SetInterpolationVelocity(BulletBody obj, Vector3 linearVel, Vector3 angularVel)
{
    BSAPICPP.SetInterpolationVelocity2(obj.ptr, linearVel, angularVel);
}

public override float GetHitFraction(BulletBody obj)
{
    return BSAPICPP.GetHitFraction2(obj.ptr);
}

public override void SetHitFraction(BulletBody obj, float val)
{
    BSAPICPP.SetHitFraction2(obj.ptr, val);
}

public override CollisionFlags GetCollisionFlags(BulletBody obj)
{
    return BSAPICPP.GetCollisionFlags2(obj.ptr);
}

public override CollisionFlags SetCollisionFlags(BulletBody obj, CollisionFlags flags)
{
    return BSAPICPP.SetCollisionFlags2(obj.ptr, flags);
}

public override CollisionFlags AddToCollisionFlags(BulletBody obj, CollisionFlags flags)
{
    return BSAPICPP.AddToCollisionFlags2(obj.ptr, flags);
}

public override CollisionFlags RemoveFromCollisionFlags(BulletBody obj, CollisionFlags flags)
{
    return BSAPICPP.RemoveFromCollisionFlags2(obj.ptr, flags);
}

public override float GetCcdMotionThreshold(BulletBody obj)
{
    return BSAPICPP.GetCcdMotionThreshold2(obj.ptr);
}


public override void SetCcdMotionThreshold(BulletBody obj, float val)
{
    BSAPICPP.SetCcdMotionThreshold2(obj.ptr, val);
}

public override float GetCcdSweptSphereRadius(BulletBody obj)
{
    return BSAPICPP.GetCcdSweptSphereRadius2(obj.ptr);
}

public override void SetCcdSweptSphereRadius(BulletBody obj, float val)
{
    BSAPICPP.SetCcdSweptSphereRadius2(obj.ptr, val);
}

public override IntPtr GetUserPointer(BulletBody obj)
{
    return BSAPICPP.GetUserPointer2(obj.ptr);
}

public override void SetUserPointer(BulletBody obj, IntPtr val)
{
    BSAPICPP.SetUserPointer2(obj.ptr, val);
}

// =====================================================================================
// btRigidBody entries
public override void ApplyGravity(BulletBody obj)
{
    BSAPICPP.ApplyGravity2(obj.ptr);
}

public override void SetGravity(BulletBody obj, Vector3 val)
{
    BSAPICPP.SetGravity2(obj.ptr, val);
}

public override Vector3 GetGravity(BulletBody obj)
{
    return BSAPICPP.GetGravity2(obj.ptr);
}

public override void SetDamping(BulletBody obj, float lin_damping, float ang_damping)
{
    BSAPICPP.SetDamping2(obj.ptr, lin_damping, ang_damping);
}

public override void SetLinearDamping(BulletBody obj, float lin_damping)
{
    BSAPICPP.SetLinearDamping2(obj.ptr, lin_damping);
}

public override void SetAngularDamping(BulletBody obj, float ang_damping)
{
    BSAPICPP.SetAngularDamping2(obj.ptr, ang_damping);
}

public override float GetLinearDamping(BulletBody obj)
{
    return BSAPICPP.GetLinearDamping2(obj.ptr);
}

public override float GetAngularDamping(BulletBody obj)
{
    return BSAPICPP.GetAngularDamping2(obj.ptr);
}

public override float GetLinearSleepingThreshold(BulletBody obj)
{
    return BSAPICPP.GetLinearSleepingThreshold2(obj.ptr);
}

public override void ApplyDamping(BulletBody obj, float timeStep)
{
    BSAPICPP.ApplyDamping2(obj.ptr, timeStep);
}

public override void SetMassProps(BulletBody obj, float mass, Vector3 inertia)
{
    BSAPICPP.SetMassProps2(obj.ptr, mass, inertia);
}

public override Vector3 GetLinearFactor(BulletBody obj)
{
    return BSAPICPP.GetLinearFactor2(obj.ptr);
}

public override void SetLinearFactor(BulletBody obj, Vector3 factor)
{
    BSAPICPP.SetLinearFactor2(obj.ptr, factor);
}

public override void SetCenterOfMassByPosRot(BulletBody obj, Vector3 pos, Quaternion rot)
{
    BSAPICPP.SetCenterOfMassByPosRot2(obj.ptr, pos, rot);
}

// Add a force to the object as if its mass is one.
public override void ApplyCentralForce(BulletBody obj, Vector3 force)
{
    BSAPICPP.ApplyCentralForce2(obj.ptr, force);
}

// Set the force being applied to the object as if its mass is one.
public override void SetObjectForce(BulletBody obj, Vector3 force)
{
    BSAPICPP.SetObjectForce2(obj.ptr, force);
}

public override Vector3 GetTotalForce(BulletBody obj)
{
    return BSAPICPP.GetTotalForce2(obj.ptr);
}

public override Vector3 GetTotalTorque(BulletBody obj)
{
    return BSAPICPP.GetTotalTorque2(obj.ptr);
}

public override Vector3 GetInvInertiaDiagLocal(BulletBody obj)
{
    return BSAPICPP.GetInvInertiaDiagLocal2(obj.ptr);
}

public override void SetInvInertiaDiagLocal(BulletBody obj, Vector3 inert)
{
    BSAPICPP.SetInvInertiaDiagLocal2(obj.ptr, inert);
}

public override void SetSleepingThresholds(BulletBody obj, float lin_threshold, float ang_threshold)
{
    BSAPICPP.SetSleepingThresholds2(obj.ptr, lin_threshold, ang_threshold);
}

public override void ApplyTorque(BulletBody obj, Vector3 torque)
{
    BSAPICPP.ApplyTorque2(obj.ptr, torque);
}

// Apply force at the given point. Will add torque to the object.
public override void ApplyForce(BulletBody obj, Vector3 force, Vector3 pos)
{
    BSAPICPP.ApplyForce2(obj.ptr, force, pos);
}

// Apply impulse to the object. Same as "ApplycentralForce" but force scaled by object's mass.
public override void ApplyCentralImpulse(BulletBody obj, Vector3 imp)
{
    BSAPICPP.ApplyCentralImpulse2(obj.ptr, imp);
}

// Apply impulse to the object's torque. Force is scaled by object's mass.
public override void ApplyTorqueImpulse(BulletBody obj, Vector3 imp)
{
    BSAPICPP.ApplyTorqueImpulse2(obj.ptr, imp);
}

// Apply impulse at the point given. For is scaled by object's mass and effects both linear and angular forces.
public override void ApplyImpulse(BulletBody obj, Vector3 imp, Vector3 pos)
{
    BSAPICPP.ApplyImpulse2(obj.ptr, imp, pos);
}

public override void ClearForces(BulletBody obj)
{
    BSAPICPP.ClearForces2(obj.ptr);
}

public override void ClearAllForces(BulletBody obj)
{
    BSAPICPP.ClearAllForces2(obj.ptr);
}

public override void UpdateInertiaTensor(BulletBody obj)
{
    BSAPICPP.UpdateInertiaTensor2(obj.ptr);
}

public override Vector3 GetLinearVelocity(BulletBody obj)
{
    return BSAPICPP.GetLinearVelocity2(obj.ptr);
}

public override Vector3 GetAngularVelocity(BulletBody obj)
{
    return BSAPICPP.GetAngularVelocity2(obj.ptr);
}

public override void SetLinearVelocity(BulletBody obj, Vector3 vel)
{
    BSAPICPP.SetLinearVelocity2(obj.ptr, vel);
}

public override void SetAngularVelocity(BulletBody obj, Vector3 angularVelocity)
{
    BSAPICPP.SetAngularVelocity2(obj.ptr, angularVelocity);
}

public override Vector3 GetVelocityInLocalPoint(BulletBody obj, Vector3 pos)
{
    return BSAPICPP.GetVelocityInLocalPoint2(obj.ptr, pos);
}

public override void Translate(BulletBody obj, Vector3 trans)
{
    BSAPICPP.Translate2(obj.ptr, trans);
}

public override void UpdateDeactivation(BulletBody obj, float timeStep)
{
    BSAPICPP.UpdateDeactivation2(obj.ptr, timeStep);
}

public override bool WantsSleeping(BulletBody obj)
{
    return BSAPICPP.WantsSleeping2(obj.ptr);
}

public override void SetAngularFactor(BulletBody obj, float factor)
{
    BSAPICPP.SetAngularFactor2(obj.ptr, factor);
}

public override void SetAngularFactorV(BulletBody obj, Vector3 factor)
{
    BSAPICPP.SetAngularFactorV2(obj.ptr, factor);
}

public override Vector3 GetAngularFactor(BulletBody obj)
{
    return BSAPICPP.GetAngularFactor2(obj.ptr);
}

public override bool IsInWorld(BulletBody obj)
{
    return BSAPICPP.IsInWorld2(obj.ptr);
}

public override void AddConstraintRef(BulletBody obj, BulletConstraint constrain)
{
    BSAPICPP.AddConstraintRef2(obj.ptr, constrain.ptr);
}

public override void RemoveConstraintRef(BulletBody obj, BulletConstraint constrain)
{
    BSAPICPP.RemoveConstraintRef2(obj.ptr, constrain.ptr);
}

public override BulletConstraint GetConstraintRef(BulletBody obj, int index)
{
    return new BulletConstraint(BSAPICPP.GetConstraintRef2(obj.ptr, index));
}

public override int GetNumConstraintRefs(BulletBody obj)
{
    return BSAPICPP.GetNumConstraintRefs2(obj.ptr);
}

public override bool SetCollisionGroupMask(BulletBody body, uint filter, uint mask)
{
    return BSAPICPP.SetCollisionGroupMask2(body.ptr, filter, mask);
}

// =====================================================================================
// btCollisionShape entries

public override float GetAngularMotionDisc(BulletShape shape)
{
    return BSAPICPP.GetAngularMotionDisc2(shape.ptr);
}

public override float GetContactBreakingThreshold(BulletShape shape, float defaultFactor)
{
    return BSAPICPP.GetContactBreakingThreshold2(shape.ptr, defaultFactor);
}

public override bool IsPolyhedral(BulletShape shape)
{
    return BSAPICPP.IsPolyhedral2(shape.ptr);
}

public override bool IsConvex2d(BulletShape shape)
{
    return BSAPICPP.IsConvex2d2(shape.ptr);
}

public override bool IsConvex(BulletShape shape)
{
    return BSAPICPP.IsConvex2(shape.ptr);
}

public override bool IsNonMoving(BulletShape shape)
{
    return BSAPICPP.IsNonMoving2(shape.ptr);
}

public override bool IsConcave(BulletShape shape)
{
    return BSAPICPP.IsConcave2(shape.ptr);
}

public override bool IsCompound(BulletShape shape)
{
    return BSAPICPP.IsCompound2(shape.ptr);
}

public override bool IsSoftBody(BulletShape shape)
{
    return BSAPICPP.IsSoftBody2(shape.ptr);
}

public override bool IsInfinite(BulletShape shape)
{
    return BSAPICPP.IsInfinite2(shape.ptr);
}

public override void SetLocalScaling(BulletShape shape, Vector3 scale)
{
    BSAPICPP.SetLocalScaling2(shape.ptr, scale);
}

public override Vector3 GetLocalScaling(BulletShape shape)
{
    return BSAPICPP.GetLocalScaling2(shape.ptr);
}

public override Vector3 CalculateLocalInertia(BulletShape shape, float mass)
{
    return BSAPICPP.CalculateLocalInertia2(shape.ptr, mass);
}

public override int GetShapeType(BulletShape shape)
{
    return BSAPICPP.GetShapeType2(shape.ptr);
}

public override void SetMargin(BulletShape shape, float val)
{
    BSAPICPP.SetMargin2(shape.ptr, val);
}

public override float GetMargin(BulletShape shape)
{
    return BSAPICPP.GetMargin2(shape.ptr);
}

// =====================================================================================
// Debugging
public override void DumpRigidBody(BulletWorld sim, BulletBody collisionObject)
{
    BSAPICPP.DumpRigidBody2(sim.ptr, collisionObject.ptr);
}

public override void DumpCollisionShape(BulletWorld sim, BulletShape collisionShape)
{
    BSAPICPP.DumpCollisionShape2(sim.ptr, collisionShape.ptr);
}

public override void DumpConstraint(BulletWorld sim, BulletConstraint constrain)
{
    BSAPICPP.DumpConstraint2(sim.ptr, constrain.ptr);
}

public override void DumpActivationInfo(BulletWorld sim)
{
    BSAPICPP.DumpActivationInfo2(sim.ptr);
}

public override void DumpAllInfo(BulletWorld sim)
{
    BSAPICPP.DumpAllInfo2(sim.ptr);
}

public override void DumpPhysicsStatistics(BulletWorld sim)
{
    BSAPICPP.DumpPhysicsStatistics2(sim.ptr);
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
public static extern IntPtr CreateHullShape2(IntPtr world,
                int hullCount, [MarshalAs(UnmanagedType.LPArray)] float[] hulls);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern IntPtr BuildHullShapeFromMesh2(IntPtr world, IntPtr meshShape);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern IntPtr BuildNativeShape2(IntPtr world, ShapeData shapeData);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern bool IsNativeShape2(IntPtr shape);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern void SetShapeCollisionMargin2(IntPtr shape, float margin);

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
public static extern IntPtr CreateHingeConstraint2(IntPtr world, IntPtr obj1, IntPtr obj2,
                    Vector3 pivotinA, Vector3 pivotinB,
                    Vector3 axisInA, Vector3 axisInB,
                    bool useLinearReferenceFrameA, bool disableCollisionsBetweenLinkedBodies);

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

}

}

}
