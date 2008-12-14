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
using System.Collections.Generic;
using OpenMetaverse;

namespace OpenSim.Framework
{
    public class AgentCircuitData
    {
        public UUID AgentID;
        public UUID BaseFolder;
        public string CapsPath = String.Empty;
        public Dictionary<ulong, string> ChildrenCapSeeds;
        public bool child;
        public uint circuitcode;
        public string firstname;
        public UUID InventoryFolder;
        public string lastname;
        public UUID SecureSessionID;
        public UUID SessionID;
        public Vector3 startpos;

        public AgentCircuitData()
        {
        }

        public AgentCircuitData(sAgentCircuitData cAgent)
        {
            AgentID = new UUID(cAgent.AgentID);
            SessionID = new UUID(cAgent.SessionID);
            SecureSessionID = new UUID(cAgent.SecureSessionID);
            startpos = new Vector3(cAgent.startposx, cAgent.startposy, cAgent.startposz);
            firstname = cAgent.firstname;
            lastname = cAgent.lastname;
            circuitcode = cAgent.circuitcode;
            child = cAgent.child;
            InventoryFolder = new UUID(cAgent.InventoryFolder);
            BaseFolder = new UUID(cAgent.BaseFolder);
            CapsPath = cAgent.CapsPath;
            ChildrenCapSeeds = cAgent.ChildrenCapSeeds;
        }
    }

    [Serializable]
    public class sAgentCircuitData
    {
        public Guid AgentID;
        public Guid BaseFolder;
        public string CapsPath = String.Empty;
        public Dictionary<ulong, string> ChildrenCapSeeds;
        public bool child;
        public uint circuitcode;
        public string firstname;
        public Guid InventoryFolder;
        public string lastname;
        public Guid SecureSessionID;
        public Guid SessionID;
        public float startposx;
        public float startposy;
        public float startposz;

        public sAgentCircuitData()
        {
        }

        public sAgentCircuitData(AgentCircuitData cAgent)
        {
            AgentID = cAgent.AgentID.Guid;
            SessionID = cAgent.SessionID.Guid;
            SecureSessionID = cAgent.SecureSessionID.Guid;
            startposx = cAgent.startpos.X;
            startposy = cAgent.startpos.Y;
            startposz = cAgent.startpos.Z;
            firstname = cAgent.firstname;
            lastname = cAgent.lastname;
            circuitcode = cAgent.circuitcode;
            child = cAgent.child;
            InventoryFolder = cAgent.InventoryFolder.Guid;
            BaseFolder = cAgent.BaseFolder.Guid;
            CapsPath = cAgent.CapsPath;
            ChildrenCapSeeds = cAgent.ChildrenCapSeeds;
        }
    }
}
