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
using System.Net;
using OpenMetaverse;
using OpenMetaverse.Packets;
using OpenMetaverse.StructuredData;
using OpenMetaverse.Messages.Linden;

using OpenSim.Framework;

namespace OpenSim.Region.ClientStack.Linden
{
    public class EventQueueHelper
    {
        private EventQueueHelper() {} // no construction possible, it's an utility class

        private static byte[] ulongToByteArray(ulong uLongValue)
        {
            // Reverse endianness of RegionHandle
            return new byte[]
            {
                (byte)((uLongValue >> 56) % 256),
                (byte)((uLongValue >> 48) % 256),
                (byte)((uLongValue >> 40) % 256),
                (byte)((uLongValue >> 32) % 256),
                (byte)((uLongValue >> 24) % 256),
                (byte)((uLongValue >> 16) % 256),
                (byte)((uLongValue >> 8) % 256),
                (byte)(uLongValue % 256)
            };
        }

//        private static byte[] uintToByteArray(uint uIntValue)
//        {
//            byte[] result = new byte[4];
//            Utils.UIntToBytesBig(uIntValue, result, 0);
//            return result;
//        }

        public static OSD BuildEvent(string eventName, OSD eventBody)
        {
            OSDMap llsdEvent = new OSDMap(2);
            llsdEvent.Add("message", new OSDString(eventName));
            llsdEvent.Add("body", eventBody);

            return llsdEvent;
        }

        public static OSD EnableSimulator(ulong handle, IPEndPoint endPoint, int regionSizeX, int regionSizeY)
        {
            OSDMap llsdSimInfo = new OSDMap(5);

            llsdSimInfo.Add("Handle", new OSDBinary(ulongToByteArray(handle)));
            llsdSimInfo.Add("IP", new OSDBinary(endPoint.Address.GetAddressBytes()));
            llsdSimInfo.Add("Port", OSD.FromInteger(endPoint.Port));
            llsdSimInfo.Add("RegionSizeX", OSD.FromUInteger((uint)regionSizeX));
            llsdSimInfo.Add("RegionSizeY", OSD.FromUInteger((uint)regionSizeY));

            OSDArray arr = new OSDArray(1);
            arr.Add(llsdSimInfo);

            OSDMap llsdBody = new OSDMap(1);
            llsdBody.Add("SimulatorInfo", arr);

            return BuildEvent("EnableSimulator", llsdBody);
        }

        public static OSD DisableSimulator(ulong handle)
        {
            //OSDMap llsdSimInfo = new OSDMap(1);

            //llsdSimInfo.Add("Handle", new OSDBinary(regionHandleToByteArray(handle)));

            //OSDArray arr = new OSDArray(1);
            //arr.Add(llsdSimInfo);

            OSDMap llsdBody = new OSDMap(0);
            //llsdBody.Add("SimulatorInfo", arr);

            return BuildEvent("DisableSimulator", llsdBody);
        }

        public static OSD CrossRegion(ulong handle, Vector3 pos, Vector3 lookAt,
                                      IPEndPoint newRegionExternalEndPoint,
                                      string capsURL, UUID agentID, UUID sessionID,
                                      int regionSizeX, int regionSizeY)
        {
            OSDArray lookAtArr = new OSDArray(3);
            lookAtArr.Add(OSD.FromReal(lookAt.X));
            lookAtArr.Add(OSD.FromReal(lookAt.Y));
            lookAtArr.Add(OSD.FromReal(lookAt.Z));

            OSDArray positionArr = new OSDArray(3);
            positionArr.Add(OSD.FromReal(pos.X));
            positionArr.Add(OSD.FromReal(pos.Y));
            positionArr.Add(OSD.FromReal(pos.Z));

            OSDMap infoMap = new OSDMap(2);
            infoMap.Add("LookAt", lookAtArr);
            infoMap.Add("Position", positionArr);

            OSDArray infoArr = new OSDArray(1);
            infoArr.Add(infoMap);

            OSDMap agentDataMap = new OSDMap(2);
            agentDataMap.Add("AgentID", OSD.FromUUID(agentID));
            agentDataMap.Add("SessionID",  OSD.FromUUID(sessionID));

            OSDArray agentDataArr = new OSDArray(1);
            agentDataArr.Add(agentDataMap);

            OSDMap regionDataMap = new OSDMap(6);
            regionDataMap.Add("RegionHandle", OSD.FromBinary(ulongToByteArray(handle)));
            regionDataMap.Add("SeedCapability", OSD.FromString(capsURL));
            regionDataMap.Add("SimIP", OSD.FromBinary(newRegionExternalEndPoint.Address.GetAddressBytes()));
            regionDataMap.Add("SimPort", OSD.FromInteger(newRegionExternalEndPoint.Port));
            regionDataMap.Add("RegionSizeX", OSD.FromUInteger((uint)regionSizeX));
            regionDataMap.Add("RegionSizeY", OSD.FromUInteger((uint)regionSizeY));

            OSDArray regionDataArr = new OSDArray(1);
            regionDataArr.Add(regionDataMap);

            OSDMap llsdBody = new OSDMap(3);
            llsdBody.Add("Info", infoArr);
            llsdBody.Add("AgentData", agentDataArr);
            llsdBody.Add("RegionData", regionDataArr);

            return BuildEvent("CrossedRegion", llsdBody);
        }

        public static OSD TeleportFinishEvent(
                        ulong regionHandle, byte simAccess, IPEndPoint regionExternalEndPoint,
                        uint locationID, uint flags, string capsURL, UUID agentID,
                        int regionSizeX, int regionSizeY)
        {
            // not sure why flags get overwritten here
            if ((flags & (uint)TeleportFlags.IsFlying) != 0)
                flags = (uint)TeleportFlags.ViaLocation | (uint)TeleportFlags.IsFlying;
            else
                flags = (uint)TeleportFlags.ViaLocation;

            OSDMap info = new OSDMap();
            info.Add("AgentID", OSD.FromUUID(agentID));
            info.Add("LocationID", OSD.FromInteger(4)); // TODO what is this?
            info.Add("RegionHandle", OSD.FromBinary(ulongToByteArray(regionHandle)));
            info.Add("SeedCapability", OSD.FromString(capsURL));
            info.Add("SimAccess", OSD.FromInteger(simAccess));
            info.Add("SimIP", OSD.FromBinary(regionExternalEndPoint.Address.GetAddressBytes()));
            info.Add("SimPort", OSD.FromInteger(regionExternalEndPoint.Port));
//            info.Add("TeleportFlags", OSD.FromULong(1L << 4)); // AgentManager.TeleportFlags.ViaLocation
            info.Add("TeleportFlags", OSD.FromUInteger(flags));
            info.Add("RegionSizeX", OSD.FromUInteger((uint)regionSizeX));
            info.Add("RegionSizeY", OSD.FromUInteger((uint)regionSizeY));

            OSDArray infoArr = new OSDArray();
            infoArr.Add(info);

            OSDMap body = new OSDMap();
            body.Add("Info", infoArr);

            return BuildEvent("TeleportFinish", body);
        }

        public static OSD ScriptRunningReplyEvent(UUID objectID, UUID itemID, bool running, bool mono)
        {
            OSDMap script = new OSDMap();
            script.Add("ObjectID", OSD.FromUUID(objectID));
            script.Add("ItemID", OSD.FromUUID(itemID));
            script.Add("Running", OSD.FromBoolean(running));
            script.Add("Mono", OSD.FromBoolean(mono));

            OSDArray scriptArr = new OSDArray();
            scriptArr.Add(script);

            OSDMap body = new OSDMap();
            body.Add("Script", scriptArr);

            return BuildEvent("ScriptRunningReply", body);
        }

        public static OSD EstablishAgentCommunication(UUID agentID, string simIpAndPort, string seedcap,
                                    ulong regionHandle, int regionSizeX, int regionSizeY)
        {
            OSDMap body = new OSDMap(6)
                              {
                                  {"agent-id", new OSDUUID(agentID)},
                                  {"sim-ip-and-port", new OSDString(simIpAndPort)},
                                  {"seed-capability", new OSDString(seedcap)},
                                  {"region-handle", OSD.FromULong(regionHandle)},
                                  {"region-size-x", OSD.FromUInteger((uint)regionSizeX)},
                                  {"region-size-y", OSD.FromUInteger((uint)regionSizeY)}
                              };

            return BuildEvent("EstablishAgentCommunication", body);
        }

        public static OSD KeepAliveEvent()
        {
            return BuildEvent("FAKEEVENT", new OSDMap());
        }

        public static OSD AgentParams(UUID agentID, bool checkEstate, int godLevel, bool limitedToEstate)
        {
            OSDMap body = new OSDMap(4);

            body.Add("agent_id", new OSDUUID(agentID));
            body.Add("check_estate", new OSDInteger(checkEstate ? 1 : 0));
            body.Add("god_level", new OSDInteger(godLevel));
            body.Add("limited_to_estate", new OSDInteger(limitedToEstate ? 1 : 0));

            return body;
        }

        public static OSD InstantMessageParams(UUID fromAgent, string message, UUID toAgent,
            string fromName, byte dialog, uint timeStamp, bool offline, int parentEstateID,
            Vector3 position, uint ttl, UUID transactionID, bool fromGroup, byte[] binaryBucket)
        {
            OSDMap messageParams = new OSDMap(15);
            messageParams.Add("type", new OSDInteger((int)dialog));

            OSDArray positionArray = new OSDArray(3);
            positionArray.Add(OSD.FromReal(position.X));
            positionArray.Add(OSD.FromReal(position.Y));
            positionArray.Add(OSD.FromReal(position.Z));
            messageParams.Add("position", positionArray);

            messageParams.Add("region_id", new OSDUUID(UUID.Zero));
            messageParams.Add("to_id", new OSDUUID(toAgent));
            messageParams.Add("source", new OSDInteger(0));

            OSDMap data = new OSDMap(1);
            data.Add("binary_bucket", OSD.FromBinary(binaryBucket));
            messageParams.Add("data", data);
            messageParams.Add("message", new OSDString(message));
            messageParams.Add("id", new OSDUUID(transactionID));
            messageParams.Add("from_name", new OSDString(fromName));
            messageParams.Add("timestamp", new OSDInteger((int)timeStamp));
            messageParams.Add("offline", new OSDInteger(offline ? 1 : 0));
            messageParams.Add("parent_estate_id", new OSDInteger(parentEstateID));
            messageParams.Add("ttl", new OSDInteger((int)ttl));
            messageParams.Add("from_id", new OSDUUID(fromAgent));
            messageParams.Add("from_group", new OSDInteger(fromGroup ? 1 : 0));

            return messageParams;
        }

        public static OSD InstantMessage(UUID fromAgent, string message, UUID toAgent,
            string fromName, byte dialog, uint timeStamp, bool offline, int parentEstateID,
            Vector3 position, uint ttl, UUID transactionID, bool fromGroup, byte[] binaryBucket,
            bool checkEstate, int godLevel, bool limitedToEstate)
        {
            OSDMap im = new OSDMap(2);
            im.Add("message_params", InstantMessageParams(fromAgent, message, toAgent,
            fromName, dialog, timeStamp, offline, parentEstateID,
            position, ttl, transactionID, fromGroup, binaryBucket));

            im.Add("agent_params", AgentParams(fromAgent, checkEstate, godLevel, limitedToEstate));

            return im;
        }


        public static OSD ChatterboxInvitation(UUID sessionID, string sessionName,
            UUID fromAgent, string message, UUID toAgent, string fromName, byte dialog,
            uint timeStamp, bool offline, int parentEstateID, Vector3 position,
            uint ttl, UUID transactionID, bool fromGroup, byte[] binaryBucket)
        {
            OSDMap body = new OSDMap(5);
            body.Add("session_id", new OSDUUID(sessionID));
            body.Add("from_name", new OSDString(fromName));
            body.Add("session_name", new OSDString(sessionName));
            body.Add("from_id", new OSDUUID(fromAgent));

            body.Add("instantmessage", InstantMessage(fromAgent, message, toAgent,
            fromName, dialog, timeStamp, offline, parentEstateID, position,
            ttl, transactionID, fromGroup, binaryBucket, true, 0, true));

            OSDMap chatterboxInvitation = new OSDMap(2);
            chatterboxInvitation.Add("message", new OSDString("ChatterBoxInvitation"));
            chatterboxInvitation.Add("body", body);
            return chatterboxInvitation;
        }

        public static OSD ChatterBoxSessionAgentListUpdates(UUID sessionID,
            UUID agentID, bool canVoiceChat, bool isModerator, bool textMute, bool isEnterorLeave)
        {
            OSDMap body = new OSDMap();
            OSDMap agentUpdates = new OSDMap();
            OSDMap infoDetail = new OSDMap();
            OSDMap mutes = new OSDMap();

            // this should be a list of agents and parameters
            // foreach agent
            mutes.Add("text", OSD.FromBoolean(textMute));
            infoDetail.Add("can_voice_chat", OSD.FromBoolean(canVoiceChat));
            infoDetail.Add("is_moderator", OSD.FromBoolean(isModerator));
            infoDetail.Add("mutes", mutes);
            OSDMap info = new OSDMap();
            info.Add("info", infoDetail);
            if(isEnterorLeave)
                info.Add("transition",OSD.FromString("ENTER"));
            else
                info.Add("transition",OSD.FromString("LEAVE"));
            agentUpdates.Add(agentID.ToString(), info);

            // foreach end

            body.Add("agent_updates", agentUpdates);
            body.Add("session_id", OSD.FromUUID(sessionID));
            body.Add("updates", new OSD());

            OSDMap chatterBoxSessionAgentListUpdates = new OSDMap();
            chatterBoxSessionAgentListUpdates.Add("message", OSD.FromString("ChatterBoxSessionAgentListUpdates"));
            chatterBoxSessionAgentListUpdates.Add("body", body);

            return chatterBoxSessionAgentListUpdates;
        }

        public static OSD ChatterBoxForceClose(UUID sessionID, string reason)
        {
            OSDMap body = new OSDMap(2);
            body.Add("session_id", new OSDUUID(sessionID));
            body.Add("reason", new OSDString(reason));

            OSDMap chatterBoxForceClose = new OSDMap(2);
            chatterBoxForceClose.Add("message", new OSDString("ForceCloseChatterBoxSession"));
            chatterBoxForceClose.Add("body", body);
            return chatterBoxForceClose;
        }

        public static OSD GroupMembershipData(UUID receiverAgent, GroupMembershipData[] data)
        {
            OSDArray AgentData = new OSDArray(1);
            OSDMap AgentDataMap = new OSDMap(1);
            AgentDataMap.Add("AgentID", OSD.FromUUID(receiverAgent));
            AgentData.Add(AgentDataMap);

            OSDArray GroupData = new OSDArray(data.Length);
            OSDArray NewGroupData = new OSDArray(data.Length);

            foreach (GroupMembershipData membership in data)
            {
                OSDMap GroupDataMap = new OSDMap(6);
                OSDMap NewGroupDataMap = new OSDMap(1);

                GroupDataMap.Add("GroupID", OSD.FromUUID(membership.GroupID));
                GroupDataMap.Add("GroupPowers", OSD.FromULong(membership.GroupPowers));
                GroupDataMap.Add("AcceptNotices", OSD.FromBoolean(membership.AcceptNotices));
                GroupDataMap.Add("GroupInsigniaID", OSD.FromUUID(membership.GroupPicture));
                GroupDataMap.Add("Contribution", OSD.FromInteger(membership.Contribution));
                GroupDataMap.Add("GroupName", OSD.FromString(membership.GroupName));
                NewGroupDataMap.Add("ListInProfile", OSD.FromBoolean(membership.ListInProfile));

                GroupData.Add(GroupDataMap);
                NewGroupData.Add(NewGroupDataMap);
            }

            OSDMap llDataStruct = new OSDMap(3);
            llDataStruct.Add("AgentData", AgentData);
            llDataStruct.Add("GroupData", GroupData);
            llDataStruct.Add("NewGroupData", NewGroupData);

            return BuildEvent("AgentGroupDataUpdate", llDataStruct);

        }

        public static OSD PlacesQuery(PlacesReplyPacket PlacesReply)
        {
            OSDMap placesReply = new OSDMap();
            placesReply.Add("message", OSD.FromString("PlacesReplyMessage"));

            OSDMap body = new OSDMap();
            OSDArray agentData = new OSDArray();
            OSDMap agentDataMap = new OSDMap();
            agentDataMap.Add("AgentID", OSD.FromUUID(PlacesReply.AgentData.AgentID));
            agentDataMap.Add("QueryID", OSD.FromUUID(PlacesReply.AgentData.QueryID));
            agentDataMap.Add("TransactionID", OSD.FromUUID(PlacesReply.TransactionData.TransactionID));
            agentData.Add(agentDataMap);
            body.Add("AgentData", agentData);

            OSDArray QueryData = new OSDArray();

            foreach (PlacesReplyPacket.QueryDataBlock groupDataBlock in PlacesReply.QueryData)
            {
                OSDMap QueryDataMap = new OSDMap();
                QueryDataMap.Add("ActualArea", OSD.FromInteger(groupDataBlock.ActualArea));
                QueryDataMap.Add("BillableArea", OSD.FromInteger(groupDataBlock.BillableArea));
                QueryDataMap.Add("Description", OSD.FromBinary(groupDataBlock.Desc));
                QueryDataMap.Add("Dwell", OSD.FromInteger((int)groupDataBlock.Dwell));
                QueryDataMap.Add("Flags", OSD.FromString(Convert.ToString(groupDataBlock.Flags)));
                QueryDataMap.Add("GlobalX", OSD.FromInteger((int)groupDataBlock.GlobalX));
                QueryDataMap.Add("GlobalY", OSD.FromInteger((int)groupDataBlock.GlobalY));
                QueryDataMap.Add("GlobalZ", OSD.FromInteger((int)groupDataBlock.GlobalZ));
                QueryDataMap.Add("Name", OSD.FromBinary(groupDataBlock.Name));
                QueryDataMap.Add("OwnerID", OSD.FromUUID(groupDataBlock.OwnerID));
                QueryDataMap.Add("SimName", OSD.FromBinary(groupDataBlock.SimName));
                QueryDataMap.Add("SnapShotID", OSD.FromUUID(groupDataBlock.SnapshotID));
                QueryDataMap.Add("ProductSku", OSD.FromInteger(0));
                QueryDataMap.Add("Price", OSD.FromInteger(groupDataBlock.Price));

                QueryData.Add(QueryDataMap);
            }
            body.Add("QueryData", QueryData);
            placesReply.Add("QueryData[]", body);

            return placesReply;
        }

        public static OSD ParcelProperties(ParcelPropertiesMessage parcelPropertiesMessage)
        {
            OSDMap message = new OSDMap();
            message.Add("message", OSD.FromString("ParcelProperties"));
            OSD message_body = parcelPropertiesMessage.Serialize();
            message.Add("body", message_body);
            return message;
        }

        public static OSD partPhysicsProperties(uint localID, byte physhapetype,
                        float density, float friction, float bounce, float gravmod)
        {

            OSDMap physinfo = new OSDMap(6);
            physinfo["LocalID"] = localID;
            physinfo["Density"] = density;
            physinfo["Friction"] = friction;
            physinfo["GravityMultiplier"] = gravmod;
            physinfo["Restitution"] = bounce;
            physinfo["PhysicsShapeType"] = (int)physhapetype;

            OSDArray array = new OSDArray(1);
            array.Add(physinfo);

            OSDMap llsdBody = new OSDMap(1);
            llsdBody.Add("ObjectData", array);

            return BuildEvent("ObjectPhysicsProperties", llsdBody);
        }
    }
}
