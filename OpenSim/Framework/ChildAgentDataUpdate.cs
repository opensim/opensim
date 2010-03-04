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
using System.Collections;
using System.Collections.Generic;
using OpenMetaverse;
using OpenMetaverse.StructuredData;

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
        public Vector3 cameraPosition;
        public float drawdistance;
        public float godlevel;
        public uint GroupAccess;
        public Vector3 Position;
        public ulong regionHandle;
        public byte[] throttles;
        public Vector3 Velocity;

        public ChildAgentDataUpdate()
        {
        }
    }

    public interface IAgentData
    {
        UUID AgentID { get; set; }

        OSDMap Pack();
        void Unpack(OSDMap map);
    }

    /// <summary>
    /// Replacement for ChildAgentDataUpdate. Used over RESTComms and LocalComms.
    /// </summary>
    public class AgentPosition : IAgentData
    {
        private UUID m_id;
        public UUID AgentID
        {
            get { return m_id; }
            set { m_id = value; }
        }

        public ulong RegionHandle;
        public uint CircuitCode;
        public UUID SessionID;

        public float Far;
        public Vector3 Position;
        public Vector3 Velocity;
        public Vector3 Center;
        public Vector3 Size;
        public Vector3 AtAxis;
        public Vector3 LeftAxis;
        public Vector3 UpAxis;
        public bool ChangedGrid;

        // This probably shouldn't be here
        public byte[] Throttles;


        public OSDMap Pack()
        {
            OSDMap args = new OSDMap();
            args["message_type"] = OSD.FromString("AgentPosition");

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

            args["far"] = OSD.FromReal(Far);
            args["changed_grid"] = OSD.FromBoolean(ChangedGrid);

            if ((Throttles != null) && (Throttles.Length > 0))
                args["throttles"] = OSD.FromBinary(Throttles);

            return args;
        }

        public void Unpack(OSDMap args)
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

            if (args["throttles"] != null)
                Throttles = args["throttles"].AsBinary();
        }

        /// <summary>
        /// Soon to be decommissioned
        /// </summary>
        /// <param name="cAgent"></param>
        public void CopyFrom(ChildAgentDataUpdate cAgent)
        {
            AgentID = new UUID(cAgent.AgentID);

            // next: ???
            Size = new Vector3();
            Size.Z = cAgent.AVHeight;

            Center = cAgent.cameraPosition;
            Far = cAgent.drawdistance;
            Position = cAgent.Position;
            RegionHandle = cAgent.regionHandle;
            Throttles = cAgent.throttles;
            Velocity = cAgent.Velocity;
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

        public AgentGroupData(OSDMap args)
        {
            UnpackUpdateMessage(args);
        }

        public OSDMap PackUpdateMessage()
        {
            OSDMap groupdata = new OSDMap();
            groupdata["group_id"] = OSD.FromUUID(GroupID);
            groupdata["group_powers"] = OSD.FromString(GroupPowers.ToString());
            groupdata["accept_notices"] = OSD.FromBoolean(AcceptNotices);

            return groupdata;
        }

        public void UnpackUpdateMessage(OSDMap args)
        {
            if (args["group_id"] != null)
                GroupID = args["group_id"].AsUUID();
            if (args["group_powers"] != null)
                UInt64.TryParse((string)args["group_powers"].AsString(), out GroupPowers);
            if (args["accept_notices"] != null)
                AcceptNotices = args["accept_notices"].AsBoolean();
        }
    }

    public class AttachmentData
    {
        public int AttachPoint;
        public UUID ItemID;
        public UUID AssetID;

        public AttachmentData(int point, UUID item, UUID asset)
        {
            AttachPoint = point;
            ItemID = item;
            AssetID = asset;
        }

        public AttachmentData(OSDMap args)
        {
            UnpackUpdateMessage(args);
        }

        public OSDMap PackUpdateMessage()
        {
            OSDMap attachdata = new OSDMap();
            attachdata["point"] = OSD.FromInteger(AttachPoint);
            attachdata["item"] = OSD.FromUUID(ItemID);
            attachdata["asset"] = OSD.FromUUID(AssetID);

            return attachdata;
        }


        public void UnpackUpdateMessage(OSDMap args)
        {
            if (args["point"] != null)
                AttachPoint = args["point"].AsInteger();
            if (args["item"] != null)
                ItemID = args["item"].AsUUID();
            if (args["asset"] != null)
                AssetID = args["asset"].AsUUID();
        }
    }

    public class AgentData : IAgentData
    {
        private UUID m_id;
        public UUID AgentID
        {
            get { return m_id; }
            set { m_id = value; }
        }
        public UUID RegionID;
        public uint CircuitCode;
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
        public UUID ActiveGroupID;

        public AgentGroupData[] Groups;
        public Animation[] Anims;

        public UUID GranterID;

        // Appearance
        public byte[] AgentTextures;
        public byte[] VisualParams;
        public UUID[] Wearables;
        public AttachmentData[] Attachments;

        public string CallbackURI;

        public virtual OSDMap Pack()
        {
            OSDMap args = new OSDMap();
            args["message_type"] = OSD.FromString("AgentData");

            args["region_id"] = OSD.FromString(RegionID.ToString());
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

            args["active_group_id"] = OSD.FromUUID(ActiveGroupID);
          
            if ((Groups != null) && (Groups.Length > 0))
            {
                OSDArray groups = new OSDArray(Groups.Length);
                foreach (AgentGroupData agd in Groups)
                    groups.Add(agd.PackUpdateMessage());
                args["groups"] = groups;
            }

            if ((Anims != null) && (Anims.Length > 0))
            {
                OSDArray anims = new OSDArray(Anims.Length);
                foreach (Animation aanim in Anims)
                    anims.Add(aanim.PackUpdateMessage());
                args["animations"] = anims;
            }

            //if ((AgentTextures != null) && (AgentTextures.Length > 0))
            //{
            //    OSDArray textures = new OSDArray(AgentTextures.Length);
            //    foreach (UUID uuid in AgentTextures)
            //        textures.Add(OSD.FromUUID(uuid));
            //    args["agent_textures"] = textures;
            //}

           
            if ((AgentTextures != null) && (AgentTextures.Length > 0))
                args["texture_entry"] = OSD.FromBinary(AgentTextures);

            if ((VisualParams != null) && (VisualParams.Length > 0))
                args["visual_params"] = OSD.FromBinary(VisualParams);

            // We might not pass this in all cases...
            if ((Wearables != null) && (Wearables.Length > 0))
            {
                OSDArray wears = new OSDArray(Wearables.Length);
                foreach (UUID uuid in Wearables)
                    wears.Add(OSD.FromUUID(uuid));
                args["wearables"] = wears;
            }

            
            if ((Attachments != null) && (Attachments.Length > 0))
            {
                OSDArray attachs = new OSDArray(Attachments.Length);
                foreach (AttachmentData att in Attachments)
                    attachs.Add(att.PackUpdateMessage());
                args["attachments"] = attachs;
            }


            if ((CallbackURI != null) && (!CallbackURI.Equals("")))
                args["callback_uri"] = OSD.FromString(CallbackURI);


            return args;
        }

        /// <summary>
        /// Deserialization of agent data.
        /// Avoiding reflection makes it painful to write, but that's the price!
        /// </summary>
        /// <param name="hash"></param>
        public virtual void Unpack(OSDMap args)
        {
            if (args.ContainsKey("region_id"))
                UUID.TryParse(args["region_id"].AsString(), out RegionID);

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

            if (args["active_group_id"] != null)
                ActiveGroupID = args["active_group_id"].AsUUID();

            if ((args["groups"] != null) && (args["groups"]).Type == OSDType.Array)
            {
                OSDArray groups = (OSDArray)(args["groups"]);
                Groups = new AgentGroupData[groups.Count];
                int i = 0;
                foreach (OSD o in groups)
                {
                    if (o.Type == OSDType.Map)
                    {
                        Groups[i++] = new AgentGroupData((OSDMap)o);
                    }
                }
            }

            if ((args["animations"] != null) && (args["animations"]).Type == OSDType.Array)
            {
                OSDArray anims = (OSDArray)(args["animations"]);
                Anims = new Animation[anims.Count];
                int i = 0;
                foreach (OSD o in anims)
                {
                    if (o.Type == OSDType.Map)
                    {
                        Anims[i++] = new Animation((OSDMap)o);
                    }
                }
            }

            //if ((args["agent_textures"] != null) && (args["agent_textures"]).Type == OSDType.Array)
            //{
            //    OSDArray textures = (OSDArray)(args["agent_textures"]);
            //    AgentTextures = new UUID[textures.Count];
            //    int i = 0;
            //    foreach (OSD o in textures)
            //        AgentTextures[i++] = o.AsUUID();
            //}

            if (args["texture_entry"] != null)
                AgentTextures = args["texture_entry"].AsBinary();

            if (args["visual_params"] != null)
                VisualParams = args["visual_params"].AsBinary();

            if ((args["wearables"] != null) && (args["wearables"]).Type == OSDType.Array)
            {
                OSDArray wears = (OSDArray)(args["wearables"]);
                Wearables = new UUID[wears.Count];
                int i = 0;
                foreach (OSD o in wears)
                    Wearables[i++] = o.AsUUID();
            }

            if ((args["attachments"] != null) && (args["attachments"]).Type == OSDType.Array)
            {
                OSDArray attachs = (OSDArray)(args["attachments"]);
                Attachments = new AttachmentData[attachs.Count];
                int i = 0;
                foreach (OSD o in attachs)
                {
                    if (o.Type == OSDType.Map)
                    {
                        Attachments[i++] = new AttachmentData((OSDMap)o);
                    }
                }
            }

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

        public void Dump()
        {
            System.Console.WriteLine("------------ AgentData ------------");
            System.Console.WriteLine("UUID: " + AgentID);
            System.Console.WriteLine("Region: " + RegionID);
            System.Console.WriteLine("Position: " + Position);
        }
    }

    public class CompleteAgentData : AgentData
    {
        public override OSDMap Pack() 
        {
            return base.Pack();
        }

        public override void Unpack(OSDMap map)
        {
            base.Unpack(map);
        }
    }
}
