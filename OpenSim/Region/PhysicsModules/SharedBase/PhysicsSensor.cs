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
using System.Timers;
using OpenMetaverse;

namespace OpenSim.Region.PhysicsModules.SharedBase
{
    [Flags]
    public enum SenseType : uint
    {
        NONE = 0,
        AGENT = 1,
        ACTIVE = 2,
        PASSIVE = 3,
        SCRIPTED = 4
    }

    public abstract class PhysicsSensor
    {
        public static PhysicsSensor Null
        {
            get { return new NullPhysicsSensor(); }
        }
        public abstract Vector3 Position { get; set; }
        public abstract void TimerCallback (object obj, ElapsedEventArgs eea);
        public abstract float radianarc {get; set;}
        public abstract string targetname {get; set;}
        public abstract Guid targetKey{get;set;}
        public abstract SenseType sensetype { get;set;}
        public abstract float range { get;set;}
        public abstract float rateSeconds { get;set;}
    }

    public class NullPhysicsSensor : PhysicsSensor
    {
        public override Vector3 Position
        {
            get { return Vector3.Zero; }
            set { return; }
        }
        public override void TimerCallback(object obj, ElapsedEventArgs eea)
        {
            // don't do squat
        }
        public override float radianarc { get { return 0f; } set { } }
        public override string targetname { get { return ""; } set { } }
        public override Guid targetKey { get { return Guid.Empty; } set { } }
        public override SenseType sensetype { get { return SenseType.NONE; } set { } }
        public override float range { get { return 0; } set { } }
        public override float rateSeconds { get { return 0; } set { } }
    }
}
