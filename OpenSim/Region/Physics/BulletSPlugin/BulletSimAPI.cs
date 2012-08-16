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
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using OpenMetaverse;

namespace OpenSim.Region.Physics.BulletSPlugin {

// Classes to allow some type checking for the API
public struct BulletSim
{
    public BulletSim(uint id, BSScene bss, IntPtr xx) { ID = id; scene = bss;  Ptr = xx; }
    public uint ID;
    // The scene is only in here so very low level routines have a handle to print debug/error messages
    public BSScene scene;
    public IntPtr Ptr;
}

public struct BulletBody
{
    public BulletBody(uint id, IntPtr xx) { ID = id;  Ptr = xx; }
    public IntPtr Ptr;
    public uint ID;
}

public struct BulletConstraint
{
    public BulletConstraint(IntPtr xx) { Ptr = xx; }
    public IntPtr Ptr;
}

// ===============================================================================
[StructLayout(LayoutKind.Sequential)]
public struct ConvexHull 
{
	Vector3 Offset;
	int VertexCount;
	Vector3[] Vertices;
}
[StructLayout(LayoutKind.Sequential)]
public struct ShapeData 
{
    public enum PhysicsShapeType
    {
		SHAPE_UNKNOWN   = 0,
		SHAPE_AVATAR    = 1,
		SHAPE_BOX       = 2,
		SHAPE_CONE      = 3,
		SHAPE_CYLINDER  = 4,
		SHAPE_SPHERE    = 5,
		SHAPE_MESH      = 6,
		SHAPE_HULL      = 7
    };
    public uint ID;
    public PhysicsShapeType Type;
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
[StructLayout(LayoutKind.Sequential)]
public struct ConfigurationParameters
{
    public float defaultFriction;
    public float defaultDensity;
    public float defaultRestitution;
    public float collisionMargin;
    public float gravity;

    public float linearDamping;
    public float angularDamping;
    public float deactivationTime;
    public float linearSleepingThreshold;
    public float angularSleepingThreshold;
	public float ccdMotionThreshold;
	public float ccdSweptSphereRadius;
    public float contactProcessingThreshold;

    public float terrainFriction;
    public float terrainHitFraction;
    public float terrainRestitution;
    public float avatarFriction;
    public float avatarDensity;
    public float avatarRestitution;
    public float avatarCapsuleRadius;
    public float avatarCapsuleHeight;
	public float avatarContactProcessingThreshold;

	public float maxPersistantManifoldPoolSize;
	public float maxCollisionAlgorithmPoolSize;
	public float shouldDisableContactPoolDynamicAllocation;
	public float shouldForceUpdateAllAabbs;
	public float shouldRandomizeSolverOrder;
	public float shouldSplitSimulationIslands;
	public float shouldEnableFrictionCaching;
	public float numberOfSolverIterations;

    public float linkConstraintUseFrameOffset;
    public float linkConstraintEnableTransMotor;
    public float linkConstraintTransMotorMaxVel;
    public float linkConstraintTransMotorMaxForce;
    public float linkConstraintERP;
    public float linkConstraintCFM;

    public const float numericTrue = 1f;
    public const float numericFalse = 0f;
}

// Values used by Bullet and BulletSim to control collisions
public enum CollisionFlags : uint
{
    CF_STATIC_OBJECT                 = 1 << 0,
    CF_KINEMATIC_OBJECT              = 1 << 1,
    CF_NO_CONTACT_RESPONSE           = 1 << 2,
    CF_CUSTOM_MATERIAL_CALLBACK      = 1 << 3,
    CF_CHARACTER_OBJECT              = 1 << 4,
    CF_DISABLE_VISUALIZE_OBJECT      = 1 << 5,
    CF_DISABLE_SPU_COLLISION_PROCESS = 1 << 6,
    // Following used by BulletSim to control collisions
    BS_SUBSCRIBE_COLLISION_EVENTS    = 1 << 10,
    BS_VOLUME_DETECT_OBJECT          = 1 << 11,
    BS_PHANTOM_OBJECT                = 1 << 12,
    BS_PHYSICAL_OBJECT               = 1 << 13,
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

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
[return: MarshalAs(UnmanagedType.LPStr)]
public static extern string GetVersion();

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern uint Initialize(Vector3 maxPosition, IntPtr parms, 
                        int maxCollisions, IntPtr collisionArray, 
                        int maxUpdates, IntPtr updateArray);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern void SetHeightmap(uint worldID, [MarshalAs(UnmanagedType.LPArray)] float[] heightMap);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern void Shutdown(uint worldID);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern bool UpdateParameter(uint worldID, uint localID,
                        [MarshalAs(UnmanagedType.LPStr)]string paramCode, float value);

// ===============================================================================
[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern int PhysicsStep(uint worldID, float timeStep, int maxSubSteps, float fixedTimeStep, 
                        out int updatedEntityCount, 
                        out IntPtr updatedEntitiesPtr,
                        out int collidersCount,
                        out IntPtr collidersPtr);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern bool CreateHull(uint worldID, System.UInt64 meshKey, 
                            int hullCount, [MarshalAs(UnmanagedType.LPArray)] float[] hulls
    );

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern bool CreateMesh(uint worldID, System.UInt64 meshKey, 
                            int indexCount, [MarshalAs(UnmanagedType.LPArray)] int[] indices,
                            int verticesCount, [MarshalAs(UnmanagedType.LPArray)] float[] vertices
    );

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern bool DestroyHull(uint worldID, System.UInt64 meshKey);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern bool DestroyMesh(uint worldID, System.UInt64 meshKey);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern bool CreateObject(uint worldID, ShapeData shapeData);

/*  Remove old functionality
[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern void CreateLinkset(uint worldID, int objectCount, ShapeData[] shapeDatas);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern void AddConstraint(uint worldID, uint id1, uint id2, 
                        Vector3 frame1, Quaternion frame1rot,
                        Vector3 frame2, Quaternion frame2rot,
                        Vector3 lowLinear, Vector3 hiLinear, Vector3 lowAngular, Vector3 hiAngular);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern bool RemoveConstraintByID(uint worldID, uint id1);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern bool RemoveConstraint(uint worldID, uint id1, uint id2);
 */

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern Vector3 GetObjectPosition(uint WorldID, uint id);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern Quaternion GetObjectOrientation(uint WorldID, uint id);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern bool SetObjectTranslation(uint worldID, uint id, Vector3 position, Quaternion rotation);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern bool SetObjectVelocity(uint worldID, uint id, Vector3 velocity);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern bool SetObjectAngularVelocity(uint worldID, uint id, Vector3 angularVelocity);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern bool SetObjectForce(uint worldID, uint id, Vector3 force);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern bool SetObjectScaleMass(uint worldID, uint id, Vector3 scale, float mass, bool isDynamic);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern bool SetObjectCollidable(uint worldID, uint id, bool phantom);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern bool SetObjectDynamic(uint worldID, uint id, bool isDynamic, float mass);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern bool SetObjectGhost(uint worldID, uint id, bool ghostly);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern bool SetObjectProperties(uint worldID, uint id, bool isStatic, bool isSolid, bool genCollisions, float mass);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern bool SetObjectBuoyancy(uint worldID, uint id, float buoyancy);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern bool HasObject(uint worldID, uint id);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern bool DestroyObject(uint worldID, uint id);

// ===============================================================================
[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern SweepHit ConvexSweepTest(uint worldID, uint id, Vector3 to, float extraMargin);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern RaycastHit RayTest(uint worldID, uint id, Vector3 from, Vector3 to);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern Vector3 RecoverFromPenetration(uint worldID, uint id);

// ===============================================================================
[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern void DumpBulletStatistics();

// Log a debug message
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
public delegate void DebugLogCallback([MarshalAs(UnmanagedType.LPStr)]string msg);
[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern void SetDebugLogCallback(DebugLogCallback callback);

// ===============================================================================
// ===============================================================================
// ===============================================================================
// A new version of the API that enables moving all the logic out of the C++ code and into
//    the C# code. This will make modifications easier for the next person.
// This interface passes the actual pointers to the objects in the unmanaged
//    address space. All the management (calls for creation/destruction/lookup)
//    is done in the C# code.
// The names have a "2" tacked on. This will be removed as the C# code gets rebuilt
//    and the old code is removed.

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern IntPtr GetSimHandle2(uint worldID);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern IntPtr GetBodyHandleWorldID2(uint worldID, uint id);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern IntPtr GetBodyHandle2(IntPtr world, uint id);

// ===============================================================================
[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern IntPtr Initialize2(Vector3 maxPosition, IntPtr parms,
											int maxCollisions,  IntPtr collisionArray,
											int maxUpdates, IntPtr updateArray);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern bool UpdateParameter2(IntPtr world, uint localID, String parm, float value);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern void SetHeightmap2(IntPtr world, float[] heightmap);

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

/*
[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern IntPtr CreateMesh2(IntPtr world, int indicesCount, int* indices, int verticesCount, float* vertices );

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern bool BuildHull2(IntPtr world, IntPtr mesh);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern bool ReleaseHull2(IntPtr world, IntPtr mesh);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern bool DestroyMesh2(IntPtr world, IntPtr mesh);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern IntPtr CreateObject2(IntPtr world, ShapeData shapeData);
*/

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

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern Vector3 AddObjectToWorld2(IntPtr world, IntPtr obj);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern Vector3 RemoveObjectFromWorld2(IntPtr world, IntPtr obj);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern Vector3 GetPosition2(IntPtr obj);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern Quaternion GetOrientation2(IntPtr obj);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern bool SetTranslation2(IntPtr obj, Vector3 position, Quaternion rotation);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern bool SetVelocity2(IntPtr obj, Vector3 velocity);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern bool SetAngularVelocity2(IntPtr obj, Vector3 angularVelocity);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern bool SetObjectForce2(IntPtr obj, Vector3 force);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern bool AddObjectForce2(IntPtr obj, Vector3 force);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern bool SetCcdMotionThreshold2(IntPtr obj, float val);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern bool SetCcdSweepSphereRadius2(IntPtr obj, float val);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern bool SetDamping2(IntPtr obj, float lin_damping, float ang_damping);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern bool SetDeactivationTime2(IntPtr obj, float val);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern bool SetSleepingThresholds2(IntPtr obj, float lin_threshold, float ang_threshold);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern bool SetContactProcessingThreshold2(IntPtr obj, float val);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern bool SetFriction2(IntPtr obj, float val);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern bool SetRestitution2(IntPtr obj, float val);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern bool SetLinearVelocity2(IntPtr obj, Vector3 val);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern bool SetInterpolation2(IntPtr obj, Vector3 lin, Vector3 ang);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern CollisionFlags GetCollisionFlags2(IntPtr obj);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern IntPtr SetCollisionFlags2(IntPtr obj, CollisionFlags flags);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern IntPtr AddToCollisionFlags2(IntPtr obj, CollisionFlags flags);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern IntPtr RemoveFromCollisionFlags2(IntPtr obj, CollisionFlags flags);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern bool SetMassProps2(IntPtr obj, float mass, Vector3 inertia);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern bool UpdateInertiaTensor2(IntPtr obj);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern bool SetGravity2(IntPtr obj, Vector3 val);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern IntPtr ClearForces2(IntPtr obj);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern IntPtr ClearAllForces2(IntPtr obj);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern bool SetMargin2(IntPtr obj, float val);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern bool UpdateSingleAabb2(IntPtr world, IntPtr obj);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern bool DestroyObject2(IntPtr world, uint id);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern void DumpPhysicsStatistics2(IntPtr sim);

}
}
