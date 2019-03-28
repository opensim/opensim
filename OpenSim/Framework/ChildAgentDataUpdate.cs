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
using System.Reflection;
using log4net;
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

        OSDMap Pack(EntityTransferContext ctx);
        void Unpack(OSDMap map, IScene scene, EntityTransferContext ctx);
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
        //public int GodLevel;
        public OSD GodData = null;
        public bool ChangedGrid;

        // This probably shouldn't be here
        public byte[] Throttles;

        public Dictionary<ulong, string> ChildrenCapSeeds = null;

        public OSDMap Pack(EntityTransferContext ctx)
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
            //args["god_level"] = OSD.FromString(GodLevel.ToString());
            if(GodData != null)
            {
                args["god_data"] = GodData;
                OSDMap g = (OSDMap)GodData;
                // Set legacy value
                // TODO: remove after 0.9 is superseded
                if (g.ContainsKey("ViewerUiIsGod"))
                    args["god_level"] = g["ViewerUiIsGod"].AsBoolean() ? 200 : 0;
            }

            if ((Throttles != null) && (Throttles.Length > 0))
                args["throttles"] = OSD.FromBinary(Throttles);

            if (ChildrenCapSeeds != null && ChildrenCapSeeds.Count > 0)
            {
                OSDArray childrenSeeds = new OSDArray(ChildrenCapSeeds.Count);
                foreach (KeyValuePair<ulong, string> kvp in ChildrenCapSeeds)
                {
                    OSDMap pair = new OSDMap();
                    pair["handle"] = OSD.FromString(kvp.Key.ToString());
                    pair["seed"] = OSD.FromString(kvp.Value);
                    childrenSeeds.Add(pair);
                }
                args["children_seeds"] = childrenSeeds;
            }

            return args;
        }

        public void Unpack(OSDMap args, IScene scene, EntityTransferContext ctx)
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
                Vector3.TryParse(args["left_axis"].AsString(), out LeftAxis);

            if (args["up_axis"] != null)
                Vector3.TryParse(args["up_axis"].AsString(), out UpAxis);

            if (args["changed_grid"] != null)
                ChangedGrid = args["changed_grid"].AsBoolean();

            //if (args["god_level"] != null)
            //    Int32.TryParse(args["god_level"].AsString(), out GodLevel);
            if (args.ContainsKey("god_data") && args["god_data"] != null)
                GodData = args["god_data"];

            if (args["far"] != null)
                Far = (float)(args["far"].AsReal());

            if (args["throttles"] != null)
                Throttles = args["throttles"].AsBinary();

            if (args.ContainsKey("children_seeds") && (args["children_seeds"] != null) &&
                            (args["children_seeds"].Type == OSDType.Array))
            {
                OSDArray childrenSeeds = (OSDArray)(args["children_seeds"]);
                ChildrenCapSeeds = new Dictionary<ulong, string>();
                foreach (OSD o in childrenSeeds)
                {
                    if (o.Type == OSDType.Map)
                    {
                        ulong handle = 0;
                        string seed = "";
                        OSDMap pair = (OSDMap)o;
                        if (pair["handle"] != null)
                            if (!UInt64.TryParse(pair["handle"].AsString(), out handle))
                                continue;
                        if (pair["seed"] != null)
                            seed = pair["seed"].AsString();
                        if (!ChildrenCapSeeds.ContainsKey(handle))
                            ChildrenCapSeeds.Add(handle, seed);
                    }
                }
            }

        }

        /// <summary>
        /// Soon to be decommissioned
        /// </summary>
        /// <param name="cAgent"></param>
        public void CopyFrom(ChildAgentDataUpdate cAgent, UUID sid)
        {
            AgentID = new UUID(cAgent.AgentID);
            SessionID = sid;

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

    public class ControllerData
    {
        public UUID ObjectID;
        public UUID ItemID;
        public uint IgnoreControls;
        public uint EventControls;

        public ControllerData(UUID obj, UUID item, uint ignore, uint ev)
        {
            ObjectID = obj;
            ItemID = item;
            IgnoreControls = ignore;
            EventControls = ev;
        }

        public ControllerData(OSDMap args)
        {
            UnpackUpdateMessage(args);
        }

        public OSDMap PackUpdateMessage()
        {
            OSDMap controldata = new OSDMap();
            controldata["object"] = OSD.FromUUID(ObjectID);
            controldata["item"] = OSD.FromUUID(ItemID);
            controldata["ignore"] = OSD.FromInteger(IgnoreControls);
            controldata["event"] = OSD.FromInteger(EventControls);

            return controldata;
        }


        public void UnpackUpdateMessage(OSDMap args)
        {
            if (args["object"] != null)
                ObjectID = args["object"].AsUUID();
            if (args["item"] != null)
                ItemID = args["item"].AsUUID();
            if (args["ignore"] != null)
                IgnoreControls = (uint)args["ignore"].AsInteger();
            if (args["event"] != null)
                EventControls = (uint)args["event"].AsInteger();
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

        /// <summary>
        /// Signal on a V2 teleport that Scene.IncomingChildAgentDataUpdate(AgentData ad) should wait for the
        /// scene presence to become root (triggered when the viewer sends a CompleteAgentMovement UDP packet after
        /// establishing the connection triggered by it's receipt of a TeleportFinish EQ message).
        /// </summary>
        public bool SenderWantsToWaitForRoot;

        public float Far;
        public float Aspect;
        //public int[] Throttles;
        public byte[] Throttles;

        public uint LocomotionState;
        public Quaternion HeadRotation;
        public Quaternion BodyRotation;
        public uint ControlFlags;
        public float EnergyLevel;
        public OSD GodData = null;
        //public Byte GodLevel;
        public bool AlwaysRun;
        public UUID PreyAgent;
        public Byte AgentAccess;
        public UUID ActiveGroupID;
        public string ActiveGroupName;
        public string ActiveGroupTitle = null;
        public UUID agentCOF;
        public byte CrossingFlags;
        public byte CrossExtraFlags;

        public Dictionary<ulong, string> ChildrenCapSeeds = null;
        public Animation[] Anims;
        public Animation DefaultAnim = null;
        public Animation AnimState = null;
        public Byte MotionState = 0;

        public UUID ParentPart;
        public Vector3 SitOffset;

        // Appearance
        public AvatarAppearance Appearance;

// DEBUG ON
        private static readonly ILog m_log =
                LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType);
// DEBUG OFF

        // Scripted
        public ControllerData[] Controllers;

        public string CallbackURI; // to remove
        public string NewCallbackURI;

        // These two must have the same Count
        public List<ISceneObject> AttachmentObjects;
        public List<string> AttachmentObjectStates;

        public Dictionary<string, UUID> MovementAnimationOverRides = new Dictionary<string, UUID>();

        public virtual OSDMap Pack(EntityTransferContext ctx)
        {
//            m_log.InfoFormat("[CHILDAGENTDATAUPDATE] Pack data");

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

            //backwards compatibility
            args["changed_grid"] = OSD.FromBoolean(SenderWantsToWaitForRoot);
            args["wait_for_root"] = OSD.FromBoolean(SenderWantsToWaitForRoot);
            args["far"] = OSD.FromReal(Far);
            args["aspect"] = OSD.FromReal(Aspect);

            if ((Throttles != null) && (Throttles.Length > 0))
                args["throttles"] = OSD.FromBinary(Throttles);

            args["locomotion_state"] = OSD.FromString(LocomotionState.ToString());
            args["head_rotation"] = OSD.FromString(HeadRotation.ToString());
            args["body_rotation"] = OSD.FromString(BodyRotation.ToString());
            args["control_flags"] = OSD.FromString(ControlFlags.ToString());

            args["energy_level"] = OSD.FromReal(EnergyLevel);
            //args["god_level"] = OSD.FromString(GodLevel.ToString());
            if(GodData != null)
            {
                args["god_data"] = GodData;
                OSDMap g = (OSDMap)GodData;
                if (g.ContainsKey("ViewerUiIsGod"))
                    args["god_level"] = g["ViewerUiIsGod"].AsBoolean() ? 200 : 0;;
            }
            args["always_run"] = OSD.FromBoolean(AlwaysRun);
            args["prey_agent"] = OSD.FromUUID(PreyAgent);
            args["agent_access"] = OSD.FromString(AgentAccess.ToString());

            args["agent_cof"] = OSD.FromUUID(agentCOF);
            args["crossingflags"] = OSD.FromInteger(CrossingFlags);
            if(CrossingFlags != 0)
                args["crossExtraFlags"] = OSD.FromInteger(CrossExtraFlags);

            args["active_group_id"] = OSD.FromUUID(ActiveGroupID);
            args["active_group_name"] = OSD.FromString(ActiveGroupName);
            if(ActiveGroupTitle != null)
                args["active_group_title"] = OSD.FromString(ActiveGroupTitle);

            if (ChildrenCapSeeds != null && ChildrenCapSeeds.Count > 0)
            {
                OSDArray childrenSeeds = new OSDArray(ChildrenCapSeeds.Count);
                foreach (KeyValuePair<ulong, string> kvp in ChildrenCapSeeds)
                {
                    OSDMap pair = new OSDMap();
                    pair["handle"] = OSD.FromString(kvp.Key.ToString());
                    pair["seed"] = OSD.FromString(kvp.Value);
                    childrenSeeds.Add(pair);
                }
                args["children_seeds"] = childrenSeeds;
            }

            if ((Anims != null) && (Anims.Length > 0))
            {
                OSDArray anims = new OSDArray(Anims.Length);
                foreach (Animation aanim in Anims)
                    anims.Add(aanim.PackUpdateMessage());
                args["animations"] = anims;
            }

            if (DefaultAnim != null)
            {
                args["default_animation"] = DefaultAnim.PackUpdateMessage();
            }

            if (AnimState != null)
            {
                args["animation_state"] = AnimState.PackUpdateMessage();
            }

            if (MovementAnimationOverRides.Count > 0)
            {
                OSDArray AOs = new OSDArray(MovementAnimationOverRides.Count);
                {
                    foreach (KeyValuePair<string, UUID> kvp in MovementAnimationOverRides)
                    {
                        OSDMap ao = new OSDMap(2);
                        ao["state"] = OSD.FromString(kvp.Key);
                        ao["uuid"] = OSD.FromUUID(kvp.Value);
                        AOs.Add(ao);
                    }
                }
                args["movementAO"] = AOs;
            }

            if (MotionState != 0)
            {
                args["motion_state"] = OSD.FromInteger(MotionState);
            }

            if (Appearance != null)
                args["packed_appearance"] = Appearance.Pack(ctx);

            if ((Controllers != null) && (Controllers.Length > 0))
            {
                OSDArray controls = new OSDArray(Controllers.Length);
                foreach (ControllerData ctl in Controllers)
                    controls.Add(ctl.PackUpdateMessage());
                args["controllers"] = controls;
            }

            if ((CallbackURI != null) && (!CallbackURI.Equals("")))
                args["callback_uri"] = OSD.FromString(CallbackURI);

            if ((NewCallbackURI != null) && (!NewCallbackURI.Equals("")))
                args["cb_uri"] = OSD.FromString(NewCallbackURI);

            // Attachment objects for fatpack messages
            if (AttachmentObjects != null)
            {
                int i = 0;
                OSDArray attObjs = new OSDArray(AttachmentObjects.Count);
                foreach (ISceneObject so in AttachmentObjects)
                {
                    OSDMap info = new OSDMap(4);
                    info["sog"] = OSD.FromString(so.ToXml2());
                    info["extra"] = OSD.FromString(so.ExtraToXmlString());
                    info["modified"] = OSD.FromBoolean(so.HasGroupChanged);
                    try
                    {
                        info["state"] = OSD.FromString(AttachmentObjectStates[i++]);
                    }
                    catch (IndexOutOfRangeException)
                    {
                        m_log.WarnFormat("[CHILD AGENT DATA]: scripts list is shorter than object list.");
                    }

                    attObjs.Add(info);
                }
                args["attach_objects"] = attObjs;
            }

            args["parent_part"] = OSD.FromUUID(ParentPart);
            args["sit_offset"] = OSD.FromString(SitOffset.ToString());

            return args;
        }

        /// <summary>
        /// Deserialization of agent data.
        /// Avoiding reflection makes it painful to write, but that's the price!
        /// </summary>
        /// <param name="hash"></param>
        public virtual void Unpack(OSDMap args, IScene scene, EntityTransferContext ctx)
        {
            //m_log.InfoFormat("[CHILDAGENTDATAUPDATE] Unpack data");

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

            if (args.ContainsKey("wait_for_root") && args["wait_for_root"] != null)
                SenderWantsToWaitForRoot = args["wait_for_root"].AsBoolean();

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

            //if (args["god_level"] != null)
            //    Byte.TryParse(args["god_level"].AsString(), out GodLevel);

            if (args.ContainsKey("god_data") && args["god_data"] != null)
                GodData = args["god_data"];

            if (args["always_run"] != null)
                AlwaysRun = args["always_run"].AsBoolean();

            if (args["prey_agent"] != null)
                PreyAgent = args["prey_agent"].AsUUID();

            if (args["agent_access"] != null)
                Byte.TryParse(args["agent_access"].AsString(), out AgentAccess);

            if (args.ContainsKey("agent_cof") && args["agent_cof"] != null)
                agentCOF = args["agent_cof"].AsUUID();

            if (args.ContainsKey("crossingflags") && args["crossingflags"] != null)
                CrossingFlags = (byte)args["crossingflags"].AsInteger();

            if(CrossingFlags != 0)
            {
                if (args.ContainsKey("crossExtraFlags") && args["crossExtraFlags"] != null)
                    CrossExtraFlags = (byte)args["crossExtraFlags"].AsInteger();
            }

            if (args.ContainsKey("active_group_id") && args["active_group_id"] != null)
                ActiveGroupID = args["active_group_id"].AsUUID();

            if (args.ContainsKey("active_group_name") && args["active_group_name"] != null)
                ActiveGroupName = args["active_group_name"].AsString();

            if(args.ContainsKey("active_group_title") && args["active_group_title"] != null)
                ActiveGroupTitle = args["active_group_title"].AsString();

            if (args.ContainsKey("children_seeds") && (args["children_seeds"] != null) &&
                            (args["children_seeds"].Type == OSDType.Array))
            {
                OSDArray childrenSeeds = (OSDArray)(args["children_seeds"]);
                ChildrenCapSeeds = new Dictionary<ulong, string>();
                foreach (OSD o in childrenSeeds)
                {
                    if (o.Type == OSDType.Map)
                    {
                        ulong handle = 0;
                        string seed = "";
                        OSDMap pair = (OSDMap)o;
                        if (pair["handle"] != null)
                            if (!UInt64.TryParse(pair["handle"].AsString(), out handle))
                                continue;
                        if (pair["seed"] != null)
                            seed = pair["seed"].AsString();
                        if (!ChildrenCapSeeds.ContainsKey(handle))
                            ChildrenCapSeeds.Add(handle, seed);
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

            if (args["default_animation"] != null)
            {
                try
                {
                    DefaultAnim = new Animation((OSDMap)args["default_animation"]);
                }
                catch
                {
                    DefaultAnim = null;
                }
            }

            if (args["animation_state"] != null)
            {
                try
                {
                    AnimState = new Animation((OSDMap)args["animation_state"]);
                }
                catch
                {
                    AnimState = null;
                }
            }

            MovementAnimationOverRides.Clear();

            if (args["movementAO"] != null && args["movementAO"].Type == OSDType.Array)
            {
                OSDArray AOs = (OSDArray)(args["movementAO"]);
                int count = AOs.Count;

                for (int i = 0; i < count; i++)
                {
                    OSDMap ao = (OSDMap)AOs[i];
                    if (ao["state"] != null && ao["uuid"] != null)
                    {
                        string state = ao["state"].AsString();
                        UUID id = ao["uuid"].AsUUID();
                        MovementAnimationOverRides[state] = id;
                    }
                }
            }

            if (args.ContainsKey("motion_state"))
                MotionState = (byte)args["motion_state"].AsInteger();

            //if ((args["agent_textures"] != null) && (args["agent_textures"]).Type == OSDType.Array)
            //{
            //    OSDArray textures = (OSDArray)(args["agent_textures"]);
            //    AgentTextures = new UUID[textures.Count];
            //    int i = 0;
            //    foreach (OSD o in textures)
            //        AgentTextures[i++] = o.AsUUID();
            //}


            // packed_appearence should contain all appearance information
            if (args.ContainsKey("packed_appearance") && (args["packed_appearance"]).Type == OSDType.Map)
            {
                m_log.WarnFormat("[CHILDAGENTDATAUPDATE] got packed appearance");
                Appearance = new AvatarAppearance((OSDMap)args["packed_appearance"]);
            }
            else
            {
                // if missing try the old pack method
                m_log.WarnFormat("[CHILDAGENTDATAUPDATE] No packed appearance, checking old method");

                Appearance = new AvatarAppearance();

                // The code to unpack textures, visuals, wearables and attachments
                // should be removed; packed appearance contains the full appearance
                // This is retained for backward compatibility only
                if (args["texture_entry"] != null)
                {
                    byte[] rawtextures = args["texture_entry"].AsBinary();
                    Primitive.TextureEntry textures = new Primitive.TextureEntry(rawtextures, 0, rawtextures.Length);
                    Appearance.SetTextureEntries(textures);
                }

                if (args["visual_params"] != null)
                    Appearance.SetVisualParams(args["visual_params"].AsBinary());

                if ((args["wearables"] != null) && (args["wearables"]).Type == OSDType.Array)
                {
                    OSDArray wears = (OSDArray)(args["wearables"]);

                    for (int i = 0; i < wears.Count / 2; i++)
                    {
                        AvatarWearable awear = new AvatarWearable((OSDArray)wears[i]);
                        Appearance.SetWearable(i, awear);
                    }
                }

                if ((args["attachments"] != null) && (args["attachments"]).Type == OSDType.Array)
                {
                    OSDArray attachs = (OSDArray)(args["attachments"]);
                    foreach (OSD o in attachs)
                    {
                        if (o.Type == OSDType.Map)
                        {
                            // We know all of these must end up as attachments so we
                            // append rather than replace to ensure multiple attachments
                            // per point continues to work
                            //                        m_log.DebugFormat("[CHILDAGENTDATAUPDATE]: Appending attachments for {0}", AgentID);
                            Appearance.AppendAttachment(new AvatarAttachment((OSDMap)o));
                        }
                    }
                }
                // end of code to remove
            }

            if ((args["controllers"] != null) && (args["controllers"]).Type == OSDType.Array)
            {
                OSDArray controls = (OSDArray)(args["controllers"]);
                Controllers = new ControllerData[controls.Count];
                int i = 0;
                foreach (OSD o in controls)
                {
                    if (o.Type == OSDType.Map)
                    {
                        Controllers[i++] = new ControllerData((OSDMap)o);
                    }
                }
            }

            if (args["callback_uri"] != null)
                CallbackURI = args["callback_uri"].AsString();

            if (args["cb_uri"] != null)
                NewCallbackURI = args["cb_uri"].AsString();

            // Attachment objects
            if (args["attach_objects"] != null && args["attach_objects"].Type == OSDType.Array)
            {
                OSDArray attObjs = (OSDArray)(args["attach_objects"]);
                AttachmentObjects = new List<ISceneObject>();
                AttachmentObjectStates = new List<string>();
                foreach (OSD o in attObjs)
                {
                    if (o.Type == OSDType.Map)
                    {
                        OSDMap info = (OSDMap)o;
                        ISceneObject so = scene.DeserializeObject(info["sog"].AsString());
                        so.ExtraFromXmlString(info["extra"].AsString());
                        so.HasGroupChanged = info["modified"].AsBoolean();
                        AttachmentObjects.Add(so);
                        AttachmentObjectStates.Add(info["state"].AsString());
                    }
                }
            }

            if (args["parent_part"] != null)
                ParentPart = args["parent_part"].AsUUID();
            if (args["sit_offset"] != null)
                Vector3.TryParse(args["sit_offset"].AsString(), out SitOffset);
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
        public override OSDMap Pack(EntityTransferContext ctx)
        {
            return base.Pack(ctx);
        }

        public override void Unpack(OSDMap map, IScene scene, EntityTransferContext ctx)
        {
            base.Unpack(map, scene, ctx);
        }
    }
}
