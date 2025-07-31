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
using System.Threading.Tasks;

using OpenSim.Framework;
using OpenSim.Services.Base;

using OpenMetaverse.StructuredData;
using OpenMetaverse;

using Nini.Config;
using log4net;

namespace WebRtcVoice
{
    public class WebRtcJanusService : ServiceBase, IWebRtcVoiceService
    {
        private static readonly ILog _log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private static readonly string LogHeader = "[JANUS WEBRTC SERVICE]";

        private readonly IConfigSource _Config;
        private bool _Enabled = false;

        private string _JanusServerURI = String.Empty;
        private string _JanusAPIToken = String.Empty;
        private string _JanusAdminURI = String.Empty;
        private string _JanusAdminToken = String.Empty;

        private bool _MessageDetails = false;

        // An extra "viewer session" that is created initially. Used to verify the service
        //     is working and for a handle for the console commands.
        private JanusViewerSession _ViewerSession;

        public WebRtcJanusService(IConfigSource pConfig) : base(pConfig)
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            string version = assembly.GetName().Version?.ToString() ?? "unknown";

            _log.DebugFormat("{0} WebRtcJanusService version {1}", LogHeader, version);
            _Config = pConfig;
            IConfig webRtcVoiceConfig = _Config.Configs["WebRtcVoice"];

            if (webRtcVoiceConfig is not null)
            {
                _Enabled = webRtcVoiceConfig.GetBoolean("Enabled", false);
                IConfig janusConfig = _Config.Configs["JanusWebRtcVoice"];
                if (_Enabled && janusConfig is not null)
                {
                    _JanusServerURI = janusConfig.GetString("JanusGatewayURI", String.Empty);
                    _JanusAPIToken = janusConfig.GetString("APIToken", String.Empty);
                    _JanusAdminURI = janusConfig.GetString("JanusGatewayAdminURI", String.Empty);
                    _JanusAdminToken = janusConfig.GetString("AdminAPIToken", String.Empty);
                    // Debugging options
                    _MessageDetails = janusConfig.GetBoolean("MessageDetails", false);

                    if (String.IsNullOrEmpty(_JanusServerURI) || String.IsNullOrEmpty(_JanusAPIToken) ||
                        String.IsNullOrEmpty(_JanusAdminURI) || String.IsNullOrEmpty(_JanusAdminToken))
                    {
                        _log.ErrorFormat("{0} JanusWebRtcVoice configuration section missing required fields", LogHeader);
                        _Enabled = false;
                    }

                    if (_Enabled)
                    {
                        _log.DebugFormat("{0} Enabled", LogHeader);
                        StartConnectionToJanus();
                        RegisterConsoleCommands();
                    }
                }
                else
                {
                    _log.ErrorFormat("{0} No JanusWebRtcVoice configuration section", LogHeader);
                    _Enabled = false;
                }
            }
            else
            {
                _log.ErrorFormat("{0} No WebRtcVoice configuration section", LogHeader);
                _Enabled = false;
            }
        }

        // Start a thread to do the connection to the Janus server.
        // Here an initial session is created and then a handle to the audio bridge plugin
        //    is created for the console commands. Since webrtc PeerConnections that are created
        //    my Janus are per-session, the other sessions will be created by the viewer requests.
        private void StartConnectionToJanus()
        {
            _log.DebugFormat("{0} StartConnectionToJanus", LogHeader);
            Task.Run(async () =>
            {
                _ViewerSession = new JanusViewerSession(this);
                await ConnectToSessionAndAudioBridge(_ViewerSession);
            });
        }

        private async Task ConnectToSessionAndAudioBridge(JanusViewerSession pViewerSession)
        {
            JanusSession janusSession = new JanusSession(_JanusServerURI, _JanusAPIToken, _JanusAdminURI, _JanusAdminToken, _MessageDetails);
            if (await janusSession.CreateSession())
            {
                _log.DebugFormat("{0} JanusSession created", LogHeader);
                janusSession.OnDisconnect += Handle_Hangup;

                // Once the session is created, create a handle to the plugin for rooms
                JanusAudioBridge audioBridge = new JanusAudioBridge(janusSession);
                janusSession.AddPlugin(audioBridge);

                pViewerSession.VoiceServiceSessionId = janusSession.SessionId;
                pViewerSession.Session = janusSession;
                pViewerSession.AudioBridge = audioBridge;

                janusSession.OnHangup += Handle_Hangup;

                if (await audioBridge.Activate(_Config))
                {
                    _log.DebugFormat("{0} AudioBridgePluginHandle created", LogHeader);
                    // Requests through the capabilities will create rooms
                }
                else
                {
                    _log.ErrorFormat("{0} JanusPluginHandle not created", LogHeader);
                }
            }
            else
            {
                _log.ErrorFormat("{0} JanusSession not created", LogHeader);
            }   
        }

        private void Handle_Hangup(EventResp pResp)
        {
            if (pResp is not null)
            {
                var sessionId = pResp.sessionId;
                _log.DebugFormat("{0} Handle_Hangup: {1}, sessionId={2}", LogHeader, pResp.RawBody.ToString(), sessionId);
                if (VoiceViewerSession.TryGetViewerSessionByVSSessionId(sessionId, out IVoiceViewerSession viewerSession))
                {
                    // There is a viewer session associated with this session
                    DisconnectViewerSession(viewerSession as JanusViewerSession);
                }
                else
                {
                    _log.DebugFormat("{0} Handle_Hangup: no session found. SessionId={1}", LogHeader, sessionId);
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
        public async Task<OSDMap> ProvisionVoiceAccountRequest(IVoiceViewerSession pSession, OSDMap pRequest, UUID pUserID, UUID pSceneID)
        {
            OSDMap ret = null;
            string errorMsg = null;
            JanusViewerSession viewerSession = pSession as JanusViewerSession;
            if (viewerSession is not null)
            {
                if (viewerSession.Session is null)
                {
                    // This is a new session so we must create a new session and handle to the audio bridge
                    await ConnectToSessionAndAudioBridge(viewerSession);
                }

                // TODO: need to keep count of users in a room to know when to close a room
                bool isLogout = pRequest.ContainsKey("logout") && pRequest["logout"].AsBoolean();
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
                int parcel_local_id = pRequest.ContainsKey("parcel_local_id") ? pRequest["parcel_local_id"].AsInteger() : JanusAudioBridge.REGION_ROOM_ID;
                string channel_id = pRequest.ContainsKey("channel_id") ? pRequest["channel_id"].AsString() : String.Empty;
                string channel_credentials = pRequest.ContainsKey("credentials") ? pRequest["credentials"].AsString() : String.Empty;
                string channel_type = pRequest["channel_type"].AsString();
                bool isSpatial = channel_type == "local";
                string voice_server_type = pRequest["voice_server_type"].AsString();

                _log.DebugFormat("{0} ProvisionVoiceAccountRequest: parcel_id={1} channel_id={2} channel_type={3} voice_server_type={4}", LogHeader, parcel_local_id, channel_id, channel_type, voice_server_type); 

                if (pRequest.ContainsKey("jsep") && pRequest["jsep"] is OSDMap jsep)
                {
                    // The jsep is the SDP from the client. This is the client's request to connect to the audio bridge.
                    string jsepType = jsep["type"].AsString();
                    string jsepSdp = jsep["sdp"].AsString();
                    if (jsepType == "offer")
                    {
                        // The client is sending an offer. Find the right room and join it.
                        // _log.DebugFormat("{0} ProvisionVoiceAccountRequest: jsep type={1} sdp={2}", LogHeader, jsepType, jsepSdp);
                        viewerSession.Room = await viewerSession.AudioBridge.SelectRoom(pSceneID.ToString(),
                                                            channel_type, isSpatial, parcel_local_id, channel_id);
                        if (viewerSession.Room is null)
                        {
                            errorMsg = "room selection failed";
                            _log.ErrorFormat("{0} ProvisionVoiceAccountRequest: room selection failed", LogHeader);
                        }
                        else {
                            viewerSession.Offer = jsepSdp;
                            viewerSession.OfferOrig = jsepSdp;
                            viewerSession.AgentId = pUserID;
                            if (await viewerSession.Room.JoinRoom(viewerSession))    
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
                                _log.ErrorFormat("{0} ProvisionVoiceAccountRequest: JoinRoom failed", LogHeader);
                            }
                        }
                    }
                    else
                    {
                        errorMsg = "jsep type not offer";
                        _log.ErrorFormat("{0} ProvisionVoiceAccountRequest: jsep type={1} not offer", LogHeader, jsepType);
                    }
                }
                else
                {
                    errorMsg = "no jsep";
                    _log.DebugFormat("{0} ProvisionVoiceAccountRequest: no jsep. req={1}", LogHeader, pRequest.ToString());
                }
            }
            else
            {
                errorMsg = "viewersession not JanusViewerSession";
                _log.ErrorFormat("{0} ProvisionVoiceAccountRequest: viewersession not JanusViewerSession", LogHeader);
            }

            if (!String.IsNullOrEmpty(errorMsg) && ret is null)
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
        public async Task<OSDMap> VoiceSignalingRequest(IVoiceViewerSession pSession, OSDMap pRequest, UUID pUserID, UUID pSceneID)
        {
            OSDMap ret = null;
            JanusViewerSession viewerSession = pSession as JanusViewerSession;
            JanusMessageResp resp = null;
            if (viewerSession is not null)
            {
                // The request should be an array of candidates
                if (pRequest.ContainsKey("candidate") && pRequest["candidate"] is OSDMap candidate)
                {
                    if (candidate.ContainsKey("completed") && candidate["completed"].AsBoolean())
                    {
                        // The client has finished sending candidates
                        resp = await viewerSession.Session.TrickleCompleted(viewerSession);
                        _log.DebugFormat("{0} VoiceSignalingRequest: candidate completed", LogHeader);
                    }
                    else
                    {
                    }
                }
                else if (pRequest.ContainsKey("candidates") && pRequest["candidates"] is OSDArray candidates)
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
                    resp = await viewerSession.Session.TrickleCandidates(viewerSession, candidatesArray);
                    _log.DebugFormat("{0} VoiceSignalingRequest: {1} candidates", LogHeader, candidatesArray.Count);
                }
                else
                {
                    _log.ErrorFormat("{0} VoiceSignalingRequest: no 'candidate' or 'candidates'", LogHeader);
                }
            }
            if (resp is null)
            {
                _log.ErrorFormat("{0} VoiceSignalingRequest: no response so returning error", LogHeader);
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
        public Task<OSDMap> ProvisionVoiceAccountRequest(OSDMap pRequest, UUID pUserID, UUID pSceneID)
        {
            throw new NotImplementedException();
        }

        // This module should not be invoked with this signature
        // IWebRtcVoiceService.VoiceSignalingRequest
        public Task<OSDMap> VoiceSignalingRequest(OSDMap pRequest, UUID pUserID, UUID pSceneID)
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

        private async void HandleJanusInfo(string module, string[] cmdparms)
        {
            if (_ViewerSession is not null && _ViewerSession.Session is not null)
            {
                WriteOut("{0} Janus session: {1}", LogHeader, _ViewerSession.Session.SessionId);
                string infoURI = _ViewerSession.Session.JanusServerURI + "/info";
                var resp = await _ViewerSession.Session.GetFromJanus(infoURI);
                if (resp is not null)
                    MainConsole.Instance.Output(resp.ToJson());
            }
        }

        private async void HandleJanusListRooms(string module, string[] cmdparms)
        {
            if (_ViewerSession is not null && _ViewerSession.Session is not null && _ViewerSession.AudioBridge is not null)
            {
                var ab = _ViewerSession.AudioBridge;
                var resp = await ab.SendAudioBridgeMsg(new AudioBridgeListRoomsReq());
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
                            MainConsole.Instance.Output(
                                "  {0,10} {1,15} {2,5} {3,10} {4,7} {5,7}",
                                room["room"], room["description"], room["num_participants"],
                                room["sampling_rate"], room["spatial_audio"], room["record"]);
                            var participantResp = await ab.SendAudioBridgeMsg(new AudioBridgeListParticipantsReq(room["room"].AsInteger()));
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
