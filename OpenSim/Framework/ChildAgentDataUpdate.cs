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
using System.Collections;
using System.Collections.Generic;

using OSD = OpenMetaverse.StructuredData.OSD;
using OSDMap = OpenMetaverse.StructuredData.OSDMap;
using OSDArray = OpenMetaverse.StructuredData.OSDArray;
using OSDParser = OpenMetaverse.StructuredData.OSDParser;

namespace OpenSim.Framework
{
    // Soon to be dismissed
    [Serializable]
    public class ChildAgentDataUpdate
    {
        public Guid ActiveGroupID;
        public Guid AgentID;
        public bool alwaysrun;
        public float AVHeight;
        public sLLVector3 cameraPosition;
        public float drawdistance;
        public float godlevel;
        public uint GroupAccess;
        public sLLVector3 Position;
        public ulong regionHandle;
        public byte[] throttles;
        public sLLVector3 Velocity;

        public ChildAgentDataUpdate()
        {
        }

        public ChildAgentDataUpdate(AgentData agent)
        {
            if (agent.ActiveGroupID != null)
                ActiveGroupID = agent.ActiveGroupID.Guid;
            if (agent.AgentID != null)
                AgentID = agent.AgentID.Guid;
            alwaysrun = agent.AlwaysRun;
            if (agent.Size != null)
                AVHeight = agent.Size.Z;
            if (agent.Center != null)
                cameraPosition = new sLLVector3(agent.Center);
            drawdistance = agent.Far;
            godlevel = (float)agent.GodLevel;
            if (agent.Groups.Length > 0)
                GroupAccess = (uint)agent.Groups[0].GroupPowers;
            if (agent.Position != null)
                Position = new sLLVector3(agent.Position);
            regionHandle = agent.RegionHandle;
            throttles = agent.Throttles;
            if (agent.Velocity != null)
                Velocity = new sLLVector3(agent.Velocity);
        }
    }

    public class AgentGroupData
    {
        public UUID GroupID;
        public ulong GroupPowers;
        public bool AcceptNotices;

        public AgentGroupData(UUID id, ulong powers, bool notices)
        {
            GroupID = id;
            GroupPowers = powers;
            AcceptNotices = notices;
        }
    }

    public class AgentAnimationData
    {
        public UUID Animation;
        public UUID ObjectID;
    }

    public class AgentData
    {
        public ulong RegionHandle;
        public uint CircuitCode;

        public UUID AgentID;
        public UUID SessionID;

        public Vector3 Position;
        public Vector3 Velocity;
        public Vector3 Center;
        public Vector3 Size;
        public Vector3 AtAxis;
        public Vector3 LeftAxis;
        public Vector3 UpAxis;
        public bool ChangedGrid;

        public float Far;
        public float Aspect;
        //public int[] Throttles;
        public byte[] Throttles;

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

        public AgentGroupData[] Groups;
        public AgentAnimationData[] Anims;

        public UUID GranterID;
        public Dictionary<string, string> NVPairs;

        byte[] VisualParams;

        public string CallbackURI;

        public OSDMap PackUpdateMessage()
        {
            OSDMap args = new OSDMap();
            args["region_handle"] = OSD.FromString(RegionHandle.ToString());
            args["circuit_code"] = OSD.FromString(CircuitCode.ToString());
            args["agent_uuid"] = OSD.FromUUID(AgentID);
            args["session_uuid"] = OSD.FromUUID(SessionID);

            args["position"] = OSD.FromString(Position.ToString());
            args["velocity"] = OSD.FromString(Velocity.ToString());
            args["center"] = OSD.FromString(Center.ToString());
            args["size"] = OSD.FromString(Size.ToString());
            args["at_axis"] = OSD.FromString(AtAxis.ToString());
            args["left_axis"] = OSD.FromString(LeftAxis.ToString());
            args["up_axis"] = OSD.FromString(UpAxis.ToString());

            args["changed_grid"] = OSD.FromBoolean(ChangedGrid);
            args["far"] = OSD.FromReal(Far);
            args["aspect"] = OSD.FromReal(Aspect);

            if ((Throttles != null) && (Throttles.Length > 0))
                args["throttles"] = OSD.FromBinary(Throttles);

            args["locomotion_state"] = OSD.FromString(LocomotionState.ToString());
            args["head_rotation"] = OSD.FromString(HeadRotation.ToString());
            args["body_rotation"] = OSD.FromString(BodyRotation.ToString());
            args["control_flags"] = OSD.FromString(ControlFlags.ToString());

            args["energy_level"] = OSD.FromReal(EnergyLevel);
            args["god_level"] = OSD.FromString(GodLevel.ToString());
            args["always_run"] = OSD.FromBoolean(AlwaysRun);
            args["prey_agent"] = OSD.FromUUID(PreyAgent);
            args["agent_access"] = OSD.FromString(AgentAccess.ToString());

            if ((AgentTextures != null) && (AgentTextures.Length > 0))
            {
                OSDArray textures = new OSDArray(AgentTextures.Length);
                foreach (UUID uuid in AgentTextures)
                    textures.Add(OSD.FromUUID(uuid));
                args["agent_textures"] = textures;
            }

            args["active_group_id"] = OSD.FromUUID(ActiveGroupID);

            // Last few fields are still missing

            if ((CallbackURI != null) && (!CallbackURI.Equals("")))
                args["callback_uri"] = OSD.FromString(CallbackURI);

            return args;
        }

        /// <summary>
        /// Deserialization of agent data.
        /// Avoiding reflection makes it painful to write, but that's the price!
        /// </summary>
        /// <param name="hash"></param>
        public void UnpackUpdateMessage(OSDMap args)
        {
            if (args.ContainsKey("region_handle"))
                UInt64.TryParse(args["region_handle"].AsString(), out RegionHandle);

            if (args["circuit_code"] != null)
                UInt32.TryParse((string)args["circuit_code"].AsString(), out CircuitCode);

            if (args["agent_uuid"] != null)
                AgentID = args["agent_uuid"].AsUUID();

            if (args["session_uuid"] != null)
                SessionID = args["session_uuid"].AsUUID();

            if (args["position"] != null)
                Vector3.TryParse(args["position"].AsString(), out Position);

            if (args["velocity"] != null)
                Vector3.TryParse(args["velocity"].AsString(), out Velocity);

            if (args["center"] != null)
                Vector3.TryParse(args["center"].AsString(), out Center);

            if (args["size"] != null)
                Vector3.TryParse(args["size"].AsString(), out Size);

            if (args["at_axis"] != null)
                Vector3.TryParse(args["at_axis"].AsString(), out AtAxis);

            if (args["left_axis"] != null)
                Vector3.TryParse(args["left_axis"].AsString(), out AtAxis);

            if (args["up_axis"] != null)
                Vector3.TryParse(args["up_axis"].AsString(), out AtAxis);

            if (args["changed_grid"] != null)
                ChangedGrid = args["changed_grid"].AsBoolean();

            if (args["far"] != null)
                Far = (float)(args["far"].AsReal());

            if (args["aspect"] != null)
                Aspect = (float)args["aspect"].AsReal();

            if (args["throttles"] != null)
                Throttles = args["throttles"].AsBinary();

            if (args["locomotion_state"] != null)
                UInt32.TryParse(args["locomotion_state"].AsString(), out LocomotionState);

            if (args["head_rotation"] != null)
                Quaternion.TryParse(args["head_rotation"].AsString(), out HeadRotation);

            if (args["body_rotation"] != null)
                Quaternion.TryParse(args["body_rotation"].AsString(), out BodyRotation);

            if (args["control_flags"] != null)
                UInt32.TryParse(args["control_flags"].AsString(), out ControlFlags);

            if (args["energy_level"] != null)
                EnergyLevel = (float)(args["energy_level"].AsReal());

            if (args["god_level"] != null)
                Byte.TryParse(args["god_level"].AsString(), out GodLevel);

            if (args["always_run"] != null)
                AlwaysRun = args["always_run"].AsBoolean();

            if (args["prey_agent"] != null)
                PreyAgent = args["prey_agent"].AsUUID();

            if (args["agent_access"] != null)
                Byte.TryParse(args["agent_access"].AsString(), out AgentAccess);

            if ((args["agent_textures"] != null) && (args["agent_textures"]).Type == OpenMetaverse.StructuredData.OSDType.Array)
            {
                OSDArray textures = (OSDArray)(args["agent_textures"]);
                AgentTextures = new UUID[textures.Count];
                int i = 0;
                foreach (OSD o in textures)
                    AgentTextures[i++] = o.AsUUID();
            }

            if (args["active_group_id"] != null)
                ActiveGroupID = args["active_group_id"].AsUUID();

            if (args["callback_uri"] != null)
                CallbackURI = args["callback_uri"].AsString();
        }

        public AgentData()
        {
        }

        public AgentData(Hashtable hash)
        {
            //UnpackUpdateMessage(hash);
        }

        /// <summary>
        /// Soon to be decommissioned
        /// </summary>
        /// <param name="cAgent"></param>
        public void CopyFrom(ChildAgentDataUpdate cAgent)
        {
            ActiveGroupID = new UUID(cAgent.ActiveGroupID);
            
            AgentID = new UUID(cAgent.AgentID);

            AlwaysRun = cAgent.alwaysrun;
            
            // next: ???
            Size = new Vector3();
            Size.Z = cAgent.AVHeight;
            
            Center = new Vector3(cAgent.cameraPosition.x, cAgent.cameraPosition.y, cAgent.cameraPosition.z);
            
            Far = cAgent.drawdistance;
            
            // downcasting ???
            GodLevel = (byte)(cAgent.godlevel);

            Groups = new AgentGroupData[1];
            Groups[0] = new AgentGroupData(new UUID(cAgent.ActiveGroupID), cAgent.GroupAccess, true);
            
            Position = new Vector3(cAgent.Position.x, cAgent.Position.y, cAgent.Position.z);
            
            RegionHandle = cAgent.regionHandle;
            
            Throttles = cAgent.throttles;
            
            Velocity = new Vector3(cAgent.Velocity.x, cAgent.Velocity.y, cAgent.Velocity.z);
        }

        public void Dump()
        {
            System.Console.WriteLine("------------ AgentData ------------");
            System.Console.WriteLine("UUID: " + AgentID);
            System.Console.WriteLine("Region: " + RegionHandle);
            System.Console.WriteLine("Position: " + Position);
        }
    }

}
