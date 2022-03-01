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

        public osUTF8 StartEvent(string eventName)
        {
            osUTF8 sb = OSUTF8Cached.Acquire();
            LLSDxmlEncode2.AddMap(sb);
            LLSDxmlEncode2.AddElem("message", eventName, sb);
            LLSDxmlEncode2.AddMap("body", sb);

            return sb;
        }

        public osUTF8 StartEvent(string eventName, int cap)
        {
            osUTF8 sb = OSUTF8Cached.Acquire(cap);
            LLSDxmlEncode2.AddMap(sb);
            LLSDxmlEncode2.AddElem("message", eventName, sb);
            LLSDxmlEncode2.AddMap("body", sb);

            return sb;
        }

        public byte[] EndEventToBytes(osUTF8 sb)
        {
            LLSDxmlEncode2.AddEndMap(sb); // close body
            LLSDxmlEncode2.AddEndMap(sb); // close event
            return OSUTF8Cached.GetArrayAndRelease(sb);
        }

        public virtual void EnableSimulator(ulong handle, IPEndPoint endPoint, UUID avatarID, int regionSizeX, int regionSizeY)
        {
            if (DebugLevel > 0)
                m_log.DebugFormat("{0} EnableSimulator. handle={1}, endPoint={2}, avatarID={3}",
                    LogHeader, handle, endPoint, avatarID, regionSizeX, regionSizeY);

            osUTF8 sb = StartEvent("EnableSimulator");
            LLSDxmlEncode2.AddArrayAndMap("SimulatorInfo", sb);
                LLSDxmlEncode2.AddElem("Handle", handle, sb);
                LLSDxmlEncode2.AddElem("IP", endPoint.Address.GetAddressBytes(), sb);
                LLSDxmlEncode2.AddElem("Port", endPoint.Port, sb);
                LLSDxmlEncode2.AddElem("RegionSizeX", (uint)regionSizeX, sb);
                LLSDxmlEncode2.AddElem("RegionSizeY", (uint)regionSizeY, sb);
            LLSDxmlEncode2.AddEndMapAndArray(sb);

            Enqueue(EndEventToBytes(sb), avatarID);
        }

        public virtual void EstablishAgentCommunication(UUID avatarID, IPEndPoint endPoint, string capsPath,
                                ulong regionHandle, int regionSizeX, int regionSizeY)
        {
            if (DebugLevel > 0)
                m_log.DebugFormat("{0} EstablishAgentCommunication. handle={1}, endPoint={2}, avatarID={3}",
                    LogHeader, regionHandle, endPoint, avatarID, regionSizeX, regionSizeY);

            osUTF8 sb = StartEvent("EstablishAgentCommunication");

            LLSDxmlEncode2.AddElem("agent-id", avatarID, sb);
            LLSDxmlEncode2.AddElem("sim-ip-and-port", endPoint.ToString(), sb);
            LLSDxmlEncode2.AddElem("seed-capability", capsPath, sb);
            // current viewers ignore this, also not needed its sent on enablesim
            //LLSDxmlEncode2.AddElem("region-handle", regionHandle, sb);
            //LLSDxmlEncode2.AddElem("region-size-x", (uint)regionSizeX, sb);
            //LLSDxmlEncode2.AddElem("region-size-y", (uint)regionSizeY, sb);

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

            osUTF8 sb = StartEvent("TeleportFinish");

            LLSDxmlEncode2.AddArrayAndMap("Info", sb);
                LLSDxmlEncode2.AddElem("AgentID", avatarID, sb);
                LLSDxmlEncode2.AddElem("LocationID", (uint)4, sb); // TODO what is this?
                LLSDxmlEncode2.AddElem("SimIP", regionExternalEndPoint.Address.GetAddressBytes(), sb);
                LLSDxmlEncode2.AddElem("SimPort", regionExternalEndPoint.Port, sb);
                LLSDxmlEncode2.AddElem("RegionHandle", regionHandle, sb);
                LLSDxmlEncode2.AddElem("SeedCapability", capsURL, sb);
                LLSDxmlEncode2.AddElem("SimAccess",(int)simAccess, sb);
                LLSDxmlEncode2.AddElem("TeleportFlags", flags, sb);
                LLSDxmlEncode2.AddElem("RegionSizeX", (uint)regionSizeX, sb);
                LLSDxmlEncode2.AddElem("RegionSizeY", (uint)regionSizeY, sb);
            LLSDxmlEncode2.AddEndMapAndArray(sb);

            Enqueue(EndEventToBytes(sb), avatarID);
        }

        public virtual void CrossRegion(ulong handle, Vector3 pos, Vector3 lookAt,
                                IPEndPoint newRegionExternalEndPoint,
                                string capsURL, UUID avatarID, UUID sessionID, int regionSizeX, int regionSizeY)
        {
            if (DebugLevel > 0)
                m_log.DebugFormat("{0} CrossRegion. handle={1}, avatarID={2}, regionSize={3},{4}>",
                    LogHeader, handle, avatarID, regionSizeX, regionSizeY);

            osUTF8 sb = StartEvent("CrossedRegion");

            LLSDxmlEncode2.AddArrayAndMap("AgentData", sb);
                LLSDxmlEncode2.AddElem("AgentID", avatarID, sb);
                LLSDxmlEncode2.AddElem("SessionID", sessionID, sb);
            LLSDxmlEncode2.AddEndMapAndArray(sb);

            LLSDxmlEncode2.AddArrayAndMap("Info", sb);
                LLSDxmlEncode2.AddElem("LookAt", lookAt, sb);
                LLSDxmlEncode2.AddElem("Position", pos, sb);
            LLSDxmlEncode2.AddEndMapAndArray(sb);

            LLSDxmlEncode2.AddArrayAndMap("RegionData", sb);
                LLSDxmlEncode2.AddElem("RegionHandle", handle, sb);
                LLSDxmlEncode2.AddElem("SeedCapability", capsURL, sb);
                LLSDxmlEncode2.AddElem("SimIP", newRegionExternalEndPoint.Address.GetAddressBytes(), sb);
                LLSDxmlEncode2.AddElem("SimPort", newRegionExternalEndPoint.Port, sb);
                LLSDxmlEncode2.AddElem("RegionSizeX", (uint)regionSizeX, sb);
                LLSDxmlEncode2.AddElem("RegionSizeY", (uint)regionSizeY, sb);
            LLSDxmlEncode2.AddEndMapAndArray(sb);

            Enqueue(EndEventToBytes(sb), avatarID);
        }

        private static string InstantMessageBody(UUID fromAgent, string message, UUID toAgent,
            string fromName, byte dialog, uint timeStamp, bool offline, int parentEstateID,
            Vector3 position, uint ttl, UUID transactionID, bool fromGroup, byte[] binaryBucket,
            bool checkEstate, int godLevel, bool limitedToEstate)
        {
            osUTF8 sb = new osUTF8(512);
            LLSDxmlEncode2.AddMap("instantmessage", sb);
            LLSDxmlEncode2.AddMap("message_params", sb); //messageParams
            LLSDxmlEncode2.AddElem("type", dialog, sb);
            LLSDxmlEncode2.AddElem("position", position, sb);
            LLSDxmlEncode2.AddElem("region_id", UUID.Zero, sb);
            LLSDxmlEncode2.AddElem("to_id", toAgent, sb);
            LLSDxmlEncode2.AddElem("source", 0, sb);

            LLSDxmlEncode2.AddMap("data", sb); //messageParams data
            LLSDxmlEncode2.AddElem("binary_bucket", binaryBucket, sb);
            LLSDxmlEncode2.AddEndMap(sb); //messageParams data

            LLSDxmlEncode2.AddElem("message", message, sb);
            LLSDxmlEncode2.AddElem("id", transactionID, sb);
            LLSDxmlEncode2.AddElem("from_name", fromName, sb);
            LLSDxmlEncode2.AddElem("timestamp", timeStamp, sb);
            LLSDxmlEncode2.AddElem("offline", (offline ? 1 : 0), sb);
            LLSDxmlEncode2.AddElem("parent_estate_id", parentEstateID, sb);
            LLSDxmlEncode2.AddElem("ttl", (int)ttl, sb);
            LLSDxmlEncode2.AddElem("from_id", fromAgent, sb);
            LLSDxmlEncode2.AddElem("from_group", fromGroup, sb);
            LLSDxmlEncode2.AddEndMap(sb); //messageParams

            LLSDxmlEncode2.AddMap("agent_params", sb);
            LLSDxmlEncode2.AddElem("agent_id", fromAgent, sb);
            LLSDxmlEncode2.AddElem("check_estate", checkEstate, sb);
            LLSDxmlEncode2.AddElem("god_level", godLevel, sb);
            LLSDxmlEncode2.AddElem("limited_to_estate", limitedToEstate, sb);
            LLSDxmlEncode2.AddEndMap(sb); // agent params
            LLSDxmlEncode2.AddEndMap(sb);

            return sb.ToString();
        }

        public void ChatterboxInvitation(UUID sessionID, string sessionName,
                                         UUID fromAgent, string message, UUID toAgent, string fromName, byte dialog,
                                         uint timeStamp, bool offline, int parentEstateID, Vector3 position,
                                         uint ttl, UUID transactionID, bool fromGroup, byte[] binaryBucket)
        {
            osUTF8 sb = StartEvent("ChatterBoxInvitation");
            LLSDxmlEncode2.AddElem("session_id", sessionID, sb);
            LLSDxmlEncode2.AddElem("from_name", fromName, sb);
            LLSDxmlEncode2.AddElem("session_name", sessionName, sb);
            LLSDxmlEncode2.AddElem("from_id", fromAgent, sb);

            LLSDxmlEncode2.AddLLSD(InstantMessageBody(fromAgent, message, toAgent,
                fromName, dialog, timeStamp, offline, parentEstateID, position,
                ttl, transactionID, fromGroup, binaryBucket, true, 0, true), sb);

            Enqueue(EndEventToBytes(sb), toAgent);
        }

        public void ChatterBoxSessionAgentListUpdates(UUID sessionID, UUID toAgent, List<GroupChatListAgentUpdateData> updates)
        {
            osUTF8 sb = StartEvent("ChatterBoxSessionAgentListUpdates");
            LLSDxmlEncode2.AddMap("agent_updates",sb);
            foreach (GroupChatListAgentUpdateData up in updates)
            {
                LLSDxmlEncode2.AddMap(up.agentID.ToString(), sb);
                    LLSDxmlEncode2.AddMap("info", sb);
                        LLSDxmlEncode2.AddElem("can_voice_chat", up.canVoice, sb);
                        LLSDxmlEncode2.AddElem("is_moderator", up.isModerator, sb);
                        LLSDxmlEncode2.AddMap("mutes",sb);
                            LLSDxmlEncode2.AddElem("text", up.mutedText, sb);
                        LLSDxmlEncode2.AddEndMap(sb); // mutes
                    LLSDxmlEncode2.AddEndMap(sb); // info
                    if (up.enterOrLeave)
                        LLSDxmlEncode2.AddElem("transition", "ENTER", sb);
                    else
                        LLSDxmlEncode2.AddElem("transition", "LEAVE", sb);
                LLSDxmlEncode2.AddEndMap(sb); //agentid
            }
            LLSDxmlEncode2.AddEndMap(sb); // agent_updates
            LLSDxmlEncode2.AddEmptyMap("updates",sb);
            LLSDxmlEncode2.AddElem("session_id", sessionID, sb);

            Enqueue(EndEventToBytes(sb), toAgent);
        }

        public void ChatterBoxSessionStartReply(UUID sessionID, string sessionName, int type,
                                bool voiceEnabled, bool voiceModerated, UUID tmpSessionID,
                                bool sucess, string error,
                                UUID toAgent)
        {
            osUTF8 sb = StartEvent("ChatterBoxSessionStartReply");
            LLSDxmlEncode2.AddElem("session_id", sessionID, sb);
            LLSDxmlEncode2.AddElem("temp_session_id", tmpSessionID, sb);
            LLSDxmlEncode2.AddElem("success", sucess, sb);
            if(sucess)
            {
                LLSDxmlEncode2.AddMap("session_info", sb);
                    LLSDxmlEncode2.AddMap("moderated_mode", sb);
                        LLSDxmlEncode2.AddElem("voice", voiceModerated, sb);
                    LLSDxmlEncode2.AddEndMap(sb);
                    LLSDxmlEncode2.AddElem("session_name", sessionName, sb);
                    LLSDxmlEncode2.AddElem("type", type, sb);
                    LLSDxmlEncode2.AddElem("voice_enabled", voiceEnabled, sb);
                LLSDxmlEncode2.AddEndMap(sb);
            }
            else
                LLSDxmlEncode2.AddElem("error", String.IsNullOrEmpty(error) ? "" : error, sb);

            Enqueue(EndEventToBytes(sb), toAgent);
        }

        public void ChatterBoxForceClose(UUID toAgent, UUID sessionID, string reason)
        {
            osUTF8 sb = StartEvent("ForceCloseChatterBoxSession");
            LLSDxmlEncode2.AddElem("session_id", sessionID, sb);
            LLSDxmlEncode2.AddElem("reason", reason, sb);

            Enqueue(EndEventToBytes(sb), toAgent);
        }

        public void GroupMembershipData(UUID AgentID, GroupMembershipData[] data)
        {
            osUTF8 sb = StartEvent("AgentGroupDataUpdate");

            LLSDxmlEncode2.AddArrayAndMap("AgentData", sb);
            LLSDxmlEncode2.AddElem("AgentID", AgentID, sb);
            LLSDxmlEncode2.AddEndMapAndArray(sb);

            if (data.Length == 0)
            {
                LLSDxmlEncode2.AddEmptyArray("GroupData", sb);
                LLSDxmlEncode2.AddEmptyArray("NewGroupData", sb);
            }
            else
            {
                List<bool> lstInProfiles = new List<bool>(data.Length);
                LLSDxmlEncode2.AddArray("GroupData", sb);
                foreach (GroupMembershipData m in data)
                {
                    LLSDxmlEncode2.AddMap(sb);
                    LLSDxmlEncode2.AddElem("GroupID", m.GroupID, sb);
                    LLSDxmlEncode2.AddElem("GroupPowers", m.GroupPowers, sb);
                    LLSDxmlEncode2.AddElem("AcceptNotices", m.AcceptNotices, sb);
                    LLSDxmlEncode2.AddElem("GroupInsigniaID", m.GroupPicture, sb);
                    LLSDxmlEncode2.AddElem("Contribution", m.Contribution, sb);
                    LLSDxmlEncode2.AddElem("GroupName", m.GroupName, sb);
                    LLSDxmlEncode2.AddEndMap(sb);
                    lstInProfiles.Add(m.ListInProfile);
                }
                LLSDxmlEncode2.AddEndArray(sb);

                LLSDxmlEncode2.AddArray("NewGroupData", sb);
                foreach(bool b in lstInProfiles)
                {
                    LLSDxmlEncode2.AddMap(sb);
                    LLSDxmlEncode2.AddElem("ListInProfile", b, sb);
                    LLSDxmlEncode2.AddEndMap(sb);
                }
                LLSDxmlEncode2.AddEndArray(sb);
            }

            Enqueue(EndEventToBytes(sb), AgentID);
        }

        public void PlacesQueryReply(UUID avatarID, UUID queryID, UUID transactionID, PlacesReplyData[] replyDataArray)
        {
            osUTF8 sb = new osUTF8(256);
            LLSDxmlEncode2.AddMap(sb);
            LLSDxmlEncode2.AddElem("message", "PlacesReplyMessage", sb);
            LLSDxmlEncode2.AddMap("QueryData[]", sb); LLSDxmlEncode2.AddArray(sb);
                LLSDxmlEncode2.AddArray("AgentData", sb);
                    LLSDxmlEncode2.AddMap(sb);
                        LLSDxmlEncode2.AddElem("AgentID", avatarID, sb);
                        LLSDxmlEncode2.AddElem("QueryID", queryID, sb);
                        LLSDxmlEncode2.AddElem("TransactionID", transactionID, sb);
                    LLSDxmlEncode2.AddEndMap(sb);
                LLSDxmlEncode2.AddEndArray(sb);

                LLSDxmlEncode2.AddArray("QueryData", sb);

                for (int i = 0; i < replyDataArray.Length; ++i)
                {
                    PlacesReplyData data = replyDataArray[i];
                    LLSDxmlEncode2.AddMap(sb);
                        LLSDxmlEncode2.AddElem("ActualArea", data.ActualArea, sb);
                        LLSDxmlEncode2.AddElem("BillableArea", data.BillableArea, sb);
                        LLSDxmlEncode2.AddElem("Description", data.Desc, sb);
                        LLSDxmlEncode2.AddElem("Dwell", data.Dwell, sb);
                        LLSDxmlEncode2.AddElem("Flags", data.Flags, sb);
                        LLSDxmlEncode2.AddElem("GlobalX", data.GlobalX, sb);
                        LLSDxmlEncode2.AddElem("GlobalY", data.GlobalY, sb);
                        LLSDxmlEncode2.AddElem("GlobalZ", data.GlobalZ, sb);
                        LLSDxmlEncode2.AddElem("Name", data.Name, sb);
                        LLSDxmlEncode2.AddElem("OwnerID", data.OwnerID, sb);
                        LLSDxmlEncode2.AddElem("SimName", data.SimName, sb);
                        LLSDxmlEncode2.AddElem("SnapShotID", data.SnapshotID, sb);
                        LLSDxmlEncode2.AddElem("ProductSku", (int)0, sb);
                        LLSDxmlEncode2.AddElem("Price", data.Price, sb);
                    LLSDxmlEncode2.AddEndMap(sb);
                }
                LLSDxmlEncode2.AddEndArray(sb);

            Enqueue(EndEventToBytes(sb), avatarID);
        }

        public void ScriptRunningEvent(UUID objectID, UUID itemID, bool running, UUID avatarID)
        {
            osUTF8 sb = StartEvent("ScriptRunningReply");
            LLSDxmlEncode2.AddArrayAndMap("Script", sb);
                LLSDxmlEncode2.AddElem("ObjectID", objectID, sb);
                LLSDxmlEncode2.AddElem("ItemID", itemID, sb);
                LLSDxmlEncode2.AddElem("Running", running, sb);
                LLSDxmlEncode2.AddElem("Mono", true, sb);
            LLSDxmlEncode2.AddEndMapAndArray(sb);

            Enqueue(EndEventToBytes(sb), avatarID);
        }

        public void partPhysicsProperties(uint localID, byte physhapetype,
                        float density, float friction, float bounce, float gravmod, UUID avatarID)
        {
            osUTF8 sb = StartEvent("ObjectPhysicsProperties");
            LLSDxmlEncode2.AddArrayAndMap("ObjectData", sb);
                LLSDxmlEncode2.AddElem("LocalID", (int)localID, sb);
                LLSDxmlEncode2.AddElem("Density", density, sb);
                LLSDxmlEncode2.AddElem("Friction", friction, sb);
                LLSDxmlEncode2.AddElem("GravityMultiplier", gravmod, sb);
                LLSDxmlEncode2.AddElem("Restitution", bounce, sb);
                LLSDxmlEncode2.AddElem("PhysicsShapeType", (int)physhapetype, sb);
            LLSDxmlEncode2.AddEndMapAndArray(sb);

            Enqueue(EndEventToBytes(sb), avatarID);
        }

        public void WindlightRefreshEvent(int interpolate, UUID avatarID)
        {
            osUTF8 sb = StartEvent("WindLightRefresh");
            LLSDxmlEncode2.AddElem("Interpolate", interpolate > 0 ? 1 : 0, sb);
            Enqueue(EndEventToBytes(sb), avatarID);
        }

        public void SendBulkUpdateInventoryItem(InventoryItemBase item, UUID avatarID, UUID? transationID = null)
        {
            const uint FULL_MASK_PERMISSIONS = (uint)0x7ffffff;

            osUTF8 sb = StartEvent("BulkUpdateInventory");
            LLSDxmlEncode2.AddArray("AgentData", sb);
                LLSDxmlEncode2.AddMap(sb);
                    LLSDxmlEncode2.AddElem("AgentID", avatarID, sb);
                    LLSDxmlEncode2.AddElem("TransactionID", transationID ?? UUID.Random(), sb);
            LLSDxmlEncode2.AddEndMapAndArray(sb);

            LLSDxmlEncode2.AddRawElem("<key>FolderData</key><array><map><key>FolderID</key><uuid>00000000-0000-0000-0000-000000000000</uuid><key>Name</key><string></string><key>ParentID</key><uuid>00000000-0000-0000-0000-000000000000</uuid ><key>Type</key ><integer>-1</integer></map ></array>",sb);

            osUTF8 osName = new osUTF8(Utils.StringToBytesNoTerm(item.Name, 255));
            osUTF8 osDesc = new osUTF8(Utils.StringToBytesNoTerm(item.Description, 255));

            LLSDxmlEncode2.AddArray("ItemData", sb);
                LLSDxmlEncode2.AddMap(sb);
                    LLSDxmlEncode2.AddElem("ItemID", item.ID, sb);
                    LLSDxmlEncode2.AddElem("AssetID", item.AssetID, sb);
                    LLSDxmlEncode2.AddElem("CreatorID", item.CreatorIdAsUuid, sb);
                    LLSDxmlEncode2.AddElem("BaseMask", item.BasePermissions, sb);
                    LLSDxmlEncode2.AddElem("CreationDate", item.CreationDate, sb);
                    LLSDxmlEncode2.AddElem("Description", osDesc, sb);
                    LLSDxmlEncode2.AddElem("EveryoneMask", item.EveryOnePermissions, sb);
                    LLSDxmlEncode2.AddElem("FolderID", item.Folder, sb);
                    LLSDxmlEncode2.AddElem("InvType", (sbyte)item.InvType, sb);
                    LLSDxmlEncode2.AddElem("Name", osName, sb);
                    LLSDxmlEncode2.AddElem("NextOwnerMask", item.NextPermissions, sb);
                    LLSDxmlEncode2.AddElem("GroupID", item.GroupID, sb);
                    LLSDxmlEncode2.AddElem("GroupMask", item.GroupPermissions, sb);
                    LLSDxmlEncode2.AddElem("GroupOwned", item.GroupOwned , sb);
                    LLSDxmlEncode2.AddElem("OwnerID", item.Owner, sb);
                    LLSDxmlEncode2.AddElem("OwnerMask", item.CurrentPermissions, sb);
                    LLSDxmlEncode2.AddElem("SalePrice", item.SalePrice, sb);
                    LLSDxmlEncode2.AddElem("SaleType", item.SaleType, sb);
                    LLSDxmlEncode2.AddElem("Type", (sbyte)item.AssetType, sb);
                    LLSDxmlEncode2.AddElem("CallbackID", (uint)0, sb);
                    LLSDxmlEncode2.AddElem("Flags", item.Flags & 0x2000ff, sb);

                    uint iCRC =
                        Helpers.InventoryCRC(1000, 0, (sbyte)item.InvType,
                                     (sbyte)item.AssetType, item.AssetID,
                                     item.GroupID, 100,
                                     item.Owner, item.CreatorIdAsUuid,
                                     item.ID, item.Folder,
                                     FULL_MASK_PERMISSIONS, 1, FULL_MASK_PERMISSIONS, FULL_MASK_PERMISSIONS,
                                     FULL_MASK_PERMISSIONS);
                    LLSDxmlEncode2.AddElem("CRC", iCRC, sb);
            LLSDxmlEncode2.AddEndMapAndArray(sb);
            Enqueue(EndEventToBytes(sb), avatarID);
        }

        public static string KeepAliveEvent()
        {
            osUTF8 sb = new osUTF8(256);
            LLSDxmlEncode2.AddMap(sb);
            LLSDxmlEncode2.AddElem("message", "FAKEEVENT", sb);
            LLSDxmlEncode2.AddMap("body", sb);
            LLSDxmlEncode2.AddEndMap(sb); // close body
            LLSDxmlEncode2.AddEndMap(sb); // close event
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
