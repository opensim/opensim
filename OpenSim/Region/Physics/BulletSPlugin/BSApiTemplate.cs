/*
 * Copyright (c) Contributors, http://opensimulator.org/
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
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
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using OpenMetaverse;

namespace OpenSim.Region.Physics.BulletSPlugin {

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
    SHAPE_AVATAR    = 24,
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
    KEY_AVATAR      = 6,
}

[StructLayout(LayoutKind.Sequential)]
public struct ShapeData
{
    public UInt32 ID;
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
    public UInt32 ID;
    public float Fraction;
    public Vector3 Normal;
    public Vector3 Point;
}
[StructLayout(LayoutKind.Sequential)]
public struct RaycastHit
{
    public UInt32 ID;
    public float Fraction;
    public Vector3 Normal;
}
[StructLayout(LayoutKind.Sequential)]
public struct CollisionDesc
{
    public UInt32 aID;
    public UInt32 bID;
    public Vector3 point;
    public Vector3 normal;
    public float penetration;
}
[StructLayout(LayoutKind.Sequential)]
public struct EntityProperties
{
    public UInt32 ID;
    public Vector3 Position;
    public Quaternion Rotation;
    public Vector3 Velocity;
    public Vector3 Acceleration;
    public Vector3 RotationalVelocity;

    public override string ToString()
    {
        StringBuilder buff = new StringBuilder();
        buff.Append("<i=");
        buff.Append(ID.ToString());
        buff.Append(",p=");
        buff.Append(Position.ToString());
        buff.Append(",r=");
        buff.Append(Rotation.ToString());
        buff.Append(",v=");
        buff.Append(Velocity.ToString());
        buff.Append(",a=");
        buff.Append(Acceleration.ToString());
        buff.Append(",rv=");
        buff.Append(RotationalVelocity.ToString());
        buff.Append(">");
        return buff.ToString();
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

	public float maxPersistantManifoldPoolSize;
	public float maxCollisionAlgorithmPoolSize;
	public float shouldDisableContactPoolDynamicAllocation;
	public float shouldForceUpdateAllAabbs;
	public float shouldRandomizeSolverOrder;
	public float shouldSplitSimulationIslands;
	public float shouldEnableFrictionCaching;
	public float numberOfSolverIterations;
    public float useSingleSidedMeshes;

    public float physicsLoggingFrames;

    public const float numericTrue = 1f;
    public const float numericFalse = 0f;
}


// The states a bullet collision object can have
public enum ActivationState : uint
{
    ACTIVE_TAG = 1,
    ISLAND_SLEEPING,
    WANTS_DEACTIVATION,
    DISABLE_DEACTIVATION,
    DISABLE_SIMULATION,
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
    BS_ALL                           = 0xFFFFFFFF
};

// Values f collisions groups and masks
public enum CollisionFilterGroups : uint
{
    // Don't use the bit definitions!!  Define the use in a
    //   filter/mask definition below. This way collision interactions
    //   are more easily found and debugged.
    BNoneGroup              = 0,
    BDefaultGroup           = 1 << 0,   // 0001
    BStaticGroup            = 1 << 1,   // 0002
    BKinematicGroup         = 1 << 2,   // 0004
    BDebrisGroup            = 1 << 3,   // 0008
    BSensorTrigger          = 1 << 4,   // 0010
    BCharacterGroup         = 1 << 5,   // 0020
    BAllGroup               = 0x000FFFFF,
    // Filter groups defined by BulletSim
    BGroundPlaneGroup       = 1 << 10,  // 0400
    BTerrainGroup           = 1 << 11,  // 0800
    BRaycastGroup           = 1 << 12,  // 1000
    BSolidGroup             = 1 << 13,  // 2000
    // BLinksetGroup        = xx  // a linkset proper is either static or dynamic
    BLinksetChildGroup      = 1 << 14,  // 4000
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

public abstract class BSAPITemplate
{
// Returns the name of the underlying Bullet engine
public abstract string BulletEngineName { get; }
public abstract string BulletEngineVersion { get; protected set;} 

// Initialization and simulation
public abstract BulletWorld Initialize(Vector3 maxPosition, ConfigurationParameters parms,
											int maxCollisions,  ref CollisionDesc[] collisionArray,
											int maxUpdates, ref EntityProperties[] updateArray
                                            );

public abstract int PhysicsStep(BulletWorld world, float timeStep, int maxSubSteps, float fixedTimeStep,
                        out int updatedEntityCount, out int collidersCount);

public abstract bool UpdateParameter(BulletWorld world, UInt32 localID, String parm, float value);

public abstract void Shutdown(BulletWorld sim);

public abstract bool PushUpdate(BulletBody obj);

// =====================================================================================
// Mesh, hull, shape and body creation helper routines
public abstract BulletShape CreateMeshShape(BulletWorld world,
                int indicesCount, int[] indices,
                int verticesCount, float[] vertices );

public abstract BulletShape CreateHullShape(BulletWorld world,
                int hullCount, float[] hulls);

public abstract BulletShape BuildHullShapeFromMesh(BulletWorld world, BulletShape meshShape);

public abstract BulletShape BuildNativeShape(BulletWorld world, ShapeData shapeData);

public abstract bool IsNativeShape(BulletShape shape);

public abstract void SetShapeCollisionMargin(BulletShape shape, float margin);

public abstract BulletShape BuildCapsuleShape(BulletWorld world, float radius, float height, Vector3 scale);

public abstract BulletShape CreateCompoundShape(BulletWorld sim, bool enableDynamicAabbTree);

public abstract int GetNumberOfCompoundChildren(BulletShape cShape);

public abstract void AddChildShapeToCompoundShape(BulletShape cShape, BulletShape addShape, Vector3 pos, Quaternion rot);

public abstract BulletShape GetChildShapeFromCompoundShapeIndex(BulletShape cShape, int indx);

public abstract BulletShape RemoveChildShapeFromCompoundShapeIndex(BulletShape cShape, int indx);

public abstract void RemoveChildShapeFromCompoundShape(BulletShape cShape, BulletShape removeShape);

public abstract void UpdateChildTransform(BulletShape pShape, int childIndex, Vector3 pos, Quaternion rot, bool shouldRecalculateLocalAabb);

public abstract void RecalculateCompoundShapeLocalAabb(BulletShape cShape);

public abstract BulletShape DuplicateCollisionShape(BulletWorld sim, BulletShape srcShape, UInt32 id);

public abstract bool DeleteCollisionShape(BulletWorld world, BulletShape shape);

public abstract CollisionObjectTypes GetBodyType(BulletBody obj);

public abstract BulletBody CreateBodyFromShape(BulletWorld sim, BulletShape shape, UInt32 id, Vector3 pos, Quaternion rot);

public abstract BulletBody CreateBodyWithDefaultMotionState(BulletShape shape, UInt32 id, Vector3 pos, Quaternion rot);

public abstract BulletBody CreateGhostFromShape(BulletWorld sim, BulletShape shape, UInt32 id, Vector3 pos, Quaternion rot);

public abstract void DestroyObject(BulletWorld sim, BulletBody obj);

// =====================================================================================
public abstract BulletShape CreateGroundPlaneShape(UInt32 id, float height, float collisionMargin);

public abstract BulletShape CreateTerrainShape(UInt32 id, Vector3 size, float minHeight, float maxHeight, float[] heightMap, 
								float scaleFactor, float collisionMargin);

// =====================================================================================
// Constraint creation and helper routines
public abstract BulletConstraint Create6DofConstraint(BulletWorld world, BulletBody obj1, BulletBody obj2,
                    Vector3 frame1loc, Quaternion frame1rot,
                    Vector3 frame2loc, Quaternion frame2rot,
                    bool useLinearReferenceFrameA, bool disableCollisionsBetweenLinkedBodies);

public abstract BulletConstraint Create6DofConstraintToPoint(BulletWorld world, BulletBody obj1, BulletBody obj2,
                    Vector3 joinPoint,
                    bool useLinearReferenceFrameA, bool disableCollisionsBetweenLinkedBodies);

public abstract BulletConstraint CreateHingeConstraint(BulletWorld world, BulletBody obj1, BulletBody obj2,
                    Vector3 pivotinA, Vector3 pivotinB,
                    Vector3 axisInA, Vector3 axisInB,
                    bool useLinearReferenceFrameA, bool disableCollisionsBetweenLinkedBodies);

public abstract void SetConstraintEnable(BulletConstraint constrain, float numericTrueFalse);

public abstract void SetConstraintNumSolverIterations(BulletConstraint constrain, float iterations);

public abstract bool SetFrames(BulletConstraint constrain,
                Vector3 frameA, Quaternion frameArot, Vector3 frameB, Quaternion frameBrot);

public abstract bool SetLinearLimits(BulletConstraint constrain, Vector3 low, Vector3 hi);

public abstract bool SetAngularLimits(BulletConstraint constrain, Vector3 low, Vector3 hi);

public abstract bool UseFrameOffset(BulletConstraint constrain, float enable);

public abstract bool TranslationalLimitMotor(BulletConstraint constrain, float enable, float targetVel, float maxMotorForce);

public abstract bool SetBreakingImpulseThreshold(BulletConstraint constrain, float threshold);

public abstract bool CalculateTransforms(BulletConstraint constrain);

public abstract bool SetConstraintParam(BulletConstraint constrain, ConstraintParams paramIndex, float value, ConstraintParamAxis axis);

public abstract bool DestroyConstraint(BulletWorld world, BulletConstraint constrain);

// =====================================================================================
// btCollisionWorld entries
public abstract void UpdateSingleAabb(BulletWorld world, BulletBody obj);

public abstract void UpdateAabbs(BulletWorld world);

public abstract bool GetForceUpdateAllAabbs(BulletWorld world);

public abstract void SetForceUpdateAllAabbs(BulletWorld world, bool force);

// =====================================================================================
// btDynamicsWorld entries
// public abstract bool AddObjectToWorld(BulletWorld world, BulletBody obj, Vector3 pos, Quaternion rot);
public abstract bool AddObjectToWorld(BulletWorld world, BulletBody obj);

public abstract bool RemoveObjectFromWorld(BulletWorld world, BulletBody obj);

public abstract bool AddConstraintToWorld(BulletWorld world, BulletConstraint constrain, bool disableCollisionsBetweenLinkedObjects);

public abstract bool RemoveConstraintFromWorld(BulletWorld world, BulletConstraint constrain);
// =====================================================================================
// btCollisionObject entries
public abstract Vector3 GetAnisotripicFriction(BulletConstraint constrain);

public abstract Vector3 SetAnisotripicFriction(BulletConstraint constrain, Vector3 frict);

public abstract bool HasAnisotripicFriction(BulletConstraint constrain);

public abstract void SetContactProcessingThreshold(BulletBody obj, float val);

public abstract float GetContactProcessingThreshold(BulletBody obj);

public abstract bool IsStaticObject(BulletBody obj);

public abstract bool IsKinematicObject(BulletBody obj);

public abstract bool IsStaticOrKinematicObject(BulletBody obj);

public abstract bool HasContactResponse(BulletBody obj);

public abstract void SetCollisionShape(BulletWorld sim, BulletBody obj, BulletShape shape);

public abstract BulletShape GetCollisionShape(BulletBody obj);

public abstract int GetActivationState(BulletBody obj);

public abstract void SetActivationState(BulletBody obj, int state);

public abstract void SetDeactivationTime(BulletBody obj, float dtime);

public abstract float GetDeactivationTime(BulletBody obj);

public abstract void ForceActivationState(BulletBody obj, ActivationState state);

public abstract void Activate(BulletBody obj, bool forceActivation);

public abstract bool IsActive(BulletBody obj);

public abstract void SetRestitution(BulletBody obj, float val);

public abstract float GetRestitution(BulletBody obj);

public abstract void SetFriction(BulletBody obj, float val);

public abstract float GetFriction(BulletBody obj);

public abstract Vector3 GetPosition(BulletBody obj);

public abstract Quaternion GetOrientation(BulletBody obj);

public abstract void SetTranslation(BulletBody obj, Vector3 position, Quaternion rotation);

// public abstract IntPtr GetBroadphaseHandle(BulletBody obj);

// public abstract void SetBroadphaseHandle(BulletBody obj, IntPtr handle);

public abstract void SetInterpolationLinearVelocity(BulletBody obj, Vector3 vel);

public abstract void SetInterpolationAngularVelocity(BulletBody obj, Vector3 vel);

public abstract void SetInterpolationVelocity(BulletBody obj, Vector3 linearVel, Vector3 angularVel);

public abstract float GetHitFraction(BulletBody obj);

public abstract void SetHitFraction(BulletBody obj, float val);

public abstract CollisionFlags GetCollisionFlags(BulletBody obj);

public abstract CollisionFlags SetCollisionFlags(BulletBody obj, CollisionFlags flags);

public abstract CollisionFlags AddToCollisionFlags(BulletBody obj, CollisionFlags flags);

public abstract CollisionFlags RemoveFromCollisionFlags(BulletBody obj, CollisionFlags flags);

public abstract float GetCcdMotionThreshold(BulletBody obj);

public abstract void SetCcdMotionThreshold(BulletBody obj, float val);

public abstract float GetCcdSweptSphereRadius(BulletBody obj);

public abstract void SetCcdSweptSphereRadius(BulletBody obj, float val);

public abstract IntPtr GetUserPointer(BulletBody obj);

public abstract void SetUserPointer(BulletBody obj, IntPtr val);

// =====================================================================================
// btRigidBody entries
public abstract void ApplyGravity(BulletBody obj);

public abstract void SetGravity(BulletBody obj, Vector3 val);

public abstract Vector3 GetGravity(BulletBody obj);

public abstract void SetDamping(BulletBody obj, float lin_damping, float ang_damping);

public abstract void SetLinearDamping(BulletBody obj, float lin_damping);

public abstract void SetAngularDamping(BulletBody obj, float ang_damping);

public abstract float GetLinearDamping(BulletBody obj);

public abstract float GetAngularDamping(BulletBody obj);

public abstract float GetLinearSleepingThreshold(BulletBody obj);

public abstract void ApplyDamping(BulletBody obj, float timeStep);

public abstract void SetMassProps(BulletBody obj, float mass, Vector3 inertia);

public abstract Vector3 GetLinearFactor(BulletBody obj);

public abstract void SetLinearFactor(BulletBody obj, Vector3 factor);

public abstract void SetCenterOfMassByPosRot(BulletBody obj, Vector3 pos, Quaternion rot);

// Add a force to the object as if its mass is one.
public abstract void ApplyCentralForce(BulletBody obj, Vector3 force);

// Set the force being applied to the object as if its mass is one.
public abstract void SetObjectForce(BulletBody obj, Vector3 force);

public abstract Vector3 GetTotalForce(BulletBody obj);

public abstract Vector3 GetTotalTorque(BulletBody obj);

public abstract Vector3 GetInvInertiaDiagLocal(BulletBody obj);

public abstract void SetInvInertiaDiagLocal(BulletBody obj, Vector3 inert);

public abstract void SetSleepingThresholds(BulletBody obj, float lin_threshold, float ang_threshold);

public abstract void ApplyTorque(BulletBody obj, Vector3 torque);

// Apply force at the given point. Will add torque to the object.
public abstract void ApplyForce(BulletBody obj, Vector3 force, Vector3 pos);

// Apply impulse to the object. Same as "ApplycentralForce" but force scaled by object's mass.
public abstract void ApplyCentralImpulse(BulletBody obj, Vector3 imp);

// Apply impulse to the object's torque. Force is scaled by object's mass.
public abstract void ApplyTorqueImpulse(BulletBody obj, Vector3 imp);

// Apply impulse at the point given. For is scaled by object's mass and effects both linear and angular forces.
public abstract void ApplyImpulse(BulletBody obj, Vector3 imp, Vector3 pos);

public abstract void ClearForces(BulletBody obj);

public abstract void ClearAllForces(BulletBody obj);

public abstract void UpdateInertiaTensor(BulletBody obj);

public abstract Vector3 GetLinearVelocity(BulletBody obj);

public abstract Vector3 GetAngularVelocity(BulletBody obj);

public abstract void SetLinearVelocity(BulletBody obj, Vector3 val);

public abstract void SetAngularVelocity(BulletBody obj, Vector3 angularVelocity);

public abstract Vector3 GetVelocityInLocalPoint(BulletBody obj, Vector3 pos);

public abstract void Translate(BulletBody obj, Vector3 trans);

public abstract void UpdateDeactivation(BulletBody obj, float timeStep);

public abstract bool WantsSleeping(BulletBody obj);

public abstract void SetAngularFactor(BulletBody obj, float factor);

public abstract void SetAngularFactorV(BulletBody obj, Vector3 factor);

public abstract Vector3 GetAngularFactor(BulletBody obj);

public abstract bool IsInWorld(BulletWorld world, BulletBody obj);

public abstract void AddConstraintRef(BulletBody obj, BulletConstraint constrain);

public abstract void RemoveConstraintRef(BulletBody obj, BulletConstraint constrain);

public abstract BulletConstraint GetConstraintRef(BulletBody obj, int index);

public abstract int GetNumConstraintRefs(BulletBody obj);

public abstract bool SetCollisionGroupMask(BulletBody body, UInt32 filter, UInt32 mask);

// =====================================================================================
// btCollisionShape entries

public abstract float GetAngularMotionDisc(BulletShape shape);

public abstract float GetContactBreakingThreshold(BulletShape shape, float defaultFactor);

public abstract bool IsPolyhedral(BulletShape shape);

public abstract bool IsConvex2d(BulletShape shape);

public abstract bool IsConvex(BulletShape shape);

public abstract bool IsNonMoving(BulletShape shape);

public abstract bool IsConcave(BulletShape shape);

public abstract bool IsCompound(BulletShape shape);

public abstract bool IsSoftBody(BulletShape shape);

public abstract bool IsInfinite(BulletShape shape);

public abstract void SetLocalScaling(BulletShape shape, Vector3 scale);

public abstract Vector3 GetLocalScaling(BulletShape shape);

public abstract Vector3 CalculateLocalInertia(BulletShape shape, float mass);

public abstract int GetShapeType(BulletShape shape);

public abstract void SetMargin(BulletShape shape, float val);

public abstract float GetMargin(BulletShape shape);

// =====================================================================================
// Debugging
public virtual void DumpRigidBody(BulletWorld sim, BulletBody collisionObject) { }

public virtual void DumpCollisionShape(BulletWorld sim, BulletShape collisionShape) { }

public virtual void DumpConstraint(BulletWorld sim, BulletConstraint constrain) { }

public virtual void DumpActivationInfo(BulletWorld sim) { }

public virtual void DumpAllInfo(BulletWorld sim) { }

public virtual void DumpPhysicsStatistics(BulletWorld sim) { }

public virtual void ResetBroadphasePool(BulletWorld sim) { }

public virtual void ResetConstraintSolver(BulletWorld sim) { }

};
}
