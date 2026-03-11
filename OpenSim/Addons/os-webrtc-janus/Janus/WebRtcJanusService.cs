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
using System.Threading.Tasks;

using OpenSim.Framework;
using OpenSim.Services.Base;

using OpenMetaverse.StructuredData;
using OpenMetaverse;

using Nini.Config;
using log4net;

namespace osWebRtcVoice
{
    public class WebRtcJanusService : ServiceBase, IWebRtcVoiceService
    {
        private static readonly ILog _log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private static readonly string LogHeader = "[JANUS WEBRTC SERVICE]";

        private readonly IConfigSource _Config;
        private bool _Enabled = false;

        private string _JanusServerURI = string.Empty;
        private string _JanusAPIToken = string.Empty;
        private string _JanusAdminURI = string.Empty;
        private string _JanusAdminToken = string.Empty;

        private bool _MessageDetails = false;

        // An extra "viewer session" that is created initially. Used to verify the service
        //     is working and for a handle for the console commands.
        private JanusViewerSession _ViewerSession;

        public WebRtcJanusService(IConfigSource pConfig) : base(pConfig)
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            string version = assembly.GetName().Version?.ToString() ?? "unknown";

            _log.Debug($"{LogHeader} WebRtcJanusService version {version}");
            _Config = pConfig;
            IConfig webRtcVoiceConfig = _Config.Configs["WebRtcVoice"];

            if (webRtcVoiceConfig is not null)
            {
                _Enabled = webRtcVoiceConfig.GetBoolean("Enabled", false);
                IConfig janusConfig = _Config.Configs["JanusWebRtcVoice"];
                if (_Enabled && janusConfig is not null)
                {
                    _JanusServerURI = janusConfig.GetString("JanusGatewayURI", string.Empty);
                    _JanusAPIToken = janusConfig.GetString("APIToken", string.Empty);
                    _JanusAdminURI = janusConfig.GetString("JanusGatewayAdminURI", string.Empty);
                    _JanusAdminToken = janusConfig.GetString("AdminAPIToken", string.Empty);
                    // Debugging options
                    _MessageDetails = janusConfig.GetBoolean("MessageDetails", false);

                    if (string.IsNullOrEmpty(_JanusServerURI) || string.IsNullOrEmpty(_JanusAPIToken) ||
                        string.IsNullOrEmpty(_JanusAdminURI) || string.IsNullOrEmpty(_JanusAdminToken))
                    {
                        _log.Error($"{LogHeader} JanusWebRtcVoice configuration section missing required fields");
                        _Enabled = false;
                    }

                    if (_Enabled)
                    {
                        if(!StartConnectionToJanus())
                        {
                            _log.Error($"{LogHeader} failed connection to Janus Gateway. Disabled");
                            _Enabled=false;
                            return;
                        }
                        RegisterConsoleCommands();
                        _log.Info($"{LogHeader} Enabled");
                    }
                }
                else
                {
                    _log.Error($"{LogHeader} No JanusWebRtcVoice configuration section");
                    _Enabled = false;
                }
            }
            else
            {
                _log.Error($"{LogHeader} No WebRtcVoice configuration section");
                _Enabled = false;
            }
        }

        // Here an initial session is created and then a handle to the audio bridge plugin
        //    is created for the console commands. Since webrtc PeerConnections that are created
        //    my Janus are per-session, the other sessions will be created by the viewer requests.
        private bool StartConnectionToJanus()
        {
            _log.DebugFormat("{0} StartConnectionToJanus", LogHeader);
                _ViewerSession = new JanusViewerSession(this);
            //bad
            return ConnectToSessionAndAudioBridge(_ViewerSession).Result;
        }

        private async Task<bool> ConnectToSessionAndAudioBridge(JanusViewerSession pViewerSession)
        {
            JanusSession janusSession = new JanusSession(_JanusServerURI, _JanusAPIToken, _JanusAdminURI, _JanusAdminToken, _MessageDetails);
            if (await janusSession.CreateSession().ConfigureAwait(false))
            {
                _log.DebugFormat("{0} JanusSession created", LogHeader);

                // Once the session is created, create a handle to the plugin for rooms
                JanusAudioBridge audioBridge = new JanusAudioBridge(janusSession);

                if (await audioBridge.Activate(_Config).ConfigureAwait(false))
                {
                    _log.Debug($"{LogHeader} AudioBridgePluginHandle created");
                    // Requests through the capabilities will create rooms
    
                    janusSession.AddPlugin(audioBridge);
                        
                    pViewerSession.VoiceServiceSessionId = janusSession.SessionId;
                    pViewerSession.Session = janusSession;
                    pViewerSession.AudioBridge = audioBridge;
                    janusSession.OnDisconnect += Handle_Hangup;
                    janusSession.OnHangup += Handle_Hangup;
                    return true;
                }
                _log.Error($"{LogHeader} JanusPluginHandle not created");
            }
            _log.Error($"{LogHeader} JanusSession not created");
            return false;
        }

        private void Handle_Hangup(EventResp pResp)
        {
            if (pResp is not null)
            {
                var sessionId = pResp.sessionId;
                _log.Debug($"{LogHeader} Handle_Hangup: {pResp.RawBody}, sessionId={sessionId}");
                if (VoiceViewerSession.TryGetViewerSessionByVSSessionId(sessionId, out IVoiceViewerSession viewerSession))
                {
                    // There is a viewer session associated with this session
                    DisconnectViewerSession(viewerSession as JanusViewerSession);
                }
                else
                {
                    _log.Debug($"{LogHeader} Handle_Hangup: no session found. SessionId={sessionId}");
                }
            }
        }

        // Disconnect the viewer session. This is called when the viewer logs out or hangs up.
        private void DisconnectViewerSession(JanusViewerSession pViewerSession)
        {
            if (pViewerSession is not null)
            {
                Task.Run(() =>
                {
                    VoiceViewerSession.RemoveViewerSession(pViewerSession.ViewerSessionID);
                    // No need to wait for the session to be shutdown
                    _ = pViewerSession.Shutdown();
                });
            }
        }   

        // The pRequest parameter is a straight conversion of the JSON request from the client.
        // This is the logic that takes the client's request and converts it into
        //     operations on rooms in the audio bridge.
        // IWebRtcVoiceService.ProvisionVoiceAccountRequest
        public OSDMap ProvisionVoiceAccountRequest(IVoiceViewerSession pSession, OSDMap pRequest, UUID pUserID, UUID pSceneID)
        {
            return ProvisionVoiceAccountRequestBAD(pSession, pRequest, pUserID, pSceneID).Result;
        }

        public async Task<OSDMap> ProvisionVoiceAccountRequestBAD(IVoiceViewerSession pSession, OSDMap pRequest, UUID pUserID, UUID pSceneID)
        {
            OSDMap ret = null;
            string errorMsg = null;
            JanusViewerSession viewerSession = pSession as JanusViewerSession;
            if (viewerSession is not null)
            {
                if (viewerSession.Session is null)
                {
                    // This is a new session so we must create a new session and handle to the audio bridge
                    await ConnectToSessionAndAudioBridge(viewerSession).ConfigureAwait(false);
                }

                // TODO: need to keep count of users in a room to know when to close a room
                bool isLogout = pRequest.TryGetBool("logout", out bool lgout) && lgout;
                if (isLogout)
                {
                    // The client is logging out. Exit the room.
                    if (viewerSession.Room is not null)
                    {
                        await viewerSession.Room.LeaveRoom(viewerSession);
                        viewerSession.Room = null;
                    }
                    return new OSDMap
                    {
                        { "response", "closed" }
                    };
                }

                // Get the parameters that select the room
                // To get here, voice_server_type has already been checked to be 'webrtc' and channel_type='local'
                int parcel_local_id = pRequest.TryGetInt("parcel_local_id", out int pli) ? pli : JanusAudioBridge.REGION_ROOM_ID;
                string channel_id = pRequest.TryGetString("channel_id", out string cli) ? cli : string.Empty;
                string channel_credentials = pRequest.TryGetString("credentials", out string cred) ? cred : string.Empty;
                string channel_type = pRequest["channel_type"].AsString();
                bool isSpatial = channel_type == "local";
                string voice_server_type = pRequest["voice_server_type"].AsString();

                _log.DebugFormat("{0} ProvisionVoiceAccountRequest: parcel_id={1} channel_id={2} channel_type={3} voice_server_type={4}", LogHeader, parcel_local_id, channel_id, channel_type, voice_server_type); 

                if (pRequest.TryGetOSDMap("jsep", out OSDMap jsep))
                {
                    // The jsep is the SDP from the client. This is the client's request to connect to the audio bridge.
                    string jsepType = jsep["type"].AsString();
                    string jsepSdp = jsep["sdp"].AsString();
                    if (jsepType == "offer")
                    {
                        // The client is sending an offer. Find the right room and join it.
                        // _log.DebugFormat("{0} ProvisionVoiceAccountRequest: jsep type={1} sdp={2}", LogHeader, jsepType, jsepSdp);
                        viewerSession.Room = await viewerSession.AudioBridge.SelectRoom(pSceneID.ToString(),
                                                            channel_type, isSpatial, parcel_local_id, channel_id).ConfigureAwait(false);
                        if (viewerSession.Room is null)
                        {
                            errorMsg = "room selection failed";
                            _log.Error($"{LogHeader} ProvisionVoiceAccountRequest: room selection failed");
                        }
                        else {
                            viewerSession.Offer = jsepSdp;
                            viewerSession.OfferOrig = jsepSdp;
                            viewerSession.AgentId = pUserID;
                            if (await viewerSession.Room.JoinRoom(viewerSession).ConfigureAwait(false))
                            {
                                ret = new OSDMap
                                {
                                    { "jsep", viewerSession.Answer },
                                    { "viewer_session", viewerSession.ViewerSessionID }
                                };
                            }
                            else
                            {
                                errorMsg = "JoinRoom failed";
                                _log.Error($"{LogHeader} ProvisionVoiceAccountRequest: JoinRoom failed");
                            }
                        }
                    }
                    else
                    {
                        errorMsg = "jsep type not offer";
                        _log.Error($"{LogHeader} ProvisionVoiceAccountRequest: jsep type={jsepType} not offer");
                    }
                }
                else
                {
                    errorMsg = "no jsep";
                    _log.Debug($"{LogHeader} ProvisionVoiceAccountRequest: no jsep. req={pRequest}");
                }
            }
            else
            {
                errorMsg = "viewersession not JanusViewerSession";
                _log.Error("{LogHeader} ProvisionVoiceAccountRequest: viewersession not JanusViewerSession");
            }

            if (!string.IsNullOrEmpty(errorMsg) && ret is null)
            {
                // The provision failed so build an error messgage to return
                ret = new OSDMap
                {
                    { "response", "failed" },
                    { "error", errorMsg }
                };
            }

            return ret;
        }

        // IWebRtcVoiceService.VoiceAccountBalanceRequest
        public OSDMap VoiceSignalingRequest(IVoiceViewerSession pSession, OSDMap pRequest, UUID pUserID, UUID pSceneID)
        {
            return VoiceSignalingRequestBAD(pSession, pRequest, pUserID, pSceneID).Result;
        }

        public async Task<OSDMap> VoiceSignalingRequestBAD(IVoiceViewerSession pSession, OSDMap pRequest, UUID pUserID, UUID pSceneID)
        {
            OSDMap ret = null;
            JanusViewerSession viewerSession = pSession as JanusViewerSession;
            JanusMessageResp resp = null;
            if (viewerSession is not null)
            {
                // The request should be an array of candidates
                if (pRequest.TryGetOSDMap("candidate", out OSDMap candidate))
                {
                    if (candidate.TryGetBool("completed", out bool iscompleted) && iscompleted)
                    {
                        // The client has finished sending candidates
                        resp = await viewerSession.Session.TrickleCompleted(viewerSession).ConfigureAwait(false);
                        _log.DebugFormat($"{LogHeader} VoiceSignalingRequest: candidate completed");
                    }
                    else
                    {
                    }
                }
                else if (pRequest.TryGetOSDArray("candidates", out OSDArray candidates))
                {
                    OSDArray candidatesArray = new OSDArray();
                    foreach (OSDMap cand in candidates)
                    {
                        candidatesArray.Add(new OSDMap() {
                            { "candidate", cand["candidate"].AsString() },
                            { "sdpMid", cand["sdpMid"].AsString() },
                            { "sdpMLineIndex", cand["sdpMLineIndex"].AsLong() }
                        });
                    }
                    resp = await viewerSession.Session.TrickleCandidates(viewerSession, candidatesArray).ConfigureAwait(false);
                    _log.Debug($"{LogHeader} VoiceSignalingRequest: {candidatesArray.Count} candidates");
                }
                else
                {
                    _log.Error($"{LogHeader} VoiceSignalingRequest: no 'candidate' or 'candidates'");
                }
            }
            if (resp is null)
            {
                _log.ErrorFormat($"{LogHeader} VoiceSignalingRequest: no response so returning error");
                ret = new OSDMap
                {
                    { "response", "error" }
                };
            }
            else
            {
                ret = resp.RawBody;
            }
            return ret;
        }

        // This module should not be invoked with this signature
        // IWebRtcVoiceService.ProvisionVoiceAccountRequest
        public OSDMap ProvisionVoiceAccountRequest(OSDMap pRequest, UUID pUserID, UUID pSceneID)
        {
            throw new NotImplementedException();
        }

        // This module should not be invoked with this signature
        // IWebRtcVoiceService.VoiceSignalingRequest
        public OSDMap VoiceSignalingRequest(OSDMap pRequest, UUID pUserID, UUID pSceneID)
        {
            throw new NotImplementedException();
        }

        // The viewer session object holds all the connection information to Janus.
        // IWebRtcVoiceService.CreateViewerSession
        public IVoiceViewerSession CreateViewerSession(OSDMap pRequest, UUID pUserID, UUID pSceneID)
        {
            return new JanusViewerSession(this)
            {
                AgentId = pUserID,
                RegionId = pSceneID
            };
        }

        // ======================================================================================================
        private void RegisterConsoleCommands()
        {
            if (_Enabled) {
                MainConsole.Instance.Commands.AddCommand("Webrtc", false, "janus info",
                    "janus info",
                    "Show Janus server information",
                    HandleJanusInfo);
                MainConsole.Instance.Commands.AddCommand("Webrtc", false, "janus list rooms",
                    "janus list rooms",
                    "List the rooms on the Janus server",
                    HandleJanusListRooms);
                // List rooms
                // List participants in a room
            }
        }

        private void HandleJanusInfo(string module, string[] cmdparms)
        {
            if (_ViewerSession is not null && _ViewerSession.Session is not null)
            {
                WriteOut("{0} Janus session: {1}", LogHeader, _ViewerSession.Session.SessionId);
                string infoURI = _ViewerSession.Session.JanusServerURI + "/info";

                var resp = _ViewerSession.Session.GetFromJanus(infoURI).Result;
                
                if (resp is not null)
                    MainConsole.Instance.Output(resp.ToJson());
            }
        }

        private void HandleJanusListRooms(string module, string[] cmdparms)
        {
            if (_ViewerSession is not null && _ViewerSession.Session is not null && _ViewerSession.AudioBridge is not null)
            {
                var ab = _ViewerSession.AudioBridge;
                var resp = ab.SendAudioBridgeMsg(new AudioBridgeListRoomsReq()).Result;
                if (resp is not null && resp.isSuccess)
                {
                    if (resp.PluginRespData.TryGetValue("list", out OSD list))
                    {
                        MainConsole.Instance.Output("");
                        MainConsole.Instance.Output(
                            "  {0,10} {1,15} {2,5} {3,10} {4,7} {5,7}",
                            "Room", "Description", "Num", "SampleRate", "Spatial", "Recording");
                        foreach (OSDMap room in list as OSDArray)
                        {
                            int roomid = room["room"].AsInteger();
                            MainConsole.Instance.Output(
                                "  {0,10} {1,15} {2,5} {3,10} {4,7} {5,7}",
                                roomid, room["description"], room["num_participants"],
                                room["sampling_rate"], room["spatial_audio"], room["record"]);

                            var participantResp = ab.SendAudioBridgeMsg(new AudioBridgeListParticipantsReq(roomid)).Result;

                            if (participantResp is not null && participantResp.AudioBridgeReturnCode == "participants")
                            {
                                if (participantResp.PluginRespData.TryGetValue("participants", out OSD participants))
                                {
                                    foreach (OSDMap participant in participants as OSDArray)
                                    {
                                        MainConsole.Instance.Output("      {0}/{1},muted={2},talking={3},pos={4}",
                                            participant["id"].AsLong(), participant["display"], participant["muted"],
                                            participant["talking"], participant["spatial_position"]);
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        MainConsole.Instance.Output("No rooms");
                    }
                }
                else
                {
                    MainConsole.Instance.Output("Failed to get room list");
                }
            }
        }

        private void WriteOut(string msg, params object[] args)
        {
            // m_log.InfoFormat(msg, args);
            MainConsole.Instance.Output(msg, args);
        }


    }
 }
