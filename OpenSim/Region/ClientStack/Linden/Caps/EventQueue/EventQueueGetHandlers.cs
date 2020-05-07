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
using System.Net;
using System.Reflection;
using System.Text;
using log4net;
using Nini.Config;
using Mono.Addins;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using OpenSim.Framework;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using Caps=OpenSim.Framework.Capabilities.Caps;

namespace OpenSim.Region.ClientStack.Linden
{
    public partial class EventQueueGetModule : IEventQueue, INonSharedRegionModule
    {
        /* this is not a event message
                public void DisableSimulator(ulong handle, UUID avatarID)
                {
                    OSD item = EventQueueHelper.DisableSimulator(handle);
                    Enqueue(item, avatarID);
                }
        */

        public StringBuilder StartEvent(string eventName)
        {
            StringBuilder sb = new StringBuilder(256);
            LLSDxmlEncode.AddMap(sb);
            LLSDxmlEncode.AddElem("message", eventName, sb);
            LLSDxmlEncode.AddMap("body", sb);

            return sb;
        }

        public StringBuilder StartEvent(string eventName, int cap)
        {
            StringBuilder sb = new StringBuilder(cap);
            LLSDxmlEncode.AddMap(sb);
            LLSDxmlEncode.AddElem("message", eventName, sb);
            LLSDxmlEncode.AddMap("body", sb);

            return sb;
        }

        public string EndEvent(StringBuilder sb)
        {
            LLSDxmlEncode.AddEndMap(sb); // close body
            LLSDxmlEncode.AddEndMap(sb); // close event
            return sb.ToString();
        }

        public byte[] EndEventToBytes(StringBuilder sb)
        {
            LLSDxmlEncode.AddEndMap(sb); // close body
            LLSDxmlEncode.AddEndMap(sb); // close event
            return Util.UTF8NBGetbytes(sb.ToString());
        }

        public virtual void EnableSimulator(ulong handle, IPEndPoint endPoint, UUID avatarID, int regionSizeX, int regionSizeY)
        {
            if (DebugLevel > 0)
                m_log.DebugFormat("{0} EnableSimulator. handle={1}, endPoint={2}, avatarID={3}",
                    LogHeader, handle, endPoint, avatarID, regionSizeX, regionSizeY);

            StringBuilder sb = StartEvent("EnableSimulator");
            LLSDxmlEncode.AddArrayAndMap("SimulatorInfo", sb);
                LLSDxmlEncode.AddElem("Handle", handle, sb);
                LLSDxmlEncode.AddElem("IP", endPoint.Address.GetAddressBytes(), sb);
                LLSDxmlEncode.AddElem("Port", endPoint.Port, sb);
                LLSDxmlEncode.AddElem("RegionSizeX", (uint)regionSizeX, sb);
                LLSDxmlEncode.AddElem("RegionSizeY", (uint)regionSizeY, sb);
            LLSDxmlEncode.AddEndMapAndArray(sb);

            Enqueue(EndEventToBytes(sb), avatarID);
        }

        public virtual void EstablishAgentCommunication(UUID avatarID, IPEndPoint endPoint, string capsPath,
                                ulong regionHandle, int regionSizeX, int regionSizeY)
        {
            if (DebugLevel > 0)
                m_log.DebugFormat("{0} EstablishAgentCommunication. handle={1}, endPoint={2}, avatarID={3}",
                    LogHeader, regionHandle, endPoint, avatarID, regionSizeX, regionSizeY);

            StringBuilder sb = StartEvent("EstablishAgentCommunication");

            LLSDxmlEncode.AddElem("agent-id", avatarID, sb);
            LLSDxmlEncode.AddElem("sim-ip-and-port", endPoint.ToString(), sb);
            LLSDxmlEncode.AddElem("seed-capability", capsPath, sb);
            // current viewers ignore this, also not needed its sent on enablesim
            //LLSDxmlEncode.AddElem("region-handle", regionHandle, sb);
            //LLSDxmlEncode.AddElem("region-size-x", (uint)regionSizeX, sb);
            //LLSDxmlEncode.AddElem("region-size-y", (uint)regionSizeY, sb);

            Enqueue(EndEventToBytes(sb), avatarID);
        }

        public virtual void TeleportFinishEvent(ulong regionHandle, byte simAccess,
                                        IPEndPoint regionExternalEndPoint,
                                        uint locationID, uint flags, string capsURL,
                                        UUID avatarID, int regionSizeX, int regionSizeY)
        {
            if (DebugLevel > 0)
                m_log.DebugFormat("{0} TeleportFinishEvent. handle={1}, endPoint={2}, avatarID={3}",
                    LogHeader, regionHandle, regionExternalEndPoint, avatarID, regionSizeX, regionSizeY);

            // not sure why flags get overwritten here
            if ((flags & (uint)TeleportFlags.IsFlying) != 0)
                flags = (uint)TeleportFlags.ViaLocation | (uint)TeleportFlags.IsFlying;
            else
                flags = (uint)TeleportFlags.ViaLocation;

            StringBuilder sb = StartEvent("TeleportFinish");

            LLSDxmlEncode.AddArrayAndMap("Info", sb);
                LLSDxmlEncode.AddElem("AgentID", avatarID, sb);
                LLSDxmlEncode.AddElem("LocationID", (uint)4, sb); // TODO what is this?
                LLSDxmlEncode.AddElem("SimIP", regionExternalEndPoint.Address.GetAddressBytes(), sb);
                LLSDxmlEncode.AddElem("SimPort", regionExternalEndPoint.Port, sb);
                LLSDxmlEncode.AddElem("RegionHandle", regionHandle, sb);
                LLSDxmlEncode.AddElem("SeedCapability", capsURL, sb);
                LLSDxmlEncode.AddElem("SimAccess",(int)simAccess, sb);
                LLSDxmlEncode.AddElem("TeleportFlags", flags, sb);
                LLSDxmlEncode.AddElem("RegionSizeX", (uint)regionSizeX, sb);
                LLSDxmlEncode.AddElem("RegionSizeY", (uint)regionSizeY, sb);
            LLSDxmlEncode.AddEndMapAndArray(sb);

            Enqueue(EndEventToBytes(sb), avatarID);
        }

        public virtual void CrossRegion(ulong handle, Vector3 pos, Vector3 lookAt,
                                IPEndPoint newRegionExternalEndPoint,
                                string capsURL, UUID avatarID, UUID sessionID, int regionSizeX, int regionSizeY)
        {
            if (DebugLevel > 0)
                m_log.DebugFormat("{0} CrossRegion. handle={1}, avatarID={2}, regionSize={3},{4}>",
                    LogHeader, handle, avatarID, regionSizeX, regionSizeY);

            StringBuilder sb = StartEvent("CrossedRegion");

            LLSDxmlEncode.AddArrayAndMap("AgentData", sb);
                LLSDxmlEncode.AddElem("AgentID", avatarID, sb);
                LLSDxmlEncode.AddElem("SessionID", sessionID, sb);
            LLSDxmlEncode.AddEndMapAndArray(sb);

            LLSDxmlEncode.AddArrayAndMap("Info", sb);
                LLSDxmlEncode.AddElem("LookAt", lookAt, sb);
                LLSDxmlEncode.AddElem("Position", pos, sb);
            LLSDxmlEncode.AddEndMapAndArray(sb);

            LLSDxmlEncode.AddArrayAndMap("RegionData", sb);
                LLSDxmlEncode.AddElem("RegionHandle", handle, sb);
                LLSDxmlEncode.AddElem("SeedCapability", capsURL, sb);
                LLSDxmlEncode.AddElem("SimIP", newRegionExternalEndPoint.Address.GetAddressBytes(), sb);
                LLSDxmlEncode.AddElem("SimPort", newRegionExternalEndPoint.Port, sb);
                LLSDxmlEncode.AddElem("RegionSizeX", (uint)regionSizeX, sb);
                LLSDxmlEncode.AddElem("RegionSizeY", (uint)regionSizeY, sb);
            LLSDxmlEncode.AddEndMapAndArray(sb);

            Enqueue(EndEventToBytes(sb), avatarID);
        }

        private static string InstantMessageBody(UUID fromAgent, string message, UUID toAgent,
            string fromName, byte dialog, uint timeStamp, bool offline, int parentEstateID,
            Vector3 position, uint ttl, UUID transactionID, bool fromGroup, byte[] binaryBucket,
            bool checkEstate, int godLevel, bool limitedToEstate)
        {
            StringBuilder sb = new StringBuilder(512);
            LLSDxmlEncode.AddMap("instantmessage", sb);
            LLSDxmlEncode.AddMap("message_params", sb); //messageParams
            LLSDxmlEncode.AddElem("type", dialog, sb);
            LLSDxmlEncode.AddElem("position", position, sb);
            LLSDxmlEncode.AddElem("region_id", UUID.Zero, sb);
            LLSDxmlEncode.AddElem("to_id", toAgent, sb);
            LLSDxmlEncode.AddElem("source", 0, sb);

            LLSDxmlEncode.AddMap("data", sb); //messageParams data
            LLSDxmlEncode.AddElem("binary_bucket", binaryBucket, sb);
            LLSDxmlEncode.AddEndMap(sb); //messageParams data

            LLSDxmlEncode.AddElem("message", message, sb);
            LLSDxmlEncode.AddElem("id", transactionID, sb);
            LLSDxmlEncode.AddElem("from_name", fromName, sb);
            LLSDxmlEncode.AddElem("timestamp", timeStamp, sb);
            LLSDxmlEncode.AddElem("offline", (offline ? 1 : 0), sb);
            LLSDxmlEncode.AddElem("parent_estate_id", parentEstateID, sb);
            LLSDxmlEncode.AddElem("ttl", (int)ttl, sb);
            LLSDxmlEncode.AddElem("from_id", fromAgent, sb);
            LLSDxmlEncode.AddElem("from_group", fromGroup, sb);
            LLSDxmlEncode.AddEndMap(sb); //messageParams

            LLSDxmlEncode.AddMap("agent_params", sb);
            LLSDxmlEncode.AddElem("agent_id", fromAgent, sb);
            LLSDxmlEncode.AddElem("check_estate", checkEstate, sb);
            LLSDxmlEncode.AddElem("god_level", godLevel, sb);
            LLSDxmlEncode.AddElem("limited_to_estate", limitedToEstate, sb);
            LLSDxmlEncode.AddEndMap(sb); // agent params
            LLSDxmlEncode.AddEndMap(sb);

            return sb.ToString();
        }

        public void ChatterboxInvitation(UUID sessionID, string sessionName,
                                         UUID fromAgent, string message, UUID toAgent, string fromName, byte dialog,
                                         uint timeStamp, bool offline, int parentEstateID, Vector3 position,
                                         uint ttl, UUID transactionID, bool fromGroup, byte[] binaryBucket)
        {
            StringBuilder sb = StartEvent("ChatterBoxInvitation");
            LLSDxmlEncode.AddElem("session_id", sessionID, sb);
            LLSDxmlEncode.AddElem("from_name", fromName, sb);
            LLSDxmlEncode.AddElem("session_name", sessionName, sb);
            LLSDxmlEncode.AddElem("from_id", fromAgent, sb);

            LLSDxmlEncode.AddLLSD(InstantMessageBody(fromAgent, message, toAgent,
                fromName, dialog, timeStamp, offline, parentEstateID, position,
                ttl, transactionID, fromGroup, binaryBucket, true, 0, true), sb);

            Enqueue(EndEventToBytes(sb), toAgent);
        }

        public void ChatterBoxSessionAgentListUpdates(UUID sessionID, UUID toAgent, List<GroupChatListAgentUpdateData> updates)
        {
            StringBuilder sb = StartEvent("ChatterBoxSessionAgentListUpdates",1024);
            LLSDxmlEncode.AddMap("agent_updates",sb);
            foreach (GroupChatListAgentUpdateData up in updates)
            {
                LLSDxmlEncode.AddMap(up.agentID.ToString(), sb);
                    LLSDxmlEncode.AddMap("info", sb);
                        LLSDxmlEncode.AddElem("can_voice_chat", up.canVoice, sb);
                        LLSDxmlEncode.AddElem("is_moderator", up.isModerator, sb);
                        LLSDxmlEncode.AddMap("mutes",sb);
                            LLSDxmlEncode.AddElem("text", up.mutedText, sb);
                        LLSDxmlEncode.AddEndMap(sb); // mutes
                    LLSDxmlEncode.AddEndMap(sb); // info
                    if (up.enterOrLeave)
                        LLSDxmlEncode.AddElem("transition", "ENTER", sb);
                    else
                        LLSDxmlEncode.AddElem("transition", "LEAVE", sb);
                LLSDxmlEncode.AddEndMap(sb); //agentid
            }
            LLSDxmlEncode.AddEndMap(sb); // agent_updates
            LLSDxmlEncode.AddEmptyMap("updates",sb);
            LLSDxmlEncode.AddElem("session_id", sessionID, sb);

            Enqueue(EndEventToBytes(sb), toAgent);
        }

        public void ChatterBoxSessionStartReply(UUID sessionID, string sessionName, int type,
                                bool voiceEnabled, bool voiceModerated, UUID tmpSessionID,
                                bool sucess, string error,
                                UUID toAgent)
        {
            StringBuilder sb = StartEvent("ChatterBoxSessionStartReply");
            LLSDxmlEncode.AddElem("session_id", sessionID, sb);
            LLSDxmlEncode.AddElem("temp_session_id", tmpSessionID, sb);
            LLSDxmlEncode.AddElem("success", sucess, sb);
            if(sucess)
            {
                LLSDxmlEncode.AddMap("session_info", sb);
                    LLSDxmlEncode.AddMap("moderated_mode", sb);
                        LLSDxmlEncode.AddElem("voice", voiceModerated, sb);
                    LLSDxmlEncode.AddEndMap(sb);
                    LLSDxmlEncode.AddElem("session_name", sessionName, sb);
                    LLSDxmlEncode.AddElem("type", type, sb);
                    LLSDxmlEncode.AddElem("voice_enabled", voiceEnabled, sb);
                LLSDxmlEncode.AddEndMap(sb);
            }
            else
                LLSDxmlEncode.AddElem("error", String.IsNullOrEmpty(error) ? "" : error, sb);

            Enqueue(EndEventToBytes(sb), toAgent);
        }

        public void ChatterBoxForceClose(UUID toAgent, UUID sessionID, string reason)
        {
            StringBuilder sb = StartEvent("ForceCloseChatterBoxSession");
            LLSDxmlEncode.AddElem("session_id", sessionID, sb);
            LLSDxmlEncode.AddElem("reason", reason, sb);

            Enqueue(EndEventToBytes(sb), toAgent);
        }

        public void GroupMembershipData(UUID AgentID, GroupMembershipData[] data)
        {
            StringBuilder sb = StartEvent("AgentGroupDataUpdate");

            LLSDxmlEncode.AddArrayAndMap("AgentData", sb);
            LLSDxmlEncode.AddElem("AgentID", AgentID, sb);
            LLSDxmlEncode.AddEndMapAndArray(sb);

            if (data.Length == 0)
            {
                LLSDxmlEncode.AddEmptyArray("GroupData", sb);
                LLSDxmlEncode.AddEmptyArray("NewGroupData", sb);
            }
            else
            {
                List<bool> lstInProfiles = new List<bool>(data.Length);
                LLSDxmlEncode.AddArray("GroupData", sb);
                foreach (GroupMembershipData m in data)
                {
                    LLSDxmlEncode.AddMap(sb);
                    LLSDxmlEncode.AddElem("GroupID", m.GroupID, sb);
                    LLSDxmlEncode.AddElem("GroupPowers", m.GroupPowers, sb);
                    LLSDxmlEncode.AddElem("AcceptNotices", m.AcceptNotices, sb);
                    LLSDxmlEncode.AddElem("GroupInsigniaID", m.GroupPicture, sb);
                    LLSDxmlEncode.AddElem("Contribution", m.Contribution, sb);
                    LLSDxmlEncode.AddElem("GroupName", m.GroupName, sb);
                    LLSDxmlEncode.AddEndMap(sb);
                    lstInProfiles.Add(m.ListInProfile);
                }
                LLSDxmlEncode.AddEndArray(sb);

                LLSDxmlEncode.AddArray("NewGroupData", sb);
                foreach(bool b in lstInProfiles)
                {
                    LLSDxmlEncode.AddMap(sb);
                    LLSDxmlEncode.AddElem("ListInProfile", b, sb);
                    LLSDxmlEncode.AddEndMap(sb);
                }
                LLSDxmlEncode.AddEndArray(sb);
            }

            Enqueue(EndEventToBytes(sb), AgentID);
        }

        public void PlacesQueryReply(UUID avatarID, UUID queryID, UUID transactionID, PlacesReplyData[] replyDataArray)
        {
            StringBuilder sb = new StringBuilder(256);
            LLSDxmlEncode.AddMap(sb);
            LLSDxmlEncode.AddElem("message", "PlacesReplyMessage", sb);
            LLSDxmlEncode.AddMap("QueryData[]", sb); LLSDxmlEncode.AddArray(sb);
                LLSDxmlEncode.AddArray("AgentData", sb);
                    LLSDxmlEncode.AddMap(sb);
                        LLSDxmlEncode.AddElem("AgentID", avatarID, sb);
                        LLSDxmlEncode.AddElem("QueryID", queryID, sb);
                        LLSDxmlEncode.AddElem("TransactionID", transactionID, sb);
                    LLSDxmlEncode.AddEndMap(sb);
                LLSDxmlEncode.AddEndArray(sb);

                LLSDxmlEncode.AddArray("QueryData", sb);

                for (int i = 0; i < replyDataArray.Length; ++i)
                {
                    PlacesReplyData data = replyDataArray[i];
                    LLSDxmlEncode.AddMap(sb);
                        LLSDxmlEncode.AddElem("ActualArea", data.ActualArea, sb);
                        LLSDxmlEncode.AddElem("BillableArea", data.BillableArea, sb);
                        LLSDxmlEncode.AddElem("Description", data.Desc, sb);
                        LLSDxmlEncode.AddElem("Dwell", data.Dwell, sb);
                        LLSDxmlEncode.AddElem("Flags", data.Flags, sb);
                        LLSDxmlEncode.AddElem("GlobalX", data.GlobalX, sb);
                        LLSDxmlEncode.AddElem("GlobalY", data.GlobalY, sb);
                        LLSDxmlEncode.AddElem("GlobalZ", data.GlobalZ, sb);
                        LLSDxmlEncode.AddElem("Name", data.Name, sb);
                        LLSDxmlEncode.AddElem("OwnerID", data.OwnerID, sb);
                        LLSDxmlEncode.AddElem("SimName", data.SimName, sb);
                        LLSDxmlEncode.AddElem("SnapShotID", data.SnapshotID, sb);
                        LLSDxmlEncode.AddElem("ProductSku", (int)0, sb);
                        LLSDxmlEncode.AddElem("Price", data.Price, sb);
                    LLSDxmlEncode.AddEndMap(sb);
                }
                LLSDxmlEncode.AddEndArray(sb);

            Enqueue(EndEventToBytes(sb), avatarID);
        }

        public void ScriptRunningEvent(UUID objectID, UUID itemID, bool running, UUID avatarID)
        {
            StringBuilder sb = StartEvent("ScriptRunningReply");
            LLSDxmlEncode.AddArrayAndMap("Script", sb);
                LLSDxmlEncode.AddElem("ObjectID", objectID, sb);
                LLSDxmlEncode.AddElem("ItemID", itemID, sb);
                LLSDxmlEncode.AddElem("Running", running, sb);
                LLSDxmlEncode.AddElem("Mono", true, sb);
            LLSDxmlEncode.AddEndMapAndArray(sb);

            Enqueue(EndEventToBytes(sb), avatarID);
        }

        public void partPhysicsProperties(uint localID, byte physhapetype,
                        float density, float friction, float bounce, float gravmod, UUID avatarID)
        {
            StringBuilder sb = StartEvent("ObjectPhysicsProperties");
            LLSDxmlEncode.AddArrayAndMap("ObjectData", sb);
                LLSDxmlEncode.AddElem("LocalID", (int)localID, sb);
                LLSDxmlEncode.AddElem("Density", density, sb);
                LLSDxmlEncode.AddElem("Friction", friction, sb);
                LLSDxmlEncode.AddElem("GravityMultiplier", gravmod, sb);
                LLSDxmlEncode.AddElem("Restitution", bounce, sb);
                LLSDxmlEncode.AddElem("PhysicsShapeType", (int)physhapetype, sb);
            LLSDxmlEncode.AddEndMapAndArray(sb);

            Enqueue(EndEventToBytes(sb), avatarID);
        }

        public void WindlightRefreshEvent(int interpolate, UUID avatarID)
        {
            StringBuilder sb = StartEvent("WindLightRefresh");
            LLSDxmlEncode.AddElem("Interpolate", interpolate > 0 ? 1 : 0, sb);
            Enqueue(EndEventToBytes(sb), avatarID);
        }

        public static string KeepAliveEvent()
        {
            StringBuilder sb = new StringBuilder(256);
            LLSDxmlEncode.AddMap(sb);
            LLSDxmlEncode.AddElem("message", "FAKEEVENT", sb);
            LLSDxmlEncode.AddMap("body", sb);
            LLSDxmlEncode.AddEndMap(sb); // close body
            LLSDxmlEncode.AddEndMap(sb); // close event
            return sb.ToString();
        }

        public byte[] BuildEvent(string eventName, OSD eventBody)
        {
            OSDMap llsdEvent = new OSDMap(2);
            llsdEvent.Add("message", new OSDString(eventName));
            llsdEvent.Add("body", eventBody);

            return Util.UTF8NBGetbytes(OSDParser.SerializeLLSDInnerXmlString(llsdEvent));
        }
    }
}
