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
using System.Linq;
using System.Text;

using BulletXNA;
using BulletXNA.LinearMath;
using BulletXNA.BulletCollision;
using BulletXNA.BulletDynamics;
using BulletXNA.BulletCollision.CollisionDispatch;

using OpenMetaverse;

namespace OpenSim.Region.Physics.BulletSPlugin
{
    /*
public sealed class BSAPIXNA : BSAPITemplate
{
    private static int m_collisionsThisFrame;
    private BSScene PhysicsScene { get; set; }

    public override string BulletEngineName { get { return "BulletXNA"; } }
    public override string BulletEngineVersion { get; protected set; }

    public BSAPIXNA(string paramName, BSScene physScene)
    {
        PhysicsScene = physScene;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="p"></param>
    /// <param name="p_2"></param>
    public override bool RemoveObjectFromWorld2(object pWorld, object pBody)
    {
        DiscreteDynamicsWorld world = pWorld as DiscreteDynamicsWorld;
        RigidBody body = pBody as RigidBody;
        world.RemoveRigidBody(body);
        return true;
    }

    public override void SetRestitution2(object pBody, float pRestitution)
    {
        RigidBody body = pBody as RigidBody;
        body.SetRestitution(pRestitution);
    }

    public override void SetMargin2(object pShape, float pMargin)
    {
        CollisionShape shape = pShape as CollisionShape;
        shape.SetMargin(pMargin);
    }

    public override void SetLocalScaling2(object pShape, Vector3 pScale)
    {
        CollisionShape shape = pShape as CollisionShape;
        IndexedVector3 vec = new IndexedVector3(pScale.X, pScale.Y, pScale.Z);
        shape.SetLocalScaling(ref vec);

    }

    public override void SetContactProcessingThreshold2(object pBody, float contactprocessingthreshold)
    {
        RigidBody body = pBody as RigidBody;
        body.SetContactProcessingThreshold(contactprocessingthreshold);
    }

    public override void SetCcdMotionThreshold2(object pBody, float pccdMotionThreashold)
    {
        RigidBody body = pBody as RigidBody;
        body.SetCcdMotionThreshold(pccdMotionThreashold);
    }

    public override void SetCcdSweptSphereRadius2(object pBody, float pCcdSweptSphereRadius)
    {
        RigidBody body = pBody as RigidBody;
        body.SetCcdSweptSphereRadius(pCcdSweptSphereRadius);
    }

    public override void SetAngularFactorV2(object pBody, Vector3 pAngularFactor)
    {
        RigidBody body = pBody as RigidBody;
        body.SetAngularFactor(new IndexedVector3(pAngularFactor.X, pAngularFactor.Y, pAngularFactor.Z));
    }

    public override CollisionFlags AddToCollisionFlags2(object pBody, CollisionFlags pcollisionFlags)
    {
        CollisionObject body = pBody as CollisionObject;
        CollisionFlags existingcollisionFlags = (CollisionFlags)(uint)body.GetCollisionFlags();
        existingcollisionFlags |= pcollisionFlags;
        body.SetCollisionFlags((BulletXNA.BulletCollision.CollisionFlags)(uint)existingcollisionFlags);
        return (CollisionFlags) (uint) existingcollisionFlags;
    }

    public override void AddObjectToWorld2(object pWorld, object pBody)
    {
        RigidBody body = pBody as RigidBody;
        DiscreteDynamicsWorld world = pWorld as DiscreteDynamicsWorld;
        //if (!(body.GetCollisionShape().GetShapeType() == BroadphaseNativeTypes.STATIC_PLANE_PROXYTYPE && body.GetCollisionShape().GetShapeType() == BroadphaseNativeTypes.TERRAIN_SHAPE_PROXYTYPE))

        world.AddRigidBody(body);

        //if (body.GetBroadphaseHandle() != null)
        //    world.UpdateSingleAabb(body);
    }

    public override void AddObjectToWorld2(object pWorld, object pBody, Vector3 _position, Quaternion _orientation)
    {
        RigidBody body = pBody as RigidBody;
        DiscreteDynamicsWorld world = pWorld as DiscreteDynamicsWorld;
        //if (!(body.GetCollisionShape().GetShapeType() == BroadphaseNativeTypes.STATIC_PLANE_PROXYTYPE && body.GetCollisionShape().GetShapeType() == BroadphaseNativeTypes.TERRAIN_SHAPE_PROXYTYPE))

        world.AddRigidBody(body);
        IndexedVector3 vposition = new IndexedVector3(_position.X, _position.Y, _position.Z);
        IndexedQuaternion vquaternion = new IndexedQuaternion(_orientation.X, _orientation.Y, _orientation.Z,
                                                              _orientation.W);
        IndexedMatrix mat = IndexedMatrix.CreateFromQuaternion(vquaternion);
        mat._origin = vposition;
        body.SetWorldTransform(mat);
        //if (body.GetBroadphaseHandle() != null)
        //    world.UpdateSingleAabb(body);
    }

    public override void ForceActivationState2(object pBody, ActivationState pActivationState)
    {
        CollisionObject body = pBody as CollisionObject;
        body.ForceActivationState((BulletXNA.BulletCollision.ActivationState)(uint)pActivationState);
    }

    public override void UpdateSingleAabb2(object pWorld, object pBody)
    {
        CollisionObject body = pBody as CollisionObject;
        DiscreteDynamicsWorld world = pWorld as DiscreteDynamicsWorld;
        world.UpdateSingleAabb(body);
    }

    public override bool SetCollisionGroupMask2(object pBody, uint pGroup, uint pMask)
    {
        RigidBody body = pBody as RigidBody;
        body.GetBroadphaseHandle().m_collisionFilterGroup = (BulletXNA.BulletCollision.CollisionFilterGroups) pGroup;
        body.GetBroadphaseHandle().m_collisionFilterGroup = (BulletXNA.BulletCollision.CollisionFilterGroups) pGroup;
        if ((uint) body.GetBroadphaseHandle().m_collisionFilterGroup == 0)
            return false;
        return true;
    }

    public override void ClearAllForces2(object pBody)
    {
        CollisionObject body = pBody as CollisionObject;
        IndexedVector3 zeroVector = new IndexedVector3(0, 0, 0);
        body.SetInterpolationLinearVelocity(ref zeroVector);
        body.SetInterpolationAngularVelocity(ref zeroVector);
        IndexedMatrix bodytransform = body.GetWorldTransform();

        body.SetInterpolationWorldTransform(ref bodytransform);

        if (body is RigidBody)
        {
            RigidBody rigidbody = body as RigidBody;
            rigidbody.SetLinearVelocity(zeroVector);
            rigidbody.SetAngularVelocity(zeroVector);
            rigidbody.ClearForces();
        }
    }

    public override void SetInterpolationAngularVelocity2(object pBody, Vector3 pVector3)
    {
        RigidBody body = pBody as RigidBody;
        IndexedVector3 vec = new IndexedVector3(pVector3.X, pVector3.Y, pVector3.Z);
        body.SetInterpolationAngularVelocity(ref vec);
    }

    public override void SetAngularVelocity2(object pBody, Vector3 pVector3)
    {
        RigidBody body = pBody as RigidBody;
        IndexedVector3 vec = new IndexedVector3(pVector3.X, pVector3.Y, pVector3.Z);
        body.SetAngularVelocity(ref vec);
    }

    public override void ClearForces2(object pBody)
    {
        RigidBody body = pBody as RigidBody;
        body.ClearForces();
    }

    public override void SetTranslation2(object pBody, Vector3 _position, Quaternion _orientation)
    {
        RigidBody body = pBody as RigidBody;
        IndexedVector3 vposition = new IndexedVector3(_position.X, _position.Y, _position.Z);
        IndexedQuaternion vquaternion = new IndexedQuaternion(_orientation.X, _orientation.Y, _orientation.Z,
                                                              _orientation.W);
        IndexedMatrix mat = IndexedMatrix.CreateFromQuaternion(vquaternion);
        mat._origin = vposition;
        body.SetWorldTransform(mat);
        
    }

    public override Vector3 GetPosition2(object pBody)
    {
        RigidBody body = pBody as RigidBody;
        IndexedVector3 pos = body.GetInterpolationWorldTransform()._origin;
        return new Vector3(pos.X, pos.Y, pos.Z);
    }

    public override Vector3 CalculateLocalInertia2(object pShape, float pphysMass)
    {
        CollisionShape shape = pShape as CollisionShape;
        IndexedVector3 inertia = IndexedVector3.Zero;
        shape.CalculateLocalInertia(pphysMass, out inertia);
        return new Vector3(inertia.X, inertia.Y, inertia.Z);
    }

    public override void SetMassProps2(object pBody, float pphysMass, Vector3 plocalInertia)
    {
        RigidBody body = pBody as RigidBody;
        IndexedVector3 inertia = new IndexedVector3(plocalInertia.X, plocalInertia.Y, plocalInertia.Z);
        body.SetMassProps(pphysMass, inertia);
    }


    public override void SetObjectForce2(object pBody, Vector3 _force)
    {
        RigidBody body = pBody as RigidBody;
        IndexedVector3 force = new IndexedVector3(_force.X, _force.Y, _force.Z);
        body.SetTotalForce(ref force);
    }

    public override void SetFriction2(object pBody, float _currentFriction)
    {
        RigidBody body = pBody as RigidBody;
        body.SetFriction(_currentFriction);
    }

    public override void SetLinearVelocity2(object pBody, Vector3 _velocity)
    {
        RigidBody body = pBody as RigidBody;
        IndexedVector3 velocity = new IndexedVector3(_velocity.X, _velocity.Y, _velocity.Z);
        body.SetLinearVelocity(velocity);
    }

    public override void Activate2(object pBody, bool pforceactivation)
    {
        RigidBody body = pBody as RigidBody;
        body.Activate(pforceactivation);
        
    }

    public override Quaternion GetOrientation2(object pBody)
    {
        RigidBody body = pBody as RigidBody;
        IndexedQuaternion mat = body.GetInterpolationWorldTransform().GetRotation();
        return new Quaternion(mat.X, mat.Y, mat.Z, mat.W);
    }

    public override CollisionFlags RemoveFromCollisionFlags2(object pBody, CollisionFlags pcollisionFlags)
    {
        RigidBody body = pBody as RigidBody;
        CollisionFlags existingcollisionFlags = (CollisionFlags)(uint)body.GetCollisionFlags();
        existingcollisionFlags &= ~pcollisionFlags;
        body.SetCollisionFlags((BulletXNA.BulletCollision.CollisionFlags)(uint)existingcollisionFlags);
        return (CollisionFlags)(uint)existingcollisionFlags;
    }

    public override void SetGravity2(object pBody, Vector3 pGravity)
    {
        RigidBody body = pBody as RigidBody;
        IndexedVector3 gravity = new IndexedVector3(pGravity.X, pGravity.Y, pGravity.Z);
        body.SetGravity(gravity);
    }

    public override bool DestroyConstraint2(object pBody, object pConstraint)
    {
        RigidBody body = pBody as RigidBody;
        TypedConstraint constraint = pConstraint as TypedConstraint;
        body.RemoveConstraintRef(constraint);
        return true;
    }

    public override bool SetLinearLimits2(object pConstraint, Vector3 low, Vector3 high)
    {
        Generic6DofConstraint constraint = pConstraint as Generic6DofConstraint;
        IndexedVector3 lowlimit = new IndexedVector3(low.X, low.Y, low.Z);
        IndexedVector3 highlimit = new IndexedVector3(high.X, high.Y, high.Z);
        constraint.SetLinearLowerLimit(lowlimit);
        constraint.SetLinearUpperLimit(highlimit);
        return true;
    }

    public override bool SetAngularLimits2(object pConstraint, Vector3 low, Vector3 high)
    {
        Generic6DofConstraint constraint = pConstraint as Generic6DofConstraint;
        IndexedVector3 lowlimit = new IndexedVector3(low.X, low.Y, low.Z);
        IndexedVector3 highlimit = new IndexedVector3(high.X, high.Y, high.Z);
        constraint.SetAngularLowerLimit(lowlimit);
        constraint.SetAngularUpperLimit(highlimit);
        return true;
    }

    public override void SetConstraintNumSolverIterations2(object pConstraint, float cnt)
    {
        Generic6DofConstraint constraint = pConstraint as Generic6DofConstraint;
        constraint.SetOverrideNumSolverIterations((int)cnt);
    }

    public override void CalculateTransforms2(object pConstraint)
    {
        Generic6DofConstraint constraint = pConstraint as Generic6DofConstraint;
        constraint.CalculateTransforms();
    }

    public override void SetConstraintEnable2(object pConstraint, float p_2)
    {
        Generic6DofConstraint constraint = pConstraint as Generic6DofConstraint;
        constraint.SetEnabled((p_2 == 0) ? false : true);
    }


    //BulletSimAPI.Create6DofConstraint2(m_world.ptr, m_body1.ptr, m_body2.ptr,frame1, frame1rot,frame2, frame2rot,useLinearReferenceFrameA, disableCollisionsBetweenLinkedBodies));
    public override object Create6DofConstraint2(object pWorld, object pBody1, object pBody2, Vector3 pframe1, Quaternion pframe1rot, Vector3 pframe2, Quaternion pframe2rot, bool puseLinearReferenceFrameA, bool pdisableCollisionsBetweenLinkedBodies)

    {
         DiscreteDynamicsWorld world = pWorld as DiscreteDynamicsWorld;
        RigidBody body1 = pBody1 as RigidBody;
        RigidBody body2 = pBody2 as RigidBody;
        IndexedVector3 frame1v = new IndexedVector3(pframe1.X, pframe1.Y, pframe1.Z);
        IndexedQuaternion frame1rot = new IndexedQuaternion(pframe1rot.X, pframe1rot.Y, pframe1rot.Z, pframe1rot.W);
        IndexedMatrix frame1 = IndexedMatrix.CreateFromQuaternion(frame1rot);
        frame1._origin = frame1v;

        IndexedVector3 frame2v = new IndexedVector3(pframe2.X, pframe2.Y, pframe2.Z);
        IndexedQuaternion frame2rot = new IndexedQuaternion(pframe2rot.X, pframe2rot.Y, pframe2rot.Z, pframe2rot.W);
        IndexedMatrix frame2 = IndexedMatrix.CreateFromQuaternion(frame2rot);
        frame2._origin = frame1v;

        Generic6DofConstraint consttr = new Generic6DofConstraint(body1, body2, ref frame1, ref frame2,
                                                                  puseLinearReferenceFrameA);
        consttr.CalculateTransforms();
        world.AddConstraint(consttr,pdisableCollisionsBetweenLinkedBodies);

        return consttr;
    }

    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="pWorld"></param>
    /// <param name="pBody1"></param>
    /// <param name="pBody2"></param>
    /// <param name="pjoinPoint"></param>
    /// <param name="puseLinearReferenceFrameA"></param>
    /// <param name="pdisableCollisionsBetweenLinkedBodies"></param>
    /// <returns></returns>
    public override object Create6DofConstraintToPoint2(object pWorld, object pBody1, object pBody2, Vector3 pjoinPoint, bool puseLinearReferenceFrameA, bool pdisableCollisionsBetweenLinkedBodies)
    {
        DiscreteDynamicsWorld world = pWorld as DiscreteDynamicsWorld;
        RigidBody body1 = pBody1 as RigidBody;
        RigidBody body2 = pBody2 as RigidBody;
        IndexedMatrix frame1 = new IndexedMatrix(IndexedBasisMatrix.Identity, new IndexedVector3(0, 0, 0));
        IndexedMatrix frame2 = new IndexedMatrix(IndexedBasisMatrix.Identity, new IndexedVector3(0, 0, 0));

        IndexedVector3 joinPoint = new IndexedVector3(pjoinPoint.X, pjoinPoint.Y, pjoinPoint.Z);
        IndexedMatrix mat = IndexedMatrix.Identity;
        mat._origin = new IndexedVector3(pjoinPoint.X, pjoinPoint.Y, pjoinPoint.Z);
        frame1._origin = body1.GetWorldTransform().Inverse()*joinPoint;
        frame2._origin = body2.GetWorldTransform().Inverse()*joinPoint;

        Generic6DofConstraint consttr = new Generic6DofConstraint(body1, body2, ref frame1, ref frame2, puseLinearReferenceFrameA);
        consttr.CalculateTransforms();
        world.AddConstraint(consttr, pdisableCollisionsBetweenLinkedBodies);

        return consttr;
    }
    //SetFrames2(m_constraint.ptr, frameA, frameArot, frameB, frameBrot);
    public override void SetFrames2(object pConstraint, Vector3 pframe1, Quaternion pframe1rot, Vector3 pframe2, Quaternion pframe2rot)
    {
        Generic6DofConstraint constraint = pConstraint as Generic6DofConstraint;
        IndexedVector3 frame1v = new IndexedVector3(pframe1.X, pframe1.Y, pframe1.Z);
        IndexedQuaternion frame1rot = new IndexedQuaternion(pframe1rot.X, pframe1rot.Y, pframe1rot.Z, pframe1rot.W);
        IndexedMatrix frame1 = IndexedMatrix.CreateFromQuaternion(frame1rot);
        frame1._origin = frame1v;

        IndexedVector3 frame2v = new IndexedVector3(pframe2.X, pframe2.Y, pframe2.Z);
        IndexedQuaternion frame2rot = new IndexedQuaternion(pframe2rot.X, pframe2rot.Y, pframe2rot.Z, pframe2rot.W);
        IndexedMatrix frame2 = IndexedMatrix.CreateFromQuaternion(frame2rot);
        frame2._origin = frame1v;
        constraint.SetFrames(ref frame1, ref frame2);
    }


    

    public override bool IsInWorld2(object pWorld, object pShapeObj)
    {
        DiscreteDynamicsWorld world = pWorld as DiscreteDynamicsWorld;
        CollisionObject shape = pShapeObj as CollisionObject;
        return world.IsInWorld(shape);
    }

    public override void SetInterpolationLinearVelocity2(object pBody, Vector3 VehicleVelocity)
    {
        RigidBody body = pBody as RigidBody;
        IndexedVector3 velocity = new IndexedVector3(VehicleVelocity.X, VehicleVelocity.Y, VehicleVelocity.Z);
        body.SetInterpolationLinearVelocity(ref velocity);
    }

    public override bool UseFrameOffset2(object pConstraint, float onOff)
    {
        Generic6DofConstraint constraint = pConstraint as Generic6DofConstraint;
        constraint.SetUseFrameOffset((onOff == 0) ? false : true);
        return true;
    }
    //SetBreakingImpulseThreshold2(m_constraint.ptr, threshold);
    public override bool SetBreakingImpulseThreshold2(object pConstraint, float threshold)
    {
        Generic6DofConstraint constraint = pConstraint as Generic6DofConstraint;
        constraint.SetBreakingImpulseThreshold(threshold);
        return true;
    }
    //BulletSimAPI.SetAngularDamping2(Prim.PhysBody.ptr, angularDamping);
    public override void SetAngularDamping2(object pBody, float angularDamping)
    {
        RigidBody body = pBody as RigidBody;
        float lineardamping = body.GetLinearDamping();
        body.SetDamping(lineardamping, angularDamping);

    }

    public override void UpdateInertiaTensor2(object pBody)
    {
        RigidBody body = pBody as RigidBody;
        body.UpdateInertiaTensor();
    }

    public override void RecalculateCompoundShapeLocalAabb2( object pCompoundShape)
    {

        CompoundShape shape = pCompoundShape as CompoundShape;
        shape.RecalculateLocalAabb();
    }

    //BulletSimAPI.GetCollisionFlags2(PhysBody.ptr)
    public override CollisionFlags GetCollisionFlags2(object pBody)
    {
        RigidBody body = pBody as RigidBody;
        uint flags = (uint)body.GetCollisionFlags();
        return (CollisionFlags) flags;
    }

    public override void SetDamping2(object pBody, float pLinear, float pAngular)
    {
        RigidBody body = pBody as RigidBody;
        body.SetDamping(pLinear, pAngular);
    }
    //PhysBody.ptr, PhysicsScene.Params.deactivationTime);
    public override void SetDeactivationTime2(object pBody, float pDeactivationTime)
    {
        RigidBody body = pBody as RigidBody;
        body.SetDeactivationTime(pDeactivationTime);
    }
    //SetSleepingThresholds2(PhysBody.ptr, PhysicsScene.Params.linearSleepingThreshold, PhysicsScene.Params.angularSleepingThreshold);
    public override void SetSleepingThresholds2(object pBody, float plinearSleepingThreshold, float pangularSleepingThreshold)
    {
        RigidBody body = pBody as RigidBody;
        body.SetSleepingThresholds(plinearSleepingThreshold, pangularSleepingThreshold);
    }

    public override CollisionObjectTypes GetBodyType2(object pBody)
    {
        RigidBody body = pBody as RigidBody;
        return (CollisionObjectTypes)(int) body.GetInternalType();
    }

    //BulletSimAPI.ApplyCentralForce2(PhysBody.ptr, fSum);
    public override void ApplyCentralForce2(object pBody, Vector3 pfSum)
    {
        RigidBody body = pBody as RigidBody;
        IndexedVector3 fSum = new IndexedVector3(pfSum.X, pfSum.Y, pfSum.Z);
        body.ApplyCentralForce(ref fSum);
    }
    public override void ApplyCentralImpulse2(object pBody, Vector3 pfSum)
    {
        RigidBody body = pBody as RigidBody;
        IndexedVector3 fSum = new IndexedVector3(pfSum.X, pfSum.Y, pfSum.Z);
        body.ApplyCentralImpulse(ref fSum);
    }
    public override void ApplyTorque2(object pBody, Vector3 pfSum)
    {
        RigidBody body = pBody as RigidBody;
        IndexedVector3 fSum = new IndexedVector3(pfSum.X, pfSum.Y, pfSum.Z);
        body.ApplyTorque(ref fSum);
    }
    public override void ApplyTorqueImpulse2(object pBody, Vector3 pfSum)
    {
        RigidBody body = pBody as RigidBody;
        IndexedVector3 fSum = new IndexedVector3(pfSum.X, pfSum.Y, pfSum.Z);
        body.ApplyTorqueImpulse(ref fSum);
    }

    public override void DumpRigidBody2(object p, object p_2)
    {
        //TODO:
    }

    public override void DumpCollisionShape2(object p, object p_2)
    {
        //TODO:
    }

    public override void DestroyObject2(object p, object p_2)
    {
        //TODO:
    }

    public override void Shutdown2(object pWorld)
    {
        DiscreteDynamicsWorld world = pWorld as DiscreteDynamicsWorld;
        world.Cleanup();
    }

    public override void DeleteCollisionShape2(object p, object p_2)
    {
        //TODO:
    }
    //(sim.ptr, shape.ptr, prim.LocalID, prim.RawPosition, prim.RawOrientation);
               
    public override object CreateBodyFromShape2(object pWorld, object pShape, uint pLocalID, Vector3 pRawPosition, Quaternion pRawOrientation)
    {
        CollisionWorld world = pWorld as CollisionWorld;
        IndexedMatrix mat =
            IndexedMatrix.CreateFromQuaternion(new IndexedQuaternion(pRawOrientation.X, pRawOrientation.Y,
                                                                     pRawOrientation.Z, pRawOrientation.W));
        mat._origin = new IndexedVector3(pRawPosition.X, pRawPosition.Y, pRawPosition.Z);
        CollisionShape shape = pShape as CollisionShape;
        //UpdateSingleAabb2(world, shape);
        // TODO: Feed Update array into null
        RigidBody body = new RigidBody(0,new SimMotionState(world,pLocalID,mat,null),shape,IndexedVector3.Zero);
        
        body.SetUserPointer(pLocalID);
        return body;
    }

    
    public override object CreateBodyWithDefaultMotionState2( object pShape, uint pLocalID, Vector3 pRawPosition, Quaternion pRawOrientation)
    {

        IndexedMatrix mat =
            IndexedMatrix.CreateFromQuaternion(new IndexedQuaternion(pRawOrientation.X, pRawOrientation.Y,
                                                                     pRawOrientation.Z, pRawOrientation.W));
        mat._origin = new IndexedVector3(pRawPosition.X, pRawPosition.Y, pRawPosition.Z);

        CollisionShape shape = pShape as CollisionShape;

        // TODO: Feed Update array into null
        RigidBody body = new RigidBody(0, new DefaultMotionState( mat, IndexedMatrix.Identity), shape, IndexedVector3.Zero);
        body.SetWorldTransform(mat);
        body.SetUserPointer(pLocalID);
        return body;
    }
    //(m_mapInfo.terrainBody.ptr, CollisionFlags.CF_STATIC_OBJECT);
    public override void SetCollisionFlags2(object pBody, CollisionFlags collisionFlags)
    {
        RigidBody body = pBody as RigidBody;
        body.SetCollisionFlags((BulletXNA.BulletCollision.CollisionFlags) (uint) collisionFlags);
    }
    //(m_mapInfo.terrainBody.ptr, PhysicsScene.Params.terrainHitFraction);
    public override void SetHitFraction2(object pBody, float pHitFraction)
    {
        RigidBody body = pBody as RigidBody;
        body.SetHitFraction(pHitFraction);
    }
    //BuildCapsuleShape2(physicsScene.World.ptr, 1f, 1f, prim.Scale);
    public override object BuildCapsuleShape2(object pWorld, float pRadius, float pHeight, Vector3 pScale)
    {
        DiscreteDynamicsWorld world = pWorld as DiscreteDynamicsWorld;
        IndexedVector3 scale = new IndexedVector3(pScale.X, pScale.Y, pScale.Z);
        CapsuleShapeZ capsuleShapeZ = new CapsuleShapeZ(pRadius, pHeight);
        capsuleShapeZ.SetMargin(world.WorldSettings.Params.collisionMargin);
        capsuleShapeZ.SetLocalScaling(ref scale);
        
        return capsuleShapeZ;
    }

    public static object Initialize2(Vector3 worldExtent, ConfigurationParameters[] o, int mMaxCollisionsPerFrame, ref List<BulletXNA.CollisionDesc> collisionArray, int mMaxUpdatesPerFrame, ref List<BulletXNA.EntityProperties> updateArray, object mDebugLogCallbackHandle)
    {
        CollisionWorld.WorldData.ParamData p = new CollisionWorld.WorldData.ParamData();

        p.angularDamping = o[0].XangularDamping;
        p.defaultFriction = o[0].defaultFriction;
        p.defaultFriction = o[0].defaultFriction;
        p.defaultDensity = o[0].defaultDensity;
        p.defaultRestitution = o[0].defaultRestitution;
        p.collisionMargin = o[0].collisionMargin;
        p.gravity = o[0].gravity;

        p.linearDamping = o[0].XlinearDamping;
        p.angularDamping = o[0].XangularDamping;
        p.deactivationTime = o[0].XdeactivationTime;
        p.linearSleepingThreshold = o[0].XlinearSleepingThreshold;
        p.angularSleepingThreshold = o[0].XangularSleepingThreshold;
        p.ccdMotionThreshold = o[0].XccdMotionThreshold;
        p.ccdSweptSphereRadius = o[0].XccdSweptSphereRadius;
        p.contactProcessingThreshold = o[0].XcontactProcessingThreshold;

        p.terrainImplementation = o[0].XterrainImplementation;
        p.terrainFriction = o[0].XterrainFriction;

        p.terrainHitFraction = o[0].XterrainHitFraction;
        p.terrainRestitution = o[0].XterrainRestitution;
        p.terrainCollisionMargin = o[0].XterrainCollisionMargin;

        p.avatarFriction = o[0].XavatarFriction;
        p.avatarStandingFriction = o[0].XavatarStandingFriction;
        p.avatarDensity = o[0].XavatarDensity;
        p.avatarRestitution = o[0].XavatarRestitution;
        p.avatarCapsuleWidth = o[0].XavatarCapsuleWidth;
        p.avatarCapsuleDepth = o[0].XavatarCapsuleDepth;
        p.avatarCapsuleHeight = o[0].XavatarCapsuleHeight;
        p.avatarContactProcessingThreshold = o[0].XavatarContactProcessingThreshold;
       
        p.vehicleAngularDamping = o[0].XvehicleAngularDamping;
        
        p.maxPersistantManifoldPoolSize = o[0].maxPersistantManifoldPoolSize;
        p.maxCollisionAlgorithmPoolSize = o[0].maxCollisionAlgorithmPoolSize;
        p.shouldDisableContactPoolDynamicAllocation = o[0].shouldDisableContactPoolDynamicAllocation;
        p.shouldForceUpdateAllAabbs = o[0].shouldForceUpdateAllAabbs;
        p.shouldRandomizeSolverOrder = o[0].shouldRandomizeSolverOrder;
        p.shouldSplitSimulationIslands = o[0].shouldSplitSimulationIslands;
        p.shouldEnableFrictionCaching = o[0].shouldEnableFrictionCaching;
        p.numberOfSolverIterations = o[0].numberOfSolverIterations;

        p.linksetImplementation = o[0].XlinksetImplementation;
        p.linkConstraintUseFrameOffset = o[0].XlinkConstraintUseFrameOffset;
        p.linkConstraintEnableTransMotor = o[0].XlinkConstraintEnableTransMotor;
        p.linkConstraintTransMotorMaxVel = o[0].XlinkConstraintTransMotorMaxVel;
        p.linkConstraintTransMotorMaxForce = o[0].XlinkConstraintTransMotorMaxForce;
        p.linkConstraintERP = o[0].XlinkConstraintERP;
        p.linkConstraintCFM = o[0].XlinkConstraintCFM;
        p.linkConstraintSolverIterations = o[0].XlinkConstraintSolverIterations;
        p.physicsLoggingFrames = o[0].physicsLoggingFrames;
        DefaultCollisionConstructionInfo ccci = new DefaultCollisionConstructionInfo();
        
        DefaultCollisionConfiguration cci = new DefaultCollisionConfiguration();
        CollisionDispatcher m_dispatcher = new CollisionDispatcher(cci);


        if (p.maxPersistantManifoldPoolSize > 0)
            cci.m_persistentManifoldPoolSize = (int)p.maxPersistantManifoldPoolSize;
        if (p.shouldDisableContactPoolDynamicAllocation !=0)
            m_dispatcher.SetDispatcherFlags(DispatcherFlags.CD_DISABLE_CONTACTPOOL_DYNAMIC_ALLOCATION);
        //if (p.maxCollisionAlgorithmPoolSize >0 )

        DbvtBroadphase m_broadphase = new DbvtBroadphase();
        //IndexedVector3 aabbMin = new IndexedVector3(0, 0, 0);
        //IndexedVector3 aabbMax = new IndexedVector3(256, 256, 256);

        //AxisSweep3Internal m_broadphase2 = new AxisSweep3Internal(ref aabbMin, ref aabbMax, Convert.ToInt32(0xfffe), 0xffff, ushort.MaxValue/2, null, true);
        m_broadphase.GetOverlappingPairCache().SetInternalGhostPairCallback(new GhostPairCallback());

        SequentialImpulseConstraintSolver m_solver = new SequentialImpulseConstraintSolver();

        DiscreteDynamicsWorld world = new DiscreteDynamicsWorld(m_dispatcher, m_broadphase, m_solver, cci);
        world.UpdatedObjects = updateArray;
        world.UpdatedCollisions = collisionArray;
        world.WorldSettings.Params = p;
        world.SetForceUpdateAllAabbs(p.shouldForceUpdateAllAabbs != 0);
        world.GetSolverInfo().m_solverMode = SolverMode.SOLVER_USE_WARMSTARTING | SolverMode.SOLVER_SIMD;
        if (p.shouldRandomizeSolverOrder != 0)
            world.GetSolverInfo().m_solverMode |= SolverMode.SOLVER_RANDMIZE_ORDER;

        world.GetSimulationIslandManager().SetSplitIslands(p.shouldSplitSimulationIslands != 0);
        //world.GetDispatchInfo().m_enableSatConvex Not implemented in C# port

        if (p.shouldEnableFrictionCaching != 0)
            world.GetSolverInfo().m_solverMode |= SolverMode.SOLVER_ENABLE_FRICTION_DIRECTION_CACHING;

        if (p.numberOfSolverIterations > 0)
            world.GetSolverInfo().m_numIterations = (int) p.numberOfSolverIterations;


        world.GetSolverInfo().m_damping = world.WorldSettings.Params.linearDamping;
        world.GetSolverInfo().m_restitution = world.WorldSettings.Params.defaultRestitution;
        world.GetSolverInfo().m_globalCfm = 0.0f;
        world.GetSolverInfo().m_tau = 0.6f;
        world.GetSolverInfo().m_friction = 0.3f;
        world.GetSolverInfo().m_maxErrorReduction = 20f;
        world.GetSolverInfo().m_numIterations = 10;
        world.GetSolverInfo().m_erp = 0.2f;
        world.GetSolverInfo().m_erp2 = 0.1f;
        world.GetSolverInfo().m_sor = 1.0f;
        world.GetSolverInfo().m_splitImpulse = false;
        world.GetSolverInfo().m_splitImpulsePenetrationThreshold = -0.02f;
        world.GetSolverInfo().m_linearSlop = 0.0f;
        world.GetSolverInfo().m_warmstartingFactor = 0.85f;
        world.GetSolverInfo().m_restingContactRestitutionThreshold = 2;
        world.SetForceUpdateAllAabbs(true);


        world.SetGravity(new IndexedVector3(0,0,p.gravity));

        return world;
    }
    //m_constraint.ptr, ConstraintParams.BT_CONSTRAINT_STOP_CFM, cfm, ConstraintParamAxis.AXIS_ALL
    public override bool SetConstraintParam2(object pConstraint, ConstraintParams paramIndex, float paramvalue, ConstraintParamAxis axis)
    {
        Generic6DofConstraint constrain = pConstraint as Generic6DofConstraint;
        if (axis == ConstraintParamAxis.AXIS_LINEAR_ALL || axis == ConstraintParamAxis.AXIS_ALL)
        {
            constrain.SetParam((BulletXNA.BulletDynamics.ConstraintParams) (int) paramIndex, paramvalue, 0);
            constrain.SetParam((BulletXNA.BulletDynamics.ConstraintParams) (int) paramIndex, paramvalue, 1);
            constrain.SetParam((BulletXNA.BulletDynamics.ConstraintParams) (int) paramIndex, paramvalue, 2);
        }
        if (axis == ConstraintParamAxis.AXIS_ANGULAR_ALL || axis == ConstraintParamAxis.AXIS_ALL)
        {
            constrain.SetParam((BulletXNA.BulletDynamics.ConstraintParams)(int)paramIndex, paramvalue, 3);
            constrain.SetParam((BulletXNA.BulletDynamics.ConstraintParams)(int)paramIndex, paramvalue, 4);
            constrain.SetParam((BulletXNA.BulletDynamics.ConstraintParams)(int)paramIndex, paramvalue, 5);
        }
        if (axis == ConstraintParamAxis.AXIS_LINEAR_ALL)
        {
            constrain.SetParam((BulletXNA.BulletDynamics.ConstraintParams)(int)paramIndex, paramvalue, (int)axis);
        }
        return true;
    }

    public override bool PushUpdate2(object pCollisionObject)
    {
        bool ret = false;
        RigidBody rb = pCollisionObject as RigidBody;
        if (rb != null)
        {
            SimMotionState sms = rb.GetMotionState() as SimMotionState;
            if (sms != null)
            {
                IndexedMatrix wt = IndexedMatrix.Identity;
                sms.GetWorldTransform(out wt);
                sms.SetWorldTransform(ref wt, true);
                ret = true;
            }
        }
        return ret;
        
    }

    public override bool IsCompound2(object pShape)
    {
        CollisionShape shape = pShape as CollisionShape;
        return shape.IsCompound();
    }
    public override bool IsPloyhedral2(object pShape)
    {
        CollisionShape shape = pShape as CollisionShape;
        return shape.IsPolyhedral();
    }
    public override bool IsConvex2d2(object pShape)
    {
        CollisionShape shape = pShape as CollisionShape;
        return shape.IsConvex2d();
    }
    public override bool IsConvex2(object pShape)
    {
        CollisionShape shape = pShape as CollisionShape;
        return shape.IsConvex();
    }
    public override bool IsNonMoving2(object pShape)
    {
        CollisionShape shape = pShape as CollisionShape;
        return shape.IsNonMoving();
    }
    public override bool IsConcave2(object pShape)
    {
        CollisionShape shape = pShape as CollisionShape;
        return shape.IsConcave();
    }
    public override bool IsInfinite2(object pShape)
    {
        CollisionShape shape = pShape as CollisionShape;
        return shape.IsInfinite();
    }
    public override bool IsNativeShape2(object pShape)
    {
        CollisionShape shape = pShape as CollisionShape;
        bool ret;
        switch (shape.GetShapeType())
        {
            case BroadphaseNativeTypes.BOX_SHAPE_PROXYTYPE:
                case BroadphaseNativeTypes.CONE_SHAPE_PROXYTYPE:
                case BroadphaseNativeTypes.SPHERE_SHAPE_PROXYTYPE:
                case BroadphaseNativeTypes.CYLINDER_SHAPE_PROXYTYPE:
                ret = true;
                break;
            default:
                ret = false;
                break;
        }
        return ret;
    }
    //sim.ptr, shape.ptr,prim.LocalID, prim.RawPosition, prim.RawOrientation
    public override object CreateGhostFromShape2(object pWorld, object pShape, uint pLocalID, Vector3 pRawPosition, Quaternion pRawOrientation)
    {
        IndexedMatrix bodyTransform = new IndexedMatrix();
        bodyTransform._origin = new IndexedVector3(pRawPosition.X, pRawPosition.Y, pRawPosition.Z);
        bodyTransform.SetRotation(new IndexedQuaternion(pRawOrientation.X,pRawOrientation.Y,pRawOrientation.Z,pRawOrientation.W));
        GhostObject gObj = new PairCachingGhostObject();
        gObj.SetWorldTransform(bodyTransform);
        CollisionShape shape = pShape as CollisionShape;
        gObj.SetCollisionShape(shape);
        gObj.SetUserPointer(pLocalID);
        // TODO: Add to Special CollisionObjects!
        return gObj;
    }

    public static void SetCollisionShape2(object pWorld, object pObj, object pShape)
    {
        var world = pWorld as DiscreteDynamicsWorld;
        var obj = pObj as CollisionObject;
        var shape = pShape as CollisionShape;
        obj.SetCollisionShape(shape);

    }
    //(PhysicsScene.World.ptr, nativeShapeData)
    public override object BuildNativeShape2(object pWorld, ShapeData pShapeData)
    {
        var world = pWorld as DiscreteDynamicsWorld;
        CollisionShape shape = null;
        switch (pShapeData.Type)
        {
            case BSPhysicsShapeType.SHAPE_BOX:
                shape = new BoxShape(new IndexedVector3(0.5f,0.5f,0.5f));
                break;
            case BSPhysicsShapeType.SHAPE_CONE:
                shape = new ConeShapeZ(0.5f, 1.0f);
                break;
            case BSPhysicsShapeType.SHAPE_CYLINDER:
                shape = new CylinderShapeZ(new IndexedVector3(0.5f, 0.5f, 0.5f));
                break;
            case BSPhysicsShapeType.SHAPE_SPHERE:
                shape = new SphereShape(0.5f);
                break;

        }
        if (shape != null)
        {
            IndexedVector3 scaling = new IndexedVector3(pShapeData.Scale.X, pShapeData.Scale.Y, pShapeData.Scale.Z);
            shape.SetMargin(world.WorldSettings.Params.collisionMargin);
            shape.SetLocalScaling(ref scaling);

        }
        return shape;
    }
    //PhysicsScene.World.ptr, false
    public override object CreateCompoundShape2(object pWorld, bool enableDynamicAabbTree)
    {
        return new CompoundShape(enableDynamicAabbTree);
    }

    public override int GetNumberOfCompoundChildren2(object pCompoundShape)
    {
        var compoundshape = pCompoundShape as CompoundShape;
        return compoundshape.GetNumChildShapes();
    }
    //LinksetRoot.PhysShape.ptr, newShape.ptr, displacementPos, displacementRot
    public override void AddChildShapeToCompoundShape2(object pCShape, object paddShape, Vector3 displacementPos, Quaternion displacementRot)
    {
        IndexedMatrix relativeTransform = new IndexedMatrix();
        var compoundshape = pCShape as CompoundShape;
        var addshape = paddShape as CollisionShape;

        relativeTransform._origin = new IndexedVector3(displacementPos.X, displacementPos.Y, displacementPos.Z);
        relativeTransform.SetRotation(new IndexedQuaternion(displacementRot.X,displacementRot.Y,displacementRot.Z,displacementRot.W));
        compoundshape.AddChildShape(ref relativeTransform, addshape);

    }

    public override object RemoveChildShapeFromCompoundShapeIndex2(object pCShape, int pii)
    {
        var compoundshape = pCShape as CompoundShape;
        CollisionShape ret = null;
        ret = compoundshape.GetChildShape(pii);
        compoundshape.RemoveChildShapeByIndex(pii);
        return ret;
    }

    public override object CreateGroundPlaneShape2(uint pLocalId, float pheight, float pcollisionMargin)
    {
        StaticPlaneShape m_planeshape = new StaticPlaneShape(new IndexedVector3(0,0,1),(int)pheight );
        m_planeshape.SetMargin(pcollisionMargin);
        m_planeshape.SetUserPointer(pLocalId);
        return m_planeshape;
    }

    public override object CreateHingeConstraint2(object pWorld, object pBody1, object ppBody2, Vector3 ppivotInA, Vector3 ppivotInB, Vector3 paxisInA, Vector3 paxisInB, bool puseLinearReferenceFrameA, bool pdisableCollisionsBetweenLinkedBodies)
    {
        HingeConstraint constrain = null;
        var rb1 = pBody1 as RigidBody;
        var rb2 = ppBody2 as RigidBody;
        if (rb1 != null && rb2 != null)
        {
            IndexedVector3 pivotInA = new IndexedVector3(ppivotInA.X, ppivotInA.Y, ppivotInA.Z);
            IndexedVector3 pivotInB = new IndexedVector3(ppivotInB.X, ppivotInB.Y, ppivotInB.Z);
            IndexedVector3 axisInA = new IndexedVector3(paxisInA.X, paxisInA.Y, paxisInA.Z);
            IndexedVector3 axisInB = new IndexedVector3(paxisInB.X, paxisInB.Y, paxisInB.Z);
            var world = pWorld as DiscreteDynamicsWorld;
            world.AddConstraint(constrain, pdisableCollisionsBetweenLinkedBodies);
        }
        return constrain;
    }

    public override bool ReleaseHeightMapInfo2(object pMapInfo)
    {
        if (pMapInfo != null)
        {
            BulletHeightMapInfo mapinfo = pMapInfo as BulletHeightMapInfo;
            if (mapinfo.heightMap != null)
                mapinfo.heightMap = null;


        }
        return true;
    }

    public override object CreateHullShape2(object pWorld, int pHullCount, float[] pConvHulls)
    {
        CompoundShape compoundshape = new CompoundShape(false);
        var world = pWorld as DiscreteDynamicsWorld;
        
        
        compoundshape.SetMargin(world.WorldSettings.Params.collisionMargin);
        int ii = 1;

        for (int i = 0; i < pHullCount; i++)
        {
            int vertexCount = (int) pConvHulls[ii];

            IndexedVector3 centroid = new IndexedVector3(pConvHulls[ii + 1], pConvHulls[ii + 2], pConvHulls[ii + 3]);
            IndexedMatrix childTrans = IndexedMatrix.Identity;
            childTrans._origin = centroid;

            List<IndexedVector3> virts = new List<IndexedVector3>();
            int ender = ((ii + 4) + (vertexCount*3));
            for (int iii = ii + 4; iii < ender; iii+=3)
            {
               
                virts.Add(new IndexedVector3(pConvHulls[iii], pConvHulls[iii + 1], pConvHulls[iii +2]));
            }
            ConvexHullShape convexShape = new ConvexHullShape(virts, vertexCount);
            convexShape.SetMargin(world.WorldSettings.Params.collisionMargin);
            compoundshape.AddChildShape(ref childTrans, convexShape);
            ii += (vertexCount*3 + 4);
        }

        
        return compoundshape;
    }

    public override object CreateMeshShape2(object pWorld, int pIndicesCount, int[] indices, int pVerticesCount, float[] verticesAsFloats)
    {
        //DumpRaw(indices,verticesAsFloats,pIndicesCount,pVerticesCount);
        
        for (int iter = 0; iter < pVerticesCount; iter++)
        {
            if (verticesAsFloats[iter] > 0 && verticesAsFloats[iter] < 0.0001) verticesAsFloats[iter] = 0;
            if (verticesAsFloats[iter] < 0 && verticesAsFloats[iter] > -0.0001) verticesAsFloats[iter] = 0;
        }
        
        ObjectArray<int> indicesarr = new ObjectArray<int>(indices);
        ObjectArray<float> vertices = new ObjectArray<float>(verticesAsFloats);
        DumpRaw(indicesarr,vertices,pIndicesCount,pVerticesCount);
        var world = pWorld as DiscreteDynamicsWorld;
        IndexedMesh mesh = new IndexedMesh();
        mesh.m_indexType = PHY_ScalarType.PHY_INTEGER;
        mesh.m_numTriangles = pIndicesCount/3;
        mesh.m_numVertices = pVerticesCount;
        mesh.m_triangleIndexBase = indicesarr;
        mesh.m_vertexBase = vertices;
        mesh.m_vertexStride = 3;
        mesh.m_vertexType = PHY_ScalarType.PHY_FLOAT;
        mesh.m_triangleIndexStride = 3;
        
        TriangleIndexVertexArray tribuilder = new TriangleIndexVertexArray();
        tribuilder.AddIndexedMesh(mesh, PHY_ScalarType.PHY_INTEGER);
        BvhTriangleMeshShape meshShape = new BvhTriangleMeshShape(tribuilder, true,true);
        meshShape.SetMargin(world.WorldSettings.Params.collisionMargin);
       // world.UpdateSingleAabb(meshShape);
        return meshShape;

    }
    public static void DumpRaw(ObjectArray<int>indices, ObjectArray<float> vertices, int pIndicesCount,int pVerticesCount )
    {
        
        String fileName = "objTest3.raw";
        String completePath = System.IO.Path.Combine(Util.configDir(), fileName);
        StreamWriter sw = new StreamWriter(completePath);
        IndexedMesh mesh = new IndexedMesh();

        mesh.m_indexType = PHY_ScalarType.PHY_INTEGER;
        mesh.m_numTriangles = pIndicesCount / 3;
        mesh.m_numVertices = pVerticesCount;
        mesh.m_triangleIndexBase = indices;
        mesh.m_vertexBase = vertices;
        mesh.m_vertexStride = 3;
        mesh.m_vertexType = PHY_ScalarType.PHY_FLOAT;
        mesh.m_triangleIndexStride = 3;

        TriangleIndexVertexArray tribuilder = new TriangleIndexVertexArray();
        tribuilder.AddIndexedMesh(mesh, PHY_ScalarType.PHY_INTEGER);



        for (int i = 0; i < pVerticesCount; i++)
        {

            string s = vertices[indices[i * 3]].ToString("0.0000");
            s += " " + vertices[indices[i * 3 + 1]].ToString("0.0000");
            s += " " + vertices[indices[i * 3 + 2]].ToString("0.0000");
        
            sw.Write(s + "\n");
        }

        sw.Close();
    }
    public static void DumpRaw(int[] indices, float[] vertices, int pIndicesCount, int pVerticesCount)
    {

        String fileName = "objTest6.raw";
        String completePath = System.IO.Path.Combine(Util.configDir(), fileName);
        StreamWriter sw = new StreamWriter(completePath);
        IndexedMesh mesh = new IndexedMesh();

        mesh.m_indexType = PHY_ScalarType.PHY_INTEGER;
        mesh.m_numTriangles = pIndicesCount / 3;
        mesh.m_numVertices = pVerticesCount;
        mesh.m_triangleIndexBase = indices;
        mesh.m_vertexBase = vertices;
        mesh.m_vertexStride = 3;
        mesh.m_vertexType = PHY_ScalarType.PHY_FLOAT;
        mesh.m_triangleIndexStride = 3;
        
        TriangleIndexVertexArray tribuilder = new TriangleIndexVertexArray();
        tribuilder.AddIndexedMesh(mesh, PHY_ScalarType.PHY_INTEGER);


        sw.WriteLine("Indices");
        sw.WriteLine(string.Format("int[] indices = new int[{0}];",pIndicesCount));
        for (int iter = 0; iter < indices.Length; iter++)
        {
            sw.WriteLine(string.Format("indices[{0}]={1};",iter,indices[iter]));
        }
        sw.WriteLine("VerticesFloats");
        sw.WriteLine(string.Format("float[] vertices = new float[{0}];", pVerticesCount));
        for (int iter = 0; iter < vertices.Length; iter++)
        {
            sw.WriteLine(string.Format("Vertices[{0}]={1};", iter, vertices[iter].ToString("0.0000")));
        }

            // for (int i = 0; i < pVerticesCount; i++)
            // {
            //
            //     string s = vertices[indices[i * 3]].ToString("0.0000");
            //     s += " " + vertices[indices[i * 3 + 1]].ToString("0.0000");
            //    s += " " + vertices[indices[i * 3 + 2]].ToString("0.0000");
            //
            //     sw.Write(s + "\n");
            //}

            sw.Close();
    }
    //PhysicsScene.World.ptr, m_mapInfo.ID, m_mapInfo.minCoords, m_mapInfo.maxCoords,  m_mapInfo.heightMap, PhysicsScene.Params.terrainCollisionMargin
    public override object CreateHeightMapInfo2(object pWorld, uint pId, Vector3 pminCoords, Vector3 pmaxCoords, float[] pheightMap, float pCollisionMargin)
    {
        BulletHeightMapInfo mapInfo = new BulletHeightMapInfo(pId, pheightMap, null);
        mapInfo.heightMap = null;
        mapInfo.minCoords = pminCoords;
        mapInfo.maxCoords = pmaxCoords;
        mapInfo.sizeX = (int) (pmaxCoords.X - pminCoords.X);
        mapInfo.sizeY = (int) (pmaxCoords.Y - pminCoords.Y);
        mapInfo.ID = pId;
        mapInfo.minZ = pminCoords.Z;
        mapInfo.maxZ = pmaxCoords.Z;
        mapInfo.collisionMargin = pCollisionMargin;
        if (mapInfo.minZ == mapInfo.maxZ)
            mapInfo.minZ -= 0.2f;
        mapInfo.heightMap = pheightMap;

        return mapInfo;

    }

    public override object CreateTerrainShape2(object pMapInfo)
    {
        BulletHeightMapInfo mapinfo = pMapInfo as BulletHeightMapInfo;
        const int upAxis = 2;
        const float scaleFactor = 1.0f;
        HeightfieldTerrainShape terrainShape = new HeightfieldTerrainShape((int)mapinfo.sizeX, (int)mapinfo.sizeY,
                                                                           mapinfo.heightMap,  scaleFactor,
                                                                           mapinfo.minZ, mapinfo.maxZ, upAxis,
                                                                            false);
        terrainShape.SetMargin(mapinfo.collisionMargin + 0.5f);
        terrainShape.SetUseDiamondSubdivision(true);
        terrainShape.SetUserPointer(mapinfo.ID);
        return terrainShape;
    }

    public override bool TranslationalLimitMotor2(object pConstraint, float ponOff, float targetVelocity, float maxMotorForce)
    {
        TypedConstraint tconstrain = pConstraint as TypedConstraint;
        bool onOff = ponOff != 0;
        bool ret = false;

        switch (tconstrain.GetConstraintType())
        {
            case TypedConstraintType.D6_CONSTRAINT_TYPE:
                Generic6DofConstraint constrain = pConstraint as Generic6DofConstraint;
                constrain.GetTranslationalLimitMotor().m_enableMotor[0] = onOff;
                constrain.GetTranslationalLimitMotor().m_targetVelocity[0] = targetVelocity;
                constrain.GetTranslationalLimitMotor().m_maxMotorForce[0] = maxMotorForce;
                ret = true;
                break;
        }


        return ret;

    }

    public override int PhysicsStep2(object pWorld, float timeStep, int m_maxSubSteps, float m_fixedTimeStep, out int updatedEntityCount, out List<BulletXNA.EntityProperties> updatedEntities, out int collidersCount, out List<BulletXNA.CollisionDesc>colliders)
    {
        int epic = PhysicsStepint2(pWorld, timeStep, m_maxSubSteps, m_fixedTimeStep, out updatedEntityCount, out updatedEntities,
                                out collidersCount, out colliders);
        return epic;
    }

    private static int PhysicsStepint2(object pWorld,float timeStep, int m_maxSubSteps, float m_fixedTimeStep, out int updatedEntityCount, out List<BulletXNA.EntityProperties> updatedEntities, out int collidersCount, out List<BulletXNA.CollisionDesc> colliders)
    {
        int numSimSteps = 0;
      

        //if (updatedEntities is null)
        //    updatedEntities = new List<BulletXNA.EntityProperties>();

        //if (colliders is null)
        //    colliders = new List<BulletXNA.CollisionDesc>();
        

        if (pWorld is DiscreteDynamicsWorld)
        {
            DiscreteDynamicsWorld world = pWorld as DiscreteDynamicsWorld;

            numSimSteps = world.StepSimulation(timeStep, m_maxSubSteps, m_fixedTimeStep);
            int updates = 0;

            updatedEntityCount = world.UpdatedObjects.Count;
            updatedEntities = new List<BulletXNA.EntityProperties>(world.UpdatedObjects);
            updatedEntityCount = updatedEntities.Count;
            world.UpdatedObjects.Clear();
            

            collidersCount = world.UpdatedCollisions.Count;
            colliders = new List<BulletXNA.CollisionDesc>(world.UpdatedCollisions);

            world.UpdatedCollisions.Clear();
            m_collisionsThisFrame = 0;
            int numManifolds = world.GetDispatcher().GetNumManifolds();
            for (int j = 0; j < numManifolds; j++)
            {
                PersistentManifold contactManifold = world.GetDispatcher().GetManifoldByIndexInternal(j);
                int numContacts = contactManifold.GetNumContacts();
                if (numContacts == 0)
                    continue;

                CollisionObject objA = contactManifold.GetBody0() as CollisionObject;
                CollisionObject objB = contactManifold.GetBody1() as CollisionObject;

                ManifoldPoint manifoldPoint = contactManifold.GetContactPoint(0);
                IndexedVector3 contactPoint = manifoldPoint.GetPositionWorldOnB();
                IndexedVector3 contactNormal = -manifoldPoint.m_normalWorldOnB; // make relative to A

                RecordCollision(world, objA, objB, contactPoint, contactNormal);
                m_collisionsThisFrame ++;
                if (m_collisionsThisFrame >= 9999999)
                    break;


            }


        }
        else
        {
            //if (updatedEntities is null)
            updatedEntities = new List<BulletXNA.EntityProperties>();
            updatedEntityCount = 0;
            //if (colliders is null)
            colliders = new List<BulletXNA.CollisionDesc>();
            collidersCount = 0;
        }
        return numSimSteps;
    }

    private static void RecordCollision(CollisionWorld world,CollisionObject objA, CollisionObject objB, IndexedVector3 contact, IndexedVector3 norm)
    {
       
        IndexedVector3 contactNormal = norm;
        if ((objA.GetCollisionFlags() & BulletXNA.BulletCollision.CollisionFlags.BS_WANTS_COLLISIONS) == 0 &&
            (objB.GetCollisionFlags() & BulletXNA.BulletCollision.CollisionFlags.BS_WANTS_COLLISIONS) == 0)
        {
            return;
        }
        uint idA = (uint)objA.GetUserPointer();
        uint idB = (uint)objB.GetUserPointer();
        if (idA > idB)
        {
            uint temp = idA;
            idA = idB;
            idB = temp;
            contactNormal = -contactNormal;
        }

        ulong collisionID = ((ulong) idA << 32) | idB;

        BulletXNA.CollisionDesc cDesc = new BulletXNA.CollisionDesc()
                                            {
                                                aID = idA,
                                                bID = idB,
                                                point = contact,
                                                normal = contactNormal
                                            };
        world.UpdatedCollisions.Add(cDesc);
        m_collisionsThisFrame++;


    }
    private static EntityProperties GetDebugProperties(object pWorld, object pBody)
    {
        EntityProperties ent = new EntityProperties();
        DiscreteDynamicsWorld world = pWorld as DiscreteDynamicsWorld;
        RigidBody body = pBody as RigidBody;
        IndexedMatrix transform = body.GetWorldTransform();
        IndexedVector3 LinearVelocity = body.GetInterpolationLinearVelocity();
        IndexedVector3 AngularVelocity = body.GetInterpolationAngularVelocity();
        IndexedQuaternion rotation = transform.GetRotation();
        ent.Acceleration = Vector3.Zero;
        ent.ID = (uint)body.GetUserPointer();
        ent.Position = new Vector3(transform._origin.X,transform._origin.Y,transform._origin.Z);
        ent.Rotation = new Quaternion(rotation.X,rotation.Y,rotation.Z,rotation.W);
        ent.Velocity = new Vector3(LinearVelocity.X, LinearVelocity.Y, LinearVelocity.Z);
        ent.RotationalVelocity = new Vector3(AngularVelocity.X, AngularVelocity.Y, AngularVelocity.Z);
        return ent;
    }

    public override Vector3 GetLocalScaling2(object pBody)
    {
        CollisionShape shape = pBody as CollisionShape;
        IndexedVector3 scale = shape.GetLocalScaling();
        return new Vector3(scale.X,scale.Y,scale.Z);
    }

    public override bool RayCastGround(object pWorld, Vector3 _RayOrigin, float pRayHeight, object NotMe)
    {
        DynamicsWorld world = pWorld as DynamicsWorld;
        if (world != null)
        {
            if (NotMe is CollisionObject || NotMe is RigidBody)
            {
                CollisionObject AvoidBody = NotMe as CollisionObject;
                
                IndexedVector3 rOrigin = new IndexedVector3(_RayOrigin.X, _RayOrigin.Y, _RayOrigin.Z);
                IndexedVector3 rEnd = new IndexedVector3(_RayOrigin.X, _RayOrigin.Y, _RayOrigin.Z - pRayHeight);
                using (
                    ClosestNotMeRayResultCallback rayCallback = new ClosestNotMeRayResultCallback(rOrigin,
                                                                                                  rEnd, AvoidBody)
                    )
                {
                    world.RayTest(ref rOrigin, ref rEnd, rayCallback);
                    if (rayCallback.HasHit())
                    {
                        IndexedVector3 hitLocation = rayCallback.m_hitPointWorld;
                        
                    }
                    return rayCallback.HasHit();
                }
           }
        }
        return false;
    }
}
*/
}
