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
using OpenMetaverse;

namespace OpenSim.Framework
{
    /// <summary>
    /// Client provided parameters for avatar movement
    /// </summary>
    public class AgentUpdateArgs : EventArgs
    {
        /// <summary>
        /// Agent's unique ID
        /// </summary>
        public UUID AgentID;

        /// <summary>
        /// Rotation of the avatar's body
        /// </summary>
        public Quaternion BodyRotation;

        /// <summary>
        /// AT portion of the camera matrix
        /// </summary>
        public Vector3 CameraAtAxis;

        /// <summary>
        /// Position of the camera in the Scene
        /// </summary>
        public Vector3 CameraCenter;
        public Vector3 CameraLeftAxis;
        public Vector3 CameraUpAxis;

        /// <summary>
        /// Bitflag field for agent movement.  Fly, forward, backward, turn left, turn right, go up, go down, Straffe, etc.
        /// </summary>
        public uint ControlFlags;

        /// <summary>
        /// Agent's client Draw distance setting
        /// </summary>
        public float Far;
        public byte Flags;

        /// <summary>
        /// Rotation of the avatar's head
        /// </summary>
        public Quaternion HeadRotation;

        /// <summary>
        /// Session Id
        /// </summary>
        public UUID SessionID;
        public byte State;

        public Vector3 ClientAgentPosition;
        public bool UseClientAgentPosition;
        public bool NeedsCameraCollision;
        public uint lastpacketSequence;

        public AgentUpdateArgs()
        {
            UseClientAgentPosition = false;
        }
    }
}
