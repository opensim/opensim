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
using Mono.Addins;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using OpenSim.Framework;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Server.Base;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using Caps = OpenSim.Framework.Capabilities.Caps;
using OSDMap = OpenMetaverse.StructuredData.OSDMap;

[assembly: Addin("WebRtcVoiceRegionModule", "1.0")]
[assembly: AddinDependency("OpenSim.Region.Framework", OpenSim.VersionInfo.VersionNumber)]

namespace osWebRtcVoice
{
    /// <summary>
    /// This module provides the WebRTC voice interface for viewer clients..
    /// 
    /// In particular, it provides the following capabilities:
    ///      ProvisionVoiceAccountRequest, VoiceSignalingRequest and limited ChatSessionRequest
    /// which are the user interface to the voice service.
    /// 
    /// Initially, when the user connects to the region, the region feature "VoiceServiceType" is
    /// set to "webrtc" and the capabilities that support voice are enabled.
    /// The capabilities then pass the user request information to the IWebRtcVoiceService interface
    /// that has been registered for the reqion.
    /// </summary>
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "RegionVoiceModule")]
    public class WebRtcVoiceRegionModule : ISharedRegionModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private static readonly string LogHeader = "[REGION WEBRTC VOICE]";

        private static byte[] llsdUndefAnswerBytes = Util.UTF8.GetBytes("<llsd><undef /></llsd>");
        private bool _MessageDetails = false;

        // Control info
        private static bool m_Enabled = false;

        private IConfig m_Config;

        private IWebRtcVoiceService m_spatialVoiceService;
        private IWebRtcVoiceService m_nonSpatialVoiceService;

        private UUID gridHash = UUID.Zero;

        // ISharedRegionModule.Initialize
        public void Initialise(IConfigSource config)
        {
            m_Config = config.Configs["WebRtcVoice"];
            if (m_Config is not null)
            {
                m_Enabled = m_Config.GetBoolean("Enabled", false);
                if (m_Enabled)
                {
                    // Get the DLLs for the two voice services
                    // TODO: spacial/nonspacial names are wrong
                    // spacial here means service for region parcels, that can be spacial or not
                    // non spacial means for other uses like IMs, that just happen to be non spacial
                    // in fact this needs more consideration than just this 2 options

                    string spatialDllName = m_Config.GetString("SpatialVoiceService", string.Empty);
                    string nonSpatialDllName = m_Config.GetString("NonSpatialVoiceService", string.Empty);
                    if (string.IsNullOrEmpty(spatialDllName) && string.IsNullOrEmpty(nonSpatialDllName))
                    {
                        m_log.Error($"{LogHeader} No VoiceService specified in configuration");
                        m_Enabled = false;
                        return;
                    }

                    // Default non-spatial to spatial if not specified
                    if (string.IsNullOrEmpty(nonSpatialDllName))
                    {
                        m_log.Debug($"{LogHeader} nonSpatialDllName not specified. Defaulting to spatialDllName");
                        nonSpatialDllName = spatialDllName;
                    }

                    // Load the two voice services
                    m_log.Debug($"{LogHeader} Loading SpatialVoiceService from {spatialDllName}");
                    m_spatialVoiceService = ServerUtils.LoadPlugin<IWebRtcVoiceService>(spatialDllName, [config]);
                    if (m_spatialVoiceService is null)
                    {
                        m_log.Error($"{LogHeader} Could not load SpatialVoiceService from {spatialDllName}, module disabled");
                        m_Enabled = false;
                        return;
                    }

                    m_log.Debug($"{LogHeader} Loading NonSpatialVoiceService from {nonSpatialDllName}");
                    if (spatialDllName != nonSpatialDllName)
                    {
                        m_nonSpatialVoiceService = ServerUtils.LoadPlugin<IWebRtcVoiceService>(nonSpatialDllName, [ m_Config ]);
                        if (m_nonSpatialVoiceService is null)
                        {
                            m_log.Error($"{LogHeader} Could not load NonSpatialVoiceService from {nonSpatialDllName}");
                            m_Enabled = false;
                        }
                    }
                    else
                        m_nonSpatialVoiceService = m_spatialVoiceService;

                    if (m_Enabled)
                    {
                        _MessageDetails = m_Config.GetBoolean("MessageDetails", false);
                        m_log.Info($"{LogHeader} WebRtcVoiceService enabled");
                    }
                }
            }
        }

        // ISharedRegionModule.PostInitialize
        public void PostInitialise()
        {
        }

        // ISharedRegionModule.AddRegion
        public void AddRegion(Scene scene)
        {
            // TODO: register module to get parcels changes etc
        }

        // ISharedRegionModule.RemoveRegion
        public void RemoveRegion(Scene scene)
        {
        }

        // ISharedRegionModule.RegionLoaded
        public void RegionLoaded(Scene scene)
        {
            if (m_Enabled)
            {
                scene.EventManager.OnRegisterCaps += delegate (UUID agentID, Caps caps)
                {
                    OnRegisterCaps(scene, agentID, caps);
                };

                scene.EventManager.OnRemovePresence += delegate (UUID agentID)
                {
                    OnRemovePresence(scene, agentID);
                };

                scene.EventManager.OnNewClient += OnNewClient;

                if(gridHash.IsZero())
                {
                    if(!string.IsNullOrEmpty(scene.SceneGridInfo.GridUrl))
                        gridHash = Util.ComputeShake128UUID(scene.SceneGridInfo.GridUrl + scene.SceneGridInfo.GridName);
                    else if (!string.IsNullOrEmpty(scene.SceneGridInfo.HomeURL + scene.SceneGridInfo.GridName))
                        gridHash = Util.ComputeShake128UUID(scene.SceneGridInfo.HomeURLNoEndSlash);
                }

                ISimulatorFeaturesModule simFeatures = scene.RequestModuleInterface<ISimulatorFeaturesModule>();
                simFeatures?.AddFeature("VoiceServerType", OSD.FromString("webrtc"));
            }
        }

        // ISharedRegionModule.Close
        public void Close()
        {
        }

        // ISharedRegionModule.Name
        public string Name
        {
            get { return "RegionVoiceModule"; }
        }

        // ISharedRegionModule.ReplaceableInterface
        public Type ReplaceableInterface
        {
            get { return null; }
        }

        private void OnNewClient(IClientAPI client)
        {
            client.OnLogout += OnClientLogOut;
        }

        private void OnClientLogOut(IClientAPI client)
        {
            client.OnLogout -= OnClientLogOut;

            if(client.SceneAgent is not ScenePresence sp)
                return;

            List<IVoiceViewerSession> toremove = [];
            if (VoiceViewerSession.TryGetViewerSessionsByAgentAndRegion(sp.UUID, sp.Scene.ID, out IEnumerable<KeyValuePair<string, IVoiceViewerSession>> vSessions))
            {
                foreach(KeyValuePair<string, IVoiceViewerSession> v in vSessions)
                {
                    if((v.Value.Flags & IVoiceViewerSession.VFlags.IsParcel) != 0)
                        toremove.Add(v.Value);
                }

                foreach(IVoiceViewerSession v in toremove)
                    VoiceViewerSession.RemoveViewerSession(v.ViewerSessionID);
            }

            Util.FireAndForget( x =>
            {
                try
                {
                    OSDMap vreq = new()
                    {
                        { "logout" , true},
                        { "viewer_session" , UUID.Zero}
                    };

                    m_spatialVoiceService?.ProvisionVoiceAccountRequest(vreq , sp.UUID, sp.Scene.ID);
                    if(m_nonSpatialVoiceService != m_spatialVoiceService)
                        m_nonSpatialVoiceService?.ProvisionVoiceAccountRequest(vreq , sp.UUID, sp.Scene.ID);
                }
                catch (Exception ex)
                {
                    m_log.Debug($"{LogHeader} OnClientLogOut exception: {ex.Message}");
                }
            });
        }

        private static void OnRemovePresence(Scene pScene, UUID pAgentID)
        {
            List<IVoiceViewerSession> toremove = [];
            if (VoiceViewerSession.TryGetViewerSessionsByAgentAndRegion(pAgentID, pScene.RegionInfo.RegionID, out IEnumerable<KeyValuePair<string, IVoiceViewerSession>> vSessions))
            {
                foreach(KeyValuePair<string, IVoiceViewerSession> v in vSessions)
                {
                    if((v.Value.Flags & IVoiceViewerSession.VFlags.IsParcel) != 0)
                        toremove.Add(v.Value);
                }

                if(toremove.Count > 0)
                {
                    foreach(IVoiceViewerSession v in toremove)
                        VoiceViewerSession.RemoveViewerSession(v.ViewerSessionID);

                    Util.FireAndForget( x =>
                    {
                       List<IVoiceViewerSession> toremoveas = toremove;
                        foreach(IVoiceViewerSession v in toremoveas)
                        try
                        {
                            OSDMap vreq = new()
                            {
                                { "logout" , true},
                                { "viewer_session" , v.ViewerSessionID}
                            };
                            v.VoiceService.ProvisionVoiceAccountRequest(v, vreq , v.AgentId, v.RegionId);
                        }
                        catch (Exception ex)
                        {
                            m_log.Debug(
                                $"{LogHeader} OnRemovePresence: failed for viewer_session {v.ViewerSessionID}: {ex.Message}");
                        }
                    });
                }
            }
        }
 
        private static void CleanupDuplicateSessions(UUID pAgentID, UUID pSceneID, string pKeepViewerSessionId)
        {
            if(VoiceViewerSession.TryGetViewerSessionsByAgentAndRegion(pAgentID, pSceneID, out IEnumerable<KeyValuePair<string, IVoiceViewerSession>> candidates))
            {
                bool noskip = string.IsNullOrEmpty(pKeepViewerSessionId);
                List<IVoiceViewerSession> toremove = [];
                foreach (KeyValuePair<string, IVoiceViewerSession> candidate in candidates)
                {
                    if (noskip && candidate.Key == pKeepViewerSessionId)
                        continue;

                    m_log.Warn(
                        $"{LogHeader} CleanupDuplicateSessions: removing stale viewer_session {candidate.Key} for agent {pAgentID}, scene {pSceneID}");
                    toremove.Add(candidate.Value);
                }

                foreach(IVoiceViewerSession v in toremove)
                    VoiceViewerSession.RemoveViewerSession(v.ViewerSessionID);

                if(toremove.Count > 0)
                {
                    Util.FireAndForget( x =>
                    {
                        foreach(IVoiceViewerSession v in toremove)
                        {
                            try
                            {
                                OSDMap vreq = new()
                                {
                                    { "logout" , true},
                                    { "viewer_session" , v.ViewerSessionID}
                                };
                                v.VoiceService.ProvisionVoiceAccountRequest(v, vreq , v.AgentId, v.RegionId);
                            }
                            catch (Exception ex)
                            {
                                m_log.Debug(
                                    $"{LogHeader} CleanupDuplicateSessions: shutdown failed for viewer_session {v.ViewerSessionID}: {ex.Message}");
                            }
                        }
                    });
                }
            }
        }

        // <summary>
        // OnRegisterCaps is invoked via the scene.EventManager
        // everytime OpenSim hands out capabilities to a client
        // (login, region crossing). We contribute three capabilities to
        // the set of capabilities handed back to the client:
        // ProvisionVoiceAccountRequest, VoiceSignalingRequest and limited ChatSessionRequest
        //
        // ProvisionVoiceAccountRequest allows the client to obtain
        // voice communication information the the avater.
        //
        // VoiceSignalingRequest: Used for trickling ICE candidates.
        //
        // ChatSessionRequest
        //
        // Note that OnRegisterCaps is called here via a closure
        // delegate containing the scene of the respective region (see
        // Initialise()).
        // </summary>
        public void OnRegisterCaps(Scene scene, UUID agentID, Caps caps)
        {
            m_log.Debug(
                $"{LogHeader}: OnRegisterCaps called with agentID {agentID} in scene {scene.Name}");

            caps.RegisterSimpleHandler("ProvisionVoiceAccountRequest",
                    new SimpleStreamHandler("/" + UUID.Random(), (IOSHttpRequest httpRequest, IOSHttpResponse httpResponse) =>
                    {
                        ProvisionVoiceAccountRequest(httpRequest, httpResponse, agentID, scene);
                    }));

            caps.RegisterSimpleHandler("VoiceSignalingRequest",
                    new SimpleStreamHandler("/" + UUID.Random(), (IOSHttpRequest httpRequest, IOSHttpResponse httpResponse) =>
                    {
                        VoiceSignalingRequest(httpRequest, httpResponse, agentID, scene);
                    }));

            caps.RegisterSimpleHandler("ChatSessionRequest",
                    new SimpleStreamHandler("/" + UUID.Random(), (IOSHttpRequest httpRequest, IOSHttpResponse httpResponse) =>
                    {
                        ChatSessionRequest(httpRequest, httpResponse, agentID, scene);
                    }));
        }

        /// <summary>
        /// Callback for a client request for Voice Account Details
        /// </summary>
        /// <param name="scene">current scene object of the client</param>
        /// <param name="request"></param>
        /// <param name="path"></param>
        /// <param name="param"></param>
        /// <param name="agentID"></param>
        /// <param name="caps"></param>
        /// <returns></returns>
        public void ProvisionVoiceAccountRequest(IOSHttpRequest request, IOSHttpResponse response, UUID agentID, Scene scene)
        {
            if(request.HttpMethod != "POST")
            {
                m_log.Debug($"{LogHeader}[ProvisionVoice]: Not a POST request. Agent={agentID}");
                response.StatusCode = (int)HttpStatusCode.NotFound;
                return;
            }

            // Deserialize the request. Convert the LLSDXml to OSD for our use
            OSDMap map = BodyToMap(request, $"{LogHeader}[ProvisionVoice]");
            if (map is null)
            {
                m_log.Error($"{LogHeader}[ProvisionVoice]: No request data found. Agent={agentID}");
                response.StatusCode = (int)HttpStatusCode.NoContent;
                return;
            }

            // Make sure the request is for WebRtc voice
            if (map.TryGetValue("voice_server_type", out OSD vstosd))
            {
                if (vstosd is OSDString vst && !((string)vst).Equals("webrtc", StringComparison.OrdinalIgnoreCase))
                {
                    m_log.Warn($"{LogHeader}[ProvisionVoice]: voice_server_type is not 'webrtc'");
                    if (m_log.IsDebugEnabled)
                        m_log.Warn($"{LogHeader}[ProvisionVoice]: Request detail: {map}");

                    response.RawBuffer = llsdUndefAnswerBytes;
                    response.StatusCode = (int)HttpStatusCode.OK;
                    return;
                }
            }

            if (_MessageDetails) m_log.Debug($"{LogHeader}[ProvisionVoice]: request: {map}");

            IVoiceViewerSession vSession = null;

            if (map.TryGetString("viewer_session", out string viewerSessionId))
            {
                if(map.TryGetBool("logout", out bool islog) && islog)
                {
                    if(UUID.ZeroString.Equals(viewerSessionId, StringComparison.OrdinalIgnoreCase))
                    {
                        if (VoiceViewerSession.TryGetViewerSessionsByAgentId(agentID, out IEnumerable<KeyValuePair<string, IVoiceViewerSession>> vSessions))
                        {
                            m_log.Info(
                                $"{LogHeader} ProvisionVoiceAccountRequest: doing logout for {vSessions.Count()} stall sessions");

                            OSDMap vreq = new() {{ "logout" , true} };

                            foreach(KeyValuePair<string, IVoiceViewerSession> kvp in vSessions)
                            {
                                IVoiceViewerSession v = kvp.Value;
                                if(v is null)
                                    continue;
                                vreq["viewer_session"] = v.ViewerSessionID;
                                VoiceViewerSession.RemoveViewerSession(v.ViewerSessionID);
                                v.VoiceService.ProvisionVoiceAccountRequest(v, vreq , agentID, scene.RegionInfo.RegionID);
                            }
                        }

                        response.RawBuffer = OSDParser.SerializeLLSDXmlBytes(new OSDMap {{ "response", "closed" }});
                        response.StatusCode = (int)HttpStatusCode.OK;
                        return ;
                    }

                    OSDMap logoutresp = null;
                    if (VoiceViewerSession.TryGetViewerSession(viewerSessionId, out vSession))
                    {
                        VoiceViewerSession.RemoveViewerSession(viewerSessionId);
                        logoutresp = vSession.VoiceService.ProvisionVoiceAccountRequest(vSession, map, agentID, scene.RegionInfo.RegionID);
                    }
                    logoutresp ??= new OSDMap() {
                        { "response", "error" },
                        { "message", "Logout session not found" } };

                    response.RawBuffer = OSDParser.SerializeLLSDXmlBytes(logoutresp);
                    response.StatusCode = (int)HttpStatusCode.OK;
                    return ;
                }

                // request has a viewer session. Use that to find the voice service
                if (VoiceViewerSession.TryGetViewerSession(viewerSessionId, out vSession))
                {
                    CleanupDuplicateSessions(agentID, scene.RegionInfo.RegionID, viewerSessionId);
                }
            }
            else
            {
                //no session id.. new channel?
                if (map.TryGetString("channel_type", out string channelType))
                {
                    CleanupDuplicateSessions(agentID, scene.RegionInfo.RegionID, null);

                    if(!scene.TryGetScenePresence(agentID, out ScenePresence sp))
                    {
                        m_log.Debug($"{LogHeader}[ProvisionVoice]:avatar not found");
                        response.RawBuffer = llsdUndefAnswerBytes;
                        response.StatusCode = (int)HttpStatusCode.NotFound;
                        return;
                    }

                    IVoiceViewerSession.VFlags flags = IVoiceViewerSession.VFlags.None;

                    //do fully not trust viewers voice parcel requests
                    if (channelType == "local")
                    {
                        if (!scene.RegionInfo.EstateSettings.AllowVoice)
                        {
                            m_log.Debug($"{LogHeader}[ProvisionVoice]:region \"{scene.Name}\": voice not enabled in estate settings");
                            response.RawBuffer = llsdUndefAnswerBytes;
                            response.StatusCode = (int)HttpStatusCode.NotImplemented;
                            return;
                        }
                        if (scene.LandChannel == null)
                        {
                            m_log.Error($"{LogHeader}[ProvisionVoice] region \"{scene.Name}\" land data not yet available");
                            response.RawBuffer = llsdUndefAnswerBytes;
                            response.StatusCode = (int)HttpStatusCode.NotImplemented;
                            return;
                        }

                        if(map.TryGetInt("parcel_local_id", out int parcelID))
                        {
                            ILandObject parcel = scene.LandChannel.GetLandObject(parcelID);
                            if (parcel == null)
                            {
                                response.RawBuffer = llsdUndefAnswerBytes;
                                response.StatusCode = (int)HttpStatusCode.NotFound;
                                return;
                            }

                            LandData land = parcel.LandData;
                            if (land == null)
                            {
                                response.RawBuffer = llsdUndefAnswerBytes;
                                response.StatusCode = (int)HttpStatusCode.NotFound;
                                return;
                            }

                            if (!scene.RegionInfo.EstateSettings.TaxFree && (land.Flags & (uint)ParcelFlags.AllowVoiceChat) == 0)
                            {
                                m_log.Debug($"{LogHeader}[ProvisionVoice]:parcel voice not allowed");
                                response.RawBuffer = llsdUndefAnswerBytes;
                                response.StatusCode = (int)HttpStatusCode.Forbidden;
                                return;
                            }

                            if ((land.Flags & (uint)ParcelFlags.UseEstateVoiceChan) != 0)
                            {
                                // By removing the parcel_local_id, the voice service will treat this as an estate channel
                                //    request and return the appropriate voice credentials for the estate channel
                                //    instead of a parcel channel
                                map.Remove("parcel_local_id"); // estate channel
                                flags = IVoiceViewerSession.VFlags.IsEstate;
                            }
                            else
                            {
                                if(parcel.IsRestrictedFromLand(agentID) || parcel.IsBannedFromLand(agentID))
                                {
                                    // check Z distance?
                                    m_log.Debug($"{LogHeader}[ProvisionVoice]:agent not allowed on parcel");
                                    response.RawBuffer = llsdUndefAnswerBytes;
                                    response.StatusCode = (int)HttpStatusCode.Forbidden;
                                    return;
                                }
                                flags = parcel.OwnerID.Equals(agentID) ?
                                        IVoiceViewerSession.VFlags.IsAdmin | IVoiceViewerSession.VFlags.IsParcel :
                                         IVoiceViewerSession.VFlags.IsParcel;
                            }
                        }
                        else
                        {
                            flags = IVoiceViewerSession.VFlags.IsEstate;
                        }

                        // TODO: check if this userId is making a new session (case that user is reconnecting)
                        vSession = m_spatialVoiceService.CreateViewerSession(map, agentID, scene.RegionInfo.RegionID);
                        if(vSession != null)
                        {
                            if(sp.IsChildAgent)
                                flags |= IVoiceViewerSession.VFlags.IsChildAgent;
                            else if(scene.Permissions.IsEstateManager(agentID))
                                flags |= IVoiceViewerSession.VFlags.IsAdmin;
                            vSession.Flags = flags;
                            VoiceViewerSession.AddViewerSession(vSession);
                        }
                    }
                    else
                    {
                        if(sp.IsChildAgent)
                        {
                            // check Z distance?
                            m_log.Debug($"{LogHeader}[ProvisionVoice]:child agent request non local voice");
                            response.RawBuffer = llsdUndefAnswerBytes;
                            response.StatusCode = (int)HttpStatusCode.Forbidden;
                            return;
                        }

                        vSession = m_nonSpatialVoiceService.CreateViewerSession(map, agentID, scene.RegionInfo.RegionID);
                        if(vSession != null)
                        {
                            vSession.Flags = IVoiceViewerSession.VFlags.IsAdmin;
                            VoiceViewerSession.AddViewerSession(vSession);
                            map["gridhash"] = gridHash;
                        }
                    }
                }
            }

            OSDMap resp = null;
            if (vSession is not null)
            {
                resp = vSession.VoiceService.ProvisionVoiceAccountRequest(vSession, map, agentID, scene.RegionInfo.RegionID);
            }

            if (resp is null)
            {
                response.StatusCode = (int)HttpStatusCode.BadRequest;
                if (_MessageDetails) m_log.Debug($"{LogHeader}[ProvisionVoice]: got null response");
                return;
            }

            if (_MessageDetails) m_log.Debug($"{LogHeader}[ProvisionVoice]: response: {resp}");

            response.RawBuffer = OSDParser.SerializeLLSDXmlToBytes(resp);
            response.StatusCode = (int)HttpStatusCode.OK;
            return;
        }

        public void VoiceSignalingRequest(IOSHttpRequest request, IOSHttpResponse response, UUID agentID, Scene scene)
        {
            if(request.HttpMethod != "POST")
            {
                m_log.Error($"{LogHeader}[VoiceSignaling]: Not a POST request. Agent={agentID}");
                response.StatusCode = (int)HttpStatusCode.NotFound;
                return;
            }

            // Deserialize the request. Convert the LLSDXml to OSD for our use
            OSDMap map = BodyToMap(request, $"{LogHeader}[VoiceSignaling]");
            if (map is null)
            {
                m_log.Error($"{LogHeader}[VoiceSignalingRequest]: No request data found. Agent={agentID}");
                response.StatusCode = (int)HttpStatusCode.NoContent;
                return;
            }

            // Make sure the request is for WebRTC voice
            if (map.TryGetValue("voice_server_type", out OSD vstosd))
            {
                if (vstosd is OSDString vst && !((string)vst).Equals("webrtc", StringComparison.OrdinalIgnoreCase))
                {
                    response.RawBuffer = llsdUndefAnswerBytes;
                    response.StatusCode = (int)HttpStatusCode.OK;
                    return;
                }
            }

            OSDMap resp = null;
            if (map.TryGetString("viewer_session", out string viewerSessionId))
            {
                // request has a viewer session. Use that to find the voice service
                if (VoiceViewerSession.TryGetViewerSession(viewerSessionId, out IVoiceViewerSession vSession))
                {
                    resp = vSession.VoiceService.VoiceSignalingRequest(vSession, map, agentID, scene.RegionInfo.RegionID);
                }
                else
                {
                    m_log.Error($"{LogHeader} VoiceSignalingRequest: viewer session {viewerSessionId} not found");
                }
            }
            else
            {
                m_log.Error($"{LogHeader} VoiceSignalingRequest: no viewer_session in request");
            }

            if (_MessageDetails) m_log.Debug($"{LogHeader}[VoiceSignalingRequest]: Response: {resp ?? "null"}");

            // TODO: check for errors
            // viewers ignore response
            response.RawBuffer = llsdUndefAnswerBytes;
            response.StatusCode = (int)HttpStatusCode.OK;
            return;
        }

        /// <summary>
        /// Callback for a client request for ChatSessionRequest.
        /// The viewer sends this request when the user tries to start a P2P text or voice session
        /// with another user. We need to generate a new session ID and return it to the client.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="agentID"></param>
        /// <param name="scene"></param>
        public void ChatSessionRequest(IOSHttpRequest request, IOSHttpResponse response, UUID agentID, Scene scene)
        {
            m_log.Debug($"{LogHeader}: ChatSessionRequest received for agent {agentID} in scene {scene.Name}");
            if (request.HttpMethod != "POST")
            {
                response.StatusCode = (int)HttpStatusCode.NotFound;
                return;
            }

            if (!scene.TryGetScenePresence(agentID, out ScenePresence sp) || sp.IsDeleted)
            {
                m_log.Warn($"{LogHeader} ChatSessionRequest: scene presence not found or deleted for agent {agentID}");
                response.StatusCode = (int)HttpStatusCode.NotFound;
                return;
            }

            OSDMap reqmap = BodyToMap(request, $"{LogHeader}[ChatSessionRequest]");
            if (reqmap is null)
            {
                m_log.Warn($"{LogHeader} ChatSessionRequest: message body not parsable in request for agent {agentID}");
                response.StatusCode = (int)HttpStatusCode.NoContent;
                return;
            }

            m_log.Debug($"{LogHeader} ChatSessionRequest");

            if (!reqmap.TryGetString("method", out string method))
            {
                m_log.Warn($"{LogHeader} ChatSessionRequest: missing required 'method' field in request for agent {agentID}");
                response.StatusCode = (int)HttpStatusCode.NotFound;
                return;
            }

            if (!reqmap.TryGetUUID("session-id", out UUID sessionID))
            {
                m_log.Warn($"{LogHeader} ChatSessionRequest: missing required 'session-id' field in request for agent {agentID}");
                response.StatusCode = (int)HttpStatusCode.NotFound;
                return;
            }

            string servertype = null;
            if(reqmap.TryGetOSDMap("alt_params", out OSDMap altparams))
            {
                if(!altparams.TryGetString("voice_server_type", out servertype))
                    _ = altparams.TryGetString("preferred_voice_server_type", out servertype);
            }

            if(!string.IsNullOrEmpty(servertype))
            {
                if(!servertype.Equals("webrtc", StringComparison.OrdinalIgnoreCase))
                {
                    response.RawBuffer = llsdUndefAnswerBytes;
                    response.StatusCode = (int)HttpStatusCode.OK;
                    return;
                }
            }

            switch (method.ToLower())
            {
                // Several different method requests that we don't know how to handle.
                // Just return OK for now.
                case "decline p2p voice":
                case "decline invitation":
                case "start conference":
                case "fetch history":
                    response.StatusCode = (int)HttpStatusCode.OK;
                    break;
                // Asking to start a P2P voice session. We need to generate a new session ID and return
                //     it to the client in a ChatterBoxSessionStartReply event.
                case "start p2p voice":
                    UUID newSessionID;
                    if (reqmap.TryGetUUID("params", out UUID otherID))
                        newSessionID = new(otherID.ulonga ^ agentID.ulonga, otherID.ulongb ^ agentID.ulongb);
                    else
                        newSessionID = UUID.Random();

                    IEventQueue queue = scene.RequestModuleInterface<IEventQueue>();
                    if (queue is null)
                    {
                        m_log.Error($"{LogHeader}: no event queue for scene {scene.Name}");
                        response.StatusCode = (int)HttpStatusCode.InternalServerError;
                    }
                    else
                    {
                        queue.ChatterBoxSessionStartReply(
                                newSessionID,
                                sp.Name,
                                2,
                                false,
                                true,
                                sessionID,
                                true,
                                string.Empty,
                                agentID);

                        response.StatusCode = (int)HttpStatusCode.OK;
                    }
                    break;
                case "call":
                    m_log.Debug($"{LogHeader}: ChatSessionRequest call: {reqmap}");
                    response.StatusCode = (int)HttpStatusCode.BadRequest;
                    break;
                default:
                    response.StatusCode = (int)HttpStatusCode.BadRequest;
                    break;
            }
        }

        /// <summary>
        /// Convert the LLSDXml body of the request to an OSDMap for easier handling.
        /// Also logs the request if message details is enabled.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="pCaller"></param>
        /// <returns>'null' if the request body is empty or cannot be deserialized</returns>
        private OSDMap BodyToMap(IOSHttpRequest request, string pCaller)
        {
            try
            {
                if (request.InputStream.Length > 0)
                { 
                    using Stream inputStream = request.InputStream;
                    OSD tmp = OSDParser.DeserializeLLSDXml(inputStream);
                    if (_MessageDetails)
                        m_log.Debug($"{pCaller} BodyToMap: Request: {tmp}");
                    if(tmp is OSDMap map)
                        return map;
                }
            }
            catch
            {
                m_log.Debug($"{pCaller} BodyToMap: Fail to decode LLSDXml request");
            }
            return null;
        }
    }
}
