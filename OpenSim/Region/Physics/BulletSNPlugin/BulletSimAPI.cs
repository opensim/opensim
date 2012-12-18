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
using System.Security;
using System.Text;
using BulletXNA;
using OpenMetaverse;
using BulletXNA.LinearMath;
using BulletXNA.BulletCollision;
using BulletXNA.BulletDynamics;
using BulletXNA.BulletCollision.CollisionDispatch;
using OpenSim.Framework;

namespace OpenSim.Region.Physics.BulletSNPlugin {

// Classes to allow some type checking for the API
// These hold pointers to allocated objects in the unmanaged space.



    // Constraint type values as defined by Bullet
public enum ConstraintType : int
{
	POINT2POINT_CONSTRAINT_TYPE = 3,
	HINGE_CONSTRAINT_TYPE,
	CONETWIST_CONSTRAINT_TYPE,
	D6_CONSTRAINT_TYPE,
	SLIDER_CONSTRAINT_TYPE,
	CONTACT_CONSTRAINT_TYPE,
	D6_SPRING_CONSTRAINT_TYPE,
	MAX_CONSTRAINT_TYPE
}

// ===============================================================================
[StructLayout(LayoutKind.Sequential)]
public struct ConvexHull
{
	Vector3 Offset;
	int VertexCount;
	Vector3[] Vertices;
}
public enum BSPhysicsShapeType
{
	SHAPE_UNKNOWN   = 0,
	SHAPE_CAPSULE   = 1,
	SHAPE_BOX       = 2,
	SHAPE_CONE      = 3,
	SHAPE_CYLINDER  = 4,
	SHAPE_SPHERE    = 5,
	SHAPE_MESH      = 6,
	SHAPE_HULL      = 7,
    // following defined by BulletSim
	SHAPE_GROUNDPLANE  = 20,
	SHAPE_TERRAIN   = 21,
	SHAPE_COMPOUND  = 22,
	SHAPE_HEIGHTMAP = 23,
};

// The native shapes have predefined shape hash keys
public enum FixedShapeKey : ulong
{
    KEY_NONE        = 0,
    KEY_BOX         = 1,
    KEY_SPHERE      = 2,
    KEY_CONE        = 3,
    KEY_CYLINDER    = 4,
    KEY_CAPSULE     = 5,
}

[StructLayout(LayoutKind.Sequential)]
public struct ShapeData
{
    public uint ID;
    public BSPhysicsShapeType Type;
    public Vector3 Position;
    public Quaternion Rotation;
    public Vector3 Velocity;
    public Vector3 Scale;
    public float Mass;
    public float Buoyancy;
    public System.UInt64 HullKey;
    public System.UInt64 MeshKey;
    public float Friction;
    public float Restitution;
    public float Collidable;    // true of things bump into this
    public float Static;        // true if a static object. Otherwise gravity, etc.
    public float Solid;         // true if object cannot be passed through
    public Vector3 Size;

    // note that bools are passed as floats since bool size changes by language and architecture
    public const float numericTrue = 1f;
    public const float numericFalse = 0f;
}
[StructLayout(LayoutKind.Sequential)]
public struct SweepHit
{
    public uint ID;
    public float Fraction;
    public Vector3 Normal;
    public Vector3 Point;
}
[StructLayout(LayoutKind.Sequential)]
public struct RaycastHit
{
    public uint ID;
    public float Fraction;
    public Vector3 Normal;
}
[StructLayout(LayoutKind.Sequential)]
public struct CollisionDesc
{
    public uint aID;
    public uint bID;
    public Vector3 point;
    public Vector3 normal;
}
[StructLayout(LayoutKind.Sequential)]
public struct EntityProperties
{
    public uint ID;
    public Vector3 Position;
    public Quaternion Rotation;
    public Vector3 Velocity;
    public Vector3 Acceleration;
    public Vector3 RotationalVelocity;
    public override string ToString()
    {
        return string.Format("ID:{0}, Pos:<{1:F},{2:F},{3:F}>, Rot:<{4:F},{5:F},{6:F},{7:F}>, LVel:<{8:F},{9:F},{10:F}>, AVel:<{11:F},{12:F},{13:F}>",
            ID.ToString(),
            Position.X,Position.Y,Position.Z,
            Rotation.X,Rotation.Y,Rotation.Z,Rotation.W,
            Velocity.X,Velocity.Y,Velocity.Z,
            RotationalVelocity.X,RotationalVelocity.Y,RotationalVelocity.Z
            );
    }
}

// Format of this structure must match the definition in the C++ code
// NOTE: adding the X causes compile breaks if used. These are unused symbols
//      that can be removed from both here and the unmanaged definition of this structure.
[StructLayout(LayoutKind.Sequential)]
public struct ConfigurationParameters
{
    public float defaultFriction;
    public float defaultDensity;
    public float defaultRestitution;
    public float collisionMargin;
    public float gravity;

    public float XlinearDamping;
    public float XangularDamping;
    public float XdeactivationTime;
    public float XlinearSleepingThreshold;
    public float XangularSleepingThreshold;
	public float XccdMotionThreshold;
	public float XccdSweptSphereRadius;
    public float XcontactProcessingThreshold;

    public float XterrainImplementation;
    public float XterrainFriction;
    public float XterrainHitFraction;
    public float XterrainRestitution;
    public float XterrainCollisionMargin;

    public float XavatarFriction;
    public float XavatarStandingFriction;
    public float XavatarDensity;
    public float XavatarRestitution;
    public float XavatarCapsuleWidth;
    public float XavatarCapsuleDepth;
    public float XavatarCapsuleHeight;
	public float XavatarContactProcessingThreshold;

    public float XvehicleAngularDamping;

	public float maxPersistantManifoldPoolSize;
	public float maxCollisionAlgorithmPoolSize;
	public float shouldDisableContactPoolDynamicAllocation;
	public float shouldForceUpdateAllAabbs;
	public float shouldRandomizeSolverOrder;
	public float shouldSplitSimulationIslands;
	public float shouldEnableFrictionCaching;
	public float numberOfSolverIterations;

    public float XlinksetImplementation;
    public float XlinkConstraintUseFrameOffset;
    public float XlinkConstraintEnableTransMotor;
    public float XlinkConstraintTransMotorMaxVel;
    public float XlinkConstraintTransMotorMaxForce;
    public float XlinkConstraintERP;
    public float XlinkConstraintCFM;
    public float XlinkConstraintSolverIterations;

    public float physicsLoggingFrames;

    public const float numericTrue = 1f;
    public const float numericFalse = 0f;
}


// The states a bullet collision object can have

public enum ActivationState : uint
{
    UNDEFINED = 0,
    ACTIVE_TAG = 1,
    ISLAND_SLEEPING = 2,
    WANTS_DEACTIVATION = 3,
    DISABLE_DEACTIVATION = 4,
    DISABLE_SIMULATION = 5,
}

public enum CollisionObjectTypes : int
{
    CO_COLLISION_OBJECT             = 1 << 0,
    CO_RIGID_BODY                   = 1 << 1,
    CO_GHOST_OBJECT                 = 1 << 2,
    CO_SOFT_BODY                    = 1 << 3,
    CO_HF_FLUID                     = 1 << 4,
    CO_USER_TYPE                    = 1 << 5,
}

// Values used by Bullet and BulletSim to control object properties.
// Bullet's "CollisionFlags" has more to do with operations on the
//    object (if collisions happen, if gravity effects it, ...).
    [Flags]
public enum CollisionFlags : uint
{
    CF_STATIC_OBJECT                 = 1 << 0,
    CF_KINEMATIC_OBJECT              = 1 << 1,
    CF_NO_CONTACT_RESPONSE           = 1 << 2,
    CF_CUSTOM_MATERIAL_CALLBACK      = 1 << 3,
    CF_CHARACTER_OBJECT              = 1 << 4,
    CF_DISABLE_VISUALIZE_OBJECT      = 1 << 5,
    CF_DISABLE_SPU_COLLISION_PROCESS = 1 << 6,
    // Following used by BulletSim to control collisions and updates
    BS_SUBSCRIBE_COLLISION_EVENTS    = 1 << 10,
    BS_FLOATS_ON_WATER               = 1 << 11,
    BS_VEHICLE_COLLISIONS            = 1 << 12,
    BS_NONE                          = 0,
    BS_ALL                           = 0xFFFFFFFF,

    // These are the collision flags switched depending on physical state.
    // The other flags are used for other things and should not be fooled with.
    BS_ACTIVE = CF_STATIC_OBJECT
                | CF_KINEMATIC_OBJECT
                | CF_NO_CONTACT_RESPONSE
};

// Values for collisions groups and masks
public enum CollisionFilterGroups : uint
{
    // Don't use the bit definitions!!  Define the use in a
    //   filter/mask definition below. This way collision interactions
    //   are more easily debugged.
    BNoneGroup              = 0,
    BDefaultGroup           = 1 << 0,
    BStaticGroup            = 1 << 1,
    BKinematicGroup         = 1 << 2,
    BDebrisGroup            = 1 << 3,
    BSensorTrigger          = 1 << 4,
    BCharacterGroup         = 1 << 5,
    BAllGroup               = 0xFFFFFFFF,
    // Filter groups defined by BulletSim
    BGroundPlaneGroup       = 1 << 10,
    BTerrainGroup           = 1 << 11,
    BRaycastGroup           = 1 << 12,
    BSolidGroup             = 1 << 13,
    // BLinksetGroup        = xx  // a linkset proper is either static or dynamic
    BLinksetChildGroup      = 1 << 14,
    // The collsion filters and masked are defined in one place -- don't want them scattered
    AvatarGroup             = BCharacterGroup,
    AvatarMask              = BAllGroup,
    ObjectGroup             = BSolidGroup,
    ObjectMask              = BAllGroup,
    StaticObjectGroup       = BStaticGroup,
    StaticObjectMask        = AvatarGroup | ObjectGroup,    // static things don't interact with much
    LinksetGroup            =  BLinksetChildGroup,
    LinksetMask             = BAllGroup & ~BLinksetChildGroup, // linkset objects don't collide with each other
    VolumeDetectGroup       = BSensorTrigger,
    VolumeDetectMask        = ~BSensorTrigger,
    TerrainGroup            = BTerrainGroup,
    TerrainMask             = BAllGroup & ~BStaticGroup,  // static objects on the ground don't collide
    GroundPlaneGroup        = BGroundPlaneGroup,
    GroundPlaneMask         = BAllGroup

};

// CFM controls the 'hardness' of the constraint. 0=fixed, 0..1=violatable. Default=0
// ERP controls amount of correction per tick. Usable range=0.1..0.8. Default=0.2.
public enum ConstraintParams : int
{
    BT_CONSTRAINT_ERP = 1,  // this one is not used in Bullet as of 20120730
    BT_CONSTRAINT_STOP_ERP,
    BT_CONSTRAINT_CFM,
    BT_CONSTRAINT_STOP_CFM,
};
public enum ConstraintParamAxis : int
{
    AXIS_LINEAR_X = 0,
    AXIS_LINEAR_Y,
    AXIS_LINEAR_Z,
    AXIS_ANGULAR_X,
    AXIS_ANGULAR_Y,
    AXIS_ANGULAR_Z,
    AXIS_LINEAR_ALL = 20,    // these last three added by BulletSim so we don't have to do zillions of calls
    AXIS_ANGULAR_ALL,
    AXIS_ALL
};

// ===============================================================================
static class BulletSimAPI {
    private static int m_collisionsThisFrame;
    public delegate void DebugLogCallback(string msg);
    /// <summary>
    /// 
    /// </summary>
    /// <param name="p"></param>
    /// <param name="p_2"></param>
    internal static bool RemoveObjectFromWorld2(object pWorld, object pBody)
    {
        DiscreteDynamicsWorld world = pWorld as DiscreteDynamicsWorld;
        RigidBody body = pBody as RigidBody;
        world.RemoveRigidBody(body);
        return true;
    }

    internal static void SetRestitution2(object pBody, float pRestitution)
    {
        RigidBody body = pBody as RigidBody;
        body.SetRestitution(pRestitution);
    }

    internal static void SetMargin2(object pShape, float pMargin)
    {
        CollisionShape shape = pShape as CollisionShape;
        shape.SetMargin(pMargin);
    }

    internal static void SetLocalScaling2(object pShape, Vector3 pScale)
    {
        CollisionShape shape = pShape as CollisionShape;
        IndexedVector3 vec = new IndexedVector3(pScale.X, pScale.Y, pScale.Z);
        shape.SetLocalScaling(ref vec);

    }

    internal static void SetContactProcessingThreshold2(object pBody, float contactprocessingthreshold)
    {
        RigidBody body = pBody as RigidBody;
        body.SetContactProcessingThreshold(contactprocessingthreshold);
    }

    internal static void SetCcdMotionThreshold2(object pBody, float pccdMotionThreashold)
    {
        RigidBody body = pBody as RigidBody;
        body.SetCcdMotionThreshold(pccdMotionThreashold);
    }

    internal static void SetCcdSweptSphereRadius2(object pBody, float pCcdSweptSphereRadius)
    {
        RigidBody body = pBody as RigidBody;
        body.SetCcdSweptSphereRadius(pCcdSweptSphereRadius);
    }

    internal static void SetAngularFactorV2(object pBody, Vector3 pAngularFactor)
    {
        RigidBody body = pBody as RigidBody;
        body.SetAngularFactor(new IndexedVector3(pAngularFactor.X, pAngularFactor.Y, pAngularFactor.Z));
    }

    internal static CollisionFlags AddToCollisionFlags2(object pBody, CollisionFlags pcollisionFlags)
    {
        CollisionObject body = pBody as CollisionObject;
        CollisionFlags existingcollisionFlags = (CollisionFlags)(uint)body.GetCollisionFlags();
        existingcollisionFlags |= pcollisionFlags;
        body.SetCollisionFlags((BulletXNA.BulletCollision.CollisionFlags)(uint)existingcollisionFlags);
        return (CollisionFlags) (uint) existingcollisionFlags;
    }

    internal static void AddObjectToWorld2(object pWorld, object pBody)
    {
        RigidBody body = pBody as RigidBody;
        DiscreteDynamicsWorld world = pWorld as DiscreteDynamicsWorld;
        //if (!(body.GetCollisionShape().GetShapeType() == BroadphaseNativeTypes.STATIC_PLANE_PROXYTYPE && body.GetCollisionShape().GetShapeType() == BroadphaseNativeTypes.TERRAIN_SHAPE_PROXYTYPE))

        world.AddRigidBody(body);

        //if (body.GetBroadphaseHandle() != null)
        //    world.UpdateSingleAabb(body);
    }

    internal static void AddObjectToWorld2(object pWorld, object pBody, Vector3 _position, Quaternion _orientation)
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

    internal static void ForceActivationState2(object pBody, ActivationState pActivationState)
    {
        CollisionObject body = pBody as CollisionObject;
        body.ForceActivationState((BulletXNA.BulletCollision.ActivationState)(uint)pActivationState);
    }

    internal static void UpdateSingleAabb2(object pWorld, object pBody)
    {
        CollisionObject body = pBody as CollisionObject;
        DiscreteDynamicsWorld world = pWorld as DiscreteDynamicsWorld;
        world.UpdateSingleAabb(body);
    }

    internal static bool SetCollisionGroupMask2(object pBody, uint pGroup, uint pMask)
    {
        RigidBody body = pBody as RigidBody;
        body.GetBroadphaseHandle().m_collisionFilterGroup = (BulletXNA.BulletCollision.CollisionFilterGroups) pGroup;
        body.GetBroadphaseHandle().m_collisionFilterGroup = (BulletXNA.BulletCollision.CollisionFilterGroups) pGroup;
        if ((uint) body.GetBroadphaseHandle().m_collisionFilterGroup == 0)
            return false;
        return true;
    }

    internal static void ClearAllForces2(object pBody)
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

    internal static void SetInterpolationAngularVelocity2(object pBody, Vector3 pVector3)
    {
        RigidBody body = pBody as RigidBody;
        IndexedVector3 vec = new IndexedVector3(pVector3.X, pVector3.Y, pVector3.Z);
        body.SetInterpolationAngularVelocity(ref vec);
    }

    internal static void SetAngularVelocity2(object pBody, Vector3 pVector3)
    {
        RigidBody body = pBody as RigidBody;
        IndexedVector3 vec = new IndexedVector3(pVector3.X, pVector3.Y, pVector3.Z);
        body.SetAngularVelocity(ref vec);
    }

    internal static void ClearForces2(object pBody)
    {
        RigidBody body = pBody as RigidBody;
        body.ClearForces();
    }

    internal static void SetTranslation2(object pBody, Vector3 _position, Quaternion _orientation)
    {
        RigidBody body = pBody as RigidBody;
        IndexedVector3 vposition = new IndexedVector3(_position.X, _position.Y, _position.Z);
        IndexedQuaternion vquaternion = new IndexedQuaternion(_orientation.X, _orientation.Y, _orientation.Z,
                                                              _orientation.W);
        IndexedMatrix mat = IndexedMatrix.CreateFromQuaternion(vquaternion);
        mat._origin = vposition;
        body.SetWorldTransform(mat);
        
    }

    internal static Vector3 GetPosition2(object pBody)
    {
        RigidBody body = pBody as RigidBody;
        IndexedVector3 pos = body.GetInterpolationWorldTransform()._origin;
        return new Vector3(pos.X, pos.Y, pos.Z);
    }

    internal static Vector3 CalculateLocalInertia2(object pShape, float pphysMass)
    {
        CollisionShape shape = pShape as CollisionShape;
        IndexedVector3 inertia = IndexedVector3.Zero;
        shape.CalculateLocalInertia(pphysMass, out inertia);
        return new Vector3(inertia.X, inertia.Y, inertia.Z);
    }

    internal static void SetMassProps2(object pBody, float pphysMass, Vector3 plocalInertia)
    {
        RigidBody body = pBody as RigidBody;
        IndexedVector3 inertia = new IndexedVector3(plocalInertia.X, plocalInertia.Y, plocalInertia.Z);
        body.SetMassProps(pphysMass, inertia);
    }


    internal static void SetObjectForce2(object pBody, Vector3 _force)
    {
        RigidBody body = pBody as RigidBody;
        IndexedVector3 force = new IndexedVector3(_force.X, _force.Y, _force.Z);
        body.SetTotalForce(ref force);
    }

    internal static void SetFriction2(object pBody, float _currentFriction)
    {
        RigidBody body = pBody as RigidBody;
        body.SetFriction(_currentFriction);
    }

    internal static void SetLinearVelocity2(object pBody, Vector3 _velocity)
    {
        RigidBody body = pBody as RigidBody;
        IndexedVector3 velocity = new IndexedVector3(_velocity.X, _velocity.Y, _velocity.Z);
        body.SetLinearVelocity(velocity);
    }

    internal static void Activate2(object pBody, bool pforceactivation)
    {
        RigidBody body = pBody as RigidBody;
        body.Activate(pforceactivation);
        
    }

    internal static Quaternion GetOrientation2(object pBody)
    {
        RigidBody body = pBody as RigidBody;
        IndexedQuaternion mat = body.GetInterpolationWorldTransform().GetRotation();
        return new Quaternion(mat.X, mat.Y, mat.Z, mat.W);
    }

    internal static CollisionFlags RemoveFromCollisionFlags2(object pBody, CollisionFlags pcollisionFlags)
    {
        RigidBody body = pBody as RigidBody;
        CollisionFlags existingcollisionFlags = (CollisionFlags)(uint)body.GetCollisionFlags();
        existingcollisionFlags &= ~pcollisionFlags;
        body.SetCollisionFlags((BulletXNA.BulletCollision.CollisionFlags)(uint)existingcollisionFlags);
        return (CollisionFlags)(uint)existingcollisionFlags;
    }

    internal static void SetGravity2(object pBody, Vector3 pGravity)
    {
        RigidBody body = pBody as RigidBody;
        IndexedVector3 gravity = new IndexedVector3(pGravity.X, pGravity.Y, pGravity.Z);
        body.SetGravity(gravity);
    }

    internal static bool DestroyConstraint2(object pBody, object pConstraint)
    {
        RigidBody body = pBody as RigidBody;
        TypedConstraint constraint = pConstraint as TypedConstraint;
        body.RemoveConstraintRef(constraint);
        return true;
    }

    internal static bool SetLinearLimits2(object pConstraint, Vector3 low, Vector3 high)
    {
        Generic6DofConstraint constraint = pConstraint as Generic6DofConstraint;
        IndexedVector3 lowlimit = new IndexedVector3(low.X, low.Y, low.Z);
        IndexedVector3 highlimit = new IndexedVector3(high.X, high.Y, high.Z);
        constraint.SetLinearLowerLimit(lowlimit);
        constraint.SetLinearUpperLimit(highlimit);
        return true;
    }

    internal static bool SetAngularLimits2(object pConstraint, Vector3 low, Vector3 high)
    {
        Generic6DofConstraint constraint = pConstraint as Generic6DofConstraint;
        IndexedVector3 lowlimit = new IndexedVector3(low.X, low.Y, low.Z);
        IndexedVector3 highlimit = new IndexedVector3(high.X, high.Y, high.Z);
        constraint.SetAngularLowerLimit(lowlimit);
        constraint.SetAngularUpperLimit(highlimit);
        return true;
    }

    internal static void SetConstraintNumSolverIterations2(object pConstraint, float cnt)
    {
        Generic6DofConstraint constraint = pConstraint as Generic6DofConstraint;
        constraint.SetOverrideNumSolverIterations((int)cnt);
    }

    internal static void CalculateTransforms2(object pConstraint)
    {
        Generic6DofConstraint constraint = pConstraint as Generic6DofConstraint;
        constraint.CalculateTransforms();
    }

    internal static void SetConstraintEnable2(object pConstraint, float p_2)
    {
        Generic6DofConstraint constraint = pConstraint as Generic6DofConstraint;
        constraint.SetEnabled((p_2 == 0) ? false : true);
    }


    //BulletSimAPI.Create6DofConstraint2(m_world.ptr, m_body1.ptr, m_body2.ptr,frame1, frame1rot,frame2, frame2rot,useLinearReferenceFrameA, disableCollisionsBetweenLinkedBodies));
    internal static object Create6DofConstraint2(object pWorld, object pBody1, object pBody2, Vector3 pframe1, Quaternion pframe1rot, Vector3 pframe2, Quaternion pframe2rot, bool puseLinearReferenceFrameA, bool pdisableCollisionsBetweenLinkedBodies)

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
    internal static object Create6DofConstraintToPoint2(object pWorld, object pBody1, object pBody2, Vector3 pjoinPoint, bool puseLinearReferenceFrameA, bool pdisableCollisionsBetweenLinkedBodies)
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
    internal static void SetFrames2(object pConstraint, Vector3 pframe1, Quaternion pframe1rot, Vector3 pframe2, Quaternion pframe2rot)
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


    

    internal static bool IsInWorld2(object pWorld, object pShapeObj)
    {
        DiscreteDynamicsWorld world = pWorld as DiscreteDynamicsWorld;
        CollisionObject shape = pShapeObj as CollisionObject;
        return world.IsInWorld(shape);
    }

    internal static void SetInterpolationLinearVelocity2(object pBody, Vector3 VehicleVelocity)
    {
        RigidBody body = pBody as RigidBody;
        IndexedVector3 velocity = new IndexedVector3(VehicleVelocity.X, VehicleVelocity.Y, VehicleVelocity.Z);
        body.SetInterpolationLinearVelocity(ref velocity);
    }

    internal static bool UseFrameOffset2(object pConstraint, float onOff)
    {
        Generic6DofConstraint constraint = pConstraint as Generic6DofConstraint;
        constraint.SetUseFrameOffset((onOff == 0) ? false : true);
        return true;
    }
    //SetBreakingImpulseThreshold2(m_constraint.ptr, threshold);
    internal static bool SetBreakingImpulseThreshold2(object pConstraint, float threshold)
    {
        Generic6DofConstraint constraint = pConstraint as Generic6DofConstraint;
        constraint.SetBreakingImpulseThreshold(threshold);
        return true;
    }
    //BulletSimAPI.SetAngularDamping2(Prim.PhysBody.ptr, angularDamping);
    internal static void SetAngularDamping2(object pBody, float angularDamping)
    {
        RigidBody body = pBody as RigidBody;
        float lineardamping = body.GetLinearDamping();
        body.SetDamping(lineardamping, angularDamping);

    }

    internal static void UpdateInertiaTensor2(object pBody)
    {
        RigidBody body = pBody as RigidBody;
        body.UpdateInertiaTensor();
    }

    internal static void RecalculateCompoundShapeLocalAabb2( object pCompoundShape)
    {

        CompoundShape shape = pCompoundShape as CompoundShape;
        shape.RecalculateLocalAabb();
    }

    //BulletSimAPI.GetCollisionFlags2(PhysBody.ptr)
    internal static CollisionFlags GetCollisionFlags2(object pBody)
    {
        RigidBody body = pBody as RigidBody;
        uint flags = (uint)body.GetCollisionFlags();
        return (CollisionFlags) flags;
    }

    internal static void SetDamping2(object pBody, float pLinear, float pAngular)
    {
        RigidBody body = pBody as RigidBody;
        body.SetDamping(pLinear, pAngular);
    }
    //PhysBody.ptr, PhysicsScene.Params.deactivationTime);
    internal static void SetDeactivationTime2(object pBody, float pDeactivationTime)
    {
        RigidBody body = pBody as RigidBody;
        body.SetDeactivationTime(pDeactivationTime);
    }
    //SetSleepingThresholds2(PhysBody.ptr, PhysicsScene.Params.linearSleepingThreshold, PhysicsScene.Params.angularSleepingThreshold);
    internal static void SetSleepingThresholds2(object pBody, float plinearSleepingThreshold, float pangularSleepingThreshold)
    {
        RigidBody body = pBody as RigidBody;
        body.SetSleepingThresholds(plinearSleepingThreshold, pangularSleepingThreshold);
    }

    internal static CollisionObjectTypes GetBodyType2(object pBody)
    {
        RigidBody body = pBody as RigidBody;
        return (CollisionObjectTypes)(int) body.GetInternalType();
    }

    //BulletSimAPI.ApplyCentralForce2(PhysBody.ptr, fSum);
    internal static void ApplyCentralForce2(object pBody, Vector3 pfSum)
    {
        RigidBody body = pBody as RigidBody;
        IndexedVector3 fSum = new IndexedVector3(pfSum.X, pfSum.Y, pfSum.Z);
        body.ApplyCentralForce(ref fSum);
    }
    internal static void ApplyCentralImpulse2(object pBody, Vector3 pfSum)
    {
        RigidBody body = pBody as RigidBody;
        IndexedVector3 fSum = new IndexedVector3(pfSum.X, pfSum.Y, pfSum.Z);
        body.ApplyCentralImpulse(ref fSum);
    }
    internal static void ApplyTorque2(object pBody, Vector3 pfSum)
    {
        RigidBody body = pBody as RigidBody;
        IndexedVector3 fSum = new IndexedVector3(pfSum.X, pfSum.Y, pfSum.Z);
        body.ApplyTorque(ref fSum);
    }
    internal static void ApplyTorqueImpulse2(object pBody, Vector3 pfSum)
    {
        RigidBody body = pBody as RigidBody;
        IndexedVector3 fSum = new IndexedVector3(pfSum.X, pfSum.Y, pfSum.Z);
        body.ApplyTorqueImpulse(ref fSum);
    }

    internal static void DumpRigidBody2(object p, object p_2)
    {
        //TODO:
    }

    internal static void DumpCollisionShape2(object p, object p_2)
    {
        //TODO:
    }

    internal static void DestroyObject2(object p, object p_2)
    {
        //TODO:
    }

    internal static void Shutdown2(object pWorld)
    {
        DiscreteDynamicsWorld world = pWorld as DiscreteDynamicsWorld;
        world.Cleanup();
    }

    internal static void DeleteCollisionShape2(object p, object p_2)
    {
        //TODO:
    }
    //(sim.ptr, shape.ptr, prim.LocalID, prim.RawPosition, prim.RawOrientation);
               
    internal static object CreateBodyFromShape2(object pWorld, object pShape, uint pLocalID, Vector3 pRawPosition, Quaternion pRawOrientation)
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

    
    internal static object CreateBodyWithDefaultMotionState2( object pShape, uint pLocalID, Vector3 pRawPosition, Quaternion pRawOrientation)
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
    internal static void SetCollisionFlags2(object pBody, CollisionFlags collisionFlags)
    {
        RigidBody body = pBody as RigidBody;
        body.SetCollisionFlags((BulletXNA.BulletCollision.CollisionFlags) (uint) collisionFlags);
    }
    //(m_mapInfo.terrainBody.ptr, PhysicsScene.Params.terrainHitFraction);
    internal static void SetHitFraction2(object pBody, float pHitFraction)
    {
        RigidBody body = pBody as RigidBody;
        body.SetHitFraction(pHitFraction);
    }
    //BuildCapsuleShape2(physicsScene.World.ptr, 1f, 1f, prim.Scale);
    internal static object BuildCapsuleShape2(object pWorld, float pRadius, float pHeight, Vector3 pScale)
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
    internal static bool SetConstraintParam2(object pConstraint, ConstraintParams paramIndex, float paramvalue, ConstraintParamAxis axis)
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

    internal static bool PushUpdate2(object pCollisionObject)
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

    internal static bool IsCompound2(object pShape)
    {
        CollisionShape shape = pShape as CollisionShape;
        return shape.IsCompound();
    }
    internal static bool IsPloyhedral2(object pShape)
    {
        CollisionShape shape = pShape as CollisionShape;
        return shape.IsPolyhedral();
    }
    internal static bool IsConvex2d2(object pShape)
    {
        CollisionShape shape = pShape as CollisionShape;
        return shape.IsConvex2d();
    }
    internal static bool IsConvex2(object pShape)
    {
        CollisionShape shape = pShape as CollisionShape;
        return shape.IsConvex();
    }
    internal static bool IsNonMoving2(object pShape)
    {
        CollisionShape shape = pShape as CollisionShape;
        return shape.IsNonMoving();
    }
    internal static bool IsConcave2(object pShape)
    {
        CollisionShape shape = pShape as CollisionShape;
        return shape.IsConcave();
    }
    internal static bool IsInfinite2(object pShape)
    {
        CollisionShape shape = pShape as CollisionShape;
        return shape.IsInfinite();
    }
    internal static bool IsNativeShape2(object pShape)
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
    internal static object CreateGhostFromShape2(object pWorld, object pShape, uint pLocalID, Vector3 pRawPosition, Quaternion pRawOrientation)
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
    internal static object BuildNativeShape2(object pWorld, ShapeData pShapeData)
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
    internal static object CreateCompoundShape2(object pWorld, bool enableDynamicAabbTree)
    {
        return new CompoundShape(enableDynamicAabbTree);
    }

    internal static int GetNumberOfCompoundChildren2(object pCompoundShape)
    {
        var compoundshape = pCompoundShape as CompoundShape;
        return compoundshape.GetNumChildShapes();
    }
    //LinksetRoot.PhysShape.ptr, newShape.ptr, displacementPos, displacementRot
    internal static void AddChildShapeToCompoundShape2(object pCShape, object paddShape, Vector3 displacementPos, Quaternion displacementRot)
    {
        IndexedMatrix relativeTransform = new IndexedMatrix();
        var compoundshape = pCShape as CompoundShape;
        var addshape = paddShape as CollisionShape;

        relativeTransform._origin = new IndexedVector3(displacementPos.X, displacementPos.Y, displacementPos.Z);
        relativeTransform.SetRotation(new IndexedQuaternion(displacementRot.X,displacementRot.Y,displacementRot.Z,displacementRot.W));
        compoundshape.AddChildShape(ref relativeTransform, addshape);

    }

    internal static object RemoveChildShapeFromCompoundShapeIndex2(object pCShape, int pii)
    {
        var compoundshape = pCShape as CompoundShape;
        CollisionShape ret = null;
        ret = compoundshape.GetChildShape(pii);
        compoundshape.RemoveChildShapeByIndex(pii);
        return ret;
    }

    internal static object CreateGroundPlaneShape2(uint pLocalId, float pheight, float pcollisionMargin)
    {
        StaticPlaneShape m_planeshape = new StaticPlaneShape(new IndexedVector3(0,0,1),(int)pheight );
        m_planeshape.SetMargin(pcollisionMargin);
        m_planeshape.SetUserPointer(pLocalId);
        return m_planeshape;
    }

    internal static object CreateHingeConstraint2(object pWorld, object pBody1, object ppBody2, Vector3 ppivotInA, Vector3 ppivotInB, Vector3 paxisInA, Vector3 paxisInB, bool puseLinearReferenceFrameA, bool pdisableCollisionsBetweenLinkedBodies)
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

    internal static bool ReleaseHeightMapInfo2(object pMapInfo)
    {
        if (pMapInfo != null)
        {
            BulletHeightMapInfo mapinfo = pMapInfo as BulletHeightMapInfo;
            if (mapinfo.heightMap != null)
                mapinfo.heightMap = null;


        }
        return true;
    }

    internal static object CreateHullShape2(object pWorld, int pHullCount, float[] pConvHulls)
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

    internal static object CreateMeshShape2(object pWorld, int pIndicesCount, int[] indices, int pVerticesCount, float[] verticesAsFloats)
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
    internal static object CreateHeightMapInfo2(object pWorld, uint pId, Vector3 pminCoords, Vector3 pmaxCoords, float[] pheightMap, float pCollisionMargin)
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

    internal static object CreateTerrainShape2(object pMapInfo)
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

    internal static bool TranslationalLimitMotor2(object pConstraint, float ponOff, float targetVelocity, float maxMotorForce)
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

    internal static int PhysicsStep2(object pWorld, float timeStep, int m_maxSubSteps, float m_fixedTimeStep, out int updatedEntityCount, out List<BulletXNA.EntityProperties> updatedEntities, out int collidersCount, out List<BulletXNA.CollisionDesc>colliders)
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


    internal static Vector3 GetLocalScaling2(object pBody)
    {
        CollisionShape shape = pBody as CollisionShape;
        IndexedVector3 scale = shape.GetLocalScaling();
        return new Vector3(scale.X,scale.Y,scale.Z);
    }

    internal static bool RayCastGround(object pWorld, Vector3 _RayOrigin, float pRayHeight, object NotMe)
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
}
