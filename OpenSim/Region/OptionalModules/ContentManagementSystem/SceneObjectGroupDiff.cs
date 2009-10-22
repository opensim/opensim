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

#region Header

// SceneObjectGroupDiff.cs
// User: bongiojp

#endregion Header

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;

using OpenMetaverse;

using Nini.Config;

using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Physics.Manager;

using log4net;

namespace OpenSim.Region.OptionalModules.ContentManagement
{
    #region Enumerations

    [Flags]
    public enum Diff
    {
        NONE = 0,
        FACECOLOR = 1,
        SHAPE = 1<<1,
        MATERIAL = 1<<2,
        TEXTURE = 1<<3,
        SCALE = 1<<4,
        POSITION = 1<<5,
        OFFSETPOSITION = 1<<6,
        ROTATIONOFFSET = 1<<7,
        ROTATIONALVELOCITY = 1<<8,
        ACCELERATION = 1<<9,
        ANGULARVELOCITY = 1<<10,
        VELOCITY = 1<<11,
        OBJECTOWNER = 1<<12,
        PERMISSIONS = 1<<13,
        DESCRIPTION = 1<<14,
        NAME = 1<<15,
        SCRIPT = 1<<16,
        CLICKACTION = 1<<17,
        PARTICLESYSTEM = 1<<18,
        GLOW = 1<<19,
        SALEPRICE = 1<<20,
        SITNAME = 1<<21,
        SITTARGETORIENTATION = 1<<22,
        SITTARGETPOSITION = 1<<23,
        TEXT = 1<<24,
        TOUCHNAME = 1<<25
    }

    #endregion Enumerations

    public static class Difference
    {
        #region Static Fields

        static float TimeToDiff = 0;
//        private static readonly ILog m_log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        #endregion Static Fields

        #region Private Methods

        private static bool AreQuaternionsEquivalent(Quaternion first, Quaternion second)
        {
            Vector3 firstVector = llRot2Euler(first);
            Vector3 secondVector = llRot2Euler(second);
            return AreVectorsEquivalent(firstVector, secondVector);
        }

        private static bool AreVectorsEquivalent(Vector3 first, Vector3 second)
        {
            if (TruncateSignificant(first.X, 2) == TruncateSignificant(second.X, 2)
               && TruncateSignificant(first.Y, 2) == TruncateSignificant(second.Y, 2)
               && TruncateSignificant(first.Z, 2) == TruncateSignificant(second.Z, 2)
               )
                return true;
            else
                return false;
        }

        // Taken from Region/ScriptEngine/Common/LSL_BuiltIn_Commands.cs
        private static double NormalizeAngle(double angle)
        {
            angle = angle % (Math.PI * 2);
            if (angle < 0) angle = angle + Math.PI * 2;
            return angle;
        }

        private static int TruncateSignificant(float num, int digits)
        {
            return (int) Math.Ceiling((Math.Truncate(num * 10 * digits)/10*digits));
            // return (int) ((num * (10*digits))/10*digits);
        }

        // Taken from Region/ScriptEngine/Common/LSL_BuiltIn_Commands.cs
        // Also changed the original function from LSL_Types to LL types
        private static Vector3 llRot2Euler(Quaternion r)
        {
            Quaternion t = new Quaternion(r.X * r.X, r.Y * r.Y, r.Z * r.Z, r.W * r.W);
            double m = (t.X + t.Y + t.Z + t.W);
            if (m == 0) return new Vector3();
            double n = 2 * (r.Y * r.W + r.X * r.Z);
            double p = m * m - n * n;
            if (p > 0)
                return new Vector3((float)NormalizeAngle(Math.Atan2(2.0 * (r.X * r.W - r.Y * r.Z), (-t.X - t.Y + t.Z + t.W))),
                                             (float)NormalizeAngle(Math.Atan2(n, Math.Sqrt(p))),
                                             (float)NormalizeAngle(Math.Atan2(2.0 * (r.Z * r.W - r.X * r.Y), (t.X - t.Y - t.Z + t.W))));
            else if (n > 0)
                return new Vector3(0.0f, (float)(Math.PI / 2), (float)NormalizeAngle(Math.Atan2((r.Z * r.W + r.X * r.Y), 0.5 - t.X - t.Z)));
            else
                return new Vector3(0.0f, (float)(-Math.PI / 2), (float)NormalizeAngle(Math.Atan2((r.Z * r.W + r.X * r.Y), 0.5 - t.X - t.Z)));
        }

        #endregion Private Methods

        #region Public Methods

        /// <summary>
        /// Compares the attributes (Vectors, Quaternions, Strings, etc.) between two scene object parts
        /// and returns a Diff bitmask which details what the differences are.
        /// </summary>
        public static Diff FindDifferences(SceneObjectPart first, SceneObjectPart second)
        {
            Stopwatch x = new Stopwatch();
            x.Start();

            Diff result = 0;

            // VECTOR COMPARISONS
            if (!AreVectorsEquivalent(first.Acceleration, second.Acceleration))
                result |= Diff.ACCELERATION;
            if (!AreVectorsEquivalent(first.AbsolutePosition, second.AbsolutePosition))
                result |= Diff.POSITION;
            if (!AreVectorsEquivalent(first.AngularVelocity, second.AngularVelocity))
                result |= Diff.ANGULARVELOCITY;
            if (!AreVectorsEquivalent(first.OffsetPosition, second.OffsetPosition))
                result |= Diff.OFFSETPOSITION;
            if (!AreVectorsEquivalent(first.RotationalVelocity, second.RotationalVelocity))
                result |= Diff.ROTATIONALVELOCITY;
            if (!AreVectorsEquivalent(first.Scale, second.Scale))
                result |= Diff.SCALE;
            if (!AreVectorsEquivalent(first.Velocity, second.Velocity))
                result |= Diff.VELOCITY;


            // QUATERNION COMPARISONS
            if (!AreQuaternionsEquivalent(first.RotationOffset, second.RotationOffset))
                result |= Diff.ROTATIONOFFSET;


            // MISC COMPARISONS (UUID, Byte)
            if (first.ClickAction != second.ClickAction)
                result |= Diff.CLICKACTION;
            if (first.OwnerID != second.OwnerID)
                result |= Diff.OBJECTOWNER;


            // STRING COMPARISONS
            if (first.Description != second.Description)
                result |= Diff.DESCRIPTION;
            if (first.Material != second.Material)
                result |= Diff.MATERIAL;
            if (first.Name != second.Name)
                result |= Diff.NAME;
            if (first.SitName != second.SitName)
                result |= Diff.SITNAME;
            if (first.Text != second.Text)
                result |= Diff.TEXT;
            if (first.TouchName != second.TouchName)
                result |= Diff.TOUCHNAME;

            x.Stop();
            TimeToDiff += x.ElapsedMilliseconds;
            //m_log.Info("[DIFFERENCES] Time spent diffing objects so far" + TimeToDiff);

            return result;
        }

        #endregion Public Methods
    }
}
