/*
 * based on:
 * Ode.NET - .NET bindings for ODE
 * Jason Perkins (starkos@industriousone.com)
  *  Licensed under the New BSD
 *  Part of the OpenDynamicsEngine
Open Dynamics Engine
Copyright (c) 2001-2007, Russell L. Smith.
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions
are met:

Redistributions of source code must retain the above copyright notice,
this list of conditions and the following disclaimer.

Redistributions in binary form must reproduce the above copyright notice,
this list of conditions and the following disclaimer in the documentation
and/or other materials provided with the distribution.

Neither the names of ODE's copyright owner nor the names of its
contributors may be used to endorse or promote products derived from
this software without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
"AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS
FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
OWNER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED
TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR
PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF
LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING
NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 * 
 * changes by opensim team; 
 * changes by Aurora team http://www.aurora-sim.org/
 * changes by Ubit Umarov
 */

using System;
using System.Runtime.InteropServices;
using System.Security;
using OMV = OpenMetaverse;
namespace OdeAPI
{
//#if dDOUBLE
// don't see much use in double precision with time steps of 20ms and 10 iterations used on opensim
// at least we save same memory and memory access time, FPU performance on intel usually is similar
//	using dReal = System.Double;
//#else
	using dReal = System.Single;
//#endif

	public static class d
	{
		public static dReal Infinity = dReal.MaxValue;
        public static int NTotalBodies = 0;
        public static int NTotalGeoms = 0;

        public const uint CONTACTS_UNIMPORTANT = 0x80000000;

		#region Flags and Enumerations

        [Flags]
        public enum AllocateODEDataFlags : uint
        {
            BasicData = 0,
            CollisionData = 0x00000001,
            All = ~0u
        }

        [Flags]
        public enum IniteODEFlags : uint
        {
            dInitFlagManualThreadCleanup = 0x00000001
        }

		[Flags]
		public enum ContactFlags : int
		{
			Mu2 = 0x001,
			FDir1 = 0x002,
			Bounce = 0x004,
			SoftERP = 0x008,
			SoftCFM = 0x010,
			Motion1 = 0x020,
			Motion2 = 0x040,
            MotionN = 0x080,
			Slip1 = 0x100,
			Slip2 = 0x200,
			Approx0 = 0x0000,
			Approx1_1 = 0x1000,
			Approx1_2 = 0x2000,
			Approx1 = 0x3000
		}

		public enum GeomClassID : int
		{
			SphereClass,
			BoxClass,
			CapsuleClass,
			CylinderClass,
			PlaneClass,
			RayClass,
			ConvexClass,
			GeomTransformClass,
			TriMeshClass,
            HeightfieldClass,
			FirstSpaceClass,
			SimpleSpaceClass = FirstSpaceClass,
			HashSpaceClass,
			QuadTreeSpaceClass,
			LastSpaceClass = QuadTreeSpaceClass,
            ubtTerrainClass,
			FirstUserClass,
			LastUserClass = FirstUserClass + MaxUserClasses - 1,
			NumClasses,
			MaxUserClasses = 5
		}

		public enum JointType : int
		{
			None,
			Ball,
			Hinge,
			Slider,
			Contact,
			Universal,
			Hinge2,
			Fixed,
			Null,
			AMotor,
			LMotor,
			Plane2D
		}

		public enum JointParam : int
		{
			LoStop,
			HiStop,
			Vel,
			FMax,
			FudgeFactor,
			Bounce,
			CFM,
			StopERP,
			StopCFM,
			SuspensionERP,
			SuspensionCFM,
			LoStop2 = 256,
			HiStop2,
			Vel2,
			FMax2,
			FudgeFactor2,
			Bounce2,
			CFM2,
			StopERP2,
			StopCFM2,
			SuspensionERP2,
			SuspensionCFM2,
			LoStop3 = 512,
			HiStop3,
			Vel3,
			FMax3,
			FudgeFactor3,
			Bounce3,
			CFM3,
			StopERP3,
			StopCFM3,
			SuspensionERP3,
			SuspensionCFM3
		}

        public enum dSweepAndPruneAxis : int
        {
            XYZ = ((0)|(1<<2)|(2<<4)),
            XZY = ((0)|(2<<2)|(1<<4)),
            YXZ = ((1)|(0<<2)|(2<<4)),
            YZX = ((1)|(2<<2)|(0<<4)),
            ZXY = ((2)|(0<<2)|(1<<4)),
            ZYX = ((2)|(1<<2)|(0<<4))
        }

		#endregion

		#region Callbacks

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		public delegate int AABBTestFn(IntPtr o1, IntPtr o2, ref AABB aabb);

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		public delegate int ColliderFn(IntPtr o1, IntPtr o2, int flags, out ContactGeom contact, int skip);

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		public delegate void GetAABBFn(IntPtr geom, out AABB aabb);

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		public delegate ColliderFn GetColliderFnFn(int num);

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		public delegate void GeomDtorFn(IntPtr o);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate dReal HeightfieldGetHeight(IntPtr p_user_data, int x, int z);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate dReal OSTerrainGetHeight(IntPtr p_user_data, int x, int z);

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		public delegate void NearCallback(IntPtr data, IntPtr geom1, IntPtr geom2);

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		public delegate int TriCallback(IntPtr trimesh, IntPtr refObject, int triangleIndex);

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		public delegate int TriArrayCallback(IntPtr trimesh, IntPtr refObject, int[] triangleIndex, int triCount);

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		public delegate int TriRayCallback(IntPtr trimesh, IntPtr ray, int triangleIndex, dReal u, dReal v);

		#endregion

		#region Structs

		[StructLayout(LayoutKind.Sequential)]
		public struct AABB
		{
			public dReal MinX, MaxX;
			public dReal MinY, MaxY;
			public dReal MinZ, MaxZ;
		}


		[StructLayout(LayoutKind.Sequential)]
		public struct Contact
		{
			public SurfaceParameters surface;
			public ContactGeom geom;
			public Vector3 fdir1;
            public static readonly int unmanagedSizeOf = Marshal.SizeOf(typeof(Contact));
		}


		[StructLayout(LayoutKind.Sequential)]
		public struct ContactGeom
		{

			public Vector3 pos;
			public Vector3 normal;
			public dReal depth;
			public IntPtr g1;
			public IntPtr g2;
			public int side1;
			public int side2;
            public static readonly int unmanagedSizeOf = Marshal.SizeOf(typeof(ContactGeom));
        }

		[StructLayout(LayoutKind.Sequential)]
		public struct GeomClass
		{
			public int bytes;
			public GetColliderFnFn collider;
			public GetAABBFn aabb;
			public AABBTestFn aabb_test;
			public GeomDtorFn dtor;
		}


		[StructLayout(LayoutKind.Sequential)]
		public struct JointFeedback 
		{
			public Vector3 f1;
			public Vector3 t1;
			public Vector3 f2;
			public Vector3 t2;
		}


		[StructLayout(LayoutKind.Sequential)]
		public struct Mass
		{
			public dReal mass;
			public Vector4 c;
			public Matrix3 I;
		}


		[StructLayout(LayoutKind.Sequential)]
		public struct Matrix3
		{
			public Matrix3(dReal m00, dReal m10, dReal m20, dReal m01, dReal m11, dReal m21, dReal m02, dReal m12, dReal m22)
			{
				M00 = m00;  M10 = m10;  M20 = m20;  _m30 = 0.0f;
				M01 = m01;  M11 = m11;  M21 = m21;  _m31 = 0.0f;
				M02 = m02;  M12 = m12;  M22 = m22;  _m32 = 0.0f;
			}
			public dReal M00, M10, M20;
			private dReal _m30;
			public dReal M01, M11, M21;
			private dReal _m31;
			public dReal M02, M12, M22;
			private dReal _m32;
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct Matrix4
		{
			public Matrix4(dReal m00, dReal m10, dReal m20, dReal m30,
				dReal m01, dReal m11, dReal m21, dReal m31,
				dReal m02, dReal m12, dReal m22, dReal m32,
				dReal m03, dReal m13, dReal m23, dReal m33)
			{
				M00 = m00; M10 = m10; M20 = m20; M30 = m30;
				M01 = m01; M11 = m11; M21 = m21; M31 = m31;
				M02 = m02; M12 = m12; M22 = m22; M32 = m32;
				M03 = m03; M13 = m13; M23 = m23; M33 = m33;
			}
			public dReal M00, M10, M20, M30;
			public dReal M01, M11, M21, M31;
			public dReal M02, M12, M22, M32;
			public dReal M03, M13, M23, M33;
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct Quaternion
		{
			public dReal W, X, Y, Z;
		}


		[StructLayout(LayoutKind.Sequential)]
		public struct SurfaceParameters
		{
			public ContactFlags mode;
			public dReal mu;
			public dReal mu2;
			public dReal bounce;
			public dReal bounce_vel;
			public dReal soft_erp;
			public dReal soft_cfm;
			public dReal motion1;
			public dReal motion2;
            public dReal motionN;
			public dReal slip1;
			public dReal slip2;
		}


		[StructLayout(LayoutKind.Sequential)]
		public struct Vector3
		{
			public Vector3(dReal x, dReal y, dReal z)
			{
				X = x;  Y = y;  Z = z;  _w = 0.0f;
			}
			public dReal X, Y, Z;
			private dReal _w;
		}


		[StructLayout(LayoutKind.Sequential)]
		public struct Vector4
		{
			public Vector4(dReal x, dReal y, dReal z, dReal w)
			{
				X = x;  Y = y;  Z = z;  W = w;
			}
			public dReal X, Y, Z, W;
		}

		#endregion

        [DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dAllocateODEDataForThread"), SuppressUnmanagedCodeSecurity]
        public static extern int AllocateODEDataForThread(uint ODEInitFlags);

        [DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dAreConnected"), SuppressUnmanagedCodeSecurity]
		public static extern bool AreConnected(IntPtr b1, IntPtr b2);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dAreConnectedExcluding"), SuppressUnmanagedCodeSecurity]
		public static extern bool AreConnectedExcluding(IntPtr b1, IntPtr b2, JointType joint_type);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dBodyAddForce"), SuppressUnmanagedCodeSecurity]
		public static extern void BodyAddForce(IntPtr body, dReal fx, dReal fy, dReal fz);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dBodyAddForceAtPos"), SuppressUnmanagedCodeSecurity]
		public static extern void BodyAddForceAtPos(IntPtr body, dReal fx, dReal fy, dReal fz, dReal px, dReal py, dReal pz);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dBodyAddForceAtRelPos"), SuppressUnmanagedCodeSecurity]
		public static extern void BodyAddForceAtRelPos(IntPtr body, dReal fx, dReal fy, dReal fz, dReal px, dReal py, dReal pz);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dBodyAddRelForce"), SuppressUnmanagedCodeSecurity]
		public static extern void BodyAddRelForce(IntPtr body, dReal fx, dReal fy, dReal fz);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dBodyAddRelForceAtPos"), SuppressUnmanagedCodeSecurity]
		public static extern void BodyAddRelForceAtPos(IntPtr body, dReal fx, dReal fy, dReal fz, dReal px, dReal py, dReal pz);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dBodyAddRelForceAtRelPos"), SuppressUnmanagedCodeSecurity]
		public static extern void BodyAddRelForceAtRelPos(IntPtr body, dReal fx, dReal fy, dReal fz, dReal px, dReal py, dReal pz);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dBodyAddRelTorque"), SuppressUnmanagedCodeSecurity]
		public static extern void BodyAddRelTorque(IntPtr body, dReal fx, dReal fy, dReal fz);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dBodyAddTorque"), SuppressUnmanagedCodeSecurity]
		public static extern void BodyAddTorque(IntPtr body, dReal fx, dReal fy, dReal fz);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dBodyCopyPosition"), SuppressUnmanagedCodeSecurity]
		public static extern void BodyCopyPosition(IntPtr body, out Vector3 pos);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dBodyCopyPosition"), SuppressUnmanagedCodeSecurity]
		public static extern void BodyCopyPosition(IntPtr body, out dReal X);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dBodyCopyQuaternion"), SuppressUnmanagedCodeSecurity]
		public static extern void BodyCopyQuaternion(IntPtr body, out Quaternion quat);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dBodyCopyQuaternion"), SuppressUnmanagedCodeSecurity]
		public static extern void BodyCopyQuaternion(IntPtr body, out dReal X);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dBodyCopyRotation"), SuppressUnmanagedCodeSecurity]
		public static extern void BodyCopyRotation(IntPtr body, out Matrix3 R);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dBodyCopyRotation"), SuppressUnmanagedCodeSecurity]
		public static extern void BodyCopyRotation(IntPtr body, out dReal M00);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dBodyCreate"), SuppressUnmanagedCodeSecurity]
		public static extern IntPtr BodyiCreate(IntPtr world);
        public static IntPtr BodyCreate(IntPtr world)
        {
            NTotalBodies++;
            return BodyiCreate(world);
        }

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dBodyDestroy"), SuppressUnmanagedCodeSecurity]
        public static extern void BodyiDestroy(IntPtr body);
        public static void BodyDestroy(IntPtr body)
        {
            NTotalBodies--;
            BodyiDestroy(body);
        }

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dBodyDisable"), SuppressUnmanagedCodeSecurity]
		public static extern void BodyDisable(IntPtr body);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dBodyEnable"), SuppressUnmanagedCodeSecurity]
		public static extern void BodyEnable(IntPtr body);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dBodyGetAutoDisableAngularThreshold"), SuppressUnmanagedCodeSecurity]
		public static extern dReal BodyGetAutoDisableAngularThreshold(IntPtr body);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dBodyGetAutoDisableFlag"), SuppressUnmanagedCodeSecurity]
		public static extern bool BodyGetAutoDisableFlag(IntPtr body);

        [DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dBodyGetAutoDisableDefaults"), SuppressUnmanagedCodeSecurity]
        public static extern void BodyGetAutoDisableDefaults(IntPtr body);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dBodyGetAutoDisableLinearThreshold"), SuppressUnmanagedCodeSecurity]
		public static extern dReal BodyGetAutoDisableLinearThreshold(IntPtr body);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dBodyGetAutoDisableSteps"), SuppressUnmanagedCodeSecurity]
		public static extern int BodyGetAutoDisableSteps(IntPtr body);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dBodyGetAutoDisableTime"), SuppressUnmanagedCodeSecurity]
		public static extern dReal BodyGetAutoDisableTime(IntPtr body);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dBodyGetAngularVel"), SuppressUnmanagedCodeSecurity]
		public extern unsafe static Vector3* BodyGetAngularVelUnsafe(IntPtr body);
		public static Vector3 BodyGetAngularVel(IntPtr body)
		{
			unsafe { return *(BodyGetAngularVelUnsafe(body)); }
		}

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dBodyGetData"), SuppressUnmanagedCodeSecurity]
		public static extern IntPtr BodyGetData(IntPtr body);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dBodyGetFiniteRotationMode"), SuppressUnmanagedCodeSecurity]
		public static extern int BodyGetFiniteRotationMode(IntPtr body);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dBodyGetFiniteRotationAxis"), SuppressUnmanagedCodeSecurity]
		public static extern void BodyGetFiniteRotationAxis(IntPtr body, out Vector3 result);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dBodyGetForce"), SuppressUnmanagedCodeSecurity]
		public extern unsafe static Vector3* BodyGetForceUnsafe(IntPtr body);
		public static Vector3 BodyGetForce(IntPtr body)
		{
			unsafe { return *(BodyGetForceUnsafe(body)); }
		}

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dBodyGetGravityMode"), SuppressUnmanagedCodeSecurity]
		public static extern bool BodyGetGravityMode(IntPtr body);

        [DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dBodyGetGyroscopicMode"), SuppressUnmanagedCodeSecurity]
        public static extern int BodyGetGyroscopicMode(IntPtr body);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dBodyGetJoint"), SuppressUnmanagedCodeSecurity]
		public static extern IntPtr BodyGetJoint(IntPtr body, int index);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dBodyGetLinearVel"), SuppressUnmanagedCodeSecurity]
		public extern unsafe static Vector3* BodyGetLinearVelUnsafe(IntPtr body);
		public static Vector3 BodyGetLinearVel(IntPtr body)
		{
			unsafe { return *(BodyGetLinearVelUnsafe(body)); }
		}

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dBodyGetMass"), SuppressUnmanagedCodeSecurity]
		public static extern void BodyGetMass(IntPtr body, out Mass mass);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dBodyGetNumJoints"), SuppressUnmanagedCodeSecurity]
		public static extern int BodyGetNumJoints(IntPtr body);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dBodyGetPointVel"), SuppressUnmanagedCodeSecurity]
		public static extern void BodyGetPointVel(IntPtr body, dReal px, dReal py, dReal pz, out Vector3 result);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dBodyGetPosition"), SuppressUnmanagedCodeSecurity]
		public extern unsafe static Vector3* BodyGetPositionUnsafe(IntPtr body);
		public static Vector3 BodyGetPosition(IntPtr body)
		{
			unsafe { return *(BodyGetPositionUnsafe(body)); }
		}

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dBodyGetPosRelPoint"), SuppressUnmanagedCodeSecurity]
		public static extern void BodyGetPosRelPoint(IntPtr body, dReal px, dReal py, dReal pz, out Vector3 result);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dBodyGetQuaternion"), SuppressUnmanagedCodeSecurity]
		public extern unsafe static Quaternion* BodyGetQuaternionUnsafe(IntPtr body);
		public static Quaternion BodyGetQuaternion(IntPtr body)
		{
			unsafe { return *(BodyGetQuaternionUnsafe(body)); }
		}

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dBodyGetRelPointPos"), SuppressUnmanagedCodeSecurity]
		public static extern void BodyGetRelPointPos(IntPtr body, dReal px, dReal py, dReal pz, out Vector3 result);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dBodyGetRelPointVel"), SuppressUnmanagedCodeSecurity]
		public static extern void BodyGetRelPointVel(IntPtr body, dReal px, dReal py, dReal pz, out Vector3 result);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dBodyGetRotation"), SuppressUnmanagedCodeSecurity]
		public extern unsafe static Matrix3* BodyGetRotationUnsafe(IntPtr body);
		public static Matrix3 BodyGetRotation(IntPtr body)
		{
			unsafe { return *(BodyGetRotationUnsafe(body)); }
		}

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dBodyGetTorque"), SuppressUnmanagedCodeSecurity]
		public extern unsafe static Vector3* BodyGetTorqueUnsafe(IntPtr body);
		public static Vector3 BodyGetTorque(IntPtr body)
		{
			unsafe { return *(BodyGetTorqueUnsafe(body)); }
		}

        [DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dBodyGetWorld"), SuppressUnmanagedCodeSecurity]
        public static extern IntPtr BodyGetWorld(IntPtr body);

        [DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dBodyGetFirstGeom"), SuppressUnmanagedCodeSecurity]
        public static extern IntPtr BodyGetFirstGeom(IntPtr body);

        [DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dBodyGetNextGeom"), SuppressUnmanagedCodeSecurity]
        public static extern IntPtr dBodyGetNextGeom(IntPtr Geom);


		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dBodyIsEnabled"), SuppressUnmanagedCodeSecurity]
		public static extern bool BodyIsEnabled(IntPtr body);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dBodySetAngularVel"), SuppressUnmanagedCodeSecurity]
		public static extern void BodySetAngularVel(IntPtr body, dReal x, dReal y, dReal z);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dBodySetAutoDisableAngularThreshold"), SuppressUnmanagedCodeSecurity]
		public static extern void BodySetAutoDisableAngularThreshold(IntPtr body, dReal angular_threshold);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dBodySetAutoDisableDefaults"), SuppressUnmanagedCodeSecurity]
		public static extern void BodySetAutoDisableDefaults(IntPtr body);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dBodySetAutoDisableFlag"), SuppressUnmanagedCodeSecurity]
		public static extern void BodySetAutoDisableFlag(IntPtr body, bool do_auto_disable);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dBodySetAutoDisableLinearThreshold"), SuppressUnmanagedCodeSecurity]
		public static extern void BodySetAutoDisableLinearThreshold(IntPtr body, dReal linear_threshold);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dBodySetAutoDisableSteps"), SuppressUnmanagedCodeSecurity]
		public static extern void BodySetAutoDisableSteps(IntPtr body, int steps);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dBodySetAutoDisableTime"), SuppressUnmanagedCodeSecurity]
		public static extern void BodySetAutoDisableTime(IntPtr body, dReal time);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dBodySetData"), SuppressUnmanagedCodeSecurity]
		public static extern void BodySetData(IntPtr body, IntPtr data);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dBodySetFiniteRotationMode"), SuppressUnmanagedCodeSecurity]
		public static extern void BodySetFiniteRotationMode(IntPtr body, int mode);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dBodySetFiniteRotationAxis"), SuppressUnmanagedCodeSecurity]
		public static extern void BodySetFiniteRotationAxis(IntPtr body, dReal x, dReal y, dReal z);

        [DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dBodySetLinearDamping"), SuppressUnmanagedCodeSecurity]
        public static extern void BodySetLinearDamping(IntPtr body, dReal scale);

        [DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dBodySetAngularDamping"), SuppressUnmanagedCodeSecurity]
        public static extern void BodySetAngularDamping(IntPtr body, dReal scale);

        [DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dBodyGetLinearDamping"), SuppressUnmanagedCodeSecurity]
        public static extern dReal BodyGetLinearDamping(IntPtr body);

        [DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dBodyGetAngularDamping"), SuppressUnmanagedCodeSecurity]
        public static extern dReal BodyGetAngularDamping(IntPtr body);

        [DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dBodySetAngularDamping"), SuppressUnmanagedCodeSecurity]
        public static extern void BodySetDamping(IntPtr body, dReal linear_scale, dReal angular_scale);

        [DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dBodySetAngularDampingThreshold"), SuppressUnmanagedCodeSecurity]
        public static extern void BodySetAngularDampingThreshold(IntPtr body, dReal threshold);

        [DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dBodySetLinearDampingThreshold"), SuppressUnmanagedCodeSecurity]
        public static extern void BodySetLinearDampingThreshold(IntPtr body, dReal threshold);

        [DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dBodyGetLinearDampingThreshold"), SuppressUnmanagedCodeSecurity]
        public static extern dReal BodyGetLinearDampingThreshold(IntPtr body);

        [DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dBodyGetAngularDampingThreshold"), SuppressUnmanagedCodeSecurity]
        public static extern dReal BodyGetAngularDampingThreshold(IntPtr body);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dBodySetForce"), SuppressUnmanagedCodeSecurity]
		public static extern void BodySetForce(IntPtr body, dReal x, dReal y, dReal z);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dBodySetGravityMode"), SuppressUnmanagedCodeSecurity]
		public static extern void BodySetGravityMode(IntPtr body, bool mode);

        /// <summary>
        /// Sets the Gyroscopic term status on the body specified.
        /// </summary>
        /// <param name="body">Pointer to body</param>
        /// <param name="enabled">NonZero enabled, Zero disabled</param>
        [DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dBodySetGyroscopicMode"), SuppressUnmanagedCodeSecurity]
        public static extern void dBodySetGyroscopicMode(IntPtr body, int enabled);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dBodySetLinearVel"), SuppressUnmanagedCodeSecurity]
		public static extern void BodySetLinearVel(IntPtr body, dReal x, dReal y, dReal z);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dBodySetMass"), SuppressUnmanagedCodeSecurity]
		public static extern void BodySetMass(IntPtr body, ref Mass mass);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dBodySetPosition"), SuppressUnmanagedCodeSecurity]
		public static extern void BodySetPosition(IntPtr body, dReal x, dReal y, dReal z);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dBodySetQuaternion"), SuppressUnmanagedCodeSecurity]
		public static extern void BodySetQuaternion(IntPtr body, ref Quaternion q);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dBodySetQuaternion"), SuppressUnmanagedCodeSecurity]
		public static extern void BodySetQuaternion(IntPtr body, ref dReal w);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dBodySetRotation"), SuppressUnmanagedCodeSecurity]
		public static extern void BodySetRotation(IntPtr body, ref Matrix3 R);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dBodySetRotation"), SuppressUnmanagedCodeSecurity]
		public static extern void BodySetRotation(IntPtr body, ref dReal M00);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dBodySetTorque"), SuppressUnmanagedCodeSecurity]
		public static extern void BodySetTorque(IntPtr body, dReal x, dReal y, dReal z);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dBodyVectorFromWorld"), SuppressUnmanagedCodeSecurity]
		public static extern void BodyVectorFromWorld(IntPtr body, dReal px, dReal py, dReal pz, out Vector3 result);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dBodyVectorToWorld"), SuppressUnmanagedCodeSecurity]
		public static extern void BodyVectorToWorld(IntPtr body, dReal px, dReal py, dReal pz, out Vector3 result);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dBoxBox"), SuppressUnmanagedCodeSecurity]
		public static extern void BoxBox(ref Vector3 p1, ref Matrix3 R1,
			ref Vector3 side1, ref Vector3 p2,
			ref Matrix3 R2, ref Vector3 side2,
			ref Vector3 normal, out dReal depth, out int return_code,
			int maxc, out ContactGeom contact, int skip);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dBoxTouchesBox"), SuppressUnmanagedCodeSecurity]
		public static extern void BoxTouchesBox(ref Vector3 _p1, ref Matrix3 R1,
			ref Vector3 side1, ref Vector3 _p2,
			ref Matrix3 R2, ref Vector3 side2);

        [DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dCleanupODEAllDataForThread"), SuppressUnmanagedCodeSecurity]
		public static extern void CleanupODEAllDataForThread();
        
		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dClosestLineSegmentPoints"), SuppressUnmanagedCodeSecurity]
		public static extern void ClosestLineSegmentPoints(ref Vector3 a1, ref Vector3 a2, 
			ref Vector3 b1, ref Vector3 b2, 
			ref Vector3 cp1, ref Vector3 cp2);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dCloseODE"), SuppressUnmanagedCodeSecurity]
		public static extern void CloseODE();

        [DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dCollide"), SuppressUnmanagedCodeSecurity]
        public static extern int Collide(IntPtr o1, IntPtr o2, int flags, [In, Out] ContactGeom[] contact, int skip);
        [DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dCollide"), SuppressUnmanagedCodeSecurity]
        public static extern int CollidePtr(IntPtr o1, IntPtr o2, int flags, IntPtr contactgeomarray, int skip);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dConnectingJoint"), SuppressUnmanagedCodeSecurity]
		public static extern IntPtr ConnectingJoint(IntPtr j1, IntPtr j2);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dCreateBox"), SuppressUnmanagedCodeSecurity]
        public static extern IntPtr CreateiBox(IntPtr space, dReal lx, dReal ly, dReal lz);
		public static IntPtr CreateBox(IntPtr space, dReal lx, dReal ly, dReal lz)
        {
            NTotalGeoms++;
            return CreateiBox(space, lx, ly, lz);
        }

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dCreateCapsule"), SuppressUnmanagedCodeSecurity]
        public static extern IntPtr CreateiCapsule(IntPtr space, dReal radius, dReal length);
        public static IntPtr CreateCapsule(IntPtr space, dReal radius, dReal length)
        {
            NTotalGeoms++;
            return CreateiCapsule(space, radius, length);
        }

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dCreateConvex"), SuppressUnmanagedCodeSecurity]
		public static extern IntPtr CreateiConvex(IntPtr space, dReal[] planes, int planeCount, dReal[] points, int pointCount, int[] polygons);
        public static IntPtr CreateConvex(IntPtr space, dReal[] planes, int planeCount, dReal[] points, int pointCount, int[] polygons)
        {
            NTotalGeoms++;
            return CreateiConvex(space, planes, planeCount, points, pointCount, polygons);
        }

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dCreateCylinder"), SuppressUnmanagedCodeSecurity]
		public static extern IntPtr CreateiCylinder(IntPtr space, dReal radius, dReal length);
        public static IntPtr CreateCylinder(IntPtr space, dReal radius, dReal length)
        {
            NTotalGeoms++;
            return CreateiCylinder(space, radius, length);
        }

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dCreateHeightfield"), SuppressUnmanagedCodeSecurity]
        public static extern IntPtr CreateiHeightfield(IntPtr space, IntPtr data, int bPlaceable);
        public static IntPtr CreateHeightfield(IntPtr space, IntPtr data, int bPlaceable)
        {
            NTotalGeoms++;
            return CreateiHeightfield(space, data, bPlaceable);
        }

        [DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dCreateOSTerrain"), SuppressUnmanagedCodeSecurity]
        public static extern IntPtr CreateiOSTerrain(IntPtr space, IntPtr data, int bPlaceable);
        public static IntPtr CreateOSTerrain(IntPtr space, IntPtr data, int bPlaceable)
        {
            NTotalGeoms++;
            return CreateiOSTerrain(space, data, bPlaceable);
        }



        

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dCreateGeom"), SuppressUnmanagedCodeSecurity]
		public static extern IntPtr CreateiGeom(int classnum);
        public static IntPtr CreateGeom(int classnum)
        {
            NTotalGeoms++;
            return CreateiGeom(classnum);
        }

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dCreateGeomClass"), SuppressUnmanagedCodeSecurity]
		public static extern int CreateGeomClass(ref GeomClass classptr);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dCreateGeomTransform"), SuppressUnmanagedCodeSecurity]
		public static extern IntPtr CreateGeomTransform(IntPtr space);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dCreatePlane"), SuppressUnmanagedCodeSecurity]
        public static extern IntPtr CreateiPlane(IntPtr space, dReal a, dReal b, dReal c, dReal d);
        public static IntPtr CreatePlane(IntPtr space, dReal a, dReal b, dReal c, dReal d)
        {
            NTotalGeoms++;
            return CreateiPlane(space, a, b, c, d);
        }

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dCreateRay"), SuppressUnmanagedCodeSecurity]
		public static extern IntPtr CreateiRay(IntPtr space, dReal length);
        public static IntPtr CreateRay(IntPtr space, dReal length)
        {
            NTotalGeoms++;
            return CreateiRay(space, length);
        }

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dCreateSphere"), SuppressUnmanagedCodeSecurity]
		public static extern IntPtr CreateiSphere(IntPtr space, dReal radius);
        public static IntPtr CreateSphere(IntPtr space, dReal radius)
        {
            NTotalGeoms++;
            return CreateiSphere(space, radius);
        }

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dCreateTriMesh"), SuppressUnmanagedCodeSecurity]
		public static extern IntPtr CreateiTriMesh(IntPtr space, IntPtr data, 
			TriCallback callback, TriArrayCallback arrayCallback, TriRayCallback rayCallback);
        public static IntPtr CreateTriMesh(IntPtr space, IntPtr data,
            TriCallback callback, TriArrayCallback arrayCallback, TriRayCallback rayCallback)
        {
            NTotalGeoms++;
            return CreateiTriMesh(space, data, callback, arrayCallback, rayCallback);
        }
        [DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dDot"), SuppressUnmanagedCodeSecurity]
		public static extern dReal Dot(ref dReal X0, ref dReal X1, int n);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dDQfromW"), SuppressUnmanagedCodeSecurity]
		public static extern void DQfromW(dReal[] dq, ref Vector3 w, ref Quaternion q);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dFactorCholesky"), SuppressUnmanagedCodeSecurity]
		public static extern int FactorCholesky(ref dReal A00, int n);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dFactorLDLT"), SuppressUnmanagedCodeSecurity]
		public static extern void FactorLDLT(ref dReal A, out dReal d, int n, int nskip);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomBoxGetLengths"), SuppressUnmanagedCodeSecurity]
		public static extern void GeomBoxGetLengths(IntPtr geom, out Vector3 len);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomBoxGetLengths"), SuppressUnmanagedCodeSecurity]
		public static extern void GeomBoxGetLengths(IntPtr geom, out dReal x);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomBoxPointDepth"), SuppressUnmanagedCodeSecurity]
		public static extern dReal GeomBoxPointDepth(IntPtr geom, dReal x, dReal y, dReal z);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomBoxSetLengths"), SuppressUnmanagedCodeSecurity]
		public static extern void GeomBoxSetLengths(IntPtr geom, dReal x, dReal y, dReal z);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomCapsuleGetParams"), SuppressUnmanagedCodeSecurity]
		public static extern void GeomCapsuleGetParams(IntPtr geom, out dReal radius, out dReal length);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomCapsulePointDepth"), SuppressUnmanagedCodeSecurity]
		public static extern dReal GeomCapsulePointDepth(IntPtr geom, dReal x, dReal y, dReal z);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomCapsuleSetParams"), SuppressUnmanagedCodeSecurity]
		public static extern void GeomCapsuleSetParams(IntPtr geom, dReal radius, dReal length);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomClearOffset"), SuppressUnmanagedCodeSecurity]
		public static extern void GeomClearOffset(IntPtr geom);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomCopyOffsetPosition"), SuppressUnmanagedCodeSecurity]
		public static extern IntPtr GeomCopyOffsetPosition(IntPtr geom, ref Vector3 pos);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomCopyOffsetPosition"), SuppressUnmanagedCodeSecurity]
		public static extern IntPtr GeomCopyOffsetPosition(IntPtr geom, ref dReal X);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomGetOffsetQuaternion"), SuppressUnmanagedCodeSecurity]
		public static extern void GeomCopyOffsetQuaternion(IntPtr geom, ref Quaternion Q);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomGetOffsetQuaternion"), SuppressUnmanagedCodeSecurity]
		public static extern void GeomCopyOffsetQuaternion(IntPtr geom, ref dReal X);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomCopyOffsetRotation"), SuppressUnmanagedCodeSecurity]
		public static extern IntPtr GeomCopyOffsetRotation(IntPtr geom, ref Matrix3 R);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomCopyOffsetRotation"), SuppressUnmanagedCodeSecurity]
		public static extern IntPtr GeomCopyOffsetRotation(IntPtr geom, ref dReal M00);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomCopyPosition"), SuppressUnmanagedCodeSecurity]
		public static extern void GeomCopyPosition(IntPtr geom, out Vector3 pos);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomCopyPosition"), SuppressUnmanagedCodeSecurity]
		public static extern void GeomCopyPosition(IntPtr geom, out dReal X);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomCopyRotation"), SuppressUnmanagedCodeSecurity]
		public static extern void GeomCopyRotation(IntPtr geom, out Matrix3 R);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomCopyRotation"), SuppressUnmanagedCodeSecurity]
		public static extern void GeomCopyRotation(IntPtr geom, out dReal M00);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomCylinderGetParams"), SuppressUnmanagedCodeSecurity]
		public static extern void GeomCylinderGetParams(IntPtr geom, out dReal radius, out dReal length);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomCylinderSetParams"), SuppressUnmanagedCodeSecurity]
		public static extern void GeomCylinderSetParams(IntPtr geom, dReal radius, dReal length);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomDestroy"), SuppressUnmanagedCodeSecurity]
		public static extern void GeomiDestroy(IntPtr geom);
        public static void GeomDestroy(IntPtr geom)
        {
            NTotalGeoms--;
            GeomiDestroy(geom);
        }


		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomDisable"), SuppressUnmanagedCodeSecurity]
		public static extern void GeomDisable(IntPtr geom);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomEnable"), SuppressUnmanagedCodeSecurity]
		public static extern void GeomEnable(IntPtr geom);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomGetAABB"), SuppressUnmanagedCodeSecurity]
		public static extern void GeomGetAABB(IntPtr geom, out AABB aabb);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomGetAABB"), SuppressUnmanagedCodeSecurity]
		public static extern void GeomGetAABB(IntPtr geom, out dReal minX);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomGetBody"), SuppressUnmanagedCodeSecurity]
		public static extern IntPtr GeomGetBody(IntPtr geom);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomGetCategoryBits"), SuppressUnmanagedCodeSecurity]
		public static extern uint GeomGetCategoryBits(IntPtr geom);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomGetClassData"), SuppressUnmanagedCodeSecurity]
		public static extern IntPtr GeomGetClassData(IntPtr geom);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomGetCollideBits"), SuppressUnmanagedCodeSecurity]
		public static extern uint GeomGetCollideBits(IntPtr geom);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomGetClass"), SuppressUnmanagedCodeSecurity]
		public static extern GeomClassID GeomGetClass(IntPtr geom);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomGetData"), SuppressUnmanagedCodeSecurity]
        public static extern IntPtr GeomGetData(IntPtr geom);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomGetOffsetPosition"), SuppressUnmanagedCodeSecurity]
		public extern unsafe static Vector3* GeomGetOffsetPositionUnsafe(IntPtr geom);
		public static Vector3 GeomGetOffsetPosition(IntPtr geom)
		{
			unsafe { return *(GeomGetOffsetPositionUnsafe(geom)); }
		}

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomGetOffsetRotation"), SuppressUnmanagedCodeSecurity]
		public extern unsafe static Matrix3* GeomGetOffsetRotationUnsafe(IntPtr geom);
		public static Matrix3 GeomGetOffsetRotation(IntPtr geom)
		{
			unsafe { return *(GeomGetOffsetRotationUnsafe(geom)); }
		}

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomGetPosition"), SuppressUnmanagedCodeSecurity]
		public extern unsafe static Vector3* GeomGetPositionUnsafe(IntPtr geom);
		public static Vector3 GeomGetPosition(IntPtr geom)
		{
			unsafe { return *(GeomGetPositionUnsafe(geom)); }
		}
		public static OMV.Vector3 GeomGetPositionOMV(IntPtr geom)
		{
            Vector3 vtmp = GeomGetPosition(geom);
            return new OMV.Vector3(vtmp.X, vtmp.Y, vtmp.Z);
		}

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomGetQuaternion"), SuppressUnmanagedCodeSecurity]
        public static extern void GeomCopyQuaternion(IntPtr geom, out Quaternion q);
        public static OMV.Quaternion GeomGetQuaternionOMV(IntPtr geom)
        {
            Quaternion qtmp;
            GeomCopyQuaternion(geom, out qtmp);
            return new OMV.Quaternion(qtmp.X, qtmp.Y, qtmp.Z, qtmp.W);
        }

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomGetQuaternion"), SuppressUnmanagedCodeSecurity]
		public static extern void GeomCopyQuaternion(IntPtr geom, out dReal X);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomGetRotation"), SuppressUnmanagedCodeSecurity]
		public extern unsafe static Matrix3* GeomGetRotationUnsafe(IntPtr geom);
		public static Matrix3 GeomGetRotation(IntPtr geom)
		{
			unsafe { return *(GeomGetRotationUnsafe(geom)); }
		}

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomGetSpace"), SuppressUnmanagedCodeSecurity]
		public static extern IntPtr GeomGetSpace(IntPtr geom);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomHeightfieldDataBuildByte"), SuppressUnmanagedCodeSecurity]
		public static extern void GeomHeightfieldDataBuildByte(IntPtr d, byte[] pHeightData, int bCopyHeightData,
				dReal width, dReal depth, int widthSamples, int depthSamples,
				dReal scale, dReal offset, dReal thickness, int bWrap);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomHeightfieldDataBuildByte"), SuppressUnmanagedCodeSecurity]
		public static extern void GeomHeightfieldDataBuildByte(IntPtr d, IntPtr pHeightData, int bCopyHeightData,
				dReal width, dReal depth, int widthSamples, int depthSamples,
				dReal scale, dReal offset, dReal thickness,	int bWrap);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomHeightfieldDataBuildCallback"), SuppressUnmanagedCodeSecurity]
		public static extern void GeomHeightfieldDataBuildCallback(IntPtr d, IntPtr pUserData, HeightfieldGetHeight pCallback,
				dReal width, dReal depth, int widthSamples, int depthSamples,
				dReal scale, dReal offset, dReal thickness, int bWrap);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomHeightfieldDataBuildShort"), SuppressUnmanagedCodeSecurity]
		public static extern void GeomHeightfieldDataBuildShort(IntPtr d, ushort[] pHeightData, int bCopyHeightData,
				dReal width, dReal depth, int widthSamples, int depthSamples,
				dReal scale, dReal offset, dReal thickness, int bWrap);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomHeightfieldDataBuildShort"), SuppressUnmanagedCodeSecurity]
		public static extern void GeomHeightfieldDataBuildShort(IntPtr d, short[] pHeightData, int bCopyHeightData,
				dReal width, dReal depth, int widthSamples, int depthSamples,
				dReal scale, dReal offset, dReal thickness, int bWrap);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomHeightfieldDataBuildShort"), SuppressUnmanagedCodeSecurity]
		public static extern void GeomHeightfieldDataBuildShort(IntPtr d, IntPtr pHeightData, int bCopyHeightData,
				dReal width, dReal depth, int widthSamples, int depthSamples,
				dReal scale, dReal offset, dReal thickness, int bWrap);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomHeightfieldDataBuildSingle"), SuppressUnmanagedCodeSecurity]
		public static extern void GeomHeightfieldDataBuildSingle(IntPtr d, float[] pHeightData, int bCopyHeightData,
				dReal width, dReal depth, int widthSamples, int depthSamples,
				dReal scale, dReal offset, dReal thickness, int bWrap);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomHeightfieldDataBuildSingle"), SuppressUnmanagedCodeSecurity]
		public static extern void GeomHeightfieldDataBuildSingle(IntPtr d, IntPtr pHeightData, int bCopyHeightData,
				dReal width, dReal depth, int widthSamples, int depthSamples,
				dReal scale, dReal offset, dReal thickness, int bWrap);



		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomHeightfieldDataBuildDouble"), SuppressUnmanagedCodeSecurity]
		public static extern void GeomHeightfieldDataBuildDouble(IntPtr d, double[] pHeightData, int bCopyHeightData,
				dReal width, dReal depth, int widthSamples, int depthSamples,
				dReal scale, dReal offset, dReal thickness, int bWrap);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomHeightfieldDataBuildDouble"), SuppressUnmanagedCodeSecurity]
		public static extern void GeomHeightfieldDataBuildDouble(IntPtr d, IntPtr pHeightData, int bCopyHeightData,
				dReal width, dReal depth, int widthSamples, int depthSamples,
				dReal scale, dReal offset, dReal thickness, int bWrap);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomHeightfieldDataCreate"), SuppressUnmanagedCodeSecurity]
		public static extern IntPtr GeomHeightfieldDataCreate();

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomHeightfieldDataDestroy"), SuppressUnmanagedCodeSecurity]
		public static extern void GeomHeightfieldDataDestroy(IntPtr d);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomHeightfieldDataSetBounds"), SuppressUnmanagedCodeSecurity]
		public static extern void GeomHeightfieldDataSetBounds(IntPtr d, dReal minHeight, dReal maxHeight);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomHeightfieldGetHeightfieldData"), SuppressUnmanagedCodeSecurity]
		public static extern IntPtr GeomHeightfieldGetHeightfieldData(IntPtr g);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomHeightfieldSetHeightfieldData"), SuppressUnmanagedCodeSecurity]
		public static extern void GeomHeightfieldSetHeightfieldData(IntPtr g, IntPtr d);


        [DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomUbitTerrainDataBuild"), SuppressUnmanagedCodeSecurity]
        public static extern void GeomOSTerrainDataBuild(IntPtr d, float[] pHeightData, int bCopyHeightData,
                dReal sampleSize, int widthSamples, int depthSamples,
                dReal offset, dReal thickness, int bWrap);

        [DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomOSTerrainDataBuild"), SuppressUnmanagedCodeSecurity]
        public static extern void GeomOSTerrainDataBuild(IntPtr d, IntPtr pHeightData, int bCopyHeightData,
                dReal sampleSize, int widthSamples, int depthSamples,
                dReal thickness, int bWrap);

        [DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomOSTerrainDataCreate"), SuppressUnmanagedCodeSecurity]
        public static extern IntPtr GeomOSTerrainDataCreate();

        [DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomOSTerrainDataDestroy"), SuppressUnmanagedCodeSecurity]
        public static extern void GeomOSTerrainDataDestroy(IntPtr d);

        [DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomOSTerrainDataSetBounds"), SuppressUnmanagedCodeSecurity]
        public static extern void GeomOSTerrainDataSetBounds(IntPtr d, dReal minHeight, dReal maxHeight);

        [DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomOSTerrainGetHeightfieldData"), SuppressUnmanagedCodeSecurity]
        public static extern IntPtr GeomOSTerrainGetHeightfieldData(IntPtr g);

        [DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomOSTerrainSetHeightfieldData"), SuppressUnmanagedCodeSecurity]
        public static extern void GeomOSTerrainSetHeightfieldData(IntPtr g, IntPtr d);


		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomIsEnabled"), SuppressUnmanagedCodeSecurity]
		public static extern bool GeomIsEnabled(IntPtr geom);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomIsOffset"), SuppressUnmanagedCodeSecurity]
		public static extern bool GeomIsOffset(IntPtr geom);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomIsSpace"), SuppressUnmanagedCodeSecurity]
		public static extern bool GeomIsSpace(IntPtr geom);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomPlaneGetParams"), SuppressUnmanagedCodeSecurity]
		public static extern void GeomPlaneGetParams(IntPtr geom, ref Vector4 result);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomPlaneGetParams"), SuppressUnmanagedCodeSecurity]
		public static extern void GeomPlaneGetParams(IntPtr geom, ref dReal A);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomPlanePointDepth"), SuppressUnmanagedCodeSecurity]
		public static extern dReal GeomPlanePointDepth(IntPtr geom, dReal x, dReal y, dReal z);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomPlaneSetParams"), SuppressUnmanagedCodeSecurity]
		public static extern void GeomPlaneSetParams(IntPtr plane, dReal a, dReal b, dReal c, dReal d);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomRayGet"), SuppressUnmanagedCodeSecurity]
		public static extern void GeomRayGet(IntPtr ray, ref Vector3 start, ref Vector3 dir);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomRayGet"), SuppressUnmanagedCodeSecurity]
		public static extern void GeomRayGet(IntPtr ray, ref dReal startX, ref dReal dirX);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomRayGetClosestHit"), SuppressUnmanagedCodeSecurity]
		public static extern int GeomRayGetClosestHit(IntPtr ray);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomRayGetLength"), SuppressUnmanagedCodeSecurity]
		public static extern dReal GeomRayGetLength(IntPtr ray);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomRayGetParams"), SuppressUnmanagedCodeSecurity]
		public static extern dReal GeomRayGetParams(IntPtr g, out int firstContact, out int backfaceCull);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomRaySet"), SuppressUnmanagedCodeSecurity]
		public static extern void GeomRaySet(IntPtr ray, dReal px, dReal py, dReal pz, dReal dx, dReal dy, dReal dz);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomRaySetClosestHit"), SuppressUnmanagedCodeSecurity]
		public static extern void GeomRaySetClosestHit(IntPtr ray, int closestHit);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomRaySetLength"), SuppressUnmanagedCodeSecurity]
		public static extern void GeomRaySetLength(IntPtr ray, dReal length);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomRaySetParams"), SuppressUnmanagedCodeSecurity]
		public static extern void GeomRaySetParams(IntPtr ray, int firstContact, int backfaceCull);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomSetBody"), SuppressUnmanagedCodeSecurity]
		public static extern void GeomSetBody(IntPtr geom, IntPtr body);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomSetCategoryBits"), SuppressUnmanagedCodeSecurity]
		public static extern void GeomSetCategoryBits(IntPtr geom, uint bits);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomSetCollideBits"), SuppressUnmanagedCodeSecurity]
		public static extern void GeomSetCollideBits(IntPtr geom, uint bits);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomSetConvex"), SuppressUnmanagedCodeSecurity]
		public static extern IntPtr GeomSetConvex(IntPtr geom, dReal[] planes, int planeCount, dReal[] points, int pointCount, int[] polygons);

        [DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomSetData"), SuppressUnmanagedCodeSecurity]
        public static extern void GeomSetData(IntPtr geom, IntPtr data);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomSetOffsetPosition"), SuppressUnmanagedCodeSecurity]
		public static extern void GeomSetOffsetPosition(IntPtr geom, dReal x, dReal y, dReal z);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomSetOffsetQuaternion"), SuppressUnmanagedCodeSecurity]
		public static extern void GeomSetOffsetQuaternion(IntPtr geom, ref Quaternion Q);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomSetOffsetQuaternion"), SuppressUnmanagedCodeSecurity]
		public static extern void GeomSetOffsetQuaternion(IntPtr geom, ref dReal X);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomSetOffsetRotation"), SuppressUnmanagedCodeSecurity]
		public static extern void GeomSetOffsetRotation(IntPtr geom, ref Matrix3 R);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomSetOffsetRotation"), SuppressUnmanagedCodeSecurity]
		public static extern void GeomSetOffsetRotation(IntPtr geom, ref dReal M00);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomSetOffsetWorldPosition"), SuppressUnmanagedCodeSecurity]
		public static extern void GeomSetOffsetWorldPosition(IntPtr geom, dReal x, dReal y, dReal z);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomSetOffsetWorldQuaternion"), SuppressUnmanagedCodeSecurity]
		public static extern void GeomSetOffsetWorldQuaternion(IntPtr geom, ref Quaternion Q);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomSetOffsetWorldQuaternion"), SuppressUnmanagedCodeSecurity]
		public static extern void GeomSetOffsetWorldQuaternion(IntPtr geom, ref dReal X);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomSetOffsetWorldRotation"), SuppressUnmanagedCodeSecurity]
		public static extern void GeomSetOffsetWorldRotation(IntPtr geom, ref Matrix3 R);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomSetOffsetWorldRotation"), SuppressUnmanagedCodeSecurity]
		public static extern void GeomSetOffsetWorldRotation(IntPtr geom, ref dReal M00);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomSetPosition"), SuppressUnmanagedCodeSecurity]
		public static extern void GeomSetPosition(IntPtr geom, dReal x, dReal y, dReal z);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomSetQuaternion"), SuppressUnmanagedCodeSecurity]
		public static extern void GeomSetQuaternion(IntPtr geom, ref Quaternion quat);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomSetQuaternion"), SuppressUnmanagedCodeSecurity]
		public static extern void GeomSetQuaternion(IntPtr geom, ref dReal w);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomSetRotation"), SuppressUnmanagedCodeSecurity]
		public static extern void GeomSetRotation(IntPtr geom, ref Matrix3 R);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomSetRotation"), SuppressUnmanagedCodeSecurity]
		public static extern void GeomSetRotation(IntPtr geom, ref dReal M00);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomSphereGetRadius"), SuppressUnmanagedCodeSecurity]
		public static extern dReal GeomSphereGetRadius(IntPtr geom);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomSpherePointDepth"), SuppressUnmanagedCodeSecurity]
		public static extern dReal GeomSpherePointDepth(IntPtr geom, dReal x, dReal y, dReal z);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomSphereSetRadius"), SuppressUnmanagedCodeSecurity]
		public static extern void GeomSphereSetRadius(IntPtr geom, dReal radius);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomTransformGetCleanup"), SuppressUnmanagedCodeSecurity]
		public static extern int GeomTransformGetCleanup(IntPtr geom);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomTransformGetGeom"), SuppressUnmanagedCodeSecurity]
		public static extern IntPtr GeomTransformGetGeom(IntPtr geom);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomTransformGetInfo"), SuppressUnmanagedCodeSecurity]
		public static extern int GeomTransformGetInfo(IntPtr geom);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomTransformSetCleanup"), SuppressUnmanagedCodeSecurity]
		public static extern void GeomTransformSetCleanup(IntPtr geom, int mode);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomTransformSetGeom"), SuppressUnmanagedCodeSecurity]
		public static extern void GeomTransformSetGeom(IntPtr geom, IntPtr obj);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomTransformSetInfo"), SuppressUnmanagedCodeSecurity]
		public static extern void GeomTransformSetInfo(IntPtr geom, int info);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomTriMeshDataBuildDouble"), SuppressUnmanagedCodeSecurity]
		public static extern void GeomTriMeshDataBuildDouble(IntPtr d,
			double[] vertices, int vertexStride, int vertexCount,
			int[] indices, int indexCount, int triStride);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomTriMeshDataBuildDouble"), SuppressUnmanagedCodeSecurity]
		public static extern void GeomTriMeshDataBuildDouble(IntPtr d,
			IntPtr vertices, int vertexStride, int vertexCount,
			IntPtr indices, int indexCount, int triStride);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomTriMeshDataBuildDouble1"), SuppressUnmanagedCodeSecurity]
		public static extern void GeomTriMeshDataBuildDouble1(IntPtr d,
			double[] vertices, int vertexStride, int vertexCount,
			int[] indices, int indexCount, int triStride,
			double[] normals);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomTriMeshDataBuildDouble1"), SuppressUnmanagedCodeSecurity]
		public static extern void GeomTriMeshDataBuildDouble(IntPtr d,
			IntPtr vertices, int vertexStride, int vertexCount,
			IntPtr indices, int indexCount, int triStride,
			IntPtr normals);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomTriMeshDataBuildSimple"), SuppressUnmanagedCodeSecurity]
		public static extern void GeomTriMeshDataBuildSingle(IntPtr d,
			dReal[] vertices, int vertexStride, int vertexCount,
			int[] indices, int indexCount, int triStride);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomTriMeshDataBuildSimple"), SuppressUnmanagedCodeSecurity]
		public static extern void GeomTriMeshDataBuildSingle(IntPtr d,
			IntPtr vertices, int vertexStride, int vertexCount,
			IntPtr indices, int indexCount, int triStride);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomTriMeshDataBuildSimple1"), SuppressUnmanagedCodeSecurity]
		public static extern void GeomTriMeshDataBuildSingle1(IntPtr d,
			dReal[] vertices, int vertexStride, int vertexCount,
			int[] indices, int indexCount, int triStride,
			dReal[] normals);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomTriMeshDataBuildSimple1"), SuppressUnmanagedCodeSecurity]
		public static extern void GeomTriMeshDataBuildSingle1(IntPtr d,
			IntPtr vertices, int vertexStride, int vertexCount,
			IntPtr indices, int indexCount, int triStride,
			IntPtr normals);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomTriMeshDataBuildSingle"), SuppressUnmanagedCodeSecurity]
		public static extern void GeomTriMeshDataBuildSimple(IntPtr d, 
			float[] vertices, int vertexStride, int vertexCount,
			int[] indices, int indexCount, int triStride);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomTriMeshDataBuildSingle"), SuppressUnmanagedCodeSecurity]
		public static extern void GeomTriMeshDataBuildSimple(IntPtr d,
			IntPtr vertices, int vertexStride, int vertexCount,
			IntPtr indices, int indexCount, int triStride);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomTriMeshDataBuildSingle1"), SuppressUnmanagedCodeSecurity]
		public static extern void GeomTriMeshDataBuildSimple1(IntPtr d, 
			float[] vertices, int vertexStride, int vertexCount,
			int[] indices, int indexCount, int triStride,
			float[] normals);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomTriMeshDataBuildSingle1"), SuppressUnmanagedCodeSecurity]
		public static extern void GeomTriMeshDataBuildSimple1(IntPtr d,
			IntPtr vertices, int vertexStride, int vertexCount,
			IntPtr indices, int indexCount, int triStride,
			IntPtr normals);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomTriMeshClearTCCache"), SuppressUnmanagedCodeSecurity]
		public static extern void GeomTriMeshClearTCCache(IntPtr g);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomTriMeshDataCreate"), SuppressUnmanagedCodeSecurity]
		public static extern IntPtr GeomTriMeshDataCreate();

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomTriMeshDataDestroy"), SuppressUnmanagedCodeSecurity]
		public static extern void GeomTriMeshDataDestroy(IntPtr d);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomTriMeshDataGet"), SuppressUnmanagedCodeSecurity]
		public static extern IntPtr GeomTriMeshDataGet(IntPtr d, int data_id);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomTriMeshDataPreprocess"), SuppressUnmanagedCodeSecurity]
		public static extern void GeomTriMeshDataPreprocess(IntPtr d);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomTriMeshDataSet"), SuppressUnmanagedCodeSecurity]
		public static extern void GeomTriMeshDataSet(IntPtr d, int data_id, IntPtr in_data);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomTriMeshDataUpdate"), SuppressUnmanagedCodeSecurity]
		public static extern void GeomTriMeshDataUpdate(IntPtr d);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomTriMeshEnableTC"), SuppressUnmanagedCodeSecurity]
		public static extern void GeomTriMeshEnableTC(IntPtr g, int geomClass, bool enable);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomTriMeshGetArrayCallback"), SuppressUnmanagedCodeSecurity]
		public static extern TriArrayCallback GeomTriMeshGetArrayCallback(IntPtr g);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomTriMeshGetCallback"), SuppressUnmanagedCodeSecurity]
		public static extern TriCallback GeomTriMeshGetCallback(IntPtr g);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomTriMeshGetData"), SuppressUnmanagedCodeSecurity]
		public static extern IntPtr GeomTriMeshGetData(IntPtr g);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomTriMeshGetLastTransform"), SuppressUnmanagedCodeSecurity]
		public extern unsafe static Matrix4* GeomTriMeshGetLastTransformUnsafe(IntPtr geom);
		public static Matrix4 GeomTriMeshGetLastTransform(IntPtr geom)
		{
			unsafe { return *(GeomTriMeshGetLastTransformUnsafe(geom)); }
		}

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomTriMeshGetPoint"), SuppressUnmanagedCodeSecurity]
		public extern static void GeomTriMeshGetPoint(IntPtr g, int index, dReal u, dReal v, ref Vector3 outVec);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomTriMeshGetRayCallback"), SuppressUnmanagedCodeSecurity]
		public static extern TriRayCallback GeomTriMeshGetRayCallback(IntPtr g);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomTriMeshGetTriangle"), SuppressUnmanagedCodeSecurity]
		public extern static void GeomTriMeshGetTriangle(IntPtr g, int index, ref Vector3 v0, ref Vector3 v1, ref Vector3 v2);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomTriMeshGetTriangleCount"), SuppressUnmanagedCodeSecurity]
		public extern static int GeomTriMeshGetTriangleCount(IntPtr g);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomTriMeshGetTriMeshDataID"), SuppressUnmanagedCodeSecurity]
		public static extern IntPtr GeomTriMeshGetTriMeshDataID(IntPtr g);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomTriMeshIsTCEnabled"), SuppressUnmanagedCodeSecurity]
		public static extern bool GeomTriMeshIsTCEnabled(IntPtr g, int geomClass);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomTriMeshSetArrayCallback"), SuppressUnmanagedCodeSecurity]
		public static extern void GeomTriMeshSetArrayCallback(IntPtr g, TriArrayCallback arrayCallback);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomTriMeshSetCallback"), SuppressUnmanagedCodeSecurity]
		public static extern void GeomTriMeshSetCallback(IntPtr g, TriCallback callback);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomTriMeshSetData"), SuppressUnmanagedCodeSecurity]
		public static extern void GeomTriMeshSetData(IntPtr g, IntPtr data);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomTriMeshSetLastTransform"), SuppressUnmanagedCodeSecurity]
		public static extern void GeomTriMeshSetLastTransform(IntPtr g, ref Matrix4 last_trans);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomTriMeshSetLastTransform"), SuppressUnmanagedCodeSecurity]
		public static extern void GeomTriMeshSetLastTransform(IntPtr g, ref dReal M00);

        [DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGeomTriMeshSetRayCallback"), SuppressUnmanagedCodeSecurity]
        public static extern void GeomTriMeshSetRayCallback(IntPtr g, TriRayCallback callback);

        [DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dGetConfiguration"), SuppressUnmanagedCodeSecurity]
        public static extern IntPtr iGetConfiguration();

        public static string GetConfiguration()
        {
            IntPtr ptr = iGetConfiguration();
            string s = Marshal.PtrToStringAnsi(ptr);
            return s;
        }

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dHashSpaceCreate"), SuppressUnmanagedCodeSecurity]
		public static extern IntPtr HashSpaceCreate(IntPtr space);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dHashSpaceGetLevels"), SuppressUnmanagedCodeSecurity]
		public static extern void HashSpaceGetLevels(IntPtr space, out int minlevel, out int maxlevel);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dHashSpaceSetLevels"), SuppressUnmanagedCodeSecurity]
		public static extern void HashSpaceSetLevels(IntPtr space, int minlevel, int maxlevel);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dInfiniteAABB"), SuppressUnmanagedCodeSecurity]
		public static extern void InfiniteAABB(IntPtr geom, out AABB aabb);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dInitODE"), SuppressUnmanagedCodeSecurity]
		public static extern void InitODE();

        [DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dInitODE2"), SuppressUnmanagedCodeSecurity]
        public static extern int InitODE2(uint ODEInitFlags);

        [DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dIsPositiveDefinite"), SuppressUnmanagedCodeSecurity]
		public static extern int IsPositiveDefinite(ref dReal A, int n);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dInvertPDMatrix"), SuppressUnmanagedCodeSecurity]
		public static extern int InvertPDMatrix(ref dReal A, out dReal Ainv, int n);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dJointAddAMotorTorques"), SuppressUnmanagedCodeSecurity]
		public static extern void JointAddAMotorTorques(IntPtr joint, dReal torque1, dReal torque2, dReal torque3);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dJointAddHingeTorque"), SuppressUnmanagedCodeSecurity]
		public static extern void JointAddHingeTorque(IntPtr joint, dReal torque);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dJointAddHinge2Torque"), SuppressUnmanagedCodeSecurity]
		public static extern void JointAddHinge2Torques(IntPtr joint, dReal torque1, dReal torque2);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dJointAddPRTorque"), SuppressUnmanagedCodeSecurity]
		public static extern void JointAddPRTorque(IntPtr joint, dReal torque);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dJointAddUniversalTorque"), SuppressUnmanagedCodeSecurity]
		public static extern void JointAddUniversalTorques(IntPtr joint, dReal torque1, dReal torque2);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dJointAddSliderForce"), SuppressUnmanagedCodeSecurity]
		public static extern void JointAddSliderForce(IntPtr joint, dReal force);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dJointAttach"), SuppressUnmanagedCodeSecurity]
		public static extern void JointAttach(IntPtr joint, IntPtr body1, IntPtr body2);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dJointCreateAMotor"), SuppressUnmanagedCodeSecurity]
		public static extern IntPtr JointCreateAMotor(IntPtr world, IntPtr group);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dJointCreateBall"), SuppressUnmanagedCodeSecurity]
		public static extern IntPtr JointCreateBall(IntPtr world, IntPtr group);

        [DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dJointCreateContact"), SuppressUnmanagedCodeSecurity]
        public static extern IntPtr JointCreateContact(IntPtr world, IntPtr group, ref Contact contact);
        [DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dJointCreateContact"), SuppressUnmanagedCodeSecurity]
        public static extern IntPtr JointCreateContactPtr(IntPtr world, IntPtr group, IntPtr contact);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dJointCreateFixed"), SuppressUnmanagedCodeSecurity]
		public static extern IntPtr JointCreateFixed(IntPtr world, IntPtr group);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dJointCreateHinge"), SuppressUnmanagedCodeSecurity]
		public static extern IntPtr JointCreateHinge(IntPtr world, IntPtr group);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dJointCreateHinge2"), SuppressUnmanagedCodeSecurity]
		public static extern IntPtr JointCreateHinge2(IntPtr world, IntPtr group);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dJointCreateLMotor"), SuppressUnmanagedCodeSecurity]
		public static extern IntPtr JointCreateLMotor(IntPtr world, IntPtr group);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dJointCreateNull"), SuppressUnmanagedCodeSecurity]
		public static extern IntPtr JointCreateNull(IntPtr world, IntPtr group);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dJointCreatePR"), SuppressUnmanagedCodeSecurity]
		public static extern IntPtr JointCreatePR(IntPtr world, IntPtr group);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dJointCreatePlane2D"), SuppressUnmanagedCodeSecurity]
		public static extern IntPtr JointCreatePlane2D(IntPtr world, IntPtr group);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dJointCreateSlider"), SuppressUnmanagedCodeSecurity]
		public static extern IntPtr JointCreateSlider(IntPtr world, IntPtr group);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dJointCreateUniversal"), SuppressUnmanagedCodeSecurity]
		public static extern IntPtr JointCreateUniversal(IntPtr world, IntPtr group);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dJointDestroy"), SuppressUnmanagedCodeSecurity]
		public static extern void JointDestroy(IntPtr j);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dJointGetAMotorAngle"), SuppressUnmanagedCodeSecurity]
		public static extern dReal JointGetAMotorAngle(IntPtr j, int anum);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dJointGetAMotorAngleRate"), SuppressUnmanagedCodeSecurity]
		public static extern dReal JointGetAMotorAngleRate(IntPtr j, int anum);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dJointGetAMotorAxis"), SuppressUnmanagedCodeSecurity]
		public static extern void JointGetAMotorAxis(IntPtr j, int anum, out Vector3 result);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dJointGetAMotorAxisRel"), SuppressUnmanagedCodeSecurity]
		public static extern int JointGetAMotorAxisRel(IntPtr j, int anum);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dJointGetAMotorMode"), SuppressUnmanagedCodeSecurity]
		public static extern int JointGetAMotorMode(IntPtr j);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dJointGetAMotorNumAxes"), SuppressUnmanagedCodeSecurity]
		public static extern int JointGetAMotorNumAxes(IntPtr j);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dJointGetAMotorParam"), SuppressUnmanagedCodeSecurity]
		public static extern dReal JointGetAMotorParam(IntPtr j, int parameter);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dJointGetBallAnchor"), SuppressUnmanagedCodeSecurity]
		public static extern void JointGetBallAnchor(IntPtr j, out Vector3 result);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dJointGetBallAnchor2"), SuppressUnmanagedCodeSecurity]
		public static extern void JointGetBallAnchor2(IntPtr j, out Vector3 result);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dJointGetBody"), SuppressUnmanagedCodeSecurity]
		public static extern IntPtr JointGetBody(IntPtr j);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dJointGetData"), SuppressUnmanagedCodeSecurity]
		public static extern IntPtr JointGetData(IntPtr j);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dJointGetFeedback"), SuppressUnmanagedCodeSecurity]
		public extern unsafe static JointFeedback* JointGetFeedbackUnsafe(IntPtr j);
		public static JointFeedback JointGetFeedback(IntPtr j)
		{
			unsafe { return *(JointGetFeedbackUnsafe(j)); }
		}

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dJointGetHingeAnchor"), SuppressUnmanagedCodeSecurity]
		public static extern void JointGetHingeAnchor(IntPtr j, out Vector3 result);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dJointGetHingeAngle"), SuppressUnmanagedCodeSecurity]
		public static extern dReal JointGetHingeAngle(IntPtr j);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dJointGetHingeAngleRate"), SuppressUnmanagedCodeSecurity]
		public static extern dReal JointGetHingeAngleRate(IntPtr j);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dJointGetHingeAxis"), SuppressUnmanagedCodeSecurity]
		public static extern void JointGetHingeAxis(IntPtr j, out Vector3 result);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dJointGetHingeParam"), SuppressUnmanagedCodeSecurity]
		public static extern dReal JointGetHingeParam(IntPtr j, int parameter);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dJointGetHinge2Angle1"), SuppressUnmanagedCodeSecurity]
		public static extern dReal JointGetHinge2Angle1(IntPtr j);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dJointGetHinge2Angle1Rate"), SuppressUnmanagedCodeSecurity]
		public static extern dReal JointGetHinge2Angle1Rate(IntPtr j);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dJointGetHinge2Angle2Rate"), SuppressUnmanagedCodeSecurity]
		public static extern dReal JointGetHinge2Angle2Rate(IntPtr j);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dJointGetHingeAnchor2"), SuppressUnmanagedCodeSecurity]
		public static extern void JointGetHingeAnchor2(IntPtr j, out Vector3 result);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dJointGetHinge2Anchor"), SuppressUnmanagedCodeSecurity]
		public static extern void JointGetHinge2Anchor(IntPtr j, out Vector3 result);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dJointGetHinge2Anchor2"), SuppressUnmanagedCodeSecurity]
		public static extern void JointGetHinge2Anchor2(IntPtr j, out Vector3 result);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dJointGetHinge2Axis1"), SuppressUnmanagedCodeSecurity]
		public static extern void JointGetHinge2Axis1(IntPtr j, out Vector3 result);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dJointGetHinge2Axis2"), SuppressUnmanagedCodeSecurity]
		public static extern void JointGetHinge2Axis2(IntPtr j, out Vector3 result);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dJointGetHinge2Param"), SuppressUnmanagedCodeSecurity]
		public static extern dReal JointGetHinge2Param(IntPtr j, int parameter);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dJointGetLMotorAxis"), SuppressUnmanagedCodeSecurity]
		public static extern void JointGetLMotorAxis(IntPtr j, int anum, out Vector3 result);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dJointGetLMotorNumAxes"), SuppressUnmanagedCodeSecurity]
		public static extern int JointGetLMotorNumAxes(IntPtr j);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dJointGetLMotorParam"), SuppressUnmanagedCodeSecurity]
		public static extern dReal JointGetLMotorParam(IntPtr j, int parameter);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dJointGetPRAnchor"), SuppressUnmanagedCodeSecurity]
		public static extern void JointGetPRAnchor(IntPtr j, out Vector3 result);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dJointGetPRAxis1"), SuppressUnmanagedCodeSecurity]
		public static extern void JointGetPRAxis1(IntPtr j, out Vector3 result);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dJointGetPRAxis2"), SuppressUnmanagedCodeSecurity]
		public static extern void JointGetPRAxis2(IntPtr j, out Vector3 result);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dJointGetPRParam"), SuppressUnmanagedCodeSecurity]
		public static extern dReal JointGetPRParam(IntPtr j, int parameter);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dJointGetPRPosition"), SuppressUnmanagedCodeSecurity]
		public static extern dReal JointGetPRPosition(IntPtr j);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dJointGetPRPositionRate"), SuppressUnmanagedCodeSecurity]
		public static extern dReal JointGetPRPositionRate(IntPtr j);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dJointGetSliderAxis"), SuppressUnmanagedCodeSecurity]
		public static extern void JointGetSliderAxis(IntPtr j, out Vector3 result);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dJointGetSliderParam"), SuppressUnmanagedCodeSecurity]
		public static extern dReal JointGetSliderParam(IntPtr j, int parameter);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dJointGetSliderPosition"), SuppressUnmanagedCodeSecurity]
		public static extern dReal JointGetSliderPosition(IntPtr j);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dJointGetSliderPositionRate"), SuppressUnmanagedCodeSecurity]
		public static extern dReal JointGetSliderPositionRate(IntPtr j);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dJointGetType"), SuppressUnmanagedCodeSecurity]
		public static extern JointType JointGetType(IntPtr j);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dJointGetUniversalAnchor"), SuppressUnmanagedCodeSecurity]
		public static extern void JointGetUniversalAnchor(IntPtr j, out Vector3 result);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dJointGetUniversalAnchor2"), SuppressUnmanagedCodeSecurity]
		public static extern void JointGetUniversalAnchor2(IntPtr j, out Vector3 result);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dJointGetUniversalAngle1"), SuppressUnmanagedCodeSecurity]
		public static extern dReal JointGetUniversalAngle1(IntPtr j);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dJointGetUniversalAngle1Rate"), SuppressUnmanagedCodeSecurity]
		public static extern dReal JointGetUniversalAngle1Rate(IntPtr j);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dJointGetUniversalAngle2"), SuppressUnmanagedCodeSecurity]
		public static extern dReal JointGetUniversalAngle2(IntPtr j);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dJointGetUniversalAngle2Rate"), SuppressUnmanagedCodeSecurity]
		public static extern dReal JointGetUniversalAngle2Rate(IntPtr j);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dJointGetUniversalAngles"), SuppressUnmanagedCodeSecurity]
		public static extern void JointGetUniversalAngles(IntPtr j, out dReal angle1, out dReal angle2);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dJointGetUniversalAxis1"), SuppressUnmanagedCodeSecurity]
		public static extern void JointGetUniversalAxis1(IntPtr j, out Vector3 result);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dJointGetUniversalAxis2"), SuppressUnmanagedCodeSecurity]
		public static extern void JointGetUniversalAxis2(IntPtr j, out Vector3 result);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dJointGetUniversalParam"), SuppressUnmanagedCodeSecurity]
		public static extern dReal JointGetUniversalParam(IntPtr j, int parameter);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dJointGroupCreate"), SuppressUnmanagedCodeSecurity]
		public static extern IntPtr JointGroupCreate(int max_size);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dJointGroupDestroy"), SuppressUnmanagedCodeSecurity]
		public static extern void JointGroupDestroy(IntPtr group);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dJointGroupEmpty"), SuppressUnmanagedCodeSecurity]
		public static extern void JointGroupEmpty(IntPtr group);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dJointSetAMotorAngle"), SuppressUnmanagedCodeSecurity]
		public static extern void JointSetAMotorAngle(IntPtr j, int anum, dReal angle);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dJointSetAMotorAxis"), SuppressUnmanagedCodeSecurity]
		public static extern void JointSetAMotorAxis(IntPtr j, int anum, int rel, dReal x, dReal y, dReal z);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dJointSetAMotorMode"), SuppressUnmanagedCodeSecurity]
		public static extern void JointSetAMotorMode(IntPtr j, int mode);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dJointSetAMotorNumAxes"), SuppressUnmanagedCodeSecurity]
		public static extern void JointSetAMotorNumAxes(IntPtr group, int num);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dJointSetAMotorParam"), SuppressUnmanagedCodeSecurity]
		public static extern void JointSetAMotorParam(IntPtr group, int parameter, dReal value);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dJointSetBallAnchor"), SuppressUnmanagedCodeSecurity]
		public static extern void JointSetBallAnchor(IntPtr j, dReal x, dReal y, dReal z);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dJointSetBallAnchor2"), SuppressUnmanagedCodeSecurity]
		public static extern void JointSetBallAnchor2(IntPtr j, dReal x, dReal y, dReal z);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dJointSetData"), SuppressUnmanagedCodeSecurity]
		public static extern void JointSetData(IntPtr j, IntPtr data);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dJointSetFeedback"), SuppressUnmanagedCodeSecurity]
		public static extern void JointSetFeedback(IntPtr j, out JointFeedback feedback);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dJointSetFixed"), SuppressUnmanagedCodeSecurity]
		public static extern void JointSetFixed(IntPtr j);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dJointSetHingeAnchor"), SuppressUnmanagedCodeSecurity]
		public static extern void JointSetHingeAnchor(IntPtr j, dReal x, dReal y, dReal z);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dJointSetHingeAnchorDelta"), SuppressUnmanagedCodeSecurity]
		public static extern void JointSetHingeAnchorDelta(IntPtr j, dReal x, dReal y, dReal z, dReal ax, dReal ay, dReal az);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dJointSetHingeAxis"), SuppressUnmanagedCodeSecurity]
		public static extern void JointSetHingeAxis(IntPtr j, dReal x, dReal y, dReal z);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dJointSetHingeParam"), SuppressUnmanagedCodeSecurity]
		public static extern void JointSetHingeParam(IntPtr j, int parameter, dReal value);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dJointSetHinge2Anchor"), SuppressUnmanagedCodeSecurity]
		public static extern void JointSetHinge2Anchor(IntPtr j, dReal x, dReal y, dReal z);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dJointSetHinge2Axis1"), SuppressUnmanagedCodeSecurity]
		public static extern void JointSetHinge2Axis1(IntPtr j, dReal x, dReal y, dReal z);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dJointSetHinge2Axis2"), SuppressUnmanagedCodeSecurity]
		public static extern void JointSetHinge2Axis2(IntPtr j, dReal x, dReal y, dReal z);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dJointSetHinge2Param"), SuppressUnmanagedCodeSecurity]
		public static extern void JointSetHinge2Param(IntPtr j, int parameter, dReal value);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dJointSetLMotorAxis"), SuppressUnmanagedCodeSecurity]
		public static extern void JointSetLMotorAxis(IntPtr j, int anum, int rel, dReal x, dReal y, dReal z);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dJointSetLMotorNumAxes"), SuppressUnmanagedCodeSecurity]
		public static extern void JointSetLMotorNumAxes(IntPtr j, int num);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dJointSetLMotorParam"), SuppressUnmanagedCodeSecurity]
		public static extern void JointSetLMotorParam(IntPtr j, int parameter, dReal value);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dJointSetPlane2DAngleParam"), SuppressUnmanagedCodeSecurity]
		public static extern void JointSetPlane2DAngleParam(IntPtr j, int parameter, dReal value);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dJointSetPlane2DXParam"), SuppressUnmanagedCodeSecurity]
		public static extern void JointSetPlane2DXParam(IntPtr j, int parameter, dReal value);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dJointSetPlane2DYParam"), SuppressUnmanagedCodeSecurity]
		public static extern void JointSetPlane2DYParam(IntPtr j, int parameter, dReal value);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dJointSetPRAnchor"), SuppressUnmanagedCodeSecurity]
		public static extern void JointSetPRAnchor(IntPtr j, dReal x, dReal y, dReal z);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dJointSetPRAxis1"), SuppressUnmanagedCodeSecurity]
		public static extern void JointSetPRAxis1(IntPtr j, dReal x, dReal y, dReal z);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dJointSetPRAxis2"), SuppressUnmanagedCodeSecurity]
		public static extern void JointSetPRAxis2(IntPtr j, dReal x, dReal y, dReal z);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dJointSetPRParam"), SuppressUnmanagedCodeSecurity]
		public static extern void JointSetPRParam(IntPtr j, int parameter, dReal value);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dJointSetSliderAxis"), SuppressUnmanagedCodeSecurity]
		public static extern void JointSetSliderAxis(IntPtr j, dReal x, dReal y, dReal z);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dJointSetSliderAxisDelta"), SuppressUnmanagedCodeSecurity]
		public static extern void JointSetSliderAxisDelta(IntPtr j, dReal x, dReal y, dReal z, dReal ax, dReal ay, dReal az);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dJointSetSliderParam"), SuppressUnmanagedCodeSecurity]
		public static extern void JointSetSliderParam(IntPtr j, int parameter, dReal value);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dJointSetUniversalAnchor"), SuppressUnmanagedCodeSecurity]
		public static extern void JointSetUniversalAnchor(IntPtr j, dReal x, dReal y, dReal z);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dJointSetUniversalAxis1"), SuppressUnmanagedCodeSecurity]
		public static extern void JointSetUniversalAxis1(IntPtr j, dReal x, dReal y, dReal z);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dJointSetUniversalAxis2"), SuppressUnmanagedCodeSecurity]
		public static extern void JointSetUniversalAxis2(IntPtr j, dReal x, dReal y, dReal z);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dJointSetUniversalParam"), SuppressUnmanagedCodeSecurity]
		public static extern void JointSetUniversalParam(IntPtr j, int parameter, dReal value);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dLDLTAddTL"), SuppressUnmanagedCodeSecurity]
		public static extern void LDLTAddTL(ref dReal L, ref dReal d, ref dReal a, int n, int nskip);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dMassAdd"), SuppressUnmanagedCodeSecurity]
		public static extern void MassAdd(ref Mass a, ref Mass b);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dMassAdjust"), SuppressUnmanagedCodeSecurity]
		public static extern void MassAdjust(ref Mass m, dReal newmass);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dMassCheck"), SuppressUnmanagedCodeSecurity]
		public static extern bool MassCheck(ref Mass m);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dMassRotate"), SuppressUnmanagedCodeSecurity]
		public static extern void MassRotate(ref Mass mass, ref Matrix3 R);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dMassRotate"), SuppressUnmanagedCodeSecurity]
		public static extern void MassRotate(ref Mass mass, ref dReal M00);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dMassSetBox"), SuppressUnmanagedCodeSecurity]
		public static extern void MassSetBox(out Mass mass, dReal density, dReal lx, dReal ly, dReal lz);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dMassSetBoxTotal"), SuppressUnmanagedCodeSecurity]
		public static extern void MassSetBoxTotal(out Mass mass, dReal total_mass, dReal lx, dReal ly, dReal lz);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dMassSetCapsule"), SuppressUnmanagedCodeSecurity]
		public static extern void MassSetCapsule(out Mass mass, dReal density, int direction, dReal radius, dReal length);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dMassSetCapsuleTotal"), SuppressUnmanagedCodeSecurity]
		public static extern void MassSetCapsuleTotal(out Mass mass, dReal total_mass, int direction, dReal radius, dReal length);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dMassSetCylinder"), SuppressUnmanagedCodeSecurity]
		public static extern void MassSetCylinder(out Mass mass, dReal density, int direction, dReal radius, dReal length);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dMassSetCylinderTotal"), SuppressUnmanagedCodeSecurity]
		public static extern void MassSetCylinderTotal(out Mass mass, dReal total_mass, int direction, dReal radius, dReal length);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dMassSetParameters"), SuppressUnmanagedCodeSecurity]
		public static extern void MassSetParameters(out Mass mass, dReal themass,
			 dReal cgx, dReal cgy, dReal cgz,
			 dReal i11, dReal i22, dReal i33,
			 dReal i12, dReal i13, dReal i23);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dMassSetSphere"), SuppressUnmanagedCodeSecurity]
		public static extern void MassSetSphere(out Mass mass, dReal density, dReal radius);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dMassSetSphereTotal"), SuppressUnmanagedCodeSecurity]
		public static extern void dMassSetSphereTotal(out Mass mass, dReal total_mass, dReal radius);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dMassSetTrimesh"), SuppressUnmanagedCodeSecurity]
		public static extern void MassSetTrimesh(out Mass mass, dReal density, IntPtr g);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dMassSetZero"), SuppressUnmanagedCodeSecurity]
		public static extern void MassSetZero(out Mass mass);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dMassTranslate"), SuppressUnmanagedCodeSecurity]
		public static extern void MassTranslate(ref Mass mass, dReal x, dReal y, dReal z);

        [DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dMultiply0"), SuppressUnmanagedCodeSecurity]
        public static extern void Multiply0(out dReal A00, ref dReal B00, ref dReal C00, int p, int q, int r);

        [DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dMultiply0"), SuppressUnmanagedCodeSecurity]
        private static extern void MultiplyiM3V3(out Vector3 vout, ref Matrix3 matrix, ref Vector3 vect,int p, int q, int r);
        public static void MultiplyM3V3(out Vector3 outvector, ref Matrix3 matrix, ref Vector3 invector)
        {
            MultiplyiM3V3(out outvector, ref matrix, ref invector, 3, 3, 1);
        }

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dMultiply1"), SuppressUnmanagedCodeSecurity]
		public static extern void Multiply1(out dReal A00, ref dReal B00, ref dReal C00, int p, int q, int r);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dMultiply2"), SuppressUnmanagedCodeSecurity]
		public static extern void Multiply2(out dReal A00, ref dReal B00, ref dReal C00, int p, int q, int r);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dQFromAxisAndAngle"), SuppressUnmanagedCodeSecurity]
		public static extern void QFromAxisAndAngle(out Quaternion q, dReal ax, dReal ay, dReal az, dReal angle);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dQfromR"), SuppressUnmanagedCodeSecurity]
		public static extern void QfromR(out Quaternion q, ref Matrix3 R);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dQMultiply0"), SuppressUnmanagedCodeSecurity]
		public static extern void QMultiply0(out Quaternion qa, ref Quaternion qb, ref Quaternion qc);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dQMultiply1"), SuppressUnmanagedCodeSecurity]
		public static extern void QMultiply1(out Quaternion qa, ref Quaternion qb, ref Quaternion qc);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dQMultiply2"), SuppressUnmanagedCodeSecurity]
		public static extern void QMultiply2(out Quaternion qa, ref Quaternion qb, ref Quaternion qc);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dQMultiply3"), SuppressUnmanagedCodeSecurity]
		public static extern void QMultiply3(out Quaternion qa, ref Quaternion qb, ref Quaternion qc);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dQSetIdentity"), SuppressUnmanagedCodeSecurity]
		public static extern void QSetIdentity(out Quaternion q);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dQuadTreeSpaceCreate"), SuppressUnmanagedCodeSecurity]
		public static extern IntPtr QuadTreeSpaceCreate(IntPtr space, ref Vector3 center, ref Vector3 extents, int depth);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dQuadTreeSpaceCreate"), SuppressUnmanagedCodeSecurity]
		public static extern IntPtr QuadTreeSpaceCreate(IntPtr space, ref dReal centerX, ref dReal extentsX, int depth);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dRandReal"), SuppressUnmanagedCodeSecurity]
		public static extern dReal RandReal();

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dRFrom2Axes"), SuppressUnmanagedCodeSecurity]
		public static extern void RFrom2Axes(out Matrix3 R, dReal ax, dReal ay, dReal az, dReal bx, dReal by, dReal bz);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dRFromAxisAndAngle"), SuppressUnmanagedCodeSecurity]
		public static extern void RFromAxisAndAngle(out Matrix3 R, dReal x, dReal y, dReal z, dReal angle);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dRFromEulerAngles"), SuppressUnmanagedCodeSecurity]
		public static extern void RFromEulerAngles(out Matrix3 R, dReal phi, dReal theta, dReal psi);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dRfromQ"), SuppressUnmanagedCodeSecurity]
		public static extern void RfromQ(out Matrix3 R, ref Quaternion q);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dRFromZAxis"), SuppressUnmanagedCodeSecurity]
		public static extern void RFromZAxis(out Matrix3 R, dReal ax, dReal ay, dReal az);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dRSetIdentity"), SuppressUnmanagedCodeSecurity]
		public static extern void RSetIdentity(out Matrix3 R);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dSetValue"), SuppressUnmanagedCodeSecurity]
		public static extern void SetValue(out dReal a, int n);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dSetZero"), SuppressUnmanagedCodeSecurity]
		public static extern void SetZero(out dReal a, int n);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dSimpleSpaceCreate"), SuppressUnmanagedCodeSecurity]
		public static extern IntPtr SimpleSpaceCreate(IntPtr space);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dSolveCholesky"), SuppressUnmanagedCodeSecurity]
		public static extern void SolveCholesky(ref dReal L, out dReal b, int n);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dSolveL1"), SuppressUnmanagedCodeSecurity]
		public static extern void SolveL1(ref dReal L, out dReal b, int n, int nskip);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dSolveL1T"), SuppressUnmanagedCodeSecurity]
		public static extern void SolveL1T(ref dReal L, out dReal b, int n, int nskip);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dSolveLDLT"), SuppressUnmanagedCodeSecurity]
		public static extern void SolveLDLT(ref dReal L, ref dReal d, out dReal b, int n, int nskip);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dSpaceAdd"), SuppressUnmanagedCodeSecurity]
		public static extern void SpaceAdd(IntPtr space, IntPtr geom);

        [DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dSpaceLockQuery"), SuppressUnmanagedCodeSecurity]
        public static extern bool SpaceLockQuery(IntPtr space);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dSpaceClean"), SuppressUnmanagedCodeSecurity]
		public static extern void SpaceClean(IntPtr space);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dSpaceCollide"), SuppressUnmanagedCodeSecurity]
		public static extern void SpaceCollide(IntPtr space, IntPtr data, NearCallback callback);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dSpaceCollide2"), SuppressUnmanagedCodeSecurity]
		public static extern void SpaceCollide2(IntPtr space1, IntPtr space2, IntPtr data, NearCallback callback);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dSpaceDestroy"), SuppressUnmanagedCodeSecurity]
		public static extern void SpaceDestroy(IntPtr space);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dSpaceGetCleanup"), SuppressUnmanagedCodeSecurity]
		public static extern bool SpaceGetCleanup(IntPtr space);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dSpaceGetNumGeoms"), SuppressUnmanagedCodeSecurity]
		public static extern int SpaceGetNumGeoms(IntPtr space);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dSpaceGetGeom"), SuppressUnmanagedCodeSecurity]
		public static extern IntPtr SpaceGetGeom(IntPtr space, int i);

        [DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dSpaceGetSublevel"), SuppressUnmanagedCodeSecurity]
        public static extern int SpaceGetSublevel(IntPtr space);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dSpaceQuery"), SuppressUnmanagedCodeSecurity]
		public static extern bool SpaceQuery(IntPtr space, IntPtr geom);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dSpaceRemove"), SuppressUnmanagedCodeSecurity]
		public static extern void SpaceRemove(IntPtr space, IntPtr geom);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dSpaceSetCleanup"), SuppressUnmanagedCodeSecurity]
		public static extern void SpaceSetCleanup(IntPtr space, bool mode);

        [DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dSpaceSetSublevel"), SuppressUnmanagedCodeSecurity]
        public static extern void SpaceSetSublevel(IntPtr space, int sublevel);

        [DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dSweepAndPruneSpaceCreate"), SuppressUnmanagedCodeSecurity]
        public static extern IntPtr SweepAndPruneSpaceCreate(IntPtr space, int AxisOrder);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dVectorScale"), SuppressUnmanagedCodeSecurity]
		public static extern void VectorScale(out dReal a, ref dReal d, int n);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dWorldCreate"), SuppressUnmanagedCodeSecurity]
		public static extern IntPtr WorldCreate();

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dWorldDestroy"), SuppressUnmanagedCodeSecurity]
		public static extern void WorldDestroy(IntPtr world);

        [DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dWorldGetAutoDisableAverageSamplesCount"), SuppressUnmanagedCodeSecurity]
        public static extern int WorldGetAutoDisableAverageSamplesCount(IntPtr world);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dWorldGetAutoDisableAngularThreshold"), SuppressUnmanagedCodeSecurity]
		public static extern dReal WorldGetAutoDisableAngularThreshold(IntPtr world);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dWorldGetAutoDisableFlag"), SuppressUnmanagedCodeSecurity]
		public static extern bool WorldGetAutoDisableFlag(IntPtr world);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dWorldGetAutoDisableLinearThreshold"), SuppressUnmanagedCodeSecurity]
		public static extern dReal WorldGetAutoDisableLinearThreshold(IntPtr world);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dWorldGetAutoDisableSteps"), SuppressUnmanagedCodeSecurity]
		public static extern int WorldGetAutoDisableSteps(IntPtr world);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dWorldGetAutoDisableTime"), SuppressUnmanagedCodeSecurity]
		public static extern dReal WorldGetAutoDisableTime(IntPtr world);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dWorldGetAutoEnableDepthSF1"), SuppressUnmanagedCodeSecurity]
		public static extern int WorldGetAutoEnableDepthSF1(IntPtr world);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dWorldGetCFM"), SuppressUnmanagedCodeSecurity]
		public static extern dReal WorldGetCFM(IntPtr world);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dWorldGetERP"), SuppressUnmanagedCodeSecurity]
		public static extern dReal WorldGetERP(IntPtr world);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dWorldGetGravity"), SuppressUnmanagedCodeSecurity]
		public static extern void WorldGetGravity(IntPtr world, out Vector3 gravity);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dWorldGetGravity"), SuppressUnmanagedCodeSecurity]
		public static extern void WorldGetGravity(IntPtr world, out dReal X);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dWorldGetContactMaxCorrectingVel"), SuppressUnmanagedCodeSecurity]
		public static extern dReal WorldGetContactMaxCorrectingVel(IntPtr world);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dWorldGetContactSurfaceLayer"), SuppressUnmanagedCodeSecurity]
		public static extern dReal WorldGetContactSurfaceLayer(IntPtr world);

        [DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dWorldGetAngularDamping"), SuppressUnmanagedCodeSecurity]
        public static extern dReal WorldGetAngularDamping(IntPtr world);

        [DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dWorldGetAngularDampingThreshold"), SuppressUnmanagedCodeSecurity]
        public static extern dReal WorldGetAngularDampingThreshold(IntPtr world);

        [DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dWorldGetLinearDamping"), SuppressUnmanagedCodeSecurity]
        public static extern dReal WorldGetLinearDamping(IntPtr world);

        [DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dWorldGetLinearDampingThreshold"), SuppressUnmanagedCodeSecurity]
        public static extern dReal WorldGetLinearDampingThreshold(IntPtr world);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dWorldGetQuickStepNumIterations"), SuppressUnmanagedCodeSecurity]
		public static extern int WorldGetQuickStepNumIterations(IntPtr world);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dWorldGetQuickStepW"), SuppressUnmanagedCodeSecurity]
		public static extern dReal WorldGetQuickStepW(IntPtr world);

        [DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dWorldGetMaxAngularSpeed"), SuppressUnmanagedCodeSecurity]
        public static extern dReal WorldGetMaxAngularSpeed(IntPtr world);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dWorldImpulseToForce"), SuppressUnmanagedCodeSecurity]
		public static extern void WorldImpulseToForce(IntPtr world, dReal stepsize, dReal ix, dReal iy, dReal iz, out Vector3 force);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dWorldImpulseToForce"), SuppressUnmanagedCodeSecurity]
		public static extern void WorldImpulseToForce(IntPtr world, dReal stepsize, dReal ix, dReal iy, dReal iz, out dReal forceX);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dWorldQuickStep"), SuppressUnmanagedCodeSecurity]
		public static extern void WorldQuickStep(IntPtr world, dReal stepsize);

        [DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dWorldSetAngularDamping"), SuppressUnmanagedCodeSecurity]
        public static extern void WorldSetAngularDamping(IntPtr world, dReal scale);

        [DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dWorldSetAngularDampingThreshold"), SuppressUnmanagedCodeSecurity]
        public static extern void WorldSetAngularDampingThreshold(IntPtr world, dReal threshold);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dWorldSetAutoDisableAngularThreshold"), SuppressUnmanagedCodeSecurity]
		public static extern void WorldSetAutoDisableAngularThreshold(IntPtr world, dReal angular_threshold);

        [DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dWorldSetAutoDisableAverageSamplesCount"), SuppressUnmanagedCodeSecurity]
        public static extern void WorldSetAutoDisableAverageSamplesCount(IntPtr world, int average_samples_count);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dWorldSetAutoDisableFlag"), SuppressUnmanagedCodeSecurity]
		public static extern void WorldSetAutoDisableFlag(IntPtr world, bool do_auto_disable);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dWorldSetAutoDisableLinearThreshold"), SuppressUnmanagedCodeSecurity]
		public static extern void WorldSetAutoDisableLinearThreshold(IntPtr world, dReal linear_threshold);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dWorldSetAutoDisableSteps"), SuppressUnmanagedCodeSecurity]
		public static extern void WorldSetAutoDisableSteps(IntPtr world, int steps);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dWorldSetAutoDisableTime"), SuppressUnmanagedCodeSecurity]
		public static extern void WorldSetAutoDisableTime(IntPtr world, dReal time);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dWorldSetAutoEnableDepthSF1"), SuppressUnmanagedCodeSecurity]
		public static extern void WorldSetAutoEnableDepthSF1(IntPtr world, int autoEnableDepth);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dWorldSetCFM"), SuppressUnmanagedCodeSecurity]
		public static extern void WorldSetCFM(IntPtr world, dReal cfm);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dWorldSetContactMaxCorrectingVel"), SuppressUnmanagedCodeSecurity]
		public static extern void WorldSetContactMaxCorrectingVel(IntPtr world, dReal vel);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dWorldSetContactSurfaceLayer"), SuppressUnmanagedCodeSecurity]
		public static extern void WorldSetContactSurfaceLayer(IntPtr world, dReal depth);

        [DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dWorldSetDamping"), SuppressUnmanagedCodeSecurity]
        public static extern void WorldSetDamping(IntPtr world, dReal linear_scale, dReal angular_scale);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dWorldSetERP"), SuppressUnmanagedCodeSecurity]
		public static extern void WorldSetERP(IntPtr world, dReal erp);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dWorldSetGravity"), SuppressUnmanagedCodeSecurity]
		public static extern void WorldSetGravity(IntPtr world, dReal x, dReal y, dReal z);
        
        [DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dWorldSetLinearDamping"), SuppressUnmanagedCodeSecurity]
        public static extern void WorldSetLinearDamping(IntPtr world, dReal scale);

        [DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dWorldSetLinearDampingThreshold"), SuppressUnmanagedCodeSecurity]
        public static extern void WorldSetLinearDampingThreshold(IntPtr world, dReal threshold);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dWorldSetQuickStepNumIterations"), SuppressUnmanagedCodeSecurity]
		public static extern void WorldSetQuickStepNumIterations(IntPtr world, int num);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dWorldSetQuickStepW"), SuppressUnmanagedCodeSecurity]
		public static extern void WorldSetQuickStepW(IntPtr world, dReal over_relaxation);

        [DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dWorldSetMaxAngularSpeed"), SuppressUnmanagedCodeSecurity]
        public static extern void WorldSetMaxAngularSpeed(IntPtr world, dReal max_speed);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dWorldStep"), SuppressUnmanagedCodeSecurity]
		public static extern void WorldStep(IntPtr world, dReal stepsize);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dWorldStepFast1"), SuppressUnmanagedCodeSecurity]
		public static extern void WorldStepFast1(IntPtr world, dReal stepsize, int maxiterations);

		[DllImport("ode", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dWorldExportDIF"), SuppressUnmanagedCodeSecurity]
		public static extern void WorldExportDIF(IntPtr world, string filename, bool append, string prefix);
	}
}
