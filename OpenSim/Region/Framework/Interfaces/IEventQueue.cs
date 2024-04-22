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

using System.Collections.Generic;
using System.Text;
using System.Net;
using OpenMetaverse;
using OpenMetaverse.Packets;
using OpenMetaverse.Messages.Linden;
using OpenMetaverse.StructuredData;
using OpenSim.Framework;

namespace OpenSim.Region.Framework.Interfaces
{
    public struct GroupChatListAgentUpdateData
    {
        public UUID agentID;
        public bool enterOrLeave; // true means enter
        public bool isModerator;
        public bool mutedText;
        public bool canVoice;

        public GroupChatListAgentUpdateData(UUID ID)
        {
            agentID = ID;
            canVoice = false;
            isModerator = false;
            mutedText = false;
            enterOrLeave = true;
        }

        public GroupChatListAgentUpdateData(UUID ID, bool eOL)
        {
            agentID = ID;
            canVoice = false;
            isModerator = false;
            mutedText = false;
            enterOrLeave = eOL;
        }

        public GroupChatListAgentUpdateData(UUID ID, bool cv, bool isMod, bool mtd, bool eOrL)
        {
            agentID = ID;
            enterOrLeave = eOrL;
            isModerator = isMod;
            mutedText = mtd;
            canVoice = cv;
        }
    }

    public interface IEventQueue
    {
        bool Enqueue(OSD o, UUID avatarID);
        bool Enqueue(byte[] o, UUID avatarID);
        bool Enqueue(osUTF8 o, UUID avatarID);

        //        void DisableSimulator(ulong handle, UUID avatarID);
        void EnableSimulator(ulong handle, IPEndPoint endPoint, UUID avatarID, int regionSizeX, int regionSizeY);
        void EstablishAgentCommunication(UUID avatarID, IPEndPoint endPoint,
                                         string capsPath, ulong regionHandle, int regionSizeX, int regionSizeY);
        void TeleportFinishEvent(ulong regionHandle, byte simAccess,
                                 IPEndPoint regionExternalEndPoint,
                                 uint locationID, uint flags, string capsURL,
                                 UUID agentID, int regionSizeX, int regionSizeY);
        void CrossRegion(ulong handle, Vector3 pos, Vector3 lookAt,
                         IPEndPoint newRegionExternalEndPoint,
                         string capsURL, UUID avatarID, UUID sessionID,
                            int regionSizeX, int regionSizeY);
        void ChatterboxInvitation(UUID sessionID, string sessionName,
                                UUID fromAgent, string message, UUID toAgent, string fromName, byte dialog,
                                uint timeStamp, bool offline, int parentEstateID, Vector3 position,
                                uint ttl, UUID transactionID, bool fromGroup, byte[] binaryBucket);
        void ChatterBoxSessionStartReply(UUID sessionID, string sessionName, int type,
                                bool voiceEnabled, bool voiceModerated, UUID tmpSessionID,
                                bool sucess, string error,
                                UUID toAgent);
        void ChatterBoxSessionAgentListUpdates(UUID sessionID, UUID toAgent, List<GroupChatListAgentUpdateData> updates);
        void ChatterBoxForceClose(UUID toAgent, UUID sessionID, string reason);
        //void ParcelProperties(ParcelPropertiesMessage parcelPropertiesMessage, UUID avatarID);
        void GroupMembershipData(UUID receiverAgent, GroupMembershipData[] data);
        void ScriptRunningEvent(UUID objectID, UUID itemID, bool running, UUID avatarID);
        byte[] BuildEvent(string eventName, OSD eventBody);
        void partPhysicsProperties(uint localID, byte physhapetype, float density, float friction, float bounce, float gravmod, UUID avatarID);
        void WindlightRefreshEvent(int interpolate, UUID avatarID);
        void SendBulkUpdateInventoryItem(InventoryItemBase item, UUID avatarID, UUID? transationID = null);
        osUTF8 StartEvent(string eventName);
        osUTF8 StartEvent(string eventName, int cap);
        void SendLargeGenericMessage(UUID avatarID, UUID? transationID, UUID? sessionID,
                string method, UUID invoice, List<byte[]> message);
        void SendLargeGenericMessage(UUID avatarID, UUID? transationID, UUID? sessionID,
                string method, UUID invoice, List<string> message);
        byte[] EndEventToBytes(osUTF8 sb);
    }
}
