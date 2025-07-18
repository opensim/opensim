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
                if (g.TryGetValue("ViewerUiIsGod", out OSD vuiig))
                    args["god_level"] = vuiig.AsBoolean() ? 200 : 0;
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
            OSD osdtmp;
            if (args.TryGetValue("region_handle", out osdtmp) && osdtmp != null)
                _ = UInt64.TryParse(osdtmp.AsString(), out RegionHandle);

            if (args.TryGetValue("circuit_code", out osdtmp) && osdtmp != null)
                _ = UInt32.TryParse(osdtmp.AsString(), out CircuitCode);

            if (args.TryGetValue("agent_uuid", out osdtmp) && osdtmp != null)
                AgentID = osdtmp.AsUUID();

            if (args.TryGetValue("session_uuid", out osdtmp) && osdtmp != null)
                SessionID = osdtmp.AsUUID();

            if (args.TryGetValue("position", out osdtmp) && osdtmp != null)
                _ = Vector3.TryParse(osdtmp.AsString(), out Position);

            if (args.TryGetValue("velocity", out osdtmp) && osdtmp != null)
                _ = Vector3.TryParse(osdtmp.AsString(), out Velocity);

            if (args.TryGetValue("center", out osdtmp) && osdtmp != null)
                _ = Vector3.TryParse(osdtmp.AsString(), out Center);

            if (args.TryGetValue("size", out osdtmp) && osdtmp != null)
                _ = Vector3.TryParse(osdtmp.AsString(), out Size);

            if (args.TryGetValue("at_axis", out osdtmp) && osdtmp != null)
                _ = Vector3.TryParse(osdtmp.AsString(), out AtAxis);

            if (args.TryGetValue("left_axis", out osdtmp) && osdtmp != null)
                _ = Vector3.TryParse(osdtmp.AsString(), out LeftAxis);

            if (args.TryGetValue("up_axis", out osdtmp) && osdtmp != null)
                _ = Vector3.TryParse(osdtmp.AsString(), out UpAxis);

            if (args.TryGetValue("changed_grid", out osdtmp) && osdtmp != null)
                ChangedGrid = osdtmp.AsBoolean();

            if (args.TryGetValue("god_data", out osdtmp))
                GodData = osdtmp;

            if (args.TryGetValue("far", out osdtmp) && osdtmp != null)
                Far = (float)(osdtmp.AsReal());

            if (args.TryGetValue("throttles", out osdtmp) && osdtmp != null)
                Throttles = osdtmp.AsBinary();

            if (args.TryGetValue("children_seeds", out osdtmp) && osdtmp is OSDArray childrenSeeds)
            {
                ChildrenCapSeeds = new Dictionary<ulong, string>();
                foreach (OSD o in childrenSeeds)
                {
                    if (o is OSDMap pair)
                    {
                        if (pair.TryGetValue("handle", out osdtmp) && osdtmp != null)
                        {
                            if (UInt64.TryParse(osdtmp.AsString(), out ulong handle))
                            { 
                                if (pair.TryGetValue("seed", out osdtmp))
                                    ChildrenCapSeeds.TryAdd(handle, osdtmp.AsString());
                                else
                                    ChildrenCapSeeds.TryAdd(handle, string.Empty);
                            }
                        }
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
            OSD osdtmp;
            if (args.TryGetValue("group_id", out osdtmp) && osdtmp != null)
                GroupID = osdtmp.AsUUID();
            if (args.TryGetValue("group_powers", out osdtmp) && osdtmp != null)
                UInt64.TryParse(osdtmp.AsString(), out GroupPowers);
            if (args.TryGetValue("accept_notices", out osdtmp) && osdtmp != null)
                AcceptNotices = osdtmp.AsBoolean();
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
            OSD osdtmp;
            if (args.TryGetValue("object", out osdtmp) && osdtmp != null)
                ObjectID = osdtmp.AsUUID();
            if (args.TryGetValue("item", out osdtmp) && osdtmp != null)
                ItemID = osdtmp.AsUUID();
            if (args.TryGetValue("ignore", out osdtmp) && osdtmp != null)
                IgnoreControls = (uint)osdtmp.AsInteger();
            if (args.TryGetValue("event", out osdtmp) && osdtmp != null)
                EventControls = (uint)osdtmp.AsInteger();
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

        // Scripted
        public ControllerData[] Controllers;

        public string CallbackURI;
        public string NewCallbackURI;

        // These two must have the same Count
        public List<ISceneObject> AttachmentObjects;
        public List<string> AttachmentObjectStates;

        public Dictionary<string, UUID> MovementAnimationOverRides = new Dictionary<string, UUID>();

        public List<UUID> CachedFriendsOnline;

        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

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
            //m_log.InfoFormat("[CHILDAGENTDATAUPDATE] Pack data");

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
                    args["god_level"] = g["ViewerUiIsGod"].AsBoolean() ? 200 : 0;
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

            if(CachedFriendsOnline != null && CachedFriendsOnline.Count > 0)
            {
                OSDArray cfonl = new OSDArray(CachedFriendsOnline.Count);
                foreach(UUID id in CachedFriendsOnline)
                    cfonl.Add(id);
                args["cfonline"] = cfonl;
            }

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
                _ = UUID.TryParse(tmp.AsString(), out RegionID);

            if (args.TryGetValue("circuit_code", out tmp) && tmp != null)
                UInt32.TryParse(tmp.AsString(), out CircuitCode);

            if (args.TryGetValue("agent_uuid", out tmp) && tmp != null)
                AgentID = tmp.AsUUID();

            if (args.TryGetValue("session_uuid", out tmp) && tmp != null)
                SessionID = tmp.AsUUID();

            if (args.TryGetValue("position", out tmp) && tmp != null)
                _ = Vector3.TryParse(tmp.AsString(), out Position);

            if (args.TryGetValue("velocity", out tmp) && tmp != null)
                _ = Vector3.TryParse(tmp.AsString(), out Velocity);

            if (args.TryGetValue("center", out tmp) && tmp != null)
                _ = Vector3.TryParse(tmp.AsString(), out Center);

            if (args.TryGetValue("size", out tmp) && tmp != null)
                _ = Vector3.TryParse(tmp.AsString(), out Size);

            if (args.TryGetValue("at_axis", out tmp) && tmp != null)
                _ = Vector3.TryParse(tmp.AsString(), out AtAxis);

            if (args.TryGetValue("left_axis", out tmp) && tmp != null)
                _ = Vector3.TryParse(tmp.AsString(), out LeftAxis);

            if (args.TryGetValue("up_axis", out tmp) && tmp != null)
                Vector3.TryParse(tmp.AsString(), out UpAxis);

            if (args.TryGetValue("wait_for_root", out tmp) && tmp != null)
                SenderWantsToWaitForRoot = tmp.AsBoolean();

            if (args.TryGetValue("far", out tmp) && tmp != null)
                Far = (float)tmp.AsReal();

            if (args.TryGetValue("aspect", out tmp) && tmp != null)
                Aspect = (float)tmp.AsReal();

            if (args.TryGetValue("throttles", out tmp) && tmp != null)
                Throttles = tmp.AsBinary();

            if (args.TryGetValue("locomotion_state", out tmp) && tmp != null)
                _ = UInt32.TryParse(tmp.AsString(), out LocomotionState);

            if (args.TryGetValue("head_rotation", out tmp) && tmp != null)
                _ = Quaternion.TryParse(tmp.AsString(), out HeadRotation);

            if (args.TryGetValue("body_rotation", out tmp) && tmp != null)
                _ = Quaternion.TryParse(tmp.AsString(), out BodyRotation);

            if (args.TryGetValue("control_flags", out tmp) && tmp != null)
                _ = UInt32.TryParse(tmp.AsString(), out ControlFlags);

            if (args.TryGetValue("energy_level", out tmp) && tmp != null)
                EnergyLevel = (float)tmp.AsReal();

            if (args.TryGetValue("god_data", out tmp) && tmp != null)
                GodData = tmp;

            if (args.TryGetValue("always_run", out tmp) && tmp != null)
                AlwaysRun = tmp.AsBoolean();

            if (args.TryGetValue("prey_agent", out tmp) && tmp != null)
                PreyAgent = tmp.AsUUID();

            if (args.TryGetValue("agent_access", out tmp) && tmp != null)
                _ = Byte.TryParse(tmp.AsString(), out AgentAccess);

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

            if (args.TryGetValue("children_seeds", out tmp) && tmp is OSDArray childrenSeeds)
            {
                ChildrenCapSeeds = new Dictionary<ulong, string>();
                foreach (OSD o in childrenSeeds)
                {
                    if (o is OSDMap pair)
                    {
                        if (pair.TryGetValue("handle", out tmp) && tmp != null && UInt64.TryParse(tmp.AsString(), out ulong handle))
                        {
                            if (pair.TryGetValue("seed", out tmp))
                                ChildrenCapSeeds.TryAdd(handle, tmp.AsString());
                            else
                                ChildrenCapSeeds.Add(handle, string.Empty);
                        }
                    }
                }
            }

            if (args.TryGetValue("animations", out tmp) && tmp is OSDArray anims)
            {
                Anims = new Animation[anims.Count];
                int i = 0;
                foreach (OSD o in anims)
                {
                    if (o is OSDMap om)
                        Anims[i++] = new Animation(om);
                }
            }

            if (args.TryGetValue("default_animation", out tmp) && tmp is OSDMap tmpm)
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

            if (args.TryGetValue("animation_state", out tmp) && tmp is OSDMap tmpms)
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

            if (args.TryGetValue("movementAO", out tmp) && tmp is OSDArray AOs)
            {
                for (int i = 0; i < AOs.Count; i++)
                {
                    if(AOs[i] is OSDMap ao)
                    {
                        if (ao.TryGetValue("state", out OSD st) && st != null &&
                            ao.TryGetValue("uuid", out OSD uid) && uid != null)
                        {
                            string state = st.AsString();
                            UUID id = uid.AsUUID();
                            MovementAnimationOverRides[state] = id;
                        }
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
            if (args.TryGetValue("packed_appearance", out tmp) && tmp is OSDMap pam)
            {
                //m_log.WarnFormat("[CHILDAGENTDATAUPDATE] got packed appearance");
                Appearance = new AvatarAppearance(pam);
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

                if (args.TryGetValue("wearables", out tmp) && tmp is OSDArray wears)
                {
                    for (int i = 0; i < wears.Count / 2; i++)
                    {
                        AvatarWearable awear = new AvatarWearable((OSDArray)wears[i]);
                        Appearance.SetWearable(i, awear);
                    }
                }

                if (args.TryGetValue("attachments", out tmp) && tmp is OSDArray attachs)
                {
                    foreach (OSD o in attachs)
                    {
                        if (o is OSDMap att)
                        {
                            // We know all of these must end up as attachments so we
                            // append rather than replace to ensure multiple attachments
                            // per point continues to work
                            //                        m_log.DebugFormat("[CHILDAGENTDATAUPDATE]: Appending attachments for {0}", AgentID);
                            Appearance.AppendAttachment(new AvatarAttachment(att));
                        }
                    }
                }
                // end of code to remove
            }

            if (args.TryGetValue("controllers", out tmp) && tmp is OSDArray controls)
            {
                Controllers = new ControllerData[controls.Count];
                int i = 0;
                foreach (OSD o in controls)
                {
                    if (o is OSDMap cntr)
                    {
                        Controllers[i++] = new ControllerData(cntr);
                    }
                }
            }

            if (args.TryGetValue("callback_uri", out tmp) && tmp != null)
                CallbackURI = tmp.AsString();

            if (args.TryGetValue("cb_uri", out tmp) && tmp != null)
                NewCallbackURI = tmp.AsString();

            // Attachment objects
            if (args.TryGetValue("attach_objects", out tmp) && tmp is OSDArray attObjs)
            {
                AttachmentObjects = new List<ISceneObject>();
                AttachmentObjectStates = new List<string>();
                foreach (OSD o in attObjs)
                {
                    if (o is OSDMap info)
                    {
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

            if (args.TryGetValue("cfonline", out tmp) && tmp is OSDArray cfonl)
            {
                CachedFriendsOnline = new List<UUID>(cfonl.Count);
                foreach(OSD o in cfonl)
                    CachedFriendsOnline.Add(o.AsUUID());
            } 
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
