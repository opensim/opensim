using System;
using System.Runtime.InteropServices;
using Ode.NET;

namespace Drawstuff.NET
{
#if dDOUBLE
	using dReal = System.Double;
#else
	using dReal = System.Single;
#endif

	public static class ds
	{
		public const int VERSION = 2;

		public enum Texture
		{
			None,
			Wood
		}

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		public delegate void CallbackFunction(int arg);

		[StructLayout(LayoutKind.Sequential)]
		public struct Functions
		{
			public int version;
			public CallbackFunction start;
			public CallbackFunction step;
			public CallbackFunction command;
			public CallbackFunction stop;
			public string path_to_textures;
		}

		[DllImport("drawstuff", EntryPoint="dsDrawBox")]
		public static extern void DrawBox(ref d.Vector3 pos, ref d.Matrix3 R, ref d.Vector3 sides);

		[DllImport("drawstuff", EntryPoint = "dsDrawCapsule")]
		public static extern void DrawCapsule(ref d.Vector3 pos, ref d.Matrix3 R, dReal length, dReal radius);

		[DllImport("drawstuff", EntryPoint = "dsDrawConvex")]
		public static extern void DrawConvex(ref d.Vector3 pos, ref d.Matrix3 R, dReal[] planes, int planeCount, dReal[] points, int pointCount, int[] polygons);

		[DllImport("drawstuff", EntryPoint="dsSetColor")]
		public static extern void SetColor(float red, float green, float blue);

		[DllImport("drawstuff", EntryPoint="dsSetTexture")]
		public static extern void SetTexture(Texture texture);

		[DllImport("drawstuff", EntryPoint="dsSetViewpoint")]
		public static extern void SetViewpoint(ref d.Vector3 xyz, ref d.Vector3 hpr);

		[DllImport("drawstuff", EntryPoint="dsSimulationLoop")]
		public static extern void SimulationLoop(int argc, string[] argv, int window_width, int window_height, ref Functions fn);
	}
}
