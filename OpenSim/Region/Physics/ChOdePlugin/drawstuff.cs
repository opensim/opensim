/*
 * Copyright ODE
 * Ode.NET - .NET bindings for ODE
 *  Jason Perkins (starkos@industriousone.com)
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
 * 
 */

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

        [DllImport("drawstuff", EntryPoint = "dsDrawBox")]
        public static extern void DrawBox(ref d.Vector3 pos, ref d.Matrix3 R, ref d.Vector3 sides);

        [DllImport("drawstuff", EntryPoint = "dsDrawCapsule")]
        public static extern void DrawCapsule(ref d.Vector3 pos, ref d.Matrix3 R, dReal length, dReal radius);

        [DllImport("drawstuff", EntryPoint = "dsDrawConvex")]
        public static extern void DrawConvex(ref d.Vector3 pos, ref d.Matrix3 R, dReal[] planes, int planeCount, dReal[] points, int pointCount, int[] polygons);

        [DllImport("drawstuff", EntryPoint = "dsSetColor")]
        public static extern void SetColor(float red, float green, float blue);

        [DllImport("drawstuff", EntryPoint = "dsSetTexture")]
        public static extern void SetTexture(Texture texture);

        [DllImport("drawstuff", EntryPoint = "dsSetViewpoint")]
        public static extern void SetViewpoint(ref d.Vector3 xyz, ref d.Vector3 hpr);

        [DllImport("drawstuff", EntryPoint = "dsSimulationLoop")]
        public static extern void SimulationLoop(int argc, string[] argv, int window_width, int window_height, ref Functions fn);
    }
}
