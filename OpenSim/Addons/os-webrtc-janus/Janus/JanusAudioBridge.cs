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
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;

using log4net;

namespace osWebRtcVoice
{
    // Encapsulization of a Session to the Janus server
    public class JanusAudioBridge : JanusPlugin
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private static readonly string LogHeader = "[JANUS AUDIO BRIDGE]";

        // Wrapper around the session connection to Janus-gateway
        public JanusAudioBridge(JanusSession pSession) : base(pSession, "janus.plugin.audiobridge")
        {
            // m_log.DebugFormat("{0} JanusAudioBridge constructor", LogHeader);
        }

        public override void Dispose()
        {
            if (IsConnected)
            {
                // Close the handle

            }
            base.Dispose();
        }

        public async Task<AudioBridgeResp> SendAudioBridgeMsg(PluginMsgReq pMsg)
        {
            try
            {
                JanusMessageResp ret = await SendPluginMsg(pMsg).ConfigureAwait(false);
                if(ret is not null)
                    return new AudioBridgeResp(ret);
                else
                    m_log.Error($"{LogHeader} AudioBridge SendPluginMsg returned null");

            }
            catch (Exception e)
            {
                m_log.Error($"{LogHeader} SendPluginMsg. Exception", e);
            }
            return null;
        }

        /// <summary>
        /// Create a room with the given criteria. This talks to Janus to create the room.
        /// If the room with this RoomId already exists, just return it.
        /// Janus could create and return the RoomId but this presumes that the Janus server
        /// is only being used for our voice service.
        /// </summary>
        /// <param name="pRoomId">integer room ID to create</param>
        /// <param name="pSpatial">boolean on whether room will be spatial or non-spatial</param>
        /// <param name="pRoomDesc">added as "description" to the created room</param>
        /// <returns></returns>
        public async Task<JanusRoom> CreateRoom(int pRoomId, bool pSpatial, string pRoomDesc, string credentials)
        {
            JanusRoom ret = null;
            try
            {
                JanusMessageResp resp = await SendPluginMsg(new AudioBridgeCreateRoomReq(pRoomId, pSpatial, pRoomDesc, credentials)).ConfigureAwait(false);
                AudioBridgeResp abResp = new(resp);

                m_log.Debug($"{LogHeader} CreateRoom. ReturnCode: '{abResp.AudioBridgeReturnCode}'");
                switch (abResp.AudioBridgeReturnCode)
                {
                    case "created":
                        ret = new JanusRoom(this, pRoomId);
                        break;
                    case "event":
                        if (abResp.AudioBridgeErrorCode == 486)
                        {
                            m_log.Info($"{LogHeader} CreateRoom. Room {pRoomId} already exists (reconnect).");
                            if (m_log.IsDebugEnabled)
                            {
                                m_log.Debug($"{LogHeader} CreateRoom. Reconnect details: {abResp}");
                            }
                            // if room already exists, just use it
                            ret = new JanusRoom(this, pRoomId);
                        }
                        else
                        {
                            m_log.Error($"{LogHeader} CreateRoom. XX Room creation failed.");
                            if (m_log.IsDebugEnabled)
                            {
                                m_log.Debug($"{LogHeader} CreateRoom. XX failure detail: {abResp}");
                            }
                        }
                        break;
                    default:
                        m_log.Error($"{LogHeader} CreateRoom. YY Room creation failed.");
                        if (m_log.IsDebugEnabled)
                        {
                            m_log.Debug($"{LogHeader} CreateRoom. YY failure detail: {abResp}");
                        }
                        break;
                }   
            }
            catch (Exception e)
            {
                m_log.Error($"{LogHeader} CreateRoom. Exception '{e.Message}'");
            }
            return ret;
        }

        public async Task<bool> DestroyRoom(JanusRoom janusRoom)
        {
            try
            {
                JanusMessageResp resp = await SendPluginMsg(new AudioBridgeDestroyRoomReq(janusRoom.RoomId)).ConfigureAwait(false);
                return true;
            }
            catch (Exception e)
            {
                m_log.Error($"{LogHeader} DestroyRoom. Exception ", e);
            }
            return false;
        }

        // Constant used to denote that this is a spatial audio room for the region (as opposed to parcels)
        public const int REGION_ROOM_ID = -999;
        private Dictionary<int, JanusRoom> _rooms = [];

        // Calculate a room number for the given parameters. The room number is a hash of the parameters.
        // The attempt is to deterministicly create a room number so all regions will generate the
        //     same room number across sessions and across the grid.
        // getHashCode() is not deterministic across sessions.
        public static int CalcRoomNumber(string regionID, string gridhash, string pChannelType, int pParcelLocalID, string pChannelID)
        {
            BHasherMdjb2 hasher = new();
            // If there is a channel specified it must be group 
            switch (pChannelType)
            {
                case "local":
                    // A "local" channel is unique to the region and parcel
                    hasher.Add(regionID);
                    hasher.Add(pChannelType);
                    hasher.Add(pParcelLocalID);
                    break;
                case "multiagent":
                    hasher.Add(gridhash);
                    hasher.Add(pChannelID);
                    hasher.Add(pChannelType);
                    break;
                default:
                    throw new Exception("Unknown channel type: " + pChannelType);
            }   
            BHash hashed = hasher.Finish();
            // The "Abs()" is because Janus room number must be a positive integer
            // And note that this is the BHash.GetHashCode() and not Object.getHashCode().
            int roomNumber = Math.Abs(hashed.GetHashCode());
            return roomNumber;
        }

        public async Task<JanusRoom> SelectRoom(string pRegionId, string pgridhash, string pChannelType, bool pSpatial, int pParcelLocalID, string pChannelID, string credentials)
        {
            int roomNumber = CalcRoomNumber(pRegionId, pgridhash, pChannelType, pParcelLocalID, pChannelID);

            // Should be unique for the given use and channel type
            m_log.Debug($"{LogHeader} SelectRoom: roomNumber={roomNumber}");

            // Check to see if the room has already been created
            JanusRoom existingRoom;
            lock (_rooms)
            {
                if (_rooms.TryGetValue(roomNumber, out existingRoom))
                {
                    return existingRoom;
                }
            }

            // The room doesn't exist. Create it.
            string roomDesc;
            if(pChannelType == "local")
                roomDesc = $"{pRegionId}/{pChannelType}/{pParcelLocalID}";
            else
                roomDesc = $"{pgridhash}/{pChannelType}/{pChannelID}";

            JanusRoom ret = await CreateRoom(roomNumber, pSpatial, roomDesc, credentials).ConfigureAwait(false);

            if (ret is not null)
            {
                lock (_rooms)
                {
                    if(!_rooms.TryGetValue(roomNumber, out existingRoom))
                    {
                        // If the room was created while we were waiting, 
                        _rooms[roomNumber] = ret;
                    }
                }
            }

            if (existingRoom is not null)
            {
                // The room we created was already created by someone else. Delete ours and use the existing one
                await DestroyRoom(ret).ConfigureAwait(false);
                return existingRoom;
            }

            return ret;
        }

        // Return the room with the given room ID or 'null' if no such room
        public bool GetRoom(int pRoomId, out JanusRoom room)
        {
            lock (_rooms)
            {
                return _rooms.TryGetValue(pRoomId, out room);
            }
        }

        public override void Handle_Event(JanusMessageResp pResp)
        {
            base.Handle_Event(pResp);
            AudioBridgeResp abResp = new AudioBridgeResp(pResp);
            if (abResp is not null && abResp.AudioBridgeReturnCode == "event")
            {
                // An audio bridge event!
                m_log.DebugFormat("{0} Handle_Event. {1}", LogHeader, abResp.ToString());
            }
        }

        public override void Handle_Message(JanusMessageResp pResp)
        {
            base.Handle_Message(pResp);
            AudioBridgeResp abResp = new AudioBridgeResp(pResp);
            if (abResp is not null && abResp.AudioBridgeReturnCode == "event")
            {
                // An audio bridge event!
                m_log.DebugFormat("{0} Handle_Event. {1}", LogHeader, abResp.ToString());
            }
        }
    }
}
