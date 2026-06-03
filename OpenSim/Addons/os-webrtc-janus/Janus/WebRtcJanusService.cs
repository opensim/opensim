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

using log4net;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using OpenSim.Framework;
using OpenSim.Services.Base;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

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

        private bool _JanusDebug = false;
        private bool _MessageDetails = false;

        // Maximum ICE candidates accepted from one VoiceSignalingRequest call.
        // <= 0 means no limit.
        private int _MaxSignalingCandidatesPerRequest = 20;
        // Delay between a disconnect and next join for same agent.
        private int _RejoinCooldownMs = 250;

        private readonly ConcurrentDictionary<UUID, DateTime> _LastDisconnectByAgent = new();
        private long _VoiceFlowCounter;

        // An extra "viewer session" that is created initially. Used to verify the service
        //     is working and for a handle for the console commands.
        private JanusViewerSession _ViewerSession;

        public WebRtcJanusService(IConfigSource pConfig) : base(pConfig)
        {
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

                    if (string.IsNullOrEmpty(_JanusServerURI) || string.IsNullOrEmpty(_JanusAPIToken) ||
                        string.IsNullOrEmpty(_JanusAdminURI) || string.IsNullOrEmpty(_JanusAdminToken))
                    {
                        _log.Error($"{LogHeader} JanusWebRtcVoice configuration section missing required fields");
                        _Enabled = false;
                        return;
                    }

                    // Debugging options
                    _JanusDebug = janusConfig.GetBoolean("JanusDebug", false);
                    _MessageDetails = janusConfig.GetBoolean("MessageDetails", false);

                    _MaxSignalingCandidatesPerRequest = janusConfig.GetInt("MaxSignalingCandidatesPerRequest", 20);
                    if (_MaxSignalingCandidatesPerRequest < 0)
                    {
                        _log.WarnFormat("{0} MaxSignalingCandidatesPerRequest < 0 ({1}), using 0 (unlimited)",
                                LogHeader, _MaxSignalingCandidatesPerRequest);
                        _MaxSignalingCandidatesPerRequest = 0;
                    }

                    _RejoinCooldownMs = janusConfig.GetInt("RejoinCooldownMs", 250);
                    if (_RejoinCooldownMs < 0)
                    {
                        _log.WarnFormat("{0} RejoinCooldownMs < 0 ({1}), using 0", LogHeader, _RejoinCooldownMs);
                        _RejoinCooldownMs = 0;
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
            JanusSession janusSession = new(_JanusServerURI, _JanusAPIToken, _JanusAdminURI, _JanusAdminToken, _MessageDetails);
            if (await janusSession.CreateSession().ConfigureAwait(false))
            {
                _log.DebugFormat("{0} JanusSession created", LogHeader);

                // Once the session is created, create a handle to the plugin for rooms
                JanusAudioBridge audioBridge = new(janusSession);

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
                string sessionId = pResp.sessionId;
                _log.Debug($"{LogHeader} Handle_Hangup: {pResp.RawBody}, sessionId={sessionId}");
                if (VoiceViewerSession.TryGetViewerSessionByVSSessionId(sessionId, out IVoiceViewerSession viewerSession))
                {
                    // There is a viewer session associated with this session
//                    DisconnectViewerSession(viewerSession as JanusViewerSession);

                    // A Janus hangup can happen during a normal room switch/re-offer cycle.
                    // Keep the viewer session alive and only clear the per-call state.
                    if (viewerSession is JanusViewerSession janusViewerSession)
                    {
                        janusViewerSession.ParticipantId = 0;
                        janusViewerSession.Answer = null;
                        janusViewerSession.Offer = string.Empty;
                        janusViewerSession.OfferOrig = string.Empty;
                        janusViewerSession.Room = null;
                    }
                }
                else
                {
                    _log.Debug($"{LogHeader} Handle_Hangup: no session found. SessionId={sessionId}");
                }
            }
        }

        private void Handle_Disconnect(EventResp pResp)
        {
            if (pResp is null)
                return;

            if (VoiceViewerSession.TryGetViewerSessionByVSSessionId(pResp.sessionId, out IVoiceViewerSession viewerSession))
            {
                DisconnectViewerSession(viewerSession as JanusViewerSession, "disconnect");
            }
            else
            {
                _log.DebugFormat("{0} Handle_Disconnect: no session found. SessionId={1}", LogHeader, pResp.sessionId);
            }
        }

        private static string FlowTag(long pFlowId, JanusViewerSession pViewerSession)
        {
            return $"flow={pFlowId}, viewer_session={pViewerSession?.ViewerSessionID ?? "<none>"}";
        }

        // Disconnect the viewer session. This is called when the viewer logs out or hangs up.

        private void DisconnectViewerSession(JanusViewerSession pViewerSession, string pReason)
        {
            if (pViewerSession is not null)
            {
                if (!pViewerSession.TryStartDisconnect(pReason))
                {
                    _log.DebugFormat("{0} DisconnectViewerSession: duplicate disconnect suppressed. viewer_session={1}, reason={2}, firstReason={3}",
                            LogHeader, pViewerSession.ViewerSessionID, pReason, pViewerSession.DisconnectReason);
                    return;
                }

                int roomId = pViewerSession.Room is not null ? pViewerSession.Room.RoomId : 0;
                _LastDisconnectByAgent[pViewerSession.AgentId] = DateTime.UtcNow;
                _log.InfoFormat("{0} ProvisionVoiceAccountRequest: disconnected by {1}. agent={2}, scene={3}, room={4}, participant={5}, viewer_session={6}",
                        LogHeader, pReason, pViewerSession.AgentId, pViewerSession.RegionId, roomId, pViewerSession.ParticipantId, pViewerSession.ViewerSessionID);
 
                VoiceViewerSession.RemoveViewerSession(pViewerSession.ViewerSessionID);
                Task.Run(() =>
                {
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
            // the bad refers to use of .Result
            // that should not be used and may deadlock, due to poor ms design of all this specially async/await crap
            return ProvisionVoiceAccountRequestBAD(pSession, pRequest, pUserID, pSceneID).Result;
        }

        public async Task<OSDMap> ProvisionVoiceAccountRequestBAD(IVoiceViewerSession pSession, OSDMap pRequest, UUID pUserID, UUID pSceneID)
        {
            long flowId = Interlocked.Increment(ref _VoiceFlowCounter);
            if(pRequest.TryGetString("voice_server_type", out string voice_server_type))
            {
                if(!"webrtc".Equals(voice_server_type,StringComparison. CurrentCultureIgnoreCase))
                {
                    _log.Error($"{LogHeader} ProvisionVoiceAccountRequest: invalid server type {voice_server_type ?? "null"}");
                    return new OSDMap
                    {
                        { "response", "failed" },
                        { "error", "Invalid server type" }
                    };
                }
            }

            OSDMap ret = null;
            string errorMsg = null;
            JanusViewerSession viewerSession = pSession as JanusViewerSession;

            if (viewerSession is not null)
            {
                // TODO: need to keep count of users in a room to know when to close a room
                bool isLogout = pRequest.TryGetBool("logout", out bool lgout) && lgout;
                if (isLogout)
                {
                    // Exit the room.
                    if (viewerSession.Room is not null)
                    {
                        _ = await viewerSession.Room.LeaveRoom(viewerSession).ConfigureAwait(false);
                        viewerSession.Room = null;
                    }

                    // The client is logging out. Disconnect the entire Janus viewer session.
                    DisconnectViewerSession(viewerSession, "logout");
                    return new OSDMap
                    {
                        { "response", "closed" }
                    };
                }

                if (viewerSession.Session is null)
                {
                    // This is a new session so we must create a new session and handle to the audio bridge
                    await ConnectToSessionAndAudioBridge(viewerSession).ConfigureAwait(false);
                }

                // Get the parameters that select the room
                // To get here, voice_server_type has already been checked to be 'webrtc' and channel_type='local'
                int parcel_local_id = pRequest.TryGetInt("parcel_local_id", out int pli) ? pli : JanusAudioBridge.REGION_ROOM_ID;
                string channel_id = pRequest.TryGetString("channel_id", out string cli) ? cli : string.Empty;
                string channel_credentials = pRequest.TryGetString("credentials", out string cred) ? cred : string.Empty;
                string channel_type = pRequest["channel_type"].AsString();
                string gridHash = pRequest.TryGetValue("gridhash", out OSD ghash) ? ghash.AsString() : string.Empty;
                bool isSpatial = channel_type == "local";

                _log.DebugFormat("{0} ProvisionVoiceAccountRequest: parcel_id={1} channel_id={2} channel_type={3} voice_server_type={4}", LogHeader, parcel_local_id, channel_id, channel_type, voice_server_type); 

                if (pRequest.TryGetOSDMap("jsep", out OSDMap jsep))
                {
                    await viewerSession.ProvisionLock.WaitAsync();

                    try
                    {
                        // The jsep is the SDP from the client. This is the client's request to connect to the audio bridge.
                        string jsepType = jsep["type"].AsString();
                        string jsepSdp = jsep["sdp"].AsString();
                        if (jsepType == "offer")
                        {
                            // The client is sending an offer. Find the right room and join it.
                            // _log.DebugFormat("{0} ProvisionVoiceAccountRequest: jsep type={1} sdp={2}", LogHeader, jsepType, jsepSdp);
                            viewerSession.Room = await viewerSession.AudioBridge.SelectRoom(pSceneID.ToString(), gridHash,
                                                                channel_type, isSpatial, parcel_local_id, channel_id, channel_credentials).ConfigureAwait(false);
                            if (viewerSession.Room is null)
                            {
                                errorMsg = "room selection failed";
                                _log.Error($"{LogHeader} ProvisionVoiceAccountRequest: room selection failed");
                            }
                            else
                            {
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
                    finally
                    {
                        viewerSession.ProvisionLock.Release();
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
                _log.WarnFormat("{0} ProvisionVoiceAccountRequest: failed ({1}) error={2}", LogHeader, FlowTag(flowId, viewerSession), errorMsg);
            }
            else
            {
                _log.DebugFormat("{0} ProvisionVoiceAccountRequest: end ({1})", LogHeader, FlowTag(flowId, viewerSession));
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
            long flowId = Interlocked.Increment(ref _VoiceFlowCounter);
            if (viewerSession is not null && viewerSession.Session is not null)
            {
                // The request should be an array of candidates
                if (pRequest.TryGetOSDMap("candidate", out OSDMap candidate))
                {
                    if (candidate.TryGetValue("completed", out OSD ocompleted) && ocompleted.AsBoolean())
                    {
                        // The client has finished sending candidates
                        resp = await viewerSession.Session.TrickleCompleted(viewerSession).ConfigureAwait(false);
                        _log.Debug($"{LogHeader} VoiceSignalingRequest: candidate completed");
                    }
                    else
                    {
                        OSDArray candidatesArray =
                        [
                            new OSDMap()
                            {
                                { "candidate", candidate.ContainsKey("candidate") ? candidate["candidate"].AsString() : String.Empty },
                                { "sdpMid", candidate.ContainsKey("sdpMid") ? candidate["sdpMid"].AsString() : String.Empty },
                                { "sdpMLineIndex", candidate.ContainsKey("sdpMLineIndex") ? candidate["sdpMLineIndex"].AsLong() : 0 }
                            }
                        ];
                        resp = await viewerSession.Session.TrickleCandidates(viewerSession, candidatesArray).ConfigureAwait(false);
                        _log.Debug($"{LogHeader} VoiceSignalingRequest: single candidate");
                    }
                }
                else if (pRequest.TryGetOSDArray("candidates", out OSDArray candidates))
                {
                    OSDArray candidatesArray = [];
                    //int sourceCount = candidates.Count;
                    //int candidateLimit = _MaxSignalingCandidatesPerRequest;
                    foreach (OSDMap cand in candidates)
                    {
                        // TODO: can not limit candidates blindly
//                        if (candidateLimit > 0 && candidatesArray.Count >= candidateLimit)
//                            break;

                        candidatesArray.Add(new OSDMap() {
                            { "candidate", cand["candidate"].AsString() },
                            { "sdpMid", cand["sdpMid"].AsString() },
                            { "sdpMLineIndex", cand["sdpMLineIndex"].AsLong() }
                        });
                    }
                    resp = await viewerSession.Session.TrickleCandidates(viewerSession, candidatesArray).ConfigureAwait(false);
//                    if (candidateLimit > 0 && sourceCount > candidatesArray.Count)
//                    {
//                        _log.WarnFormat("{0} VoiceSignalingRequest: capped candidates {1}/{2} (MaxSignalingCandidatesPerRequest={3})",
//                                LogHeader, candidatesArray.Count, sourceCount, candidateLimit);
//                    }
//                    else
//                    {
                        _log.DebugFormat("{0} VoiceSignalingRequest: {1} candidates", LogHeader, candidatesArray.Count);
//                    }
                }
                else
                {
                    _log.Error($"{LogHeader} VoiceSignalingRequest: no 'candidate' or 'candidates'");
                }
            }
            if (resp is null)
            {
                _log.Error($"{LogHeader} VoiceSignalingRequest: no response so returning error");
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
                    "Show Janus server information in human-readable form (use 'janus info json' for raw JSON)",
                    HandleJanusInfo);
                MainConsole.Instance.Commands.AddCommand("Webrtc", false, "janus show",
                    "janus show",
                    "Alias for 'janus info'",
                    HandleJanusShow);
                MainConsole.Instance.Commands.AddCommand("Webrtc", false, "janus list rooms",
                    "janus list rooms",
                    "List the rooms on the Janus server",
                    HandleJanusListRooms);
                MainConsole.Instance.Commands.AddCommand("Webrtc", false, "janus list sessions",
                    "janus list sessions",
                    "List active Janus sessions (admin API)",
                    HandleJanusListSessions);
                MainConsole.Instance.Commands.AddCommand("Webrtc", false, "janus list",
                    "janus list",
                    "List Janus rooms and sessions (shortcut for diagnostics)",
                    HandleJanusList);
                MainConsole.Instance.Commands.AddCommand("Webrtc", false, "janus room",
                    "janus room <roomId>",
                    "Show one room with participant details",
                    HandleJanusRoom);

                // List rooms
                // List participants in a room
            }
        }

        private void HandleJanusList(string module, string[] cmdparms)
        {
            WriteOut("janus list: showing rooms then sessions");
            HandleJanusListRooms(module, cmdparms);
            HandleJanusListSessions(module, cmdparms);
        }

        private void HandleJanusInfo(string module, string[] cmdparms)
        {
            if (_ViewerSession is not null && _ViewerSession.Session is not null)
            {
                WriteOut("{0} Janus session: {1}", LogHeader, _ViewerSession.Session.SessionId);
                string infoURI = _ViewerSession.Session.JanusServerURI + "/info";

                JanusMessageResp resp = _ViewerSession.Session.GetFromJanus(infoURI).Result;
                if (resp is null)
                {
                    WriteOut("{0} Failed to query Janus /info", LogHeader);
                    return;
                }

                bool requestJson = cmdparms is not null
                                   && cmdparms.Length > 2
                                   && cmdparms[2].Equals("json", StringComparison.OrdinalIgnoreCase);
 
                resp = _ViewerSession.Session.GetFromJanus(infoURI).Result;

                if (requestJson)
                {
                     MainConsole.Instance.Output(resp.ToJson());
                    return;
                }

                OSDMap info = resp.RawBody;
                if (info is null || info.Count == 0)
                {
                    WriteOut("{0} Janus /info returned no data", LogHeader);
                    return;
                }

                PrintJanusInfo(info, "janus info json");
            }
        }

        private void HandleJanusShow(string module, string[] cmdparms)
        {
            if (_ViewerSession is not null && _ViewerSession.Session is not null)
            {
                WriteOut("{0} Janus session: {1}", LogHeader, _ViewerSession.Session.SessionId);
                string infoURI = _ViewerSession.Session.JanusServerURI + "/info";
                JanusMessageResp resp = _ViewerSession.Session.GetFromJanus(infoURI).Result;
                if (resp is null)
                {
                    WriteOut("{0} Failed to query Janus /info", LogHeader);
                    return;
                }

                OSDMap info = resp.RawBody;
                if (info is null || info.Count == 0)
                {
                    WriteOut("{0} Janus /info returned no data", LogHeader);
                    return;
                }

                PrintJanusInfo(info, "janus info json");
            }
        }

        private void PrintJanusInfo(OSDMap info, string jsonHintCommand)
        {
            WriteOut("");
            WriteOut("Janus Server Info");
            WriteOut("  Name            : {0}", GetMapString(info, "name"));
            WriteOut("  Server Name     : {0}", GetMapString(info, "server-name"));
            WriteOut("  Version         : {0} ({1})", GetMapString(info, "version_string"), GetMapString(info, "version"));
            WriteOut("  Author          : {0}", GetMapString(info, "author"));
            WriteOut("  Local IP        : {0}", GetMapString(info, "local-ip"));
            WriteOut("  New Sessions    : {0}", GetMapString(info, "accepting-new-sessions"));

            WriteOut("");
            WriteOut("Session / Timeouts");
            WriteOut("  session-timeout : {0}", GetMapString(info, "session-timeout"));
            WriteOut("  reclaim-timeout : {0}", GetMapString(info, "reclaim-session-timeout"));
            WriteOut("  candidates-time : {0}", GetMapString(info, "candidates-timeout"));

            WriteOut("");
            WriteOut("ICE / Network");
            WriteOut("  ice-lite        : {0}", GetMapString(info, "ice-lite"));
            WriteOut("  ice-tcp         : {0}", GetMapString(info, "ice-tcp"));
            WriteOut("  full-trickle    : {0}", GetMapString(info, "full-trickle"));
            WriteOut("  mdns-enabled    : {0}", GetMapString(info, "mdns-enabled"));
            WriteOut("  dtls-mtu        : {0}", GetMapString(info, "dtls-mtu"));

            WriteOut("");
            WriteOut("Security");
            WriteOut("  api_secret      : {0}", GetMapString(info, "api_secret"));
            WriteOut("  auth_token      : {0}", GetMapString(info, "auth_token"));

            WriteOut("");
            WriteOut("Transports");
            PrintNamedModuleMap(info, "transports");

            WriteOut("");
            WriteOut("Plugins");
            PrintNamedModuleMap(info, "plugins");

            WriteOut("");
            WriteOut("Tip: use '{0}' for full JSON output", jsonHintCommand);
        }

        private static string GetMapString(OSDMap map, string key)
        {
            if (map is not null && map.TryGetValue(key, out OSD value) && value is not null)
            {
                return value.AsString();
            }
            return "-";
        }

        private void PrintNamedModuleMap(OSDMap root, string key)
        {
            if (!root.TryGetValue(key, out OSD node) || node is not OSDMap entries || entries.Count == 0)
            {
                WriteOut("  (none)");
                return;
            }

            foreach (string entryKey in entries.Keys)
            {
                OSD entryValue = entries[entryKey];
                if (entryValue is OSDMap detail)
                {
                    string version = detail.TryGetValue("version_string", out OSD v) ? v.AsString() : "-";
                    string name = detail.TryGetValue("name", out OSD n) ? n.AsString() : entryKey;
                    WriteOut("  - {0} [{1}]", name, version);
                }
                else
                {
                    WriteOut("  - {0}", entryKey);
                }
            }
        }

        private void HandleJanusListRooms(string module, string[] cmdparms)
        {
            if (_ViewerSession is not null && _ViewerSession.Session is not null && _ViewerSession.AudioBridge is not null)
            {
                JanusAudioBridge ab = _ViewerSession.AudioBridge;
                AudioBridgeResp resp = ab.SendAudioBridgeMsg(new AudioBridgeListRoomsReq()).Result;
                if (resp is not null && resp.isSuccess)
                {
                    if (resp.PluginRespData.TryGetValue("list", out OSD list))
                    {
                        MainConsole.Instance.Output("");
                        MainConsole.Instance.Output(
                            "  {0,10} {1,15} {2,5} {3,10} {4,7} {5,7} {6}",
                            "Room", "Description", "Num", "SampleRate", "Spatial", "Recording", "MappedSession");
                        foreach (OSDMap room in list as OSDArray)
                        {
                            int roomid = room["room"].AsInteger();
                            MainConsole.Instance.Output(
                                "  {0,10} {1,15} {2,5} {3,10} {4,7} {5,7}",
                                roomid, room["description"], room["num_participants"],
                                room["sampling_rate"], room["spatial_audio"], room["record"]);

                            AudioBridgeResp participantResp = ab.SendAudioBridgeMsg(new AudioBridgeListParticipantsReq(roomid)).Result;

                            if (participantResp is not null && participantResp.AudioBridgeReturnCode == "participants")
                            {
                                if (participantResp.PluginRespData.TryGetValue("participants", out OSD participants))
                                {
                                    foreach (OSDMap participant in participants as OSDArray)
                                    {
                                        long participantId = participant.TryGetValue("id", out OSD participantIdNode)
                                                ? participantIdNode.AsLong()
                                                : 0L;
                                        string mapping = BuildParticipantMapping(participantId);
                                        MainConsole.Instance.Output("      {0}/{1},muted={2},talking={3},pos={4} {5}",
                                            participantId, participant["display"], participant["muted"],
                                            participant["talking"], participant["spatial_position"],
                                            string.IsNullOrEmpty(mapping) ? "mapped=<none>" : mapping.Substring(2));                                    }
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

        private async void HandleJanusListSessions(string module, string[] cmdparms)
        {
            if (_ViewerSession is null || _ViewerSession.Session is null)
                return;

            JanusMessageResp resp = await _ViewerSession.Session.SendToJanusAdmin(new JanusMessageReq("list_sessions")).ConfigureAwait(false);
            if (resp is null)
            {
                WriteOut("Failed to get sessions (no response)");
                return;
            }

            if (!resp.isSuccess)
            {
                if (resp.isError)
                {
                    ErrorResp err = new(resp);
                    WriteOut("Failed to get sessions: {0} ({1})", err.errorReason, err.errorCode);
                }
                else
                {
                    WriteOut("Failed to get sessions: {0}", resp.ReturnCode);
                }
                return;
            }

            OSD sessionsNode = null;
            if (!resp.RawBody.TryGetValue("sessions", out sessionsNode) && resp.dataSection is not null)
            {
                resp.dataSection.TryGetValue("sessions", out sessionsNode);
            }

            if (sessionsNode is not OSDArray sessions)
            {
                WriteOut("No sessions field in admin response");
                return;
            }

            WriteOut("Active Janus sessions: {0}", sessions.Count);
            foreach (OSD session in sessions)
            {
                string janusSessionId = session.AsLong().ToString();
                if (VoiceViewerSession.TryGetViewerSessionByVSSessionId(janusSessionId, out IVoiceViewerSession viewerSession))
                {
                    WriteOut("  - {0}  viewer_session={1} agent={2} scene={3}",
                            janusSessionId,
                            viewerSession.ViewerSessionID,
                            viewerSession.AgentId,
                            viewerSession.RegionId);
                }
                else
                {
                    WriteOut("  - {0}", janusSessionId);
                }
            }
        }

        private async void HandleJanusRoom(string module, string[] cmdparms)
        {
            if (_ViewerSession is null || _ViewerSession.Session is null || _ViewerSession.AudioBridge is null)
                return;

            if (cmdparms is null || cmdparms.Length < 3 || !int.TryParse(cmdparms[2], out int roomId))
            {
                WriteOut("Usage: janus room <roomId>");
                return;
            }

            JanusAudioBridge ab = _ViewerSession.AudioBridge;
            AudioBridgeResp roomsResp = await ab.SendAudioBridgeMsg(new AudioBridgeListRoomsReq()).ConfigureAwait(false);
            if (roomsResp is null || !roomsResp.isSuccess || roomsResp.PluginRespData is null)
            {
                WriteOut("Failed to get room list");
                return;
            }

            if (!roomsResp.PluginRespData.TryGetValue("list", out OSD listNode) || listNode is not OSDArray roomList)
            {
                WriteOut("No rooms available");
                return;
            }

            OSDMap foundRoom = null;
            foreach (OSDMap room in roomList)
            {
                if (room is not null && room.TryGetValue("room", out OSD roomOsd) && roomOsd.AsInteger() == roomId)
                {
                    foundRoom = room;
                    break;
                }
            }

            if (foundRoom is null)
            {
                WriteOut("Room {0} not found", roomId);
                return;
            }

            WriteOut("");
            WriteOut("Room {0}", roomId);
            WriteOut("  Description : {0}", GetMapString(foundRoom, "description"));
            WriteOut("  Participants: {0}", GetMapString(foundRoom, "num_participants"));
            WriteOut("  SampleRate  : {0}", GetMapString(foundRoom, "sampling_rate"));
            WriteOut("  Spatial     : {0}", GetMapString(foundRoom, "spatial_audio"));
            WriteOut("  Recording   : {0}", GetMapString(foundRoom, "record"));

            AudioBridgeResp participantResp = await ab.SendAudioBridgeMsg(new AudioBridgeListParticipantsReq(roomId)).ConfigureAwait(false);
            if (participantResp is null || participantResp.PluginRespData is null ||
                !participantResp.PluginRespData.TryGetValue("participants", out OSD participantsNode) ||
                participantsNode is not OSDArray participants)
            {
                WriteOut("  Participant list not available");
                return;
            }

            WriteOut("  Participant details:");
            if (participants.Count == 0)
            {
                WriteOut("    (none)");
                return;
            }

            foreach (OSDMap participant in participants)
            {
                long participantId = participant.TryGetValue("id", out OSD participantIdNode)
                        ? participantIdNode.AsLong()
                        : 0L;
                string mapping = BuildParticipantMapping(participantId);
                WriteOut("    - {0}/{1}, muted={2}, talking={3}, pos={4}{5}",
                    participantId,
                    GetMapString(participant, "display"),
                    GetMapString(participant, "muted"),
                    GetMapString(participant, "talking"),
                    GetMapString(participant, "spatial_position"),
                    mapping);
            }
        }

        private static string BuildParticipantMapping(long participantId)
        {
            if (participantId <= 0)
                return "";

            lock (VoiceViewerSession.ViewerSessions)
            {
                foreach (KeyValuePair<string, IVoiceViewerSession> entry in VoiceViewerSession.ViewerSessions)
                {
                    if (entry.Value is JanusViewerSession janusViewerSession && janusViewerSession.ParticipantId == participantId)
                    {
                        return string.Format(", viewer_session={0}, agent={1}, scene={2}",
                                entry.Key,
                                entry.Value.AgentId,
                                entry.Value.RegionId);
                    }
                }
            }

            return "";
        }

        private void WriteOut(string msg, params object[] args)
        {
            // m_log.InfoFormat(msg, args);
            MainConsole.Instance.Output(msg, args);
        }
    }
 }
