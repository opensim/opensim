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
using System.Text;
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

        public static StringBuilder StartEvent(string eventName)
        {
            StringBuilder sb = new StringBuilder(256);
            LLSDxmlEncode.AddMap(sb);
            LLSDxmlEncode.AddElem("message", eventName, sb);
            LLSDxmlEncode.AddMap("body", sb);

            return sb;
        }

        public static string EndEvent(StringBuilder sb)
        {
            LLSDxmlEncode.AddEndMap(sb); // close body
            LLSDxmlEncode.AddEndMap(sb); // close event
            return sb.ToString();
        }

        public static OSD BuildEvent(string eventName, OSD eventBody)
        {
            OSDMap llsdEvent = new OSDMap(2);
            llsdEvent.Add("message", new OSDString(eventName));
            llsdEvent.Add("body", eventBody);

            return llsdEvent;
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
/*
            StringBuilder sb = new StringBuilder(256);
            LLSDxmlEncode.AddMap(sb); //messageParams

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
            LLSDxmlEncode.AddElem("from_group",fromGroup, sb);

            LLSDxmlEncode.AddEndMap(sb); //messageParams
            string tt = sb.ToString();
*/
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
    }
}
