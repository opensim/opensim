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
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

using OpenSim.Framework;

using OpenMetaverse;

using BulletXNA;
using BulletXNA.LinearMath;
using BulletXNA.BulletCollision;
using BulletXNA.BulletDynamics;
using BulletXNA.BulletCollision.CollisionDispatch;

namespace OpenSim.Region.PhysicsModule.BulletS
{
public sealed class BSAPIXNA : BSAPITemplate
{
private sealed class BulletWorldXNA : BulletWorld
{
    public DiscreteDynamicsWorld world;
    public BulletWorldXNA(uint id, BSScene physScene, DiscreteDynamicsWorld xx)
        : base(id, physScene)
    {
        world = xx;
    }
}

private sealed class BulletBodyXNA : BulletBody
{
    public CollisionObject body;
    public RigidBody rigidBody { get { return RigidBody.Upcast(body); } }

    public BulletBodyXNA(uint id, CollisionObject xx)
        : base(id)
    {
        body = xx;
    }
    public override bool HasPhysicalBody
    {
        get { return body != null; }
    }
    public override void Clear()
    {
        body = null;
    }
    public override string AddrString
    {
        get { return "XNARigidBody"; }
    }
}

private sealed class BulletShapeXNA : BulletShape
{
    public CollisionShape shape;
    public BulletShapeXNA(CollisionShape xx, BSPhysicsShapeType typ)
        : base()
    {
        shape = xx;
        shapeType = typ;
    }
    public override bool HasPhysicalShape
    {
        get { return shape != null; }
    }
    public override void Clear()
    {
        shape = null;
    }
    public override BulletShape Clone()
    {
        return new BulletShapeXNA(shape, shapeType);
    }
    public override bool ReferenceSame(BulletShape other)
    {
        BulletShapeXNA otheru = other as BulletShapeXNA;
        return (otheru != null) && (this.shape == otheru.shape);

    }
    public override string AddrString
    {
        get { return "XNACollisionShape"; }
    }
}
private sealed class BulletConstraintXNA : BulletConstraint
{
    public TypedConstraint constrain;
    public BulletConstraintXNA(TypedConstraint xx) : base()
    {
        constrain = xx;
    }

    public override void Clear()
    {
        constrain = null;
    }
    public override bool HasPhysicalConstraint { get { return constrain != null; } }

    // Used for log messages for a unique display of the memory/object allocated to this instance
    public override string AddrString
    {
        get { return "XNAConstraint"; }
    }
}
    internal int m_maxCollisions;
    internal CollisionDesc[] UpdatedCollisions;
    internal int LastCollisionDesc = 0;
    internal int m_maxUpdatesPerFrame;
    internal int LastEntityProperty = 0;

    internal EntityProperties[] UpdatedObjects;
    internal Dictionary<uint, GhostObject> specialCollisionObjects;

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
    public override bool RemoveObjectFromWorld(BulletWorld pWorld, BulletBody pBody)
    {
        DiscreteDynamicsWorld world = (pWorld as BulletWorldXNA).world;
        RigidBody body = ((BulletBodyXNA)pBody).rigidBody;
        CollisionObject collisionObject = ((BulletBodyXNA)pBody).body;
        if (body != null)
            world.RemoveRigidBody(body);
        else if (collisionObject != null)
            world.RemoveCollisionObject(collisionObject);
        else
            return false;
        return true;
    }

    public override bool ClearCollisionProxyCache(BulletWorld pWorld, BulletBody pBody)
    {
        DiscreteDynamicsWorld world = (pWorld as BulletWorldXNA).world;
        RigidBody body = ((BulletBodyXNA)pBody).rigidBody;
        CollisionObject collisionObject = ((BulletBodyXNA)pBody).body;
        if (body != null && collisionObject != null && collisionObject.GetBroadphaseHandle() != null)
        {
            world.RemoveCollisionObject(collisionObject);
            world.AddCollisionObject(collisionObject);
        }
        return true;
    }

    public override bool AddConstraintToWorld(BulletWorld pWorld, BulletConstraint pConstraint, bool pDisableCollisionsBetweenLinkedObjects)
    {
        DiscreteDynamicsWorld world = (pWorld as BulletWorldXNA).world;
        TypedConstraint constraint = (pConstraint as BulletConstraintXNA).constrain;
        world.AddConstraint(constraint, pDisableCollisionsBetweenLinkedObjects);

        return true;

    }

    public override bool RemoveConstraintFromWorld(BulletWorld pWorld, BulletConstraint pConstraint)
    {
        DiscreteDynamicsWorld world = (pWorld as BulletWorldXNA).world;
        TypedConstraint constraint = (pConstraint as BulletConstraintXNA).constrain;
        world.RemoveConstraint(constraint);
        return true;
    }

    public override void SetRestitution(BulletBody pCollisionObject, float pRestitution)
    {
        CollisionObject collisionObject = (pCollisionObject as BulletBodyXNA).body;
        collisionObject.SetRestitution(pRestitution);
    }

    public override int GetShapeType(BulletShape pShape)
    {
        CollisionShape shape = (pShape as BulletShapeXNA).shape;
        return (int)shape.GetShapeType();
    }
    public override void SetMargin(BulletShape pShape, float pMargin)
    {
        CollisionShape shape = (pShape as BulletShapeXNA).shape;
        shape.SetMargin(pMargin);
    }

    public override float GetMargin(BulletShape pShape)
    {
        CollisionShape shape = (pShape as BulletShapeXNA).shape;
        return shape.GetMargin();
    }

    public override void SetLocalScaling(BulletShape pShape, Vector3 pScale)
    {
        CollisionShape shape = (pShape as BulletShapeXNA).shape;
        IndexedVector3 vec = new IndexedVector3(pScale.X, pScale.Y, pScale.Z);
        shape.SetLocalScaling(ref vec);

    }

    public override void SetContactProcessingThreshold(BulletBody pCollisionObject, float contactprocessingthreshold)
    {
        CollisionObject collisionObject = (pCollisionObject as BulletBodyXNA).rigidBody;
        collisionObject.SetContactProcessingThreshold(contactprocessingthreshold);
    }

    public override void SetCcdMotionThreshold(BulletBody pCollisionObject, float pccdMotionThreashold)
    {
        CollisionObject collisionObject = (pCollisionObject as BulletBodyXNA).body;
        collisionObject.SetCcdMotionThreshold(pccdMotionThreashold);
    }

    public override void SetCcdSweptSphereRadius(BulletBody pCollisionObject, float pCcdSweptSphereRadius)
    {
        CollisionObject collisionObject = (pCollisionObject as BulletBodyXNA).body;
        collisionObject.SetCcdSweptSphereRadius(pCcdSweptSphereRadius);
    }

    public override void SetAngularFactorV(BulletBody pBody, Vector3 pAngularFactor)
    {
        RigidBody body = (pBody as BulletBodyXNA).rigidBody;
        body.SetAngularFactor(new IndexedVector3(pAngularFactor.X, pAngularFactor.Y, pAngularFactor.Z));
    }

    public override CollisionFlags AddToCollisionFlags(BulletBody pCollisionObject, CollisionFlags pcollisionFlags)
    {
        CollisionObject collisionObject = (pCollisionObject as BulletBodyXNA).body;
        CollisionFlags existingcollisionFlags = (CollisionFlags)(uint)collisionObject.GetCollisionFlags();
        existingcollisionFlags |= pcollisionFlags;
        collisionObject.SetCollisionFlags((BulletXNA.BulletCollision.CollisionFlags)(uint)existingcollisionFlags);
        return (CollisionFlags) (uint) existingcollisionFlags;
    }

    public override bool AddObjectToWorld(BulletWorld pWorld, BulletBody pBody)
    {
        DiscreteDynamicsWorld world = (pWorld as BulletWorldXNA).world;
        CollisionObject cbody = (pBody as BulletBodyXNA).body;
        RigidBody rbody = cbody as RigidBody;

        // Bullet resets several variables when an object is added to the world. In particular,
        //   BulletXNA resets position and rotation. Gravity is also reset depending on the static/dynamic
        //   type. Of course, the collision flags in the broadphase proxy are initialized to default.
        IndexedMatrix origPos = cbody.GetWorldTransform();
        if (rbody != null)
        {
            IndexedVector3 origGrav = rbody.GetGravity();
            world.AddRigidBody(rbody);
            rbody.SetGravity(origGrav);
        }
        else
        {
            world.AddCollisionObject(cbody);
        }
        cbody.SetWorldTransform(origPos);

        pBody.ApplyCollisionMask(pWorld.physicsScene);

        //if (body.GetBroadphaseHandle() != null)
        //    world.UpdateSingleAabb(body);
        return true;
    }

    public override void ForceActivationState(BulletBody pCollisionObject, ActivationState pActivationState)
    {
        CollisionObject collisionObject = (pCollisionObject as BulletBodyXNA).body;
        collisionObject.ForceActivationState((BulletXNA.BulletCollision.ActivationState)(uint)pActivationState);
    }

    public override void UpdateSingleAabb(BulletWorld pWorld, BulletBody pCollisionObject)
    {
        DiscreteDynamicsWorld world = (pWorld as BulletWorldXNA).world;
        CollisionObject collisionObject = (pCollisionObject as BulletBodyXNA).body;
        world.UpdateSingleAabb(collisionObject);
    }

    public override void UpdateAabbs(BulletWorld pWorld) {
        DiscreteDynamicsWorld world = (pWorld as BulletWorldXNA).world;
        world.UpdateAabbs();
    }
    public override bool GetForceUpdateAllAabbs(BulletWorld pWorld) {
        DiscreteDynamicsWorld world = (pWorld as BulletWorldXNA).world;
        return world.GetForceUpdateAllAabbs();

    }
    public override void SetForceUpdateAllAabbs(BulletWorld pWorld, bool pForce)
    {
        DiscreteDynamicsWorld world = (pWorld as BulletWorldXNA).world;
        world.SetForceUpdateAllAabbs(pForce);
    }

    public override bool SetCollisionGroupMask(BulletBody pCollisionObject, uint pGroup, uint pMask)
    {
        CollisionObject collisionObject = (pCollisionObject as BulletBodyXNA).body;
        collisionObject.GetBroadphaseHandle().m_collisionFilterGroup = (BulletXNA.BulletCollision.CollisionFilterGroups) pGroup;
        collisionObject.GetBroadphaseHandle().m_collisionFilterGroup = (BulletXNA.BulletCollision.CollisionFilterGroups) pGroup;
        if ((uint) collisionObject.GetBroadphaseHandle().m_collisionFilterGroup == 0)
            return false;
        return true;
    }

    public override void ClearAllForces(BulletBody pCollisionObject)
    {
        CollisionObject collisionObject = (pCollisionObject as BulletBodyXNA).body;
        IndexedVector3 zeroVector = new IndexedVector3(0, 0, 0);
        collisionObject.SetInterpolationLinearVelocity(ref zeroVector);
        collisionObject.SetInterpolationAngularVelocity(ref zeroVector);
        IndexedMatrix bodytransform = collisionObject.GetWorldTransform();

        collisionObject.SetInterpolationWorldTransform(ref bodytransform);

        if (collisionObject is RigidBody)
        {
            RigidBody rigidbody = collisionObject as RigidBody;
            rigidbody.SetLinearVelocity(zeroVector);
            rigidbody.SetAngularVelocity(zeroVector);
            rigidbody.ClearForces();
        }
    }

    public override void SetInterpolationAngularVelocity(BulletBody pCollisionObject, Vector3 pVector3)
    {
        CollisionObject collisionObject = (pCollisionObject as BulletBodyXNA).rigidBody;
        IndexedVector3 vec = new IndexedVector3(pVector3.X, pVector3.Y, pVector3.Z);
        collisionObject.SetInterpolationAngularVelocity(ref vec);
    }

    public override void SetAngularVelocity(BulletBody pBody, Vector3 pVector3)
    {
        RigidBody body = (pBody as BulletBodyXNA).rigidBody;
        IndexedVector3 vec = new IndexedVector3(pVector3.X, pVector3.Y, pVector3.Z);
        body.SetAngularVelocity(ref vec);
    }
    public override Vector3 GetTotalForce(BulletBody pBody)
    {
        RigidBody body = (pBody as BulletBodyXNA).rigidBody;
        IndexedVector3 iv3 = body.GetTotalForce();
        return new Vector3(iv3.X, iv3.Y, iv3.Z);
    }
    public override Vector3 GetTotalTorque(BulletBody pBody)
    {
        RigidBody body = (pBody as BulletBodyXNA).rigidBody;
        IndexedVector3 iv3 = body.GetTotalTorque();
        return new Vector3(iv3.X, iv3.Y, iv3.Z);
    }
    public override Vector3 GetInvInertiaDiagLocal(BulletBody pBody)
    {
        RigidBody body = (pBody as BulletBodyXNA).rigidBody;
        IndexedVector3 iv3 = body.GetInvInertiaDiagLocal();
        return new Vector3(iv3.X, iv3.Y, iv3.Z);
    }
    public override void SetInvInertiaDiagLocal(BulletBody pBody, Vector3 inert)
    {
        RigidBody body = (pBody as BulletBodyXNA).rigidBody;
        IndexedVector3 iv3 = new IndexedVector3(inert.X, inert.Y, inert.Z);
        body.SetInvInertiaDiagLocal(ref iv3);
    }
    public override void ApplyForce(BulletBody pBody, Vector3 force, Vector3 pos)
    {
        RigidBody body = (pBody as BulletBodyXNA).rigidBody;
        IndexedVector3 forceiv3 = new IndexedVector3(force.X, force.Y, force.Z);
        IndexedVector3 posiv3 = new IndexedVector3(pos.X, pos.Y, pos.Z);
        body.ApplyForce(ref forceiv3, ref posiv3);
    }
    public override void ApplyImpulse(BulletBody pBody, Vector3 imp, Vector3 pos)
    {
        RigidBody body = (pBody as BulletBodyXNA).rigidBody;
        IndexedVector3 impiv3 = new IndexedVector3(imp.X, imp.Y, imp.Z);
        IndexedVector3 posiv3 = new IndexedVector3(pos.X, pos.Y, pos.Z);
        body.ApplyImpulse(ref impiv3, ref posiv3);
    }

    public override void ClearForces(BulletBody pBody)
    {
        RigidBody body = (pBody as BulletBodyXNA).rigidBody;
        body.ClearForces();
    }

    public override void SetTranslation(BulletBody pCollisionObject, Vector3 _position, Quaternion _orientation)
    {
        CollisionObject collisionObject = (pCollisionObject as BulletBodyXNA).body;
        IndexedVector3 vposition = new IndexedVector3(_position.X, _position.Y, _position.Z);
        IndexedQuaternion vquaternion = new IndexedQuaternion(_orientation.X, _orientation.Y, _orientation.Z,
                                                              _orientation.W);
        IndexedMatrix mat = IndexedMatrix.CreateFromQuaternion(vquaternion);
        mat._origin = vposition;
        collisionObject.SetWorldTransform(mat);

    }

    public override Vector3 GetPosition(BulletBody pCollisionObject)
    {
        CollisionObject collisionObject = (pCollisionObject as BulletBodyXNA).rigidBody;
        IndexedVector3 pos = collisionObject.GetInterpolationWorldTransform()._origin;
        return new Vector3(pos.X, pos.Y, pos.Z);
    }

    public override Vector3 CalculateLocalInertia(BulletShape pShape, float pphysMass)
    {
        CollisionShape shape = (pShape as BulletShapeXNA).shape;
        IndexedVector3 inertia = IndexedVector3.Zero;
        shape.CalculateLocalInertia(pphysMass, out inertia);
        return new Vector3(inertia.X, inertia.Y, inertia.Z);
    }

    public override void SetMassProps(BulletBody pBody, float pphysMass, Vector3 plocalInertia)
    {
        RigidBody body = (pBody as BulletBodyXNA).rigidBody;
        if (body != null) // Can't set mass props on collision object.
        {
            IndexedVector3 inertia = new IndexedVector3(plocalInertia.X, plocalInertia.Y, plocalInertia.Z);
            body.SetMassProps(pphysMass, inertia);
        }
    }


    public override void SetObjectForce(BulletBody pBody, Vector3 _force)
    {
        RigidBody body = (pBody as BulletBodyXNA).rigidBody;
        IndexedVector3 force = new IndexedVector3(_force.X, _force.Y, _force.Z);
        body.SetTotalForce(ref force);
    }

    public override void SetFriction(BulletBody pCollisionObject, float _currentFriction)
    {
        CollisionObject collisionObject = (pCollisionObject as BulletBodyXNA).body;
        collisionObject.SetFriction(_currentFriction);
    }

    public override void SetLinearVelocity(BulletBody pBody, Vector3 _velocity)
    {
        RigidBody body = (pBody as BulletBodyXNA).rigidBody;
        IndexedVector3 velocity = new IndexedVector3(_velocity.X, _velocity.Y, _velocity.Z);
        body.SetLinearVelocity(velocity);
    }

    public override void Activate(BulletBody pCollisionObject, bool pforceactivation)
    {
        CollisionObject collisionObject = (pCollisionObject as BulletBodyXNA).rigidBody;
        collisionObject.Activate(pforceactivation);

    }

    public override Quaternion GetOrientation(BulletBody pCollisionObject)
    {
        CollisionObject collisionObject = (pCollisionObject as BulletBodyXNA).rigidBody;
        IndexedQuaternion mat = collisionObject.GetInterpolationWorldTransform().GetRotation();
        return new Quaternion(mat.X, mat.Y, mat.Z, mat.W);
    }

    public override CollisionFlags RemoveFromCollisionFlags(BulletBody pCollisionObject, CollisionFlags pcollisionFlags)
    {
        CollisionObject collisionObject = (pCollisionObject as BulletBodyXNA).body;
        CollisionFlags existingcollisionFlags = (CollisionFlags)(uint)collisionObject.GetCollisionFlags();
        existingcollisionFlags &= ~pcollisionFlags;
        collisionObject.SetCollisionFlags((BulletXNA.BulletCollision.CollisionFlags)(uint)existingcollisionFlags);
        return (CollisionFlags)(uint)existingcollisionFlags;
    }

    public override float GetCcdMotionThreshold(BulletBody pCollisionObject)
    {
        CollisionObject collisionObject = (pCollisionObject as BulletBodyXNA).rigidBody;
        return collisionObject.GetCcdSquareMotionThreshold();
    }

    public override float GetCcdSweptSphereRadius(BulletBody pCollisionObject)
    {
        CollisionObject collisionObject = (pCollisionObject as BulletBodyXNA).rigidBody;
        return collisionObject.GetCcdSweptSphereRadius();

    }

    public override IntPtr GetUserPointer(BulletBody pCollisionObject)
    {
        CollisionObject shape = (pCollisionObject as BulletBodyXNA).body;
        return (IntPtr)shape.GetUserPointer();
    }

    public override void SetUserPointer(BulletBody pCollisionObject, IntPtr val)
    {
        CollisionObject shape = (pCollisionObject as BulletBodyXNA).body;
        shape.SetUserPointer(val);
    }

    public override void SetGravity(BulletBody pBody, Vector3 pGravity)
    {
        RigidBody body = (pBody as BulletBodyXNA).rigidBody;
        if (body != null) // Can't set collisionobject.set gravity
        {
            IndexedVector3 gravity = new IndexedVector3(pGravity.X, pGravity.Y, pGravity.Z);
            body.SetGravity(gravity);
        }
    }

    public override bool DestroyConstraint(BulletWorld pWorld, BulletConstraint pConstraint)
    {
        DiscreteDynamicsWorld world = (pWorld as BulletWorldXNA).world;
        TypedConstraint constraint = (pConstraint as BulletConstraintXNA).constrain;
        world.RemoveConstraint(constraint);
        return true;
    }

    public override bool SetLinearLimits(BulletConstraint pConstraint, Vector3 low, Vector3 high)
    {
        Generic6DofConstraint constraint = (pConstraint as BulletConstraintXNA).constrain as Generic6DofConstraint;
        IndexedVector3 lowlimit = new IndexedVector3(low.X, low.Y, low.Z);
        IndexedVector3 highlimit = new IndexedVector3(high.X, high.Y, high.Z);
        constraint.SetLinearLowerLimit(lowlimit);
        constraint.SetLinearUpperLimit(highlimit);
        return true;
    }

    public override bool SetAngularLimits(BulletConstraint pConstraint, Vector3 low, Vector3 high)
    {
        Generic6DofConstraint constraint = (pConstraint as BulletConstraintXNA).constrain as Generic6DofConstraint;
        IndexedVector3 lowlimit = new IndexedVector3(low.X, low.Y, low.Z);
        IndexedVector3 highlimit = new IndexedVector3(high.X, high.Y, high.Z);
        constraint.SetAngularLowerLimit(lowlimit);
        constraint.SetAngularUpperLimit(highlimit);
        return true;
    }

    public override void SetConstraintNumSolverIterations(BulletConstraint pConstraint, float cnt)
    {
        Generic6DofConstraint constraint = (pConstraint as BulletConstraintXNA).constrain as Generic6DofConstraint;
        constraint.SetOverrideNumSolverIterations((int)cnt);
    }

    public override bool CalculateTransforms(BulletConstraint pConstraint)
    {
        Generic6DofConstraint constraint = (pConstraint as BulletConstraintXNA).constrain as Generic6DofConstraint;
        constraint.CalculateTransforms();
        return true;
    }

    public override void SetConstraintEnable(BulletConstraint pConstraint, float p_2)
    {
        Generic6DofConstraint constraint = (pConstraint as BulletConstraintXNA).constrain as Generic6DofConstraint;
        constraint.SetEnabled((p_2 == 0) ? false : true);
    }


    public override BulletConstraint Create6DofConstraint(BulletWorld pWorld, BulletBody pBody1, BulletBody pBody2,
                                    Vector3 pframe1, Quaternion pframe1rot, Vector3 pframe2, Quaternion pframe2rot,
                                    bool puseLinearReferenceFrameA, bool pdisableCollisionsBetweenLinkedBodies)

    {
        DiscreteDynamicsWorld world = (pWorld as BulletWorldXNA).world;
        RigidBody body1 = (pBody1 as BulletBodyXNA).rigidBody;
        RigidBody body2 = (pBody2 as BulletBodyXNA).rigidBody;
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

        return new BulletConstraintXNA(consttr);
    }

    public override BulletConstraint Create6DofConstraintFixed(BulletWorld pWorld, BulletBody pBody1,
                                        Vector3 pframe1, Quaternion pframe1rot,
                                        bool pUseLinearReferenceFrameB, bool pdisableCollisionsBetweenLinkedBodies)
    {
        DiscreteDynamicsWorld world = (pWorld as BulletWorldXNA).world;
        RigidBody body1 = (pBody1 as BulletBodyXNA).rigidBody;
        IndexedVector3 frame1v = new IndexedVector3(pframe1.X, pframe1.Y, pframe1.Z);
        IndexedQuaternion frame1rot = new IndexedQuaternion(pframe1rot.X, pframe1rot.Y, pframe1rot.Z, pframe1rot.W);
        IndexedMatrix frame1 = IndexedMatrix.CreateFromQuaternion(frame1rot);
        frame1._origin = frame1v;

        Generic6DofConstraint consttr = new Generic6DofConstraint(body1, ref frame1, pUseLinearReferenceFrameB);
        consttr.CalculateTransforms();
        world.AddConstraint(consttr,pdisableCollisionsBetweenLinkedBodies);

        return new BulletConstraintXNA(consttr);
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
    public override BulletConstraint Create6DofConstraintToPoint(BulletWorld pWorld, BulletBody pBody1, BulletBody pBody2, Vector3 pjoinPoint, bool puseLinearReferenceFrameA, bool pdisableCollisionsBetweenLinkedBodies)
    {
        DiscreteDynamicsWorld world = (pWorld as BulletWorldXNA).world;
        RigidBody body1 = (pBody1 as BulletBodyXNA).rigidBody;
        RigidBody body2 = (pBody2 as BulletBodyXNA).rigidBody;
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

        return new BulletConstraintXNA(consttr);
    }
    //SetFrames(m_constraint.ptr, frameA, frameArot, frameB, frameBrot);
    public override bool SetFrames(BulletConstraint pConstraint, Vector3 pframe1, Quaternion pframe1rot, Vector3 pframe2, Quaternion pframe2rot)
    {
        Generic6DofConstraint constraint = (pConstraint as BulletConstraintXNA).constrain as Generic6DofConstraint;
        IndexedVector3 frame1v = new IndexedVector3(pframe1.X, pframe1.Y, pframe1.Z);
        IndexedQuaternion frame1rot = new IndexedQuaternion(pframe1rot.X, pframe1rot.Y, pframe1rot.Z, pframe1rot.W);
        IndexedMatrix frame1 = IndexedMatrix.CreateFromQuaternion(frame1rot);
        frame1._origin = frame1v;

        IndexedVector3 frame2v = new IndexedVector3(pframe2.X, pframe2.Y, pframe2.Z);
        IndexedQuaternion frame2rot = new IndexedQuaternion(pframe2rot.X, pframe2rot.Y, pframe2rot.Z, pframe2rot.W);
        IndexedMatrix frame2 = IndexedMatrix.CreateFromQuaternion(frame2rot);
        frame2._origin = frame2v;
        constraint.SetFrames(ref frame1, ref frame2);
        return true;
    }

    public override Vector3 GetLinearVelocity(BulletBody pBody)
    {
        RigidBody body = (pBody as BulletBodyXNA).rigidBody;
        IndexedVector3 iv3 = body.GetLinearVelocity();
        return new Vector3(iv3.X, iv3.Y, iv3.Z);
    }
    public override Vector3 GetAngularVelocity(BulletBody pBody)
    {
        RigidBody body = (pBody as BulletBodyXNA).rigidBody;
        IndexedVector3 iv3 = body.GetAngularVelocity();
        return new Vector3(iv3.X, iv3.Y, iv3.Z);
    }
    public override Vector3 GetVelocityInLocalPoint(BulletBody pBody, Vector3 pos)
    {
        RigidBody body = (pBody as BulletBodyXNA).rigidBody;
        IndexedVector3 posiv3 = new IndexedVector3(pos.X, pos.Y, pos.Z);
        IndexedVector3 iv3 = body.GetVelocityInLocalPoint(ref posiv3);
        return new Vector3(iv3.X, iv3.Y, iv3.Z);
    }
    public override void Translate(BulletBody pCollisionObject, Vector3 trans)
    {
        CollisionObject collisionObject = (pCollisionObject as BulletBodyXNA).rigidBody;
        collisionObject.Translate(new IndexedVector3(trans.X,trans.Y,trans.Z));
    }
    public override void UpdateDeactivation(BulletBody pBody, float timeStep)
    {
        RigidBody body = (pBody as BulletBodyXNA).rigidBody;
        body.UpdateDeactivation(timeStep);
    }

    public override bool WantsSleeping(BulletBody pBody)
    {
        RigidBody body = (pBody as BulletBodyXNA).rigidBody;
        return body.WantsSleeping();
    }

    public override void SetAngularFactor(BulletBody pBody, float factor)
    {
        RigidBody body = (pBody as BulletBodyXNA).rigidBody;
        body.SetAngularFactor(factor);
    }

    public override Vector3 GetAngularFactor(BulletBody pBody)
    {
        RigidBody body = (pBody as BulletBodyXNA).rigidBody;
        IndexedVector3 iv3 =  body.GetAngularFactor();
        return new Vector3(iv3.X, iv3.Y, iv3.Z);
    }

    public override bool IsInWorld(BulletWorld pWorld, BulletBody pCollisionObject)
    {
        DiscreteDynamicsWorld world = (pWorld as BulletWorldXNA).world;
        CollisionObject collisionObject = (pCollisionObject as BulletBodyXNA).body;
        return world.IsInWorld(collisionObject);
    }

    public override void AddConstraintRef(BulletBody pBody, BulletConstraint pConstraint)
    {
        RigidBody body = (pBody as BulletBodyXNA).rigidBody;
        TypedConstraint constrain = (pConstraint as BulletConstraintXNA).constrain;
        body.AddConstraintRef(constrain);
    }

    public override void RemoveConstraintRef(BulletBody pBody, BulletConstraint pConstraint)
    {
        RigidBody body = (pBody as BulletBodyXNA).rigidBody;
        TypedConstraint constrain = (pConstraint as BulletConstraintXNA).constrain;
        body.RemoveConstraintRef(constrain);
    }

    public override BulletConstraint GetConstraintRef(BulletBody pBody, int index)
    {
        RigidBody body = (pBody as BulletBodyXNA).rigidBody;
        return new BulletConstraintXNA(body.GetConstraintRef(index));
    }

    public override int GetNumConstraintRefs(BulletBody pBody)
    {
        RigidBody body = (pBody as BulletBodyXNA).rigidBody;
        return body.GetNumConstraintRefs();
    }

    public override void SetInterpolationLinearVelocity(BulletBody pCollisionObject, Vector3 VehicleVelocity)
    {
        CollisionObject collisionObject = (pCollisionObject as BulletBodyXNA).rigidBody;
        IndexedVector3 velocity = new IndexedVector3(VehicleVelocity.X, VehicleVelocity.Y, VehicleVelocity.Z);
        collisionObject.SetInterpolationLinearVelocity(ref velocity);
    }

    public override bool UseFrameOffset(BulletConstraint pConstraint, float onOff)
    {
        Generic6DofConstraint constraint = (pConstraint as BulletConstraintXNA).constrain as Generic6DofConstraint;
        constraint.SetUseFrameOffset((onOff == 0) ? false : true);
        return true;
    }
    //SetBreakingImpulseThreshold(m_constraint.ptr, threshold);
    public override bool SetBreakingImpulseThreshold(BulletConstraint pConstraint, float threshold)
    {
        Generic6DofConstraint constraint = (pConstraint as BulletConstraintXNA).constrain as Generic6DofConstraint;
        constraint.SetBreakingImpulseThreshold(threshold);
        return true;
    }
    public override bool HingeSetLimits(BulletConstraint pConstraint, float low, float high, float softness, float bias, float relaxation)
    {
        HingeConstraint constraint = (pConstraint as BulletConstraintXNA).constrain as HingeConstraint;
        if (softness == HINGE_NOT_SPECIFIED)
            constraint.SetLimit(low, high);
        else
            constraint.SetLimit(low, high, softness, bias, relaxation);
        return true;
    }
    public override bool SpringEnable(BulletConstraint pConstraint, int index, float numericTrueFalse)
    {
        Generic6DofSpringConstraint constraint = (pConstraint as BulletConstraintXNA).constrain as Generic6DofSpringConstraint;
        constraint.EnableSpring(index, (numericTrueFalse == 0f ? false : true));
        return true;
    }

    public override bool SpringSetEquilibriumPoint(BulletConstraint pConstraint, int index, float equilibriumPoint)
    {
        Generic6DofSpringConstraint constraint = (pConstraint as BulletConstraintXNA).constrain as Generic6DofSpringConstraint;
        if (index == SPRING_NOT_SPECIFIED)
        {
            constraint.SetEquilibriumPoint();
        }
        else
        {
            if (equilibriumPoint == SPRING_NOT_SPECIFIED)
                constraint.SetEquilibriumPoint(index);
            else
                constraint.SetEquilibriumPoint(index, equilibriumPoint);
        }
        return true;
    }

    public override bool SpringSetStiffness(BulletConstraint pConstraint, int index, float stiffness)
    {
        Generic6DofSpringConstraint constraint = (pConstraint as BulletConstraintXNA).constrain as Generic6DofSpringConstraint;
        constraint.SetStiffness(index, stiffness);
        return true;
    }

    public override bool SpringSetDamping(BulletConstraint pConstraint, int index, float damping)
    {
        Generic6DofSpringConstraint constraint = (pConstraint as BulletConstraintXNA).constrain as Generic6DofSpringConstraint;
        constraint.SetDamping(index, damping);
        return true;
    }

    public override bool SliderSetLimits(BulletConstraint pConstraint, int lowerUpper, int linAng, float val)
    {
        SliderConstraint constraint = (pConstraint as BulletConstraintXNA).constrain as SliderConstraint;
		switch (lowerUpper)
		{
			case SLIDER_LOWER_LIMIT:
				switch (linAng)
				{
					case SLIDER_LINEAR:
						constraint.SetLowerLinLimit(val);
						break;
					case SLIDER_ANGULAR:
						constraint.SetLowerAngLimit(val);
						break;
				}
				break;
			case SLIDER_UPPER_LIMIT:
				switch (linAng)
				{
					case SLIDER_LINEAR:
						constraint.SetUpperLinLimit(val);
						break;
					case SLIDER_ANGULAR:
						constraint.SetUpperAngLimit(val);
						break;
				}
				break;
		}
        return true;
    }
    public override bool SliderSet(BulletConstraint pConstraint, int softRestDamp, int dirLimOrtho, int linAng, float val)
    {
        SliderConstraint constraint = (pConstraint as BulletConstraintXNA).constrain as SliderConstraint;
		switch (softRestDamp)
		{
			case SLIDER_SET_SOFTNESS:
				switch (dirLimOrtho)
				{
					case SLIDER_SET_DIRECTION:
						switch (linAng)
						{
							case SLIDER_LINEAR: constraint.SetSoftnessDirLin(val); break;
							case SLIDER_ANGULAR: constraint.SetSoftnessDirAng(val); break;
						}
						break;
					case SLIDER_SET_LIMIT:
						switch (linAng)
						{
							case SLIDER_LINEAR: constraint.SetSoftnessLimLin(val); break;
							case SLIDER_ANGULAR: constraint.SetSoftnessLimAng(val); break;
						}
						break;
					case SLIDER_SET_ORTHO:
						switch (linAng)
						{
							case SLIDER_LINEAR: constraint.SetSoftnessOrthoLin(val); break;
							case SLIDER_ANGULAR: constraint.SetSoftnessOrthoAng(val); break;
						}
						break;
				}
				break;
			case SLIDER_SET_RESTITUTION:
				switch (dirLimOrtho)
				{
					case SLIDER_SET_DIRECTION:
						switch (linAng)
						{
							case SLIDER_LINEAR: constraint.SetRestitutionDirLin(val); break;
							case SLIDER_ANGULAR: constraint.SetRestitutionDirAng(val); break;
						}
						break;
					case SLIDER_SET_LIMIT:
						switch (linAng)
						{
							case SLIDER_LINEAR: constraint.SetRestitutionLimLin(val); break;
							case SLIDER_ANGULAR: constraint.SetRestitutionLimAng(val); break;
						}
						break;
					case SLIDER_SET_ORTHO:
						switch (linAng)
						{
							case SLIDER_LINEAR: constraint.SetRestitutionOrthoLin(val); break;
							case SLIDER_ANGULAR: constraint.SetRestitutionOrthoAng(val); break;
						}
						break;
				}
				break;
			case SLIDER_SET_DAMPING:
				switch (dirLimOrtho)
				{
					case SLIDER_SET_DIRECTION:
						switch (linAng)
						{
							case SLIDER_LINEAR: constraint.SetDampingDirLin(val); break;
							case SLIDER_ANGULAR: constraint.SetDampingDirAng(val); break;
						}
						break;
					case SLIDER_SET_LIMIT:
						switch (linAng)
						{
							case SLIDER_LINEAR: constraint.SetDampingLimLin(val); break;
							case SLIDER_ANGULAR: constraint.SetDampingLimAng(val); break;
						}
						break;
					case SLIDER_SET_ORTHO:
						switch (linAng)
						{
							case SLIDER_LINEAR: constraint.SetDampingOrthoLin(val); break;
							case SLIDER_ANGULAR: constraint.SetDampingOrthoAng(val); break;
						}
						break;
				}
				break;
		}
        return true;
    }
    public override bool SliderMotorEnable(BulletConstraint pConstraint, int linAng, float numericTrueFalse)
    {
        SliderConstraint constraint = (pConstraint as BulletConstraintXNA).constrain as SliderConstraint;
		switch (linAng)
		{
			case SLIDER_LINEAR:
				constraint.SetPoweredLinMotor(numericTrueFalse == 0.0 ? false : true);
				break;
			case SLIDER_ANGULAR:
				constraint.SetPoweredAngMotor(numericTrueFalse == 0.0 ? false : true);
				break;
		}
        return true;
    }
    public override bool SliderMotor(BulletConstraint pConstraint, int forceVel, int linAng, float val)
    {
        SliderConstraint constraint = (pConstraint as BulletConstraintXNA).constrain as SliderConstraint;
		switch (forceVel)
		{
			case SLIDER_MOTOR_VELOCITY:
				switch (linAng)
				{
					case SLIDER_LINEAR:
						constraint.SetTargetLinMotorVelocity(val);
						break;
					case SLIDER_ANGULAR:
						constraint.SetTargetAngMotorVelocity(val);
						break;
				}
				break;
			case SLIDER_MAX_MOTOR_FORCE:
				switch (linAng)
				{
					case SLIDER_LINEAR:
						constraint.SetMaxLinMotorForce(val);
						break;
					case SLIDER_ANGULAR:
						constraint.SetMaxAngMotorForce(val);
						break;
				}
				break;
		}
        return true;
    }

    //BulletSimAPI.SetAngularDamping(Prim.PhysBody.ptr, angularDamping);
    public override void SetAngularDamping(BulletBody pBody, float angularDamping)
    {
        RigidBody body = (pBody as BulletBodyXNA).rigidBody;
        float lineardamping = body.GetLinearDamping();
        body.SetDamping(lineardamping, angularDamping);

    }

    public override void UpdateInertiaTensor(BulletBody pBody)
    {
        RigidBody body = (pBody as BulletBodyXNA).rigidBody;
        if (body != null)  // can't update inertia tensor on CollisionObject
            body.UpdateInertiaTensor();
    }

    public override void RecalculateCompoundShapeLocalAabb(BulletShape pCompoundShape)
    {
        CompoundShape shape = (pCompoundShape as BulletShapeXNA).shape as CompoundShape;
        shape.RecalculateLocalAabb();
    }

    //BulletSimAPI.GetCollisionFlags(PhysBody.ptr)
    public override CollisionFlags GetCollisionFlags(BulletBody pCollisionObject)
    {
        CollisionObject collisionObject = (pCollisionObject as BulletBodyXNA).rigidBody;
        uint flags = (uint)collisionObject.GetCollisionFlags();
        return (CollisionFlags) flags;
    }

    public override void SetDamping(BulletBody pBody, float pLinear, float pAngular)
    {
        RigidBody body = (pBody as BulletBodyXNA).rigidBody;
        body.SetDamping(pLinear, pAngular);
    }
    //PhysBody.ptr, PhysicsScene.Params.deactivationTime);
    public override void SetDeactivationTime(BulletBody pCollisionObject, float pDeactivationTime)
    {
        CollisionObject collisionObject = (pCollisionObject as BulletBodyXNA).rigidBody;
        collisionObject.SetDeactivationTime(pDeactivationTime);
    }
    //SetSleepingThresholds(PhysBody.ptr, PhysicsScene.Params.linearSleepingThreshold, PhysicsScene.Params.angularSleepingThreshold);
    public override void SetSleepingThresholds(BulletBody pBody, float plinearSleepingThreshold, float pangularSleepingThreshold)
    {
        RigidBody body = (pBody as BulletBodyXNA).rigidBody;
        body.SetSleepingThresholds(plinearSleepingThreshold, pangularSleepingThreshold);
    }

    public override CollisionObjectTypes GetBodyType(BulletBody pCollisionObject)
    {
        CollisionObject collisionObject = (pCollisionObject as BulletBodyXNA).body;
        return (CollisionObjectTypes)(int) collisionObject.GetInternalType();
    }

    public override void ApplyGravity(BulletBody pBody)
    {

        RigidBody body = (pBody as BulletBodyXNA).rigidBody;
        body.ApplyGravity();
    }

    public override Vector3 GetGravity(BulletBody pBody)
    {
        RigidBody body = (pBody as BulletBodyXNA).rigidBody;
        IndexedVector3 gravity = body.GetGravity();
        return new Vector3(gravity.X, gravity.Y, gravity.Z);
    }

    public override void SetLinearDamping(BulletBody pBody, float lin_damping)
    {
        RigidBody body = (pBody as BulletBodyXNA).rigidBody;
        float angularDamping = body.GetAngularDamping();
        body.SetDamping(lin_damping, angularDamping);
    }

    public override float GetLinearDamping(BulletBody pBody)
    {
        RigidBody body = (pBody as BulletBodyXNA).rigidBody;
        return body.GetLinearDamping();
    }

    public override float GetAngularDamping(BulletBody pBody)
    {
        RigidBody body = (pBody as BulletBodyXNA).rigidBody;
        return body.GetAngularDamping();
    }

    public override float GetLinearSleepingThreshold(BulletBody pBody)
    {
        RigidBody body = (pBody as BulletBodyXNA).rigidBody;
        return body.GetLinearSleepingThreshold();
    }

    public override void ApplyDamping(BulletBody pBody, float timeStep)
    {
        RigidBody body = (pBody as BulletBodyXNA).rigidBody;
        body.ApplyDamping(timeStep);
    }

    public override Vector3 GetLinearFactor(BulletBody pBody)
    {
        RigidBody body = (pBody as BulletBodyXNA).rigidBody;
        IndexedVector3 linearFactor = body.GetLinearFactor();
        return new Vector3(linearFactor.X, linearFactor.Y, linearFactor.Z);
    }

    public override void SetLinearFactor(BulletBody pBody, Vector3 factor)
    {
        RigidBody body = (pBody as BulletBodyXNA).rigidBody;
        body.SetLinearFactor(new IndexedVector3(factor.X, factor.Y, factor.Z));
    }

    public override void SetCenterOfMassByPosRot(BulletBody pBody, Vector3 pos, Quaternion rot)
    {
        RigidBody body = (pBody as BulletBodyXNA).rigidBody;
        IndexedQuaternion quat = new IndexedQuaternion(rot.X, rot.Y, rot.Z,rot.W);
        IndexedMatrix mat = IndexedMatrix.CreateFromQuaternion(quat);
        mat._origin = new IndexedVector3(pos.X, pos.Y, pos.Z);
        body.SetCenterOfMassTransform( ref mat);
        /* TODO: double check this */
    }

    //BulletSimAPI.ApplyCentralForce(PhysBody.ptr, fSum);
    public override void ApplyCentralForce(BulletBody pBody, Vector3 pfSum)
    {
        RigidBody body = (pBody as BulletBodyXNA).rigidBody;
        IndexedVector3 fSum = new IndexedVector3(pfSum.X, pfSum.Y, pfSum.Z);
        body.ApplyCentralForce(ref fSum);
    }
    public override void ApplyCentralImpulse(BulletBody pBody, Vector3 pfSum)
    {
        RigidBody body = (pBody as BulletBodyXNA).rigidBody;
        IndexedVector3 fSum = new IndexedVector3(pfSum.X, pfSum.Y, pfSum.Z);
        body.ApplyCentralImpulse(ref fSum);
    }
    public override void ApplyTorque(BulletBody pBody, Vector3 pfSum)
    {
        RigidBody body = (pBody as BulletBodyXNA).rigidBody;
        IndexedVector3 fSum = new IndexedVector3(pfSum.X, pfSum.Y, pfSum.Z);
        body.ApplyTorque(ref fSum);
    }
    public override void ApplyTorqueImpulse(BulletBody pBody, Vector3 pfSum)
    {
        RigidBody body = (pBody as BulletBodyXNA).rigidBody;
        IndexedVector3 fSum = new IndexedVector3(pfSum.X, pfSum.Y, pfSum.Z);
        body.ApplyTorqueImpulse(ref fSum);
    }

    public override void DestroyObject(BulletWorld pWorld, BulletBody pBody)
    {
        DiscreteDynamicsWorld world = (pWorld as BulletWorldXNA).world;
        CollisionObject co = (pBody as BulletBodyXNA).rigidBody;
        RigidBody bo = co as RigidBody;
        if (bo == null)
        {

            if (world.IsInWorld(co))
            {
                world.RemoveCollisionObject(co);
            }
        }
        else
        {

            if (world.IsInWorld(bo))
            {
                world.RemoveRigidBody(bo);
            }
        }
        if (co != null)
        {
            if (co.GetUserPointer() != null)
            {
                uint localId = (uint) co.GetUserPointer();
                if (specialCollisionObjects.ContainsKey(localId))
                {
                    specialCollisionObjects.Remove(localId);
                }
            }
        }

    }

    public override void Shutdown(BulletWorld pWorld)
    {
        DiscreteDynamicsWorld world = (pWorld as BulletWorldXNA).world;
        world.Cleanup();
    }

    public override BulletShape DuplicateCollisionShape(BulletWorld pWorld, BulletShape pShape, uint id)
    {
        CollisionShape shape1 = (pShape as BulletShapeXNA).shape;

        // TODO:  Turn this from a reference copy to a Value Copy.
        BulletShapeXNA shape2 = new BulletShapeXNA(shape1, BSShapeTypeFromBroadPhaseNativeType(shape1.GetShapeType()));

        return shape2;
    }

    public override bool DeleteCollisionShape(BulletWorld pWorld, BulletShape pShape)
    {
        //TODO:
        return false;
    }
    //(sim.ptr, shape.ptr, prim.LocalID, prim.RawPosition, prim.RawOrientation);

    public override BulletBody CreateBodyFromShape(BulletWorld pWorld, BulletShape pShape, uint pLocalID, Vector3 pRawPosition, Quaternion pRawOrientation)
    {
        CollisionWorld world = (pWorld as BulletWorldXNA).world;
        IndexedMatrix mat =
            IndexedMatrix.CreateFromQuaternion(new IndexedQuaternion(pRawOrientation.X, pRawOrientation.Y,
                                                                     pRawOrientation.Z, pRawOrientation.W));
        mat._origin = new IndexedVector3(pRawPosition.X, pRawPosition.Y, pRawPosition.Z);
        CollisionShape shape = (pShape as BulletShapeXNA).shape;
        //UpdateSingleAabb(world, shape);
        // TODO: Feed Update array into null
        SimMotionState motionState = new SimMotionState(this, pLocalID, mat, null);
        RigidBody body = new RigidBody(0,motionState,shape,IndexedVector3.Zero);
        RigidBodyConstructionInfo constructionInfo = new RigidBodyConstructionInfo(0, motionState, shape, IndexedVector3.Zero)
                                                         {
                                                             m_mass = 0
                                                         };
        /*
            m_mass = mass;
			m_motionState =motionState;
			m_collisionShape = collisionShape;
			m_localInertia = localInertia;
			m_linearDamping = 0f;
			m_angularDamping = 0f;
			m_friction = 0.5f;
			m_restitution = 0f;
			m_linearSleepingThreshold = 0.8f;
			m_angularSleepingThreshold = 1f;
			m_additionalDamping = false;
			m_additionalDampingFactor = 0.005f;
			m_additionalLinearDampingThresholdSqr = 0.01f;
			m_additionalAngularDampingThresholdSqr = 0.01f;
			m_additionalAngularDampingFactor = 0.01f;
            m_startWorldTransform = IndexedMatrix.Identity;
        */
        body.SetUserPointer(pLocalID);

        return new BulletBodyXNA(pLocalID, body);
    }


    public override BulletBody CreateBodyWithDefaultMotionState( BulletShape pShape, uint pLocalID, Vector3 pRawPosition, Quaternion pRawOrientation)
    {

        IndexedMatrix mat =
            IndexedMatrix.CreateFromQuaternion(new IndexedQuaternion(pRawOrientation.X, pRawOrientation.Y,
                                                                     pRawOrientation.Z, pRawOrientation.W));
        mat._origin = new IndexedVector3(pRawPosition.X, pRawPosition.Y, pRawPosition.Z);

        CollisionShape shape = (pShape as BulletShapeXNA).shape;

        // TODO: Feed Update array into null
        RigidBody body = new RigidBody(0, new DefaultMotionState( mat, IndexedMatrix.Identity), shape, IndexedVector3.Zero);
        body.SetWorldTransform(mat);
        body.SetUserPointer(pLocalID);
        return new BulletBodyXNA(pLocalID, body);
    }
    //(m_mapInfo.terrainBody.ptr, CollisionFlags.CF_STATIC_OBJECT);
    public override CollisionFlags SetCollisionFlags(BulletBody pCollisionObject, CollisionFlags collisionFlags)
    {
        CollisionObject collisionObject = (pCollisionObject as BulletBodyXNA).rigidBody;
        collisionObject.SetCollisionFlags((BulletXNA.BulletCollision.CollisionFlags) (uint) collisionFlags);
        return (CollisionFlags)collisionObject.GetCollisionFlags();
    }

    public override Vector3 GetAnisotripicFriction(BulletConstraint pconstrain)
    {

        /* TODO */
        return Vector3.Zero;
    }
    public override Vector3 SetAnisotripicFriction(BulletConstraint pconstrain, Vector3 frict) { /* TODO */ return Vector3.Zero; }
    public override bool HasAnisotripicFriction(BulletConstraint pconstrain) { /* TODO */ return false; }
    public override float GetContactProcessingThreshold(BulletBody pBody) { /* TODO */ return 0f; }
    public override bool IsStaticObject(BulletBody pCollisionObject)
    {
        CollisionObject collisionObject = (pCollisionObject as BulletBodyXNA).rigidBody;
        return collisionObject.IsStaticObject();

    }
    public override bool IsKinematicObject(BulletBody pCollisionObject)
    {
        CollisionObject collisionObject = (pCollisionObject as BulletBodyXNA).rigidBody;
        return collisionObject.IsKinematicObject();
    }
    public override bool IsStaticOrKinematicObject(BulletBody pCollisionObject)
    {
        CollisionObject collisionObject = (pCollisionObject as BulletBodyXNA).rigidBody;
        return collisionObject.IsStaticOrKinematicObject();
    }
    public override bool HasContactResponse(BulletBody pCollisionObject)
    {
        CollisionObject collisionObject = (pCollisionObject as BulletBodyXNA).rigidBody;
        return collisionObject.HasContactResponse();
    }
    public override int GetActivationState(BulletBody pBody) { /* TODO */ return 0; }
    public override void SetActivationState(BulletBody pBody, int state) { /* TODO */ }
    public override float GetDeactivationTime(BulletBody pBody) { /* TODO */ return 0f; }
    public override bool IsActive(BulletBody pBody) { /* TODO */ return false; }
    public override float GetRestitution(BulletBody pBody) { /* TODO */ return 0f; }
    public override float GetFriction(BulletBody pBody) { /* TODO */ return 0f; }
    public override void SetInterpolationVelocity(BulletBody pBody, Vector3 linearVel, Vector3 angularVel) { /* TODO */ }
    public override float GetHitFraction(BulletBody pBody) { /* TODO */ return 0f; }

    //(m_mapInfo.terrainBody.ptr, PhysicsScene.Params.terrainHitFraction);
    public override void SetHitFraction(BulletBody pCollisionObject, float pHitFraction)
    {
        CollisionObject collisionObject = (pCollisionObject as BulletBodyXNA).rigidBody;
        collisionObject.SetHitFraction(pHitFraction);
    }
    //BuildCapsuleShape(physicsScene.World.ptr, 1f, 1f, prim.Scale);
    public override BulletShape BuildCapsuleShape(BulletWorld pWorld, float pRadius, float pHeight, Vector3 pScale)
    {
        DiscreteDynamicsWorld world = (pWorld as BulletWorldXNA).world;
        IndexedVector3 scale = new IndexedVector3(pScale.X, pScale.Y, pScale.Z);
        CapsuleShapeZ capsuleShapeZ = new CapsuleShapeZ(pRadius, pHeight);
        capsuleShapeZ.SetMargin(world.WorldSettings.Params.collisionMargin);
        capsuleShapeZ.SetLocalScaling(ref scale);

        return new BulletShapeXNA(capsuleShapeZ, BSPhysicsShapeType.SHAPE_CAPSULE); ;
    }

    public override BulletWorld Initialize(Vector3 maxPosition, ConfigurationParameters parms,
                                            int maxCollisions, ref CollisionDesc[] collisionArray,
                                            int maxUpdates, ref EntityProperties[] updateArray
                                            )
    {

        UpdatedObjects = updateArray;
        UpdatedCollisions = collisionArray;
        /* TODO */
        ConfigurationParameters[] configparms = new ConfigurationParameters[1];
        configparms[0] = parms;
        Vector3 worldExtent = maxPosition;
        m_maxCollisions = maxCollisions;
        m_maxUpdatesPerFrame = maxUpdates;
        specialCollisionObjects = new Dictionary<uint, GhostObject>();

        return new BulletWorldXNA(1, PhysicsScene, BSAPIXNA.Initialize2(worldExtent, configparms, maxCollisions, ref collisionArray, maxUpdates, ref updateArray, null));
    }

    private static DiscreteDynamicsWorld Initialize2(Vector3 worldExtent,
                        ConfigurationParameters[] o,
                        int mMaxCollisionsPerFrame, ref CollisionDesc[] collisionArray,
                        int mMaxUpdatesPerFrame, ref EntityProperties[] updateArray,
                        object mDebugLogCallbackHandle)
    {
        CollisionWorld.WorldData.ParamData p = new CollisionWorld.WorldData.ParamData();

        p.angularDamping = BSParam.AngularDamping;
        p.defaultFriction = o[0].defaultFriction;
        p.defaultFriction = o[0].defaultFriction;
        p.defaultDensity = o[0].defaultDensity;
        p.defaultRestitution = o[0].defaultRestitution;
        p.collisionMargin = o[0].collisionMargin;
        p.gravity = o[0].gravity;

        p.linearDamping = BSParam.LinearDamping;
        p.angularDamping = BSParam.AngularDamping;
        p.deactivationTime = BSParam.DeactivationTime;
        p.linearSleepingThreshold = BSParam.LinearSleepingThreshold;
        p.angularSleepingThreshold = BSParam.AngularSleepingThreshold;
        p.ccdMotionThreshold = BSParam.CcdMotionThreshold;
        p.ccdSweptSphereRadius = BSParam.CcdSweptSphereRadius;
        p.contactProcessingThreshold = BSParam.ContactProcessingThreshold;

        p.terrainImplementation = BSParam.TerrainImplementation;
        p.terrainFriction = BSParam.TerrainFriction;

        p.terrainHitFraction = BSParam.TerrainHitFraction;
        p.terrainRestitution = BSParam.TerrainRestitution;
        p.terrainCollisionMargin = BSParam.TerrainCollisionMargin;

        p.avatarFriction = BSParam.AvatarFriction;
        p.avatarStandingFriction = BSParam.AvatarStandingFriction;
        p.avatarDensity = BSParam.AvatarDensity;
        p.avatarRestitution = BSParam.AvatarRestitution;
        p.avatarCapsuleWidth = BSParam.AvatarCapsuleWidth;
        p.avatarCapsuleDepth = BSParam.AvatarCapsuleDepth;
        p.avatarCapsuleHeight = BSParam.AvatarCapsuleHeight;
        p.avatarContactProcessingThreshold = BSParam.AvatarContactProcessingThreshold;

        p.vehicleAngularDamping = BSParam.VehicleAngularDamping;

        p.maxPersistantManifoldPoolSize = o[0].maxPersistantManifoldPoolSize;
        p.maxCollisionAlgorithmPoolSize = o[0].maxCollisionAlgorithmPoolSize;
        p.shouldDisableContactPoolDynamicAllocation = o[0].shouldDisableContactPoolDynamicAllocation;
        p.shouldForceUpdateAllAabbs = o[0].shouldForceUpdateAllAabbs;
        p.shouldRandomizeSolverOrder = o[0].shouldRandomizeSolverOrder;
        p.shouldSplitSimulationIslands = o[0].shouldSplitSimulationIslands;
        p.shouldEnableFrictionCaching = o[0].shouldEnableFrictionCaching;
        p.numberOfSolverIterations = o[0].numberOfSolverIterations;

        p.linksetImplementation = BSParam.LinksetImplementation;
        p.linkConstraintUseFrameOffset = BSParam.NumericBool(BSParam.LinkConstraintUseFrameOffset);
        p.linkConstraintEnableTransMotor = BSParam.NumericBool(BSParam.LinkConstraintEnableTransMotor);
        p.linkConstraintTransMotorMaxVel = BSParam.LinkConstraintTransMotorMaxVel;
        p.linkConstraintTransMotorMaxForce = BSParam.LinkConstraintTransMotorMaxForce;
        p.linkConstraintERP = BSParam.LinkConstraintERP;
        p.linkConstraintCFM = BSParam.LinkConstraintCFM;
        p.linkConstraintSolverIterations = BSParam.LinkConstraintSolverIterations;
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

        world.LastCollisionDesc = 0;
        world.LastEntityProperty = 0;

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

        //BSParam.TerrainImplementation = 0;
        world.SetGravity(new IndexedVector3(0,0,p.gravity));

        // Turn off Pooling since globals and pooling are bad for threading.
        BulletGlobals.VoronoiSimplexSolverPool.SetPoolingEnabled(false);
        BulletGlobals.SubSimplexConvexCastPool.SetPoolingEnabled(false);
        BulletGlobals.ManifoldPointPool.SetPoolingEnabled(false);
        BulletGlobals.CastResultPool.SetPoolingEnabled(false);
        BulletGlobals.SphereShapePool.SetPoolingEnabled(false);
        BulletGlobals.DbvtNodePool.SetPoolingEnabled(false);
        BulletGlobals.SingleRayCallbackPool.SetPoolingEnabled(false);
        BulletGlobals.SubSimplexClosestResultPool.SetPoolingEnabled(false);
        BulletGlobals.GjkPairDetectorPool.SetPoolingEnabled(false);
        BulletGlobals.DbvtTreeColliderPool.SetPoolingEnabled(false);
        BulletGlobals.SingleSweepCallbackPool.SetPoolingEnabled(false);
        BulletGlobals.BroadphaseRayTesterPool.SetPoolingEnabled(false);
        BulletGlobals.ClosestNotMeConvexResultCallbackPool.SetPoolingEnabled(false);
        BulletGlobals.GjkEpaPenetrationDepthSolverPool.SetPoolingEnabled(false);
        BulletGlobals.ContinuousConvexCollisionPool.SetPoolingEnabled(false);
        BulletGlobals.DbvtStackDataBlockPool.SetPoolingEnabled(false);

        BulletGlobals.BoxBoxCollisionAlgorithmPool.SetPoolingEnabled(false);
        BulletGlobals.CompoundCollisionAlgorithmPool.SetPoolingEnabled(false);
        BulletGlobals.ConvexConcaveCollisionAlgorithmPool.SetPoolingEnabled(false);
        BulletGlobals.ConvexConvexAlgorithmPool.SetPoolingEnabled(false);
        BulletGlobals.ConvexPlaneAlgorithmPool.SetPoolingEnabled(false);
        BulletGlobals.SphereBoxCollisionAlgorithmPool.SetPoolingEnabled(false);
        BulletGlobals.SphereSphereCollisionAlgorithmPool.SetPoolingEnabled(false);
        BulletGlobals.SphereTriangleCollisionAlgorithmPool.SetPoolingEnabled(false);
        BulletGlobals.GImpactCollisionAlgorithmPool.SetPoolingEnabled(false);
        BulletGlobals.GjkEpaSolver2MinkowskiDiffPool.SetPoolingEnabled(false);
        BulletGlobals.PersistentManifoldPool.SetPoolingEnabled(false);
        BulletGlobals.ManifoldResultPool.SetPoolingEnabled(false);
        BulletGlobals.GJKPool.SetPoolingEnabled(false);
        BulletGlobals.GIM_ShapeRetrieverPool.SetPoolingEnabled(false);
        BulletGlobals.TriangleShapePool.SetPoolingEnabled(false);
        BulletGlobals.SphereTriangleDetectorPool.SetPoolingEnabled(false);
        BulletGlobals.CompoundLeafCallbackPool.SetPoolingEnabled(false);
        BulletGlobals.GjkConvexCastPool.SetPoolingEnabled(false);
        BulletGlobals.LocalTriangleSphereCastCallbackPool.SetPoolingEnabled(false);
        BulletGlobals.BridgeTriangleRaycastCallbackPool.SetPoolingEnabled(false);
        BulletGlobals.BridgeTriangleConcaveRaycastCallbackPool.SetPoolingEnabled(false);
        BulletGlobals.BridgeTriangleConvexcastCallbackPool.SetPoolingEnabled(false);
        BulletGlobals.MyNodeOverlapCallbackPool.SetPoolingEnabled(false);
        BulletGlobals.ClosestRayResultCallbackPool.SetPoolingEnabled(false);
        BulletGlobals.DebugDrawcallbackPool.SetPoolingEnabled(false);

        return world;
    }
    //m_constraint.ptr, ConstraintParams.BT_CONSTRAINT_STOP_CFM, cfm, ConstraintParamAxis.AXIS_ALL
    public override bool SetConstraintParam(BulletConstraint pConstraint, ConstraintParams paramIndex, float paramvalue, ConstraintParamAxis axis)
    {
        Generic6DofConstraint constrain = (pConstraint as BulletConstraintXNA).constrain as Generic6DofConstraint;
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

    public override bool PushUpdate(BulletBody pCollisionObject)
    {
        bool ret = false;
        CollisionObject collisionObject = (pCollisionObject as BulletBodyXNA).rigidBody;
        RigidBody rb = collisionObject as RigidBody;
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

    public override float GetAngularMotionDisc(BulletShape pShape)
    {
        CollisionShape shape = (pShape as BulletShapeXNA).shape;
        return shape.GetAngularMotionDisc();
    }
    public override float GetContactBreakingThreshold(BulletShape pShape, float defaultFactor)
    {
        CollisionShape shape = (pShape as BulletShapeXNA).shape;
        return shape.GetContactBreakingThreshold(defaultFactor);
    }
    public override bool IsCompound(BulletShape pShape)
    {
        CollisionShape shape = (pShape as BulletShapeXNA).shape;
        return shape.IsCompound();
    }
    public override bool IsSoftBody(BulletShape pShape)
    {
        CollisionShape shape = (pShape as BulletShapeXNA).shape;
        return shape.IsSoftBody();
    }
    public override bool IsPolyhedral(BulletShape pShape)
    {
        CollisionShape shape = (pShape as BulletShapeXNA).shape;
        return shape.IsPolyhedral();
    }
    public override bool IsConvex2d(BulletShape pShape)
    {
        CollisionShape shape = (pShape as BulletShapeXNA).shape;
        return shape.IsConvex2d();
    }
    public override bool IsConvex(BulletShape pShape)
    {
        CollisionShape shape = (pShape as BulletShapeXNA).shape;
        return shape.IsConvex();
    }
    public override bool IsNonMoving(BulletShape pShape)
    {
        CollisionShape shape = (pShape as BulletShapeXNA).shape;
        return shape.IsNonMoving();
    }
    public override bool IsConcave(BulletShape pShape)
    {
        CollisionShape shape = (pShape as BulletShapeXNA).shape;
        return shape.IsConcave();
    }
    public override bool IsInfinite(BulletShape pShape)
    {
        CollisionShape shape = (pShape as BulletShapeXNA).shape;
        return shape.IsInfinite();
    }
    public override bool IsNativeShape(BulletShape pShape)
    {
        CollisionShape shape = (pShape as BulletShapeXNA).shape;
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

    public override void SetShapeCollisionMargin(BulletShape pShape, float pMargin)
    {
        CollisionShape shape = (pShape as BulletShapeXNA).shape;
        shape.SetMargin(pMargin);
    }

    //sim.ptr, shape.ptr,prim.LocalID, prim.RawPosition, prim.RawOrientation
    public override BulletBody CreateGhostFromShape(BulletWorld pWorld, BulletShape pShape, uint pLocalID, Vector3 pRawPosition, Quaternion pRawOrientation)
    {
        DiscreteDynamicsWorld world = (pWorld as BulletWorldXNA).world;
        IndexedMatrix bodyTransform = new IndexedMatrix();
        bodyTransform._origin = new IndexedVector3(pRawPosition.X, pRawPosition.Y, pRawPosition.Z);
        bodyTransform.SetRotation(new IndexedQuaternion(pRawOrientation.X,pRawOrientation.Y,pRawOrientation.Z,pRawOrientation.W));
        GhostObject gObj = new PairCachingGhostObject();
        gObj.SetWorldTransform(bodyTransform);
        CollisionShape shape = (pShape as BulletShapeXNA).shape;
        gObj.SetCollisionShape(shape);
        gObj.SetUserPointer(pLocalID);

        if (specialCollisionObjects.ContainsKey(pLocalID))
            specialCollisionObjects[pLocalID] = gObj;
        else
            specialCollisionObjects.Add(pLocalID, gObj);

        // TODO: Add to Special CollisionObjects!
        return new BulletBodyXNA(pLocalID, gObj);
    }

    public override void SetCollisionShape(BulletWorld pWorld, BulletBody pCollisionObject, BulletShape pShape)
    {
        DiscreteDynamicsWorld world = (pWorld as BulletWorldXNA).world;
        CollisionObject collisionObject = (pCollisionObject as BulletBodyXNA).body;
        if (pShape == null)
        {
            collisionObject.SetCollisionShape(new EmptyShape());
        }
        else
        {
            CollisionShape shape = (pShape as BulletShapeXNA).shape;
            collisionObject.SetCollisionShape(shape);
        }
    }
    public override BulletShape GetCollisionShape(BulletBody pCollisionObject)
    {
        CollisionObject collisionObject = (pCollisionObject as BulletBodyXNA).rigidBody;
        CollisionShape shape = collisionObject.GetCollisionShape();
        return new BulletShapeXNA(shape, BSShapeTypeFromBroadPhaseNativeType(shape.GetShapeType()));
    }

    //(PhysicsScene.World.ptr, nativeShapeData)
    public override BulletShape BuildNativeShape(BulletWorld pWorld, ShapeData pShapeData)
    {
        DiscreteDynamicsWorld world = (pWorld as BulletWorldXNA).world;
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
        return new BulletShapeXNA(shape, pShapeData.Type);
    }
    //PhysicsScene.World.ptr, false
    public override BulletShape CreateCompoundShape(BulletWorld pWorld, bool enableDynamicAabbTree)
    {
        return new BulletShapeXNA(new CompoundShape(enableDynamicAabbTree), BSPhysicsShapeType.SHAPE_COMPOUND);
    }

    public override int GetNumberOfCompoundChildren(BulletShape pCompoundShape)
    {
        CompoundShape compoundshape = (pCompoundShape as BulletShapeXNA).shape as CompoundShape;
        return compoundshape.GetNumChildShapes();
    }
    //LinksetRoot.PhysShape.ptr, newShape.ptr, displacementPos, displacementRot
    public override void AddChildShapeToCompoundShape(BulletShape pCShape, BulletShape paddShape, Vector3 displacementPos, Quaternion displacementRot)
    {
        IndexedMatrix relativeTransform = new IndexedMatrix();
        CompoundShape compoundshape = (pCShape as BulletShapeXNA).shape as CompoundShape;
        CollisionShape addshape = (paddShape as BulletShapeXNA).shape;

        relativeTransform._origin = new IndexedVector3(displacementPos.X, displacementPos.Y, displacementPos.Z);
        relativeTransform.SetRotation(new IndexedQuaternion(displacementRot.X,displacementRot.Y,displacementRot.Z,displacementRot.W));
        compoundshape.AddChildShape(ref relativeTransform, addshape);

    }

    public override BulletShape RemoveChildShapeFromCompoundShapeIndex(BulletShape pCShape, int pii)
    {
        CompoundShape compoundshape = (pCShape as BulletShapeXNA).shape as CompoundShape;
        CollisionShape ret = null;
        ret = compoundshape.GetChildShape(pii);
        compoundshape.RemoveChildShapeByIndex(pii);
        return new BulletShapeXNA(ret, BSShapeTypeFromBroadPhaseNativeType(ret.GetShapeType()));
    }

    public override BulletShape GetChildShapeFromCompoundShapeIndex(BulletShape cShape, int indx) {

        if (cShape == null)
            return null;
        CompoundShape compoundShape = (cShape as BulletShapeXNA).shape as CompoundShape;
        CollisionShape shape = compoundShape.GetChildShape(indx);
        BulletShape retShape = new BulletShapeXNA(shape, BSShapeTypeFromBroadPhaseNativeType(shape.GetShapeType()));


        return retShape;
    }

    public BSPhysicsShapeType BSShapeTypeFromBroadPhaseNativeType(BroadphaseNativeTypes pin)
    {
        BSPhysicsShapeType ret = BSPhysicsShapeType.SHAPE_UNKNOWN;
        switch (pin)
        {
            case BroadphaseNativeTypes.BOX_SHAPE_PROXYTYPE:
                ret =  BSPhysicsShapeType.SHAPE_BOX;
                break;
            case BroadphaseNativeTypes.TRIANGLE_SHAPE_PROXYTYPE:
                ret =  BSPhysicsShapeType.SHAPE_UNKNOWN;
                break;

            case BroadphaseNativeTypes.TETRAHEDRAL_SHAPE_PROXYTYPE:
                ret =  BSPhysicsShapeType.SHAPE_UNKNOWN;
                break;
            case BroadphaseNativeTypes.CONVEX_TRIANGLEMESH_SHAPE_PROXYTYPE:
                ret =  BSPhysicsShapeType.SHAPE_CONVEXHULL;
                break;
            case BroadphaseNativeTypes.CONVEX_HULL_SHAPE_PROXYTYPE:
                ret =  BSPhysicsShapeType.SHAPE_HULL;
                break;
            case BroadphaseNativeTypes.CONVEX_POINT_CLOUD_SHAPE_PROXYTYPE:
                ret =  BSPhysicsShapeType.SHAPE_UNKNOWN;
                break;
            case BroadphaseNativeTypes.CUSTOM_POLYHEDRAL_SHAPE_TYPE:
                ret =  BSPhysicsShapeType.SHAPE_UNKNOWN;
                break;
            //implicit convex shapes
            case BroadphaseNativeTypes.IMPLICIT_CONVEX_SHAPES_START_HERE:
                ret =  BSPhysicsShapeType.SHAPE_UNKNOWN;
                break;
            case BroadphaseNativeTypes.SPHERE_SHAPE_PROXYTYPE:
                ret =  BSPhysicsShapeType.SHAPE_SPHERE;
                break;
            case BroadphaseNativeTypes.MULTI_SPHERE_SHAPE_PROXYTYPE:
                ret =  BSPhysicsShapeType.SHAPE_UNKNOWN;
                break;
            case BroadphaseNativeTypes.CAPSULE_SHAPE_PROXYTYPE:
                ret =  BSPhysicsShapeType.SHAPE_CAPSULE;
                break;
            case BroadphaseNativeTypes.CONE_SHAPE_PROXYTYPE:
                ret =  BSPhysicsShapeType.SHAPE_CONE;
                break;
            case BroadphaseNativeTypes.CONVEX_SHAPE_PROXYTYPE:
                ret =  BSPhysicsShapeType.SHAPE_CONVEXHULL;
                break;
            case BroadphaseNativeTypes.CYLINDER_SHAPE_PROXYTYPE:
                ret =  BSPhysicsShapeType.SHAPE_CYLINDER;
                break;
            case BroadphaseNativeTypes.UNIFORM_SCALING_SHAPE_PROXYTYPE:
                ret =  BSPhysicsShapeType.SHAPE_UNKNOWN;
                break;
            case BroadphaseNativeTypes.MINKOWSKI_SUM_SHAPE_PROXYTYPE:
                ret =  BSPhysicsShapeType.SHAPE_UNKNOWN;
                break;
            case BroadphaseNativeTypes.MINKOWSKI_DIFFERENCE_SHAPE_PROXYTYPE:
                ret =  BSPhysicsShapeType.SHAPE_UNKNOWN;
                break;
            case BroadphaseNativeTypes.BOX_2D_SHAPE_PROXYTYPE:
                ret =  BSPhysicsShapeType.SHAPE_UNKNOWN;
                break;
            case BroadphaseNativeTypes.CONVEX_2D_SHAPE_PROXYTYPE:
                ret =  BSPhysicsShapeType.SHAPE_UNKNOWN;
                break;
            case BroadphaseNativeTypes.CUSTOM_CONVEX_SHAPE_TYPE:
                ret =  BSPhysicsShapeType.SHAPE_UNKNOWN;
                break;
            //concave shape
            case BroadphaseNativeTypes.CONCAVE_SHAPES_START_HERE:
                ret =  BSPhysicsShapeType.SHAPE_UNKNOWN;
                break;
            //keep all the convex shapetype below here, for the check IsConvexShape in broadphase proxy!
            case BroadphaseNativeTypes.TRIANGLE_MESH_SHAPE_PROXYTYPE:
                ret =  BSPhysicsShapeType.SHAPE_MESH;
                break;
            case BroadphaseNativeTypes.SCALED_TRIANGLE_MESH_SHAPE_PROXYTYPE:
                ret =  BSPhysicsShapeType.SHAPE_MESH;
                break;
            ///used for demo integration FAST/Swift collision library and Bullet
            case BroadphaseNativeTypes.FAST_CONCAVE_MESH_PROXYTYPE:
                ret =  BSPhysicsShapeType.SHAPE_MESH;
                break;
            //terrain
            case BroadphaseNativeTypes.TERRAIN_SHAPE_PROXYTYPE:
                ret =  BSPhysicsShapeType.SHAPE_HEIGHTMAP;
                break;
            ///Used for GIMPACT Trimesh integration
            case BroadphaseNativeTypes.GIMPACT_SHAPE_PROXYTYPE:
                ret =  BSPhysicsShapeType.SHAPE_GIMPACT;
                break;
            ///Multimaterial mesh
            case BroadphaseNativeTypes.MULTIMATERIAL_TRIANGLE_MESH_PROXYTYPE:
                ret =  BSPhysicsShapeType.SHAPE_MESH;
                break;

            case BroadphaseNativeTypes.EMPTY_SHAPE_PROXYTYPE:
                ret =  BSPhysicsShapeType.SHAPE_UNKNOWN;
                break;
            case BroadphaseNativeTypes.STATIC_PLANE_PROXYTYPE:
                ret =  BSPhysicsShapeType.SHAPE_GROUNDPLANE;
                break;
            case BroadphaseNativeTypes.CUSTOM_CONCAVE_SHAPE_TYPE:
                ret =  BSPhysicsShapeType.SHAPE_UNKNOWN;
                break;
            case BroadphaseNativeTypes.CONCAVE_SHAPES_END_HERE:
                ret =  BSPhysicsShapeType.SHAPE_UNKNOWN;
                break;

            case BroadphaseNativeTypes.COMPOUND_SHAPE_PROXYTYPE:
                ret =  BSPhysicsShapeType.SHAPE_COMPOUND;
                break;

            case BroadphaseNativeTypes.SOFTBODY_SHAPE_PROXYTYPE:
                ret =  BSPhysicsShapeType.SHAPE_MESH;
                break;
            case BroadphaseNativeTypes.HFFLUID_SHAPE_PROXYTYPE:
                ret =  BSPhysicsShapeType.SHAPE_UNKNOWN;
                break;
            case BroadphaseNativeTypes.HFFLUID_BUOYANT_CONVEX_SHAPE_PROXYTYPE:
                ret =  BSPhysicsShapeType.SHAPE_UNKNOWN;
                break;
            case BroadphaseNativeTypes.INVALID_SHAPE_PROXYTYPE:
                ret =  BSPhysicsShapeType.SHAPE_UNKNOWN;
                break;
        }
        return ret;
    }

    public override void RemoveChildShapeFromCompoundShape(BulletShape cShape, BulletShape removeShape) { /* TODO */ }
    public override void UpdateChildTransform(BulletShape pShape, int childIndex, Vector3 pos, Quaternion rot, bool shouldRecalculateLocalAabb) { /* TODO */ }

    public override BulletShape CreateGroundPlaneShape(uint pLocalId, float pheight, float pcollisionMargin)
    {
        StaticPlaneShape m_planeshape = new StaticPlaneShape(new IndexedVector3(0,0,1),(int)pheight );
        m_planeshape.SetMargin(pcollisionMargin);
        m_planeshape.SetUserPointer(pLocalId);
        return new BulletShapeXNA(m_planeshape, BSPhysicsShapeType.SHAPE_GROUNDPLANE);
    }

    public override BulletConstraint Create6DofSpringConstraint(BulletWorld pWorld, BulletBody pBody1, BulletBody pBody2,
                                    Vector3 pframe1, Quaternion pframe1rot, Vector3 pframe2, Quaternion pframe2rot,
                                    bool puseLinearReferenceFrameA, bool pdisableCollisionsBetweenLinkedBodies)

    {
        Generic6DofSpringConstraint constrain = null;
        DiscreteDynamicsWorld world = (pWorld as BulletWorldXNA).world;
        RigidBody body1 = (pBody1 as BulletBodyXNA).rigidBody;
        RigidBody body2 = (pBody2 as BulletBodyXNA).rigidBody;
        if (body1 != null && body2 != null)
        {
            IndexedVector3 frame1v = new IndexedVector3(pframe1.X, pframe1.Y, pframe1.Z);
            IndexedQuaternion frame1rot = new IndexedQuaternion(pframe1rot.X, pframe1rot.Y, pframe1rot.Z, pframe1rot.W);
            IndexedMatrix frame1 = IndexedMatrix.CreateFromQuaternion(frame1rot);
            frame1._origin = frame1v;

            IndexedVector3 frame2v = new IndexedVector3(pframe2.X, pframe2.Y, pframe2.Z);
            IndexedQuaternion frame2rot = new IndexedQuaternion(pframe2rot.X, pframe2rot.Y, pframe2rot.Z, pframe2rot.W);
            IndexedMatrix frame2 = IndexedMatrix.CreateFromQuaternion(frame2rot);
            frame2._origin = frame1v;

            constrain = new Generic6DofSpringConstraint(body1, body2, ref frame1, ref frame2, puseLinearReferenceFrameA);
            world.AddConstraint(constrain, pdisableCollisionsBetweenLinkedBodies);

            constrain.CalculateTransforms();
        }

        return new BulletConstraintXNA(constrain);
    }

    public override BulletConstraint CreateHingeConstraint(BulletWorld pWorld, BulletBody pBody1, BulletBody pBody2,
                        Vector3 ppivotInA, Vector3 ppivotInB, Vector3 paxisInA, Vector3 paxisInB,
                        bool puseLinearReferenceFrameA, bool pdisableCollisionsBetweenLinkedBodies)
    {
        HingeConstraint constrain = null;
        DiscreteDynamicsWorld world = (pWorld as BulletWorldXNA).world;
        RigidBody rb1 = (pBody1 as BulletBodyXNA).rigidBody;
        RigidBody rb2 = (pBody2 as BulletBodyXNA).rigidBody;
        if (rb1 != null && rb2 != null)
        {
            IndexedVector3 pivotInA = new IndexedVector3(ppivotInA.X, ppivotInA.Y, ppivotInA.Z);
            IndexedVector3 pivotInB = new IndexedVector3(ppivotInB.X, ppivotInB.Y, ppivotInB.Z);
            IndexedVector3 axisInA = new IndexedVector3(paxisInA.X, paxisInA.Y, paxisInA.Z);
            IndexedVector3 axisInB = new IndexedVector3(paxisInB.X, paxisInB.Y, paxisInB.Z);
            constrain = new HingeConstraint(rb1, rb2, ref pivotInA, ref pivotInB, ref axisInA, ref axisInB, puseLinearReferenceFrameA);
            world.AddConstraint(constrain, pdisableCollisionsBetweenLinkedBodies);
        }
        return new BulletConstraintXNA(constrain);
    }

    public override BulletConstraint CreateSliderConstraint(BulletWorld pWorld, BulletBody pBody1, BulletBody pBody2,
                        Vector3 pframe1, Quaternion pframe1rot,
                        Vector3 pframe2, Quaternion pframe2rot,
                        bool puseLinearReferenceFrameA, bool pdisableCollisionsBetweenLinkedBodies)
    {
        SliderConstraint constrain = null;
        DiscreteDynamicsWorld world = (pWorld as BulletWorldXNA).world;
        RigidBody rb1 = (pBody1 as BulletBodyXNA).rigidBody;
        RigidBody rb2 = (pBody2 as BulletBodyXNA).rigidBody;
        if (rb1 != null && rb2 != null)
        {
            IndexedVector3 frame1v = new IndexedVector3(pframe1.X, pframe1.Y, pframe1.Z);
            IndexedQuaternion frame1rot = new IndexedQuaternion(pframe1rot.X, pframe1rot.Y, pframe1rot.Z, pframe1rot.W);
            IndexedMatrix frame1 = IndexedMatrix.CreateFromQuaternion(frame1rot);
            frame1._origin = frame1v;

            IndexedVector3 frame2v = new IndexedVector3(pframe2.X, pframe2.Y, pframe2.Z);
            IndexedQuaternion frame2rot = new IndexedQuaternion(pframe2rot.X, pframe2rot.Y, pframe2rot.Z, pframe2rot.W);
            IndexedMatrix frame2 = IndexedMatrix.CreateFromQuaternion(frame2rot);
            frame2._origin = frame1v;

            constrain = new SliderConstraint(rb1, rb2, ref frame1, ref frame2, puseLinearReferenceFrameA);
            world.AddConstraint(constrain, pdisableCollisionsBetweenLinkedBodies);
        }
        return new BulletConstraintXNA(constrain);
    }

    public override BulletConstraint CreateConeTwistConstraint(BulletWorld pWorld, BulletBody pBody1, BulletBody pBody2,
                        Vector3 pframe1, Quaternion pframe1rot,
                        Vector3 pframe2, Quaternion pframe2rot,
                        bool pdisableCollisionsBetweenLinkedBodies)
    {
        ConeTwistConstraint constrain = null;
        DiscreteDynamicsWorld world = (pWorld as BulletWorldXNA).world;
        RigidBody rb1 = (pBody1 as BulletBodyXNA).rigidBody;
        RigidBody rb2 = (pBody2 as BulletBodyXNA).rigidBody;
        if (rb1 != null && rb2 != null)
        {
            IndexedVector3 frame1v = new IndexedVector3(pframe1.X, pframe1.Y, pframe1.Z);
            IndexedQuaternion frame1rot = new IndexedQuaternion(pframe1rot.X, pframe1rot.Y, pframe1rot.Z, pframe1rot.W);
            IndexedMatrix frame1 = IndexedMatrix.CreateFromQuaternion(frame1rot);
            frame1._origin = frame1v;

            IndexedVector3 frame2v = new IndexedVector3(pframe2.X, pframe2.Y, pframe2.Z);
            IndexedQuaternion frame2rot = new IndexedQuaternion(pframe2rot.X, pframe2rot.Y, pframe2rot.Z, pframe2rot.W);
            IndexedMatrix frame2 = IndexedMatrix.CreateFromQuaternion(frame2rot);
            frame2._origin = frame1v;

            constrain = new ConeTwistConstraint(rb1, rb2, ref frame1, ref frame2);
            world.AddConstraint(constrain, pdisableCollisionsBetweenLinkedBodies);
        }
        return new BulletConstraintXNA(constrain);
    }

    public override BulletConstraint CreateGearConstraint(BulletWorld pWorld, BulletBody pBody1, BulletBody pBody2,
                        Vector3 paxisInA, Vector3 paxisInB,
                        float pratio, bool pdisableCollisionsBetweenLinkedBodies)
    {
        Generic6DofConstraint constrain = null;
        /*   BulletXNA does not have a gear constraint
        GearConstraint constrain = null;
        DiscreteDynamicsWorld world = (pWorld as BulletWorldXNA).world;
        RigidBody rb1 = (pBody1 as BulletBodyXNA).rigidBody;
        RigidBody rb2 = (pBody2 as BulletBodyXNA).rigidBody;
        if (rb1 != null && rb2 != null)
        {
            IndexedVector3 axis1 = new IndexedVector3(paxisInA.X, paxisInA.Y, paxisInA.Z);
            IndexedVector3 axis2 = new IndexedVector3(paxisInB.X, paxisInB.Y, paxisInB.Z);
            constrain = new GearConstraint(rb1, rb2, ref axis1, ref axis2, pratio);
            world.AddConstraint(constrain, pdisableCollisionsBetweenLinkedBodies);
        }
        */
        return new BulletConstraintXNA(constrain);
    }

    public override BulletConstraint CreatePoint2PointConstraint(BulletWorld pWorld, BulletBody pBody1, BulletBody pBody2,
                        Vector3 ppivotInA, Vector3 ppivotInB,
                        bool pdisableCollisionsBetweenLinkedBodies)
    {
        Point2PointConstraint constrain = null;
        DiscreteDynamicsWorld world = (pWorld as BulletWorldXNA).world;
        RigidBody rb1 = (pBody1 as BulletBodyXNA).rigidBody;
        RigidBody rb2 = (pBody2 as BulletBodyXNA).rigidBody;
        if (rb1 != null && rb2 != null)
        {
            IndexedVector3 pivotInA = new IndexedVector3(ppivotInA.X, ppivotInA.Y, ppivotInA.Z);
            IndexedVector3 pivotInB = new IndexedVector3(ppivotInB.X, ppivotInB.Y, ppivotInB.Z);
            constrain = new Point2PointConstraint(rb1, rb2, ref pivotInA, ref pivotInB);
            world.AddConstraint(constrain, pdisableCollisionsBetweenLinkedBodies);
        }
        return new BulletConstraintXNA(constrain);
    }

    public override BulletShape CreateHullShape(BulletWorld pWorld, int pHullCount, float[] pConvHulls)
    {
        DiscreteDynamicsWorld world = (pWorld as BulletWorldXNA).world;
        CompoundShape compoundshape = new CompoundShape(false);

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

        return new BulletShapeXNA(compoundshape, BSPhysicsShapeType.SHAPE_HULL);
    }

    public override BulletShape BuildHullShapeFromMesh(BulletWorld world, BulletShape meshShape, HACDParams parms)
    {
        /* TODO */ return null;
    }

    public override BulletShape BuildConvexHullShapeFromMesh(BulletWorld world, BulletShape meshShape)
    {
        /* TODO */ return null;
    }

    public override BulletShape CreateConvexHullShape(BulletWorld pWorld, int pIndicesCount, int[] indices, int pVerticesCount, float[] verticesAsFloats)
    {
        /* TODO */ return null;
    }

    public override BulletShape CreateMeshShape(BulletWorld pWorld, int pIndicesCount, int[] indices, int pVerticesCount, float[] verticesAsFloats)
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
        DiscreteDynamicsWorld world = (pWorld as BulletWorldXNA).world;
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
        return new BulletShapeXNA(meshShape, BSPhysicsShapeType.SHAPE_MESH);

    }
    public override BulletShape CreateGImpactShape(BulletWorld pWorld, int pIndicesCount, int[] indices, int pVerticesCount, float[] verticesAsFloats)
    {
        // TODO:
        return null;
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

    public override BulletShape CreateTerrainShape(uint id, Vector3 size, float minHeight, float maxHeight, float[] heightMap,
								float scaleFactor, float collisionMargin)
    {
        const int upAxis = 2;
        HeightfieldTerrainShape terrainShape = new HeightfieldTerrainShape((int)size.X, (int)size.Y,
                                                                           heightMap,  scaleFactor,
                                                                           minHeight, maxHeight, upAxis,
                                                                            false);
        terrainShape.SetMargin(collisionMargin);
        terrainShape.SetUseDiamondSubdivision(true);
        terrainShape.SetUserPointer(id);
        return new BulletShapeXNA(terrainShape, BSPhysicsShapeType.SHAPE_TERRAIN);
    }

    public override bool TranslationalLimitMotor(BulletConstraint pConstraint, float ponOff, float targetVelocity, float maxMotorForce)
    {
        TypedConstraint tconstrain = (pConstraint as BulletConstraintXNA).constrain;
        bool onOff = ponOff != 0;
        bool ret = false;

        switch (tconstrain.GetConstraintType())
        {
            case TypedConstraintType.D6_CONSTRAINT_TYPE:
                Generic6DofConstraint constrain = tconstrain as Generic6DofConstraint;
                constrain.GetTranslationalLimitMotor().m_enableMotor[0] = onOff;
                constrain.GetTranslationalLimitMotor().m_targetVelocity[0] = targetVelocity;
                constrain.GetTranslationalLimitMotor().m_maxMotorForce[0] = maxMotorForce;
                ret = true;
                break;
        }


        return ret;

    }

    public override int PhysicsStep(BulletWorld world, float timeStep, int maxSubSteps, float fixedTimeStep,
                        out int updatedEntityCount, out int collidersCount)
    {
        /* TODO */
        updatedEntityCount = 0;
        collidersCount = 0;


        int ret = PhysicsStep2(world,timeStep,maxSubSteps,fixedTimeStep,out updatedEntityCount,out world.physicsScene.m_updateArray, out collidersCount, out world.physicsScene.m_collisionArray);

        return ret;
    }

    private int PhysicsStep2(BulletWorld pWorld, float timeStep, int m_maxSubSteps, float m_fixedTimeStep,
                    out int updatedEntityCount, out EntityProperties[] updatedEntities,
                    out int collidersCount, out CollisionDesc[] colliders)
    {
        int epic = PhysicsStepint(pWorld, timeStep, m_maxSubSteps, m_fixedTimeStep, out updatedEntityCount, out updatedEntities,
                                out collidersCount, out colliders, m_maxCollisions, m_maxUpdatesPerFrame);
        return epic;
    }

    private int PhysicsStepint(BulletWorld pWorld,float timeStep, int m_maxSubSteps, float m_fixedTimeStep, out int updatedEntityCount,
        out  EntityProperties[] updatedEntities, out int collidersCount, out CollisionDesc[] colliders, int maxCollisions, int maxUpdates)
    {
        int numSimSteps = 0;
        Array.Clear(UpdatedObjects, 0, UpdatedObjects.Length);
        Array.Clear(UpdatedCollisions, 0, UpdatedCollisions.Length);
        LastEntityProperty=0;






        LastCollisionDesc=0;

        updatedEntityCount = 0;
        collidersCount = 0;


        if (pWorld is BulletWorldXNA)
        {
            DiscreteDynamicsWorld world = (pWorld as BulletWorldXNA).world;

            world.LastCollisionDesc = 0;
            world.LastEntityProperty = 0;
            numSimSteps = world.StepSimulation(timeStep, m_maxSubSteps, m_fixedTimeStep);

            PersistentManifold contactManifold;
            CollisionObject objA;
            CollisionObject objB;
            ManifoldPoint manifoldPoint;
            PairCachingGhostObject pairCachingGhostObject;

            m_collisionsThisFrame = 0;
            int numManifolds = world.GetDispatcher().GetNumManifolds();
            for (int j = 0; j < numManifolds; j++)
            {
                contactManifold = world.GetDispatcher().GetManifoldByIndexInternal(j);
                int numContacts = contactManifold.GetNumContacts();
                if (numContacts == 0)
                    continue;

                objA = contactManifold.GetBody0() as CollisionObject;
                objB = contactManifold.GetBody1() as CollisionObject;

                manifoldPoint = contactManifold.GetContactPoint(0);
                //IndexedVector3 contactPoint = manifoldPoint.GetPositionWorldOnB();
               // IndexedVector3 contactNormal = -manifoldPoint.m_normalWorldOnB; // make relative to A

                RecordCollision(this, objA, objB, manifoldPoint.GetPositionWorldOnB(), -manifoldPoint.m_normalWorldOnB, manifoldPoint.GetDistance());
                m_collisionsThisFrame ++;
                if (m_collisionsThisFrame >= 9999999)
                    break;


            }

            foreach (GhostObject ghostObject in specialCollisionObjects.Values)
            {
                pairCachingGhostObject = ghostObject as PairCachingGhostObject;
                if (pairCachingGhostObject != null)
                {
                    RecordGhostCollisions(pairCachingGhostObject);
                }

            }


            updatedEntityCount = LastEntityProperty;
            updatedEntities = UpdatedObjects;

            collidersCount = LastCollisionDesc;
            colliders = UpdatedCollisions;


        }
        else
        {
            //if (updatedEntities is null)
            //updatedEntities = new List<BulletXNA.EntityProperties>();
            //updatedEntityCount = 0;


            //collidersCount = 0;

            updatedEntities = new EntityProperties[0];


            colliders = new CollisionDesc[0];

        }
        return numSimSteps;
    }
    public void RecordGhostCollisions(PairCachingGhostObject obj)
    {
        IOverlappingPairCache cache = obj.GetOverlappingPairCache();
        ObjectArray<BroadphasePair> pairs = cache.GetOverlappingPairArray();

        DiscreteDynamicsWorld world = (PhysicsScene.World as BulletWorldXNA).world;
        PersistentManifoldArray manifoldArray = new PersistentManifoldArray();
        BroadphasePair collisionPair;
        PersistentManifold contactManifold;

        CollisionObject objA;
        CollisionObject objB;

        ManifoldPoint pt;

        int numPairs = pairs.Count;

        for (int i = 0; i < numPairs; i++)
        {
            manifoldArray.Clear();
            if (LastCollisionDesc < UpdatedCollisions.Length)
                break;
            collisionPair = world.GetPairCache().FindPair(pairs[i].m_pProxy0, pairs[i].m_pProxy1);
            if (collisionPair == null)
                continue;

            collisionPair.m_algorithm.GetAllContactManifolds(manifoldArray);
            for (int j = 0; j < manifoldArray.Count; j++)
            {
                contactManifold = manifoldArray[j];
                int numContacts = contactManifold.GetNumContacts();
                objA = contactManifold.GetBody0() as CollisionObject;
                objB = contactManifold.GetBody1() as CollisionObject;
                for (int p = 0; p < numContacts; p++)
                {
                    pt = contactManifold.GetContactPoint(p);
                    if (pt.GetDistance() < 0.0f)
                    {
                        RecordCollision(this, objA, objB, pt.GetPositionWorldOnA(), -pt.m_normalWorldOnB,pt.GetDistance());
                        break;
                    }
                }
            }
        }

    }
    private static void RecordCollision(BSAPIXNA world, CollisionObject objA, CollisionObject objB, IndexedVector3 contact, IndexedVector3 norm, float penetration)
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

        //ulong collisionID = ((ulong) idA << 32) | idB;

        CollisionDesc cDesc = new CollisionDesc()
                                            {
                                                aID = idA,
                                                bID = idB,
                                                point = new Vector3(contact.X,contact.Y,contact.Z),
                                                normal = new Vector3(contactNormal.X,contactNormal.Y,contactNormal.Z),
                                                penetration = penetration

                                            };
        if (world.LastCollisionDesc < world.UpdatedCollisions.Length)
            world.UpdatedCollisions[world.LastCollisionDesc++] = (cDesc);
        m_collisionsThisFrame++;


    }
    private static EntityProperties GetDebugProperties(BulletWorld pWorld, BulletBody pCollisionObject)
    {
        EntityProperties ent = new EntityProperties();
        DiscreteDynamicsWorld world = (pWorld as BulletWorldXNA).world;
        CollisionObject collisionObject = (pCollisionObject as BulletBodyXNA).rigidBody;
        IndexedMatrix transform = collisionObject.GetWorldTransform();
        IndexedVector3 LinearVelocity = collisionObject.GetInterpolationLinearVelocity();
        IndexedVector3 AngularVelocity = collisionObject.GetInterpolationAngularVelocity();
        IndexedQuaternion rotation = transform.GetRotation();
        ent.Acceleration = Vector3.Zero;
        ent.ID = (uint)collisionObject.GetUserPointer();
        ent.Position = new Vector3(transform._origin.X,transform._origin.Y,transform._origin.Z);
        ent.Rotation = new Quaternion(rotation.X,rotation.Y,rotation.Z,rotation.W);
        ent.Velocity = new Vector3(LinearVelocity.X, LinearVelocity.Y, LinearVelocity.Z);
        ent.RotationalVelocity = new Vector3(AngularVelocity.X, AngularVelocity.Y, AngularVelocity.Z);
        return ent;
    }

    public override bool UpdateParameter(BulletWorld world, uint localID, String parm, float value) { /* TODO */
        return false; }

    public override Vector3 GetLocalScaling(BulletShape pShape)
    {
        CollisionShape shape = (pShape as BulletShapeXNA).shape;
        IndexedVector3 scale = shape.GetLocalScaling();
        return new Vector3(scale.X,scale.Y,scale.Z);
    }

    public bool RayCastGround(BulletWorld pWorld, Vector3 _RayOrigin, float pRayHeight, BulletBody NotMe)
    {
        DiscreteDynamicsWorld world = (pWorld as BulletWorldXNA).world;
        if (world != null)
        {
            if (NotMe is BulletBodyXNA && NotMe.HasPhysicalBody)
            {
                CollisionObject AvoidBody = (NotMe as BulletBodyXNA).body;

                IndexedVector3 rOrigin = new IndexedVector3(_RayOrigin.X, _RayOrigin.Y, _RayOrigin.Z);
                IndexedVector3 rEnd = new IndexedVector3(_RayOrigin.X, _RayOrigin.Y, _RayOrigin.Z - pRayHeight);
                using (
                    ClosestNotMeRayResultCallback rayCallback =
                                            new ClosestNotMeRayResultCallback(rOrigin, rEnd, AvoidBody)
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




    public class SimMotionState : DefaultMotionState
    {
        public RigidBody Rigidbody;
        public Vector3 ZeroVect;

        private IndexedMatrix m_xform;

        private EntityProperties m_properties;
        private EntityProperties m_lastProperties;
        private BSAPIXNA m_world;

        const float POSITION_TOLERANCE = 0.05f;
        const float VELOCITY_TOLERANCE = 0.001f;
        const float ROTATION_TOLERANCE = 0.01f;
        const float ANGULARVELOCITY_TOLERANCE = 0.01f;

        public SimMotionState(BSAPIXNA pWorld, uint id, IndexedMatrix starTransform, object frameUpdates)
        {
            IndexedQuaternion OrientationQuaterion = starTransform.GetRotation();
            m_properties = new EntityProperties()
                               {
                                   ID = id,
                                   Position = new Vector3(starTransform._origin.X, starTransform._origin.Y,starTransform._origin.Z),
                                   Rotation = new Quaternion(OrientationQuaterion.X,OrientationQuaterion.Y,OrientationQuaterion.Z,OrientationQuaterion.W)
                               };
            m_lastProperties = new EntityProperties()
            {
                ID = id,
                Position = new Vector3(starTransform._origin.X, starTransform._origin.Y, starTransform._origin.Z),
                Rotation = new Quaternion(OrientationQuaterion.X, OrientationQuaterion.Y, OrientationQuaterion.Z, OrientationQuaterion.W)
            };
            m_world = pWorld;
            m_xform = starTransform;
        }

        public override void GetWorldTransform(out IndexedMatrix worldTrans)
        {
            worldTrans = m_xform;
        }

        public override void SetWorldTransform(IndexedMatrix worldTrans)
        {
            SetWorldTransform(ref worldTrans);
        }

        public override void SetWorldTransform(ref IndexedMatrix worldTrans)
        {
            SetWorldTransform(ref worldTrans, false);
        }
        public void SetWorldTransform(ref IndexedMatrix worldTrans, bool force)
        {
            m_xform = worldTrans;
            // Put the new transform into m_properties
            IndexedQuaternion OrientationQuaternion = m_xform.GetRotation();
            IndexedVector3 LinearVelocityVector = Rigidbody.GetLinearVelocity();
            IndexedVector3 AngularVelocityVector = Rigidbody.GetAngularVelocity();
            m_properties.Position = new Vector3(m_xform._origin.X, m_xform._origin.Y, m_xform._origin.Z);
            m_properties.Rotation = new Quaternion(OrientationQuaternion.X, OrientationQuaternion.Y,
                                                   OrientationQuaternion.Z, OrientationQuaternion.W);
            // A problem with stock Bullet is that we don't get an event when an object is deactivated.
            // This means that the last non-zero values for linear and angular velocity
            // are left in the viewer who does dead reconning and the objects look like
            // they float off.
            // BulletSim ships with a patch to Bullet which creates such an event.
            m_properties.Velocity = new Vector3(LinearVelocityVector.X, LinearVelocityVector.Y, LinearVelocityVector.Z);
            m_properties.RotationalVelocity = new Vector3(AngularVelocityVector.X, AngularVelocityVector.Y, AngularVelocityVector.Z);

            if (force

                || !AlmostEqual(ref m_lastProperties.Position, ref m_properties.Position, POSITION_TOLERANCE)
                || !AlmostEqual(ref m_properties.Rotation, ref m_lastProperties.Rotation, ROTATION_TOLERANCE)
                // If the Velocity and AngularVelocity are zero, most likely the object has
                //    been deactivated. If they both are zero and they have become zero recently,
                //    make sure a property update is sent so the zeros make it to the viewer.
                || ((m_properties.Velocity == ZeroVect && m_properties.RotationalVelocity == ZeroVect)
                    &&
                    (m_properties.Velocity != m_lastProperties.Velocity ||
                     m_properties.RotationalVelocity != m_lastProperties.RotationalVelocity))
                //	If Velocity and AngularVelocity are non-zero but have changed, send an update.
                || !AlmostEqual(ref m_properties.Velocity, ref m_lastProperties.Velocity, VELOCITY_TOLERANCE)
                ||
                !AlmostEqual(ref m_properties.RotationalVelocity, ref m_lastProperties.RotationalVelocity,
                             ANGULARVELOCITY_TOLERANCE)
                )


            {
                // Add this update to the list of updates for this frame.
                m_lastProperties = m_properties;
                if (m_world.LastEntityProperty < m_world.UpdatedObjects.Length)
                    m_world.UpdatedObjects[m_world.LastEntityProperty++]=(m_properties);

                //(*m_updatesThisFrame)[m_properties.ID] = &m_properties;
            }




        }
        public override void SetRigidBody(RigidBody body)
        {
            Rigidbody = body;
        }
        internal static bool AlmostEqual(ref Vector3 v1, ref Vector3 v2, float nEpsilon)
        {
            return
            (((v1.X - nEpsilon) < v2.X) && (v2.X < (v1.X + nEpsilon))) &&
            (((v1.Y - nEpsilon) < v2.Y) && (v2.Y < (v1.Y + nEpsilon))) &&
            (((v1.Z - nEpsilon) < v2.Z) && (v2.Z < (v1.Z + nEpsilon)));
        }

        internal static bool AlmostEqual(ref Quaternion v1, ref Quaternion v2, float nEpsilon)
        {
            return
            (((v1.X - nEpsilon) < v2.X) && (v2.X < (v1.X + nEpsilon))) &&
            (((v1.Y - nEpsilon) < v2.Y) && (v2.Y < (v1.Y + nEpsilon))) &&
            (((v1.Z - nEpsilon) < v2.Z) && (v2.Z < (v1.Z + nEpsilon))) &&
            (((v1.W - nEpsilon) < v2.W) && (v2.W < (v1.W + nEpsilon)));
        }

    }
}

