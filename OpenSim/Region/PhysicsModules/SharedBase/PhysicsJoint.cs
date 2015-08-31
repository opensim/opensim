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
using OpenSim.Framework;
using OpenMetaverse;

namespace OpenSim.Region.PhysicsModules.SharedBase
{
    public enum PhysicsJointType : int
    {
        Ball = 0,
        Hinge = 1
    }

    public class PhysicsJoint
    {
        public virtual bool IsInPhysicsEngine { get { return false; } } // set internally to indicate if this joint has already been passed to the physics engine or is still pending
        public PhysicsJointType Type;
        public string RawParams;
        public List<string> BodyNames = new List<string>();
        public Vector3 Position; // global coords
        public Quaternion Rotation; // global coords
        public string ObjectNameInScene; // proxy object in scene that represents the joint position/orientation
        public string TrackedBodyName; // body name that this joint is attached to (ObjectNameInScene will follow TrackedBodyName)
        public Quaternion LocalRotation; // joint orientation relative to one of the involved bodies, the tracked body
        public int ErrorMessageCount; // total # of error messages printed for this joint since its creation. if too many, further error messages are suppressed to prevent flooding.
        public const int maxErrorMessages = 100; // no more than this # of error messages will be printed for each joint
    }
}
