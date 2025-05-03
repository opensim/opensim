// Copyright 2024 Robert Adams (misterblue@misterblue.com)
//
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;

using log4net;

namespace WebRtcVoice
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
            AudioBridgeResp ret = null;
            try
            {
                ret = new AudioBridgeResp(await SendPluginMsg(pMsg));
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("{0} SendPluginMsg. Exception {1}", LogHeader, e);
            }
            return ret;
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
        public async Task<JanusRoom> CreateRoom(int pRoomId, bool pSpatial, string pRoomDesc)
        {
            JanusRoom ret = null;
            try
            {
                JanusMessageResp resp = await SendPluginMsg(new AudioBridgeCreateRoomReq(pRoomId, pSpatial, pRoomDesc));
                AudioBridgeResp abResp = new AudioBridgeResp(resp);

                m_log.DebugFormat("{0} CreateRoom. ReturnCode: {1}", LogHeader, abResp.AudioBridgeReturnCode);
                switch (abResp.AudioBridgeReturnCode)
                {
                    case "created":
                        ret = new JanusRoom(this, pRoomId);
                        break;
                    case "event":
                        if (abResp.AudioBridgeErrorCode == 486)
                        {
                            m_log.WarnFormat("{0} CreateRoom. Room {1} already exists. Reusing! {2}", LogHeader, pRoomId, abResp.ToString());
                            // if room already exists, just use it
                            ret = new JanusRoom(this, pRoomId);
                        }
                        else
                        {
                            m_log.ErrorFormat("{0} CreateRoom. XX Room creation failed: {1}", LogHeader, abResp.ToString());
                        }
                        break;
                    default:
                        m_log.ErrorFormat("{0} CreateRoom. YY Room creation failed: {1}", LogHeader, abResp.ToString());
                        break;
                }   
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("{0} CreateRoom. Exception {1}", LogHeader, e);
            }
            return ret;
        }

        public async Task<bool> DestroyRoom(JanusRoom janusRoom)
        {
            bool ret = false;
            try
            {
                JanusMessageResp resp = await SendPluginMsg(new AudioBridgeDestroyRoomReq(janusRoom.RoomId));
                ret = true;
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("{0} DestroyRoom. Exception {1}", LogHeader, e);
            }
            return ret;
        }

        // Constant used to denote that this is a spatial audio room for the region (as opposed to parcels)
        public const int REGION_ROOM_ID = -999;
        private Dictionary<int, JanusRoom> _rooms = new Dictionary<int, JanusRoom>();

        // Calculate a room number for the given parameters. The room number is a hash of the parameters.
        // The attempt is to deterministicly create a room number so all regions will generate the
        //     same room number across sessions and across the grid.
        // getHashCode() is not deterministic across sessions.
        public static int CalcRoomNumber(string pRegionId, string pChannelType, int pParcelLocalID, string pChannelID)
        {
            var hasher = new BHasherMdjb2();
            // If there is a channel specified it must be group 
            switch (pChannelType)
            {
                case "local":
                    // A "local" channel is unique to the region and parcel
                    hasher.Add(pRegionId);
                    hasher.Add(pChannelType);
                    hasher.Add(pParcelLocalID);
                    break;
                case "multiagent":
                    // A "multiagent" channel is unique to the grid
                    // should add a GridId here
                    hasher.Add(pChannelID);
                    hasher.Add(pChannelType);
                    break;
                default:
                    throw new Exception("Unknown channel type: " + pChannelType);
            }   
            var hashed = hasher.Finish();
            // The "Abs()" is because Janus room number must be a positive integer
            // And note that this is the BHash.GetHashCode() and not Object.getHashCode().
            int roomNumber = Math.Abs(hashed.GetHashCode());
            return roomNumber;
        }
        public async Task<JanusRoom> SelectRoom(string pRegionId, string pChannelType, bool pSpatial, int pParcelLocalID, string pChannelID)
        {
            int roomNumber = CalcRoomNumber(pRegionId, pChannelType, pParcelLocalID, pChannelID);

            // Should be unique for the given use and channel type
            m_log.DebugFormat("{0} SelectRoom: roomNumber={1}", LogHeader, roomNumber);

            // Check to see if the room has already been created
            lock (_rooms)
            {
                if (_rooms.ContainsKey(roomNumber))
                {
                    return _rooms[roomNumber];
                }
            }

            // The room doesn't exist. Create it.
            string roomDesc = pRegionId + "/" + pChannelType + "/" + pParcelLocalID + "/" + pChannelID;
            JanusRoom ret = await CreateRoom(roomNumber, pSpatial, roomDesc);

            JanusRoom existingRoom = null;
            if (ret is not null)
            {
                lock (_rooms)
                {
                    if (_rooms.ContainsKey(roomNumber))
                    {
                        // If the room was created while we were waiting, 
                        existingRoom = _rooms[roomNumber];
                    }
                    else
                    {
                        // Our room is the first one created. Save it.
                        _rooms[roomNumber] = ret;
                    }
                }
            }
            if (existingRoom is not null)
            {
                // The room we created was already created by someone else. Delete ours and use the existing one
                await DestroyRoom(ret);
                ret = existingRoom;
            }
            return ret;
        }

        // Return the room with the given room ID or 'null' if no such room
        public JanusRoom GetRoom(int pRoomId)
        {
            JanusRoom ret = null;
            lock (_rooms)
            {
                _rooms.TryGetValue(pRoomId, out ret);
            }
            return ret;
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
