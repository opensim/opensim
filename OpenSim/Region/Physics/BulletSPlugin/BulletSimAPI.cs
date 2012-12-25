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

public abstract class BulletSimAPITemplate
{
// Initialization and simulation
public abstract BulletWorld Initialize2(Vector3 maxPosition, IntPtr parms,
											int maxCollisions,  IntPtr collisionArray,
											int maxUpdates, IntPtr updateArray
                                            );

public abstract bool UpdateParameter2(BulletWorld world, uint localID, String parm, float value);

public abstract void SetHeightMap2(BulletWorld world, float[] heightmap);

public abstract void Shutdown2(BulletWorld sim);

public abstract int PhysicsStep2(BulletWorld world, float timeStep, int maxSubSteps, float fixedTimeStep,
                        out int updatedEntityCount,
                        out IntPtr updatedEntitiesPtr,
                        out int collidersCount,
                        out IntPtr collidersPtr);

public abstract bool PushUpdate2(BulletBody obj);

// =====================================================================================
// Mesh, hull, shape and body creation helper routines
public abstract BulletShape CreateMeshShape2(BulletWorld world,
                int indicesCount, [MarshalAs(UnmanagedType.LPArray)] int[] indices,
                int verticesCount, [MarshalAs(UnmanagedType.LPArray)] float[] vertices );

public abstract BulletShape CreateHullShape2(BulletWorld world,
                int hullCount, [MarshalAs(UnmanagedType.LPArray)] float[] hulls);

public abstract BulletShape BuildHullShapeFromMesh2(BulletWorld world, BulletShape meshShape);

public abstract BulletShape BuildNativeShape2(BulletWorld world, ShapeData shapeData);

public abstract bool IsNativeShape2(BulletShape shape);

public abstract void SetShapeCollisionMargin(BulletShape shape, float margin);

public abstract BulletShape BuildCapsuleShape2(BulletWorld world, float radius, float height, Vector3 scale);

public abstract BulletShape CreateCompoundShape2(BulletWorld sim, bool enableDynamicAabbTree);

public abstract int GetNumberOfCompoundChildren2(BulletShape cShape);

public abstract void AddChildShapeToCompoundShape2(BulletShape cShape, BulletShape addShape, Vector3 pos, Quaternion rot);

public abstract BulletShape GetChildShapeFromCompoundShapeIndex2(BulletShape cShape, int indx);

public abstract BulletShape RemoveChildShapeFromCompoundShapeIndex2(BulletShape cShape, int indx);

public abstract void RemoveChildShapeFromCompoundShape2(BulletShape cShape, BulletShape removeShape);

public abstract void RecalculateCompoundShapeLocalAabb2(BulletShape cShape);

public abstract BulletShape DuplicateCollisionShape2(BulletWorld sim, BulletShape srcShape, uint id);

public abstract BulletBody CreateBodyFromShapeAndInfo2(BulletWorld sim, BulletShape shape, uint id, IntPtr constructionInfo);

public abstract bool DeleteCollisionShape2(BulletWorld world, BulletShape shape);

public abstract int GetBodyType2(BulletBody obj);

public abstract BulletBody CreateBodyFromShape2(BulletWorld sim, BulletShape shape, uint id, Vector3 pos, Quaternion rot);

public abstract BulletBody CreateBodyWithDefaultMotionState2(BulletShape shape, uint id, Vector3 pos, Quaternion rot);

public abstract BulletBody CreateGhostFromShape2(BulletWorld sim, BulletShape shape, uint id, Vector3 pos, Quaternion rot);

public abstract IntPtr AllocateBodyInfo2(BulletBody obj);

public abstract void ReleaseBodyInfo2(IntPtr obj);

public abstract void DestroyObject2(BulletWorld sim, BulletBody obj);

// =====================================================================================
// Terrain creation and helper routines
public abstract IntPtr CreateHeightMapInfo2(BulletWorld sim, uint id, Vector3 minCoords, Vector3 maxCoords,
        [MarshalAs(UnmanagedType.LPArray)] float[] heightMap, float collisionMargin);

public abstract IntPtr FillHeightMapInfo2(BulletWorld sim, IntPtr mapInfo, uint id, Vector3 minCoords, Vector3 maxCoords,
        [MarshalAs(UnmanagedType.LPArray)] float[] heightMap, float collisionMargin);

public abstract bool ReleaseHeightMapInfo2(IntPtr heightMapInfo);

public abstract BulletBody CreateGroundPlaneShape2(uint id, float height, float collisionMargin);

public abstract BulletBody CreateTerrainShape2(IntPtr mapInfo);

// =====================================================================================
// Constraint creation and helper routines
public abstract BulletConstraint Create6DofConstraint2(BulletWorld world, BulletBody obj1, BulletBody obj2,
                    Vector3 frame1loc, Quaternion frame1rot,
                    Vector3 frame2loc, Quaternion frame2rot,
                    bool useLinearReferenceFrameA, bool disableCollisionsBetweenLinkedBodies);

public abstract BulletConstraint Create6DofConstraintToPoint2(BulletWorld world, BulletBody obj1, BulletBody obj2,
                    Vector3 joinPoint,
                    bool useLinearReferenceFrameA, bool disableCollisionsBetweenLinkedBodies);

public abstract BulletConstraint CreateHingeConstraint2(BulletWorld world, BulletBody obj1, BulletBody obj2,
                    Vector3 pivotinA, Vector3 pivotinB,
                    Vector3 axisInA, Vector3 axisInB,
                    bool useLinearReferenceFrameA, bool disableCollisionsBetweenLinkedBodies);

public abstract void SetConstraintEnable2(BulletConstraint constrain, float numericTrueFalse);

public abstract void SetConstraintNumSolverIterations2(BulletConstraint constrain, float iterations);

public abstract bool SetFrames2(BulletConstraint constrain,
                Vector3 frameA, Quaternion frameArot, Vector3 frameB, Quaternion frameBrot);

public abstract bool SetLinearLimits2(BulletConstraint constrain, Vector3 low, Vector3 hi);

public abstract bool SetAngularLimits2(BulletConstraint constrain, Vector3 low, Vector3 hi);

public abstract bool UseFrameOffset2(BulletConstraint constrain, float enable);

public abstract bool TranslationalLimitMotor2(BulletConstraint constrain, float enable, float targetVel, float maxMotorForce);

public abstract bool SetBreakingImpulseThreshold2(BulletConstraint constrain, float threshold);

public abstract bool CalculateTransforms2(BulletConstraint constrain);

public abstract bool SetConstraintParam2(BulletConstraint constrain, ConstraintParams paramIndex, float value, ConstraintParamAxis axis);

public abstract bool DestroyConstraint2(BulletWorld world, BulletConstraint constrain);

// =====================================================================================
// btCollisionWorld entries
public abstract void UpdateSingleAabb2(BulletWorld world, BulletBody obj);

public abstract void UpdateAabbs2(BulletWorld world);

public abstract bool GetForceUpdateAllAabbs2(BulletWorld world);

public abstract void SetForceUpdateAllAabbs2(BulletWorld world, bool force);

// =====================================================================================
// btDynamicsWorld entries
public abstract bool AddObjectToWorld2(BulletWorld world, BulletBody obj);

public abstract bool RemoveObjectFromWorld2(BulletWorld world, BulletBody obj);

public abstract bool AddConstraintToWorld2(BulletWorld world, BulletConstraint constrain, bool disableCollisionsBetweenLinkedObjects);

public abstract bool RemoveConstraintFromWorld2(BulletWorld world, BulletConstraint constrain);
// =====================================================================================
// btCollisionObject entries
public abstract Vector3 GetAnisotripicFriction2(BulletConstraint constrain);

public abstract Vector3 SetAnisotripicFriction2(BulletConstraint constrain, Vector3 frict);

public abstract bool HasAnisotripicFriction2(BulletConstraint constrain);

public abstract void SetContactProcessingThreshold2(BulletBody obj, float val);

public abstract float GetContactProcessingThreshold2(BulletBody obj);

public abstract bool IsStaticObject2(BulletBody obj);

public abstract bool IsKinematicObject2(BulletBody obj);

public abstract bool IsStaticOrKinematicObject2(BulletBody obj);

public abstract bool HasContactResponse2(BulletBody obj);

public abstract void SetCollisionShape2(BulletWorld sim, BulletBody obj, BulletBody shape);

public abstract BulletShape GetCollisionShape2(BulletBody obj);

public abstract int GetActivationState2(BulletBody obj);

public abstract void SetActivationState2(BulletBody obj, int state);

public abstract void SetDeactivationTime2(BulletBody obj, float dtime);

public abstract float GetDeactivationTime2(BulletBody obj);

public abstract void ForceActivationState2(BulletBody obj, ActivationState state);

public abstract void Activate2(BulletBody obj, bool forceActivation);

public abstract bool IsActive2(BulletBody obj);

public abstract void SetRestitution2(BulletBody obj, float val);

public abstract float GetRestitution2(BulletBody obj);

public abstract void SetFriction2(BulletBody obj, float val);

public abstract float GetFriction2(BulletBody obj);

    /* Haven't defined the type 'Transform'
public abstract Transform GetWorldTransform2(BulletBody obj);

public abstract void setWorldTransform2(BulletBody obj, Transform trans);
     */

public abstract Vector3 GetPosition2(BulletBody obj);

public abstract Quaternion GetOrientation2(BulletBody obj);

public abstract void SetTranslation2(BulletBody obj, Vector3 position, Quaternion rotation);

public abstract IntPtr GetBroadphaseHandle2(BulletBody obj);

public abstract void SetBroadphaseHandle2(BulletBody obj, IntPtr handle);

    /*
public abstract Transform GetInterpolationWorldTransform2(IntPtr obj);

public abstract void SetInterpolationWorldTransform2(IntPtr obj, Transform trans);
     */

public abstract void SetInterpolationLinearVelocity2(BulletBody obj, Vector3 vel);

public abstract void SetInterpolationAngularVelocity2(BulletBody obj, Vector3 vel);

public abstract void SetInterpolationVelocity2(BulletBody obj, Vector3 linearVel, Vector3 angularVel);

public abstract float GetHitFraction2(BulletBody obj);

public abstract void SetHitFraction2(BulletBody obj, float val);

public abstract CollisionFlags GetCollisionFlags2(BulletBody obj);

public abstract CollisionFlags SetCollisionFlags2(BulletBody obj, CollisionFlags flags);

public abstract CollisionFlags AddToCollisionFlags2(BulletBody obj, CollisionFlags flags);

public abstract CollisionFlags RemoveFromCollisionFlags2(BulletBody obj, CollisionFlags flags);

public abstract float GetCcdMotionThreshold2(BulletBody obj);

public abstract void SetCcdMotionThreshold2(BulletBody obj, float val);

public abstract float GetCcdSweptSphereRadius2(BulletBody obj);

public abstract void SetCcdSweptSphereRadius2(BulletBody obj, float val);

public abstract IntPtr GetUserPointer2(BulletBody obj);

public abstract void SetUserPointer2(BulletBody obj, IntPtr val);

// =====================================================================================
// btRigidBody entries
public abstract void ApplyGravity2(BulletBody obj);

public abstract void SetGravity2(BulletBody obj, Vector3 val);

public abstract Vector3 GetGravity2(BulletBody obj);

public abstract void SetDamping2(BulletBody obj, float lin_damping, float ang_damping);

public abstract void SetLinearDamping2(BulletBody obj, float lin_damping);

public abstract void SetAngularDamping2(BulletBody obj, float ang_damping);

public abstract float GetLinearDamping2(BulletBody obj);

public abstract float GetAngularDamping2(BulletBody obj);

public abstract float GetLinearSleepingThreshold2(BulletBody obj);


public abstract void ApplyDamping2(BulletBody obj, float timeStep);

public abstract void SetMassProps2(BulletBody obj, float mass, Vector3 inertia);

public abstract Vector3 GetLinearFactor2(BulletBody obj);

public abstract void SetLinearFactor2(BulletBody obj, Vector3 factor);

    /*
public abstract void SetCenterOfMassTransform2(BulletBody obj, Transform trans);
     */

public abstract void SetCenterOfMassByPosRot2(BulletBody obj, Vector3 pos, Quaternion rot);

// Add a force to the object as if its mass is one.
public abstract void ApplyCentralForce2(BulletBody obj, Vector3 force);

// Set the force being applied to the object as if its mass is one.
public abstract void SetObjectForce2(BulletBody obj, Vector3 force);

public abstract Vector3 GetTotalForce2(BulletBody obj);

public abstract Vector3 GetTotalTorque2(BulletBody obj);

public abstract Vector3 GetInvInertiaDiagLocal2(BulletBody obj);

public abstract void SetInvInertiaDiagLocal2(BulletBody obj, Vector3 inert);

public abstract void SetSleepingThresholds2(BulletBody obj, float lin_threshold, float ang_threshold);

public abstract void ApplyTorque2(BulletBody obj, Vector3 torque);

// Apply force at the given point. Will add torque to the object.
public abstract void ApplyForce2(BulletBody obj, Vector3 force, Vector3 pos);

// Apply impulse to the object. Same as "ApplycentralForce" but force scaled by object's mass.
public abstract void ApplyCentralImpulse2(BulletBody obj, Vector3 imp);

// Apply impulse to the object's torque. Force is scaled by object's mass.
public abstract void ApplyTorqueImpulse2(BulletBody obj, Vector3 imp);

// Apply impulse at the point given. For is scaled by object's mass and effects both linear and angular forces.
public abstract void ApplyImpulse2(BulletBody obj, Vector3 imp, Vector3 pos);

public abstract void ClearForces2(BulletBody obj);

public abstract void ClearAllForces2(BulletBody obj);

public abstract void UpdateInertiaTensor2(BulletBody obj);


    /*
public abstract Transform GetCenterOfMassTransform2(BulletBody obj);
     */

public abstract Vector3 GetLinearVelocity2(BulletBody obj);

public abstract Vector3 GetAngularVelocity2(BulletBody obj);

public abstract void SetLinearVelocity2(BulletBody obj, Vector3 val);

public abstract void SetAngularVelocity2(BulletBody obj, Vector3 angularVelocity);

public abstract Vector3 GetVelocityInLocalPoint2(BulletBody obj, Vector3 pos);

public abstract void Translate2(BulletBody obj, Vector3 trans);

public abstract void UpdateDeactivation2(BulletBody obj, float timeStep);

public abstract bool WantsSleeping2(BulletBody obj);

public abstract void SetAngularFactor2(BulletBody obj, float factor);

public abstract void SetAngularFactorV2(BulletBody obj, Vector3 factor);

public abstract Vector3 GetAngularFactor2(BulletBody obj);

public abstract bool IsInWorld2(BulletBody obj);

public abstract void AddConstraintRef2(BulletBody obj, BulletConstraint constrain);

public abstract void RemoveConstraintRef2(BulletBody obj, BulletConstraint constrain);

public abstract BulletConstraint GetConstraintRef2(BulletBody obj, int index);

public abstract int GetNumConstraintRefs2(BulletBody obj);

public abstract bool SetCollisionGroupMask2(BulletBody body, uint filter, uint mask);

// =====================================================================================
// btCollisionShape entries

public abstract float GetAngularMotionDisc2(BulletShape shape);

public abstract float GetContactBreakingThreshold2(BulletShape shape, float defaultFactor);

public abstract bool IsPolyhedral2(BulletShape shape);

public abstract bool IsConvex2d2(BulletShape shape);

public abstract bool IsConvex2(BulletShape shape);

public abstract bool IsNonMoving2(BulletShape shape);

public abstract bool IsConcave2(BulletShape shape);

public abstract bool IsCompound2(BulletShape shape);

public abstract bool IsSoftBody2(BulletShape shape);

public abstract bool IsInfinite2(BulletShape shape);

public abstract void SetLocalScaling2(BulletShape shape, Vector3 scale);

public abstract Vector3 GetLocalScaling2(BulletShape shape);

public abstract Vector3 CalculateLocalInertia2(BulletShape shape, float mass);

public abstract int GetShapeType2(BulletShape shape);

public abstract void SetMargin2(BulletShape shape, float val);

public abstract float GetMargin2(BulletShape shape);

};

// ===============================================================================
static class BulletSimAPI {
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
public static extern bool UpdateParameter2(IntPtr world, uint localID, String parm, float value);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern void SetHeightMap2(IntPtr world, float[] heightmap);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern void Shutdown2(IntPtr sim);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern int PhysicsStep2(IntPtr world, float timeStep, int maxSubSteps, float fixedTimeStep,
                        out int updatedEntityCount,
                        out IntPtr updatedEntitiesPtr,
                        out int collidersCount,
                        out IntPtr collidersPtr);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern bool PushUpdate2(IntPtr obj);

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
public static extern void RecalculateCompoundShapeLocalAabb2(IntPtr cShape);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern IntPtr DuplicateCollisionShape2(IntPtr sim, IntPtr srcShape, uint id);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern IntPtr CreateBodyFromShapeAndInfo2(IntPtr sim, IntPtr shape, uint id, IntPtr constructionInfo);

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
public static extern IntPtr AllocateBodyInfo2(IntPtr obj);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern void ReleaseBodyInfo2(IntPtr obj);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern void DestroyObject2(IntPtr sim, IntPtr obj);

// =====================================================================================
// Terrain creation and helper routines
[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern IntPtr CreateHeightMapInfo2(IntPtr sim, uint id, Vector3 minCoords, Vector3 maxCoords,
        [MarshalAs(UnmanagedType.LPArray)] float[] heightMap, float collisionMargin);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern IntPtr FillHeightMapInfo2(IntPtr sim, IntPtr mapInfo, uint id, Vector3 minCoords, Vector3 maxCoords,
        [MarshalAs(UnmanagedType.LPArray)] float[] heightMap, float collisionMargin);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern bool ReleaseHeightMapInfo2(IntPtr heightMapInfo);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern IntPtr CreateGroundPlaneShape2(uint id, float height, float collisionMargin);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern IntPtr CreateTerrainShape2(IntPtr mapInfo);

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
