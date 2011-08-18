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
		SHAPE_AVATAR = 0,
		SHAPE_BOX = 1,
		SHAPE_CONE = 2,
		SHAPE_CYLINDER = 3,
		SHAPE_SPHERE = 4,
		SHAPE_HULL = 5
    };
    public uint ID;
    public PhysicsShapeType Type;
    public Vector3 Position;
    public Quaternion Rotation;
    public Vector3 Velocity;
    public Vector3 Scale;
    public float Mass;
    public float Buoyancy;
    public System.UInt64 MeshKey;
    public float Friction;
    public float Restitution;
    public int Collidable;
    public int Static;  // true if a static object. Otherwise gravity, etc.

    // note that bools are passed as ints since bool size changes by language and architecture
    public const int numericTrue = 1;
    public const int numericFalse = 0;
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

    public float terrainFriction;
    public float terrainHitFraction;
    public float terrainRestitution;
    public float avatarFriction;
    public float avatarDensity;
    public float avatarRestitution;
    public float avatarCapsuleRadius;
    public float avatarCapsuleHeight;

    public const float numericTrue = 1f;
    public const float numericFalse = 0f;
}

static class BulletSimAPI {

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
[return: MarshalAs(UnmanagedType.LPStr)]
public static extern string GetVersion();

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern uint Initialize(Vector3 maxPosition, IntPtr parms, 
                        int maxCollisions, IntPtr collisionArray, 
                        int maxUpdates, IntPtr updateArray);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern bool UpdateParameter(uint worldID, uint localID,
                        [MarshalAs(UnmanagedType.LPStr)]string paramCode, float value);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern void SetHeightmap(uint worldID, [MarshalAs(UnmanagedType.LPArray)] float[] heightMap);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern void Shutdown(uint worldID);


[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern int PhysicsStep(uint worldID, float timeStep, int maxSubSteps, float fixedTimeStep, 
    out int updatedEntityCount, 
    out IntPtr updatedEntitiesPtr,
    out int collidersCount,
    out IntPtr collidersPtr);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern bool CreateHull(uint worldID, System.UInt64 meshKey, int hullCount, 
                            [MarshalAs(UnmanagedType.LPArray)] float[] hulls
    );

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern bool DestroyHull(uint worldID, System.UInt64 meshKey);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern bool CreateObject(uint worldID, ShapeData shapeData);

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

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern Vector3 GetObjectPosition(uint WorldID, uint id);

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

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern SweepHit ConvexSweepTest(uint worldID, uint id, Vector3 to, float extraMargin);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern RaycastHit RayTest(uint worldID, uint id, Vector3 from, Vector3 to);

[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern Vector3 RecoverFromPenetration(uint worldID, uint id);

// Log a debug message
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
public delegate void DebugLogCallback([MarshalAs(UnmanagedType.LPStr)]string msg);
[DllImport("BulletSim", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
public static extern void SetDebugLogCallback(DebugLogCallback callback);
}
}
