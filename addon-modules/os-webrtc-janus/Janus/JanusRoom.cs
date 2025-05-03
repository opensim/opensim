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
using System.Reflection;

using OpenSim.Framework;
using OpenSim.Services.Interfaces;
using OpenSim.Services.Base;

using OpenMetaverse.StructuredData;
using OpenMetaverse;

using Nini.Config;
using log4net;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Collections.Generic;

namespace WebRtcVoice
{
    // Encapsulization of a Session to the Janus server
    public class JanusRoom : IDisposable
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private static readonly string LogHeader = "[JANUS ROOM]";

        public int RoomId { get; private set; }

        private JanusPlugin _AudioBridge;

        // Wrapper around the session connection to Janus-gateway
        public JanusRoom(JanusPlugin pAudioBridge, int pRoomId)
        {
            _AudioBridge = pAudioBridge;
            RoomId = pRoomId;
        }

        public void Dispose()
        {
            // Close the room
        }

        public async Task<bool> JoinRoom(JanusViewerSession pVSession)
        {
            bool ret = false;
            try
            {
                // m_log.DebugFormat("{0} JoinRoom. New joinReq for room {1}", LogHeader, RoomId);

                // Discovered that AudioBridge doesn't care if the data portion is present
                //    and, if removed, the viewer complains that the "m=" sections are
                //    out of order. Not "cleaning" (removing the data section) seems to work.
                // string cleanSdp = CleanupSdp(pSdp);
                var joinReq = new AudioBridgeJoinRoomReq(RoomId, pVSession.AgentId.ToString());
                // joinReq.SetJsep("offer", cleanSdp);
                joinReq.SetJsep("offer", pVSession.Offer);

                JanusMessageResp resp = await _AudioBridge.SendPluginMsg(joinReq);
                AudioBridgeJoinRoomResp joinResp = new AudioBridgeJoinRoomResp(resp);

                if (joinResp is not null && joinResp.AudioBridgeReturnCode == "joined")
                {
                    pVSession.ParticipantId = joinResp.ParticipantId;
                    pVSession.Answer = joinResp.Jsep;
                    ret = true;
                    m_log.DebugFormat("{0} JoinRoom. Joined room {1}. Participant={2}", LogHeader, RoomId, pVSession.ParticipantId);
                }
                else
                {
                    m_log.ErrorFormat("{0} JoinRoom. Failed to join room {1}. Resp={2}", LogHeader, RoomId, joinResp.ToString());
                }
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("{0} JoinRoom. Exception {1}", LogHeader, e);
            }
            return ret;
        }

        // TODO: this doesn't work.
        // Not sure if it is needed. Janus generates Hangup events when the viewer leaves.
        /*
        public async Task<bool> Hangup(JanusViewerSession pAttendeeSession)
        {
            bool ret = false;
            try
            {
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("{0} LeaveRoom. Exception {1}", LogHeader, e);
            }
            return ret;
        }
        */

        public async Task<bool> LeaveRoom(JanusViewerSession pAttendeeSession)
        {
            bool ret = false;
            try
            {
                JanusMessageResp resp = await _AudioBridge.SendPluginMsg(
                    new AudioBridgeLeaveRoomReq(RoomId, pAttendeeSession.ParticipantId));
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("{0} LeaveRoom. Exception {1}", LogHeader, e);
            }
            return ret;
        }

    }
}
