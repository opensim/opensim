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

        public void SetLookAt(Vector3 value)
        {
            if (value.X == 0 && value.Y == 0)
            {
                AtAxis = Vector3.UnitX;
                LeftAxis = Vector3.UnitY;
                AtAxis = Vector3.UnitZ;
                return;
            }
            AtAxis = new Vector3(value.X, value.Y, 0);
            AtAxis.Normalize();
            LeftAxis = new Vector3(-AtAxis.Y, AtAxis.X, 0);
            UpAxis = Vector3.UnitZ;
        }

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

            if (!string.IsNullOrEmpty(CallbackURI))
                args["callback_uri"] = OSD.FromString(CallbackURI);

            if (!string.IsNullOrEmpty(NewCallbackURI))
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
            OSD tmp;
            if (args.TryGetValue("region_id", out tmp) && tmp != null)
                UUID.TryParse(tmp.AsString(), out RegionID);

            if (args.TryGetValue("circuit_code", out tmp) && tmp != null)
                UInt32.TryParse(tmp.AsString(), out CircuitCode);

            if (args.TryGetValue("agent_uuid", out tmp) && tmp != null)
                AgentID = tmp.AsUUID();

            if (args.TryGetValue("session_uuid", out tmp) && tmp != null)
                SessionID = tmp.AsUUID();

            if (args.TryGetValue("position", out tmp) && tmp != null)
                Vector3.TryParse(tmp.AsString(), out Position);

            if (args.TryGetValue("velocity", out tmp) && tmp != null)
                Vector3.TryParse(tmp.AsString(), out Velocity);

            if (args.TryGetValue("center", out tmp) && tmp != null)
                Vector3.TryParse(tmp.AsString(), out Center);

            if (args.TryGetValue("size", out tmp) && tmp != null)
                Vector3.TryParse(tmp.AsString(), out Size);

            if (args.TryGetValue("at_axis", out tmp) && tmp != null)
                Vector3.TryParse(tmp.AsString(), out AtAxis);

            if (args.TryGetValue("left_axis", out tmp) && tmp != null)
                Vector3.TryParse(tmp.AsString(), out LeftAxis);

            if (args.TryGetValue("up_axis", out tmp) && tmp != null)
                Vector3.TryParse(tmp.AsString(), out UpAxis);

            if (args.TryGetValue("wait_for_root", out tmp) && tmp != null)
                SenderWantsToWaitForRoot = tmp.AsBoolean();

            if (args.TryGetValue("far", out tmp) && tmp != null)
                Far = (float)(tmp.AsReal());

            if (args.TryGetValue("aspect", out tmp) && tmp != null)
                Aspect = (float)tmp.AsReal();

            if (args.TryGetValue("throttles", out tmp) && tmp != null)
                Throttles = tmp.AsBinary();

            if (args.TryGetValue("locomotion_state", out tmp) && tmp != null)
                UInt32.TryParse(tmp.AsString(), out LocomotionState);

            if (args.TryGetValue("head_rotation", out tmp) && tmp != null)
                Quaternion.TryParse(tmp.AsString(), out HeadRotation);

            if (args.TryGetValue("body_rotation", out tmp) && tmp != null)
                Quaternion.TryParse(tmp.AsString(), out BodyRotation);

            if (args.TryGetValue("control_flags", out tmp) && tmp != null)
                UInt32.TryParse(tmp.AsString(), out ControlFlags);

            if (args.TryGetValue("energy_level", out tmp) && tmp != null)
                EnergyLevel = (float)(tmp.AsReal());

            if (args.TryGetValue("god_data", out tmp) && tmp != null)
                GodData = tmp;

            if (args.TryGetValue("always_run", out tmp) && tmp != null)
                AlwaysRun = tmp.AsBoolean();

            if (args.TryGetValue("prey_agent", out tmp) && tmp != null)
                PreyAgent = tmp.AsUUID();

            if (args.TryGetValue("agent_access", out tmp) && tmp != null)
                Byte.TryParse(tmp.AsString(), out AgentAccess);

            if (args.TryGetValue("agent_cof", out tmp) && tmp != null)
                agentCOF = tmp.AsUUID();

            if (args.TryGetValue("crossingflags", out tmp) && tmp != null)
                CrossingFlags = (byte)tmp.AsInteger();

            if(CrossingFlags != 0)
            {
                if (args.TryGetValue("crossExtraFlags", out tmp) && tmp != null)
                    CrossExtraFlags = (byte)tmp.AsInteger();
            }

            if (args.TryGetValue("active_group_id", out tmp) && tmp != null)
                ActiveGroupID = tmp.AsUUID();

            if (args.TryGetValue("active_group_name", out tmp) && tmp != null)
                ActiveGroupName = tmp.AsString();

            if(args.TryGetValue("active_group_title", out tmp) && tmp != null)
                ActiveGroupTitle = tmp.AsString();

            if (args.TryGetValue("children_seeds", out tmp) && tmp is OSDArray)
            {
                OSDArray childrenSeeds = (OSDArray)tmp;
                ChildrenCapSeeds = new Dictionary<ulong, string>();
                foreach (OSD o in childrenSeeds)
                {
                    if (o is OSDMap)
                    {
                        ulong handle = 0;
                        string seed = "";
                        OSDMap pair = (OSDMap)o;
                        if (pair.TryGetValue("handle", out tmp))
                            if (!UInt64.TryParse(tmp.AsString(), out handle))
                                continue;
                        if (pair.TryGetValue("seed", out tmp))
                            seed = tmp.AsString();
                        if (!ChildrenCapSeeds.ContainsKey(handle))
                            ChildrenCapSeeds.Add(handle, seed);
                    }
                }
            }

            if (args.TryGetValue("animations", out tmp) && tmp is OSDArray)
            {
                OSDArray anims = (OSDArray)tmp;
                Anims = new Animation[anims.Count];
                int i = 0;
                foreach (OSD o in anims)
                {
                    if (o is OSDMap)
                    {
                        Anims[i++] = new Animation((OSDMap)o);
                    }
                }
            }

            if (args.TryGetValue("default_animation", out tmp) && tmp is OSDMap)
            {
                try
                {
                    DefaultAnim = new Animation((OSDMap)tmp);
                }
                catch
                {
                    DefaultAnim = null;
                }
            }

            if (args.TryGetValue("animation_state", out tmp) && tmp is OSDMap)
            {
                try
                {
                    AnimState = new Animation((OSDMap)tmp);
                }
                catch
                {
                    AnimState = null;
                }
            }

            MovementAnimationOverRides.Clear();

            if (args.TryGetValue("movementAO", out tmp) && tmp is OSDArray)
            {
                OSDArray AOs = (OSDArray)tmp;
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

            if (args.TryGetValue("motion_state", out tmp) && tmp != null)
                MotionState = (byte)tmp.AsInteger();

            //if ((args["agent_textures"] != null) && (args["agent_textures"]).Type == OSDType.Array)
            //{
            //    OSDArray textures = (OSDArray)(args["agent_textures"]);
            //    AgentTextures = new UUID[textures.Count];
            //    int i = 0;
            //    foreach (OSD o in textures)
            //        AgentTextures[i++] = o.AsUUID();
            //}


            // packed_appearence should contain all appearance information
            if (args.TryGetValue("packed_appearance", out tmp) && tmp is OSDMap)
            {
                //m_log.WarnFormat("[CHILDAGENTDATAUPDATE] got packed appearance");
                Appearance = new AvatarAppearance((OSDMap)tmp);
            }
            else
            {
                // if missing try the old pack method
                m_log.WarnFormat("[CHILDAGENTDATAUPDATE] No packed appearance, checking old method");

                Appearance = new AvatarAppearance();

                // The code to unpack textures, visuals, wearables and attachments
                // should be removed; packed appearance contains the full appearance
                // This is retained for backward compatibility only
                if (args.TryGetValue("texture_entry", out tmp) && tmp != null)
                {
                    byte[] rawtextures = tmp.AsBinary();
                    Primitive.TextureEntry textures = new Primitive.TextureEntry(rawtextures, 0, rawtextures.Length);
                    Appearance.SetTextureEntries(textures);
                }

                if (args.TryGetValue("visual_params", out tmp) && tmp != null)
                    Appearance.SetVisualParams(tmp.AsBinary());

                if (args.TryGetValue("wearables", out tmp) && tmp is OSDArray)
                {
                    OSDArray wears = (OSDArray)tmp;

                    for (int i = 0; i < wears.Count / 2; i++)
                    {
                        AvatarWearable awear = new AvatarWearable((OSDArray)wears[i]);
                        Appearance.SetWearable(i, awear);
                    }
                }

                if (args.TryGetValue("attachments", out tmp) && tmp is OSDArray)
                {
                    OSDArray attachs = (OSDArray)tmp;
                    foreach (OSD o in attachs)
                    {
                        if (o is OSDMap)
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

            if (args.TryGetValue("controllers", out tmp) && tmp is OSDArray)
            {
                OSDArray controls = (OSDArray)tmp;
                Controllers = new ControllerData[controls.Count];
                int i = 0;
                foreach (OSD o in controls)
                {
                    if (o is OSDMap)
                    {
                        Controllers[i++] = new ControllerData((OSDMap)o);
                    }
                }
            }

            if (args.TryGetValue("callback_uri", out tmp) && tmp != null)
                CallbackURI = tmp.AsString();

            if (args.TryGetValue("cb_uri", out tmp) && tmp != null)
                NewCallbackURI = tmp.AsString();

            // Attachment objects
            if (args.TryGetValue("attach_objects", out tmp) && tmp is OSDArray)
            {
                OSDArray attObjs = (OSDArray)tmp;
                AttachmentObjects = new List<ISceneObject>();
                AttachmentObjectStates = new List<string>();
                foreach (OSD o in attObjs)
                {
                    if (o is OSDMap)
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

            if (args.TryGetValue("parent_part", out tmp) && tmp != null)
                ParentPart = tmp.AsUUID();
            if (args.TryGetValue("sit_offset", out tmp) && tmp != null)
                Vector3.TryParse(tmp.AsString(), out SitOffset);
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
