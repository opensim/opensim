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
 *     * Neither the name of the OpenSim Project nor the
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
using System.Collections.Generic;

namespace OpenSim.Framework
{
    public struct InterregionGroupData
    {
        public UUID GroupID;
        public ulong GroupPowers;
        public bool AcceptNotices;
    }

    public struct InterregionAnimationData
    {
        public UUID Animation;
        public UUID ObjectID;
    }

    public struct InterregionData
    {
        public ulong RegionHandle;
        public uint CircuitCode;

        public UUID AgentID;
        public UUID SessionID;

        public Vector3 AgentPos;
        public Vector3 AgentVel;
        public Vector3 Center;
        public Vector3 Size;
        public Vector3 AtAxis;
        public Vector3 LeftAxis;
        public Vector3 UpAxis;
        public bool ChangedGrid;

        public float Far;
        public float Aspect;
        public int[] Throttles;

        public uint LocomotionState;
        public Quaternion HeadRotation;
        public Quaternion BodyRotation;
        public uint ControlFlags;
        public float EnergyLevel;
        public Byte GodLevel;
        public bool AlwaysRun;
        public UUID PreyAgent;
        public Byte AgentAccess;
        public UUID[] AgentTextures;
        public UUID ActiveGroupID;

        public InterregionGroupData[] Groups;
        public InterregionAnimationData[] Anims;
    }
}
