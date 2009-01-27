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
using System.Net;
using OpenMetaverse;
using OpenMetaverse.Packets;
using OpenMetaverse.StructuredData;

namespace OpenSim.Region.Environment
{
    public class EventQueueHelper
    {
        private EventQueueHelper() {} // no construction possible, it's an utility class

        private static byte[] regionHandleToByteArray(ulong regionHandle)
        {
            // Reverse endianness of RegionHandle
            return new byte[]
            {
                (byte)((regionHandle >> 56) % 256),
                (byte)((regionHandle >> 48) % 256),
                (byte)((regionHandle >> 40) % 256),
                (byte)((regionHandle >> 32) % 256),
                (byte)((regionHandle >> 24) % 256),
                (byte)((regionHandle >> 16) % 256),
                (byte)((regionHandle >> 8) % 256),
                (byte)(regionHandle % 256)
            };
        }

        private static byte[] uintToByteArray(uint uIntValue)
        {
            // Reverse endianness of a uint
            return new byte[]
            {
                (byte)((uIntValue >> 24) % 256),
                (byte)((uIntValue >> 16) % 256),
                (byte)((uIntValue >> 8) % 256),
                (byte)(uIntValue % 256)

            };
        }

        public static OSD buildEvent(string eventName, OSD eventBody)
        {
            OSDMap llsdEvent = new OSDMap(2);
            llsdEvent.Add("message", new OSDString(eventName));
            llsdEvent.Add("body", eventBody);

            return llsdEvent;
        }

        public static OSD EnableSimulator(ulong Handle, IPEndPoint endPoint)
        {
            OSDMap llsdSimInfo = new OSDMap(3);

            llsdSimInfo.Add("Handle", new OSDBinary(regionHandleToByteArray(Handle)));
            llsdSimInfo.Add("IP", new OSDBinary(endPoint.Address.GetAddressBytes()));
            llsdSimInfo.Add("Port", new OSDInteger(endPoint.Port));

            OSDArray arr = new OSDArray(1);
            arr.Add(llsdSimInfo);

            OSDMap llsdBody = new OSDMap(1);
            llsdBody.Add("SimulatorInfo", arr);

            return buildEvent("EnableSimulator", llsdBody);
        }

        public static OSD DisableSimulator(ulong Handle)
        {
            //OSDMap llsdSimInfo = new OSDMap(1);

            //llsdSimInfo.Add("Handle", new OSDBinary(regionHandleToByteArray(Handle)));

            //OSDArray arr = new OSDArray(1);
            //arr.Add(llsdSimInfo);

            OSDMap llsdBody = new OSDMap(0);
            //llsdBody.Add("SimulatorInfo", arr);

            return buildEvent("DisableSimulator", llsdBody);
        }
        
        public static OSD CrossRegion(ulong Handle, Vector3 pos, Vector3 lookAt,
                                       IPEndPoint newRegionExternalEndPoint,
                                       string capsURL, UUID AgentID, UUID SessionID)
        {
            OSDArray LookAtArr = new OSDArray(3);
            LookAtArr.Add(OSD.FromReal(lookAt.X));
            LookAtArr.Add(OSD.FromReal(lookAt.Y));
            LookAtArr.Add(OSD.FromReal(lookAt.Z));

            OSDArray PositionArr = new OSDArray(3);
            PositionArr.Add(OSD.FromReal(pos.X));
            PositionArr.Add(OSD.FromReal(pos.Y));
            PositionArr.Add(OSD.FromReal(pos.Z));

            OSDMap InfoMap = new OSDMap(2);
            InfoMap.Add("LookAt", LookAtArr);
            InfoMap.Add("Position", PositionArr);

            OSDArray InfoArr = new OSDArray(1);
            InfoArr.Add(InfoMap);

            OSDMap AgentDataMap = new OSDMap(2);
            AgentDataMap.Add("AgentID", OSD.FromUUID(AgentID));
            AgentDataMap.Add("SessionID",  OSD.FromUUID(SessionID));

            OSDArray AgentDataArr = new OSDArray(1);
            AgentDataArr.Add(AgentDataMap);

            OSDMap RegionDataMap = new OSDMap(4);
            RegionDataMap.Add("RegionHandle", OSD.FromBinary(regionHandleToByteArray(Handle)));
            RegionDataMap.Add("SeedCapability", OSD.FromString(capsURL));
            RegionDataMap.Add("SimIP", OSD.FromBinary(newRegionExternalEndPoint.Address.GetAddressBytes()));
            RegionDataMap.Add("SimPort", OSD.FromInteger(newRegionExternalEndPoint.Port));

            OSDArray RegionDataArr = new OSDArray(1);
            RegionDataArr.Add(RegionDataMap);

            OSDMap llsdBody = new OSDMap(3);
            llsdBody.Add("Info", InfoArr);
            llsdBody.Add("AgentData", AgentDataArr);
            llsdBody.Add("RegionData", RegionDataArr);

            return buildEvent("CrossedRegion", llsdBody);
        }

        public static OSD TeleportFinishEvent(
            ulong regionHandle, byte simAccess, IPEndPoint regionExternalEndPoint,
            uint locationID, uint flags, string capsURL, UUID AgentID)
        {
            OSDMap info = new OSDMap();
            info.Add("AgentID", OSD.FromUUID(AgentID));
            info.Add("LocationID", OSD.FromInteger(4)); // TODO what is this?
            info.Add("RegionHandle", OSD.FromBinary(regionHandleToByteArray(regionHandle)));
            info.Add("SeedCapability", OSD.FromString(capsURL));
            info.Add("SimAccess", OSD.FromInteger(simAccess));
            info.Add("SimIP", OSD.FromBinary(regionExternalEndPoint.Address.GetAddressBytes()));
            info.Add("SimPort", OSD.FromInteger(regionExternalEndPoint.Port));
            info.Add("TeleportFlags", OSD.FromBinary(1L << 4)); // AgentManager.TeleportFlags.ViaLocation

            OSDArray infoArr = new OSDArray();
            infoArr.Add(info);

            OSDMap body = new OSDMap();
            body.Add("Info", infoArr);

            return buildEvent("TeleportFinish", body);
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
            
            return buildEvent("ScriptRunningReply", body);
        }

        public static OSD EstablishAgentCommunication(UUID agentID, string simIpAndPort, string seedcap)
        {
            OSDMap body = new OSDMap(3);
            body.Add("agent-id", new OSDUUID(agentID));
            body.Add("sim-ip-and-port", new OSDString(simIpAndPort));
            body.Add("seed-capability", new OSDString(seedcap));

            return buildEvent("EstablishAgentCommunication", body);
        }

        public static OSD KeepAliveEvent()
        {
            return buildEvent("FAKEEVENT", new OSDMap());
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
            UUID agentID, bool canVoiceChat, bool isModerator, bool textMute)
        {
            OSDMap body = new OSDMap();
            OSDMap agentUpdates = new OSDMap();
            OSDMap infoDetail = new OSDMap();
            OSDMap mutes = new OSDMap();

            mutes.Add("text", OSD.FromBoolean(textMute));
            infoDetail.Add("can_voice_chat", OSD.FromBoolean(canVoiceChat));
            infoDetail.Add("is_moderator", OSD.FromBoolean(isModerator));
            infoDetail.Add("mutes", mutes);
            OSDMap info = new OSDMap();
            info.Add("info", infoDetail);
            agentUpdates.Add(agentID.ToString(), info);
            body.Add("agent_updates", agentUpdates);
            body.Add("session_id", OSD.FromUUID(sessionID));
            body.Add("updates", new OSD());

            OSDMap chatterBoxSessionAgentListUpdates = new OSDMap();
            chatterBoxSessionAgentListUpdates.Add("message", OSD.FromString("ChatterBoxSessionAgentListUpdates"));
            chatterBoxSessionAgentListUpdates.Add("body", body);

            return chatterBoxSessionAgentListUpdates;
        }

        public static OSD ParcelProperties(ParcelPropertiesPacket parcelPropertiesPacket)
        {
            OSDMap parcelProperties = new OSDMap();
            OSDMap body = new OSDMap();

            OSDArray ageVerificationBlock = new OSDArray();
            OSDMap ageVerificationMap = new OSDMap();
            ageVerificationMap.Add("RegionDenyAgeVerified",
                OSD.FromBoolean(parcelPropertiesPacket.AgeVerificationBlock.RegionDenyAgeUnverified));
            ageVerificationBlock.Add(ageVerificationMap);
            body.Add("AgeVerificationBlock", ageVerificationBlock);

            OSDArray mediaData = new OSDArray();
            OSDMap mediaDataMap = new OSDMap();
            mediaDataMap.Add("MediaDesc", OSD.FromString(""));
            mediaDataMap.Add("MediaHeight", OSD.FromInteger(0));
            mediaDataMap.Add("MediaLoop", OSD.FromInteger(0));
            mediaDataMap.Add("MediaType", OSD.FromString("type/type"));
            mediaDataMap.Add("MediaWidth", OSD.FromInteger(0));
            mediaDataMap.Add("ObscureMedia", OSD.FromInteger(0));
            mediaDataMap.Add("ObscureMusic", OSD.FromInteger(0));
            mediaData.Add(mediaDataMap);
            body.Add("MediaData", mediaData);

            OSDArray parcelData = new OSDArray();
            OSDMap parcelDataMap = new OSDMap();
            OSDArray AABBMax = new OSDArray(3);
            AABBMax.Add(OSD.FromReal(parcelPropertiesPacket.ParcelData.AABBMax.X));
            AABBMax.Add(OSD.FromReal(parcelPropertiesPacket.ParcelData.AABBMax.Y));
            AABBMax.Add(OSD.FromReal(parcelPropertiesPacket.ParcelData.AABBMax.Z));
            parcelDataMap.Add("AABBMax", AABBMax);

            OSDArray AABBMin = new OSDArray(3);
            AABBMin.Add(OSD.FromReal(parcelPropertiesPacket.ParcelData.AABBMin.X));
            AABBMin.Add(OSD.FromReal(parcelPropertiesPacket.ParcelData.AABBMin.Y));
            AABBMin.Add(OSD.FromReal(parcelPropertiesPacket.ParcelData.AABBMin.Z));
            parcelDataMap.Add("AABBMin", AABBMin);

            parcelDataMap.Add("Area", OSD.FromInteger(parcelPropertiesPacket.ParcelData.Area));
            parcelDataMap.Add("AuctionID", OSD.FromBinary(parcelPropertiesPacket.ParcelData.AuctionID));
            parcelDataMap.Add("AuthBuyerID", OSD.FromUUID(parcelPropertiesPacket.ParcelData.AuthBuyerID));
            parcelDataMap.Add("Bitmap", OSD.FromBinary(parcelPropertiesPacket.ParcelData.Bitmap));
            parcelDataMap.Add("Category", OSD.FromInteger((int)parcelPropertiesPacket.ParcelData.Category));
            parcelDataMap.Add("ClaimDate", OSD.FromInteger(parcelPropertiesPacket.ParcelData.ClaimDate));
            parcelDataMap.Add("Desc", OSD.FromString(Utils.BytesToString(parcelPropertiesPacket.ParcelData.Desc)));
            parcelDataMap.Add("GroupID", OSD.FromUUID(parcelPropertiesPacket.ParcelData.GroupID));
            parcelDataMap.Add("GroupPrims", OSD.FromInteger(parcelPropertiesPacket.ParcelData.GroupPrims));
            parcelDataMap.Add("IsGroupOwned", OSD.FromBoolean(parcelPropertiesPacket.ParcelData.IsGroupOwned));
            parcelDataMap.Add("LandingType", OSD.FromInteger(parcelPropertiesPacket.ParcelData.LandingType));
            parcelDataMap.Add("LocalID", OSD.FromInteger(parcelPropertiesPacket.ParcelData.LocalID));
            parcelDataMap.Add("MaxPrims", OSD.FromInteger(parcelPropertiesPacket.ParcelData.MaxPrims));
            parcelDataMap.Add("MediaAutoScale", OSD.FromInteger((int)parcelPropertiesPacket.ParcelData.MediaAutoScale));
            parcelDataMap.Add("MediaID", OSD.FromUUID(parcelPropertiesPacket.ParcelData.MediaID));
            parcelDataMap.Add("MediaURL", OSD.FromString(Utils.BytesToString(parcelPropertiesPacket.ParcelData.MediaURL)));
            parcelDataMap.Add("MusicURL", OSD.FromString(Utils.BytesToString(parcelPropertiesPacket.ParcelData.MusicURL)));
            parcelDataMap.Add("Name", OSD.FromString(Utils.BytesToString(parcelPropertiesPacket.ParcelData.Name)));
            parcelDataMap.Add("OtherCleanTime", OSD.FromInteger(parcelPropertiesPacket.ParcelData.OtherCleanTime));
            parcelDataMap.Add("OtherCount", OSD.FromInteger(parcelPropertiesPacket.ParcelData.OtherCount));
            parcelDataMap.Add("OtherPrims", OSD.FromInteger(parcelPropertiesPacket.ParcelData.OtherPrims));
            parcelDataMap.Add("OwnerID", OSD.FromUUID(parcelPropertiesPacket.ParcelData.OwnerID));
            parcelDataMap.Add("OwnerPrims", OSD.FromInteger(parcelPropertiesPacket.ParcelData.OwnerPrims));
            parcelDataMap.Add("ParcelFlags", OSD.FromBinary(uintToByteArray(parcelPropertiesPacket.ParcelData.ParcelFlags)));
            parcelDataMap.Add("ParcelPrimBonus", OSD.FromReal(parcelPropertiesPacket.ParcelData.ParcelPrimBonus));
            parcelDataMap.Add("PassHours", OSD.FromReal(parcelPropertiesPacket.ParcelData.PassHours));
            parcelDataMap.Add("PassPrice", OSD.FromInteger(parcelPropertiesPacket.ParcelData.PassPrice));
            parcelDataMap.Add("PublicCount", OSD.FromInteger(parcelPropertiesPacket.ParcelData.PublicCount));
            parcelDataMap.Add("RegionDenyAnonymous", OSD.FromBoolean(parcelPropertiesPacket.ParcelData.RegionDenyAnonymous));
            parcelDataMap.Add("RegionDenyIdentified", OSD.FromBoolean(parcelPropertiesPacket.ParcelData.RegionDenyIdentified));
            parcelDataMap.Add("RegionDenyTransacted", OSD.FromBoolean(parcelPropertiesPacket.ParcelData.RegionDenyTransacted));
            parcelDataMap.Add("RegionPushOverride", OSD.FromBoolean(parcelPropertiesPacket.ParcelData.RegionPushOverride));
            parcelDataMap.Add("RentPrice", OSD.FromInteger(parcelPropertiesPacket.ParcelData.RentPrice));
            parcelDataMap.Add("RequestResult", OSD.FromInteger(parcelPropertiesPacket.ParcelData.RequestResult));
            parcelDataMap.Add("SalePrice", OSD.FromInteger(parcelPropertiesPacket.ParcelData.SalePrice));
            parcelDataMap.Add("SelectedPrims", OSD.FromInteger(parcelPropertiesPacket.ParcelData.SelectedPrims));
            parcelDataMap.Add("SelfCount", OSD.FromInteger(parcelPropertiesPacket.ParcelData.SelfCount));
            parcelDataMap.Add("SequenceID", OSD.FromInteger(parcelPropertiesPacket.ParcelData.SequenceID));
            parcelDataMap.Add("SimWideMaxPrims", OSD.FromInteger(parcelPropertiesPacket.ParcelData.SimWideMaxPrims));
            parcelDataMap.Add("SimWideTotalPrims", OSD.FromInteger(parcelPropertiesPacket.ParcelData.SimWideTotalPrims));
            parcelDataMap.Add("SnapSelection", OSD.FromBoolean(parcelPropertiesPacket.ParcelData.SnapSelection));
            parcelDataMap.Add("SnapshotID", OSD.FromUUID(parcelPropertiesPacket.ParcelData.SnapshotID));
            parcelDataMap.Add("Status", OSD.FromInteger((int)parcelPropertiesPacket.ParcelData.Status));
            parcelDataMap.Add("TotalPrims", OSD.FromInteger(parcelPropertiesPacket.ParcelData.TotalPrims));

            OSDArray UserLocation = new OSDArray(3);
            UserLocation.Add(OSD.FromReal(parcelPropertiesPacket.ParcelData.UserLocation.X));
            UserLocation.Add(OSD.FromReal(parcelPropertiesPacket.ParcelData.UserLocation.Y));
            UserLocation.Add(OSD.FromReal(parcelPropertiesPacket.ParcelData.UserLocation.Z));
            parcelDataMap.Add("UserLocation", UserLocation);

            OSDArray UserLookAt = new OSDArray(3);
            UserLookAt.Add(OSD.FromReal(parcelPropertiesPacket.ParcelData.UserLookAt.X));
            UserLookAt.Add(OSD.FromReal(parcelPropertiesPacket.ParcelData.UserLookAt.Y));
            UserLookAt.Add(OSD.FromReal(parcelPropertiesPacket.ParcelData.UserLookAt.Z));
            parcelDataMap.Add("UserLookAt", UserLookAt);

            parcelData.Add(parcelDataMap);
            body.Add("ParcelData", parcelData);
            parcelProperties.Add("body", body);
            parcelProperties.Add("message", OSD.FromString("ParcelProperties"));

            return parcelProperties;
        }

    }
}
