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
using System.Reflection;

using OpenMetaverse.StructuredData;

using log4net;
using System.Threading.Tasks;

namespace osWebRtcVoice
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
            try
            {
                // m_log.DebugFormat("{0} JoinRoom. New joinReq for room {1}", LogHeader, RoomId);

                // Discovered that AudioBridge doesn't care if the data portion is present
                //    and, if removed, the viewer complains that the "m=" sections are
                //    out of order. Not "cleaning" (removing the data section) seems to work.
                // string cleanSdp = CleanupSdp(pSdp);
                AudioBridgeAgentJoinRoomReq joinReq = new(RoomId, pVSession.AgentId);
                // joinReq.SetJsep("offer", cleanSdp);
                joinReq.SetJsep("offer", pVSession.Offer);

                JanusMessageResp resp = await _AudioBridge.SendPluginMsg(joinReq).ConfigureAwait(false);
                AudioBridgeJoinRoomResp joinResp = new(resp);

                if (joinResp is not null && joinResp.AudioBridgeReturnCode == "joined" && joinResp.ParticipantId > 0)
                {
                    pVSession.ParticipantId = joinResp.ParticipantId;
                    pVSession.Answer = joinResp.Jsep;
                    m_log.Debug($"{LogHeader} JoinRoom. Joined room {RoomId}. Participant={pVSession.ParticipantId}");
                    return true;
                }
                
                if (joinResp is not null && (joinResp.AudioBridgeErrorCode == 490 || joinResp.AudioBridgeErrorCode == 491))
                {
                    m_log.Warn($"{LogHeader} JoinRoom. Already in a room for agent {pVSession.AgentId}. Attempting recovery.");

                    bool recovered = await RecoverAlreadyInRoomAndLeave(pVSession.AgentId.ToString()).ConfigureAwait(false);
                    if (recovered)
                    {
                        AudioBridgeAgentJoinRoomReq retryJoinReq = new(RoomId, pVSession.AgentId);
                        retryJoinReq.SetJsep("offer", pVSession.Offer);
                        JanusMessageResp retryResp = await _AudioBridge.SendPluginMsg(retryJoinReq).ConfigureAwait(false);
                        AudioBridgeJoinRoomResp retryJoinResp = new(retryResp);

                        if (retryJoinResp is not null && retryJoinResp.AudioBridgeReturnCode == "joined" && retryJoinResp.ParticipantId > 0)
                        {
                            pVSession.ParticipantId = retryJoinResp.ParticipantId;
                            pVSession.Answer = retryJoinResp.Jsep;
                            m_log.Info($"{LogHeader} JoinRoom. Recovery succeeded for room {RoomId}. Participant={pVSession.ParticipantId}");
                            return true;
                        }

                        m_log.Error($"{LogHeader} JoinRoom. Recovery retry failed for room {RoomId}. Resp={retryJoinResp?.ToString() ?? "null"}");
                    }
                    else
                    {
                        m_log.Error($"{LogHeader} JoinRoom. Recovery failed: could not clear previous room membership. Resp={joinResp}");
                    }
                }
                else
                {
                    if (joinResp is not null && joinResp.AudioBridgeReturnCode == "joined" && joinResp.ParticipantId <= 0)
                    {
                        m_log.ErrorFormat("{0} JoinRoom. Joined response contains invalid participant id {1} for room {2}",
                                LogHeader, joinResp.ParticipantId, RoomId);
                        if (m_log.IsDebugEnabled)
                            m_log.DebugFormat("{0} JoinRoom. Invalid participant detail: {1}", LogHeader, joinResp.ToString());
                    }
                    else
                    {
                        m_log.ErrorFormat("{0} JoinRoom. Failed to join room {1}", LogHeader, RoomId);
                        if (m_log.IsDebugEnabled)
                            m_log.DebugFormat("{0} JoinRoom. Failure detail: {1}", LogHeader, joinResp?.ToString() ?? "null");
                    }
                }
            }
            catch (Exception e)
            {
                m_log.Error($"{LogHeader} JoinRoom. Exception ", e);
            }
            return false;
        }

        private async Task<bool> RecoverAlreadyInRoomAndLeave(string pDisplay)
        {
            try
            {
                JanusMessageResp listRoomsRespRaw = await _AudioBridge.SendPluginMsg(new AudioBridgeListRoomsReq()).ConfigureAwait(false);
                AudioBridgeResp listRoomsResp = new AudioBridgeResp(listRoomsRespRaw);
                if (listRoomsResp?.PluginRespData is null ||
                    !listRoomsResp.PluginRespData.TryGetValue("list", out OSD roomListNode) ||
                    roomListNode is not OSDArray roomList)
                {
                    return false;
                }

                foreach (OSD roomNode in roomList)
                {
                    if (roomNode is not OSDMap roomMap ||
                        !roomMap.TryGetValue("room", out OSD roomIdNode))
                        continue;

                    int roomId = roomIdNode.AsInteger();
//                    if (roomId <= 0)
//                        continue;

                    JanusMessageResp listParticipantsRespRaw = await _AudioBridge.SendPluginMsg(new AudioBridgeListParticipantsReq(roomId)).ConfigureAwait(false);
                    AudioBridgeResp listParticipantsResp = new AudioBridgeResp(listParticipantsRespRaw);
                    if (listParticipantsResp?.PluginRespData is null ||
                        !listParticipantsResp.PluginRespData.TryGetValue("participants", out OSD participantsNode) ||
                        participantsNode is not OSDArray participants)
                        continue;

                    foreach (OSD participantNode in participants)
                    {
                        if (participantNode is not OSDMap participant)
                            continue;

                        string display = participant.TryGetValue("display", out OSD displayNode) ? displayNode.AsString() : string.Empty;
                        if (!string.Equals(display, pDisplay, StringComparison.Ordinal))
                            continue;

                        long participantId = participant.TryGetValue("id", out OSD idNode) ? idNode.AsLong() : 0L;
                        if (participantId <= 0)
                            continue;

                        JanusMessageResp leaveRespRaw = await _AudioBridge.SendPluginMsg(new AudioBridgeLeaveRoomReq(roomId, participantId)).ConfigureAwait(false);
                        AudioBridgeResp leaveResp = new(leaveRespRaw);

                        if (leaveResp is not null)
                        {
                            int errorCode = leaveResp.AudioBridgeErrorCode;
                            string abCode = leaveResp.AudioBridgeReturnCode;
                            string janusCode = leaveRespRaw.ReturnCode;

                            if (errorCode == 0 || abCode == "left" || abCode == "event" || janusCode == "ack")
                            {
                                m_log.Info($"{LogHeader} RecoverAlreadyInRoomAndLeave. Cleared stale participant {participantId} from room {roomId}");
                                return true;
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                m_log.Error($"{LogHeader} RecoverAlreadyInRoomAndLeave. Exception ", e);
            }

            return false;
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
            try
            {
                JanusMessageResp resp = await _AudioBridge.SendPluginMsg(
                    new AudioBridgeLeaveRoomReq(RoomId, pAttendeeSession.ParticipantId)).ConfigureAwait(false);

                if (resp is null)
                {
                    m_log.Error($"{LogHeader} LeaveRoom. Null response for room {RoomId}, participant={pAttendeeSession.ParticipantId}");
                    return false;
                }

                AudioBridgeResp abResp = new(resp);
                string returnCode = abResp.AudioBridgeReturnCode;
                string janusReturnCode = resp.ReturnCode;
                int errorCode = abResp.AudioBridgeErrorCode;

                if (errorCode == 0 &&
                    (abResp.isSuccess || returnCode == "left" || returnCode == "event" || returnCode == "success" || janusReturnCode == "ack"))
                {
                    if (janusReturnCode == "ack" && string.IsNullOrEmpty(returnCode))
                    {
                        m_log.Debug($"{LogHeader} LeaveRoom. Ack for room {RoomId}, participant={pAttendeeSession.ParticipantId}");
                    }
                    return true;
                }

                if (errorCode == 487 &&
                    (returnCode == "event" || janusReturnCode == "event" || janusReturnCode == "ack"))
                {
                    m_log.Info($"{LogHeader} LeaveRoom. Participant already left room {RoomId}, participant={pAttendeeSession.ParticipantId} (errorCode=487)");
                    return true;
                }

                m_log.Error($"{LogHeader} LeaveRoom. Failed room {RoomId}, participant={pAttendeeSession.ParticipantId}, janus={janusReturnCode}, audiobridge={returnCode}, errorCode={errorCode}");
            }
            catch (Exception e)
            {
                m_log.Error($"{LogHeader} LeaveRoom. Exception ", e);
            }
            return false;
        }
    }
}
