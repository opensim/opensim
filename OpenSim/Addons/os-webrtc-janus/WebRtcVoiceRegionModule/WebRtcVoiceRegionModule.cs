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
using System.IO;
using System.Net;
using System.Reflection;

using Mono.Addins;

using OpenSim.Framework;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using Caps = OpenSim.Framework.Capabilities.Caps;

using OpenMetaverse;
using OpenMetaverse.StructuredData;
using OSDMap = OpenMetaverse.StructuredData.OSDMap;

using log4net;
using Nini.Config;

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
        private static readonly string logHeader = "[REGION WEBRTC VOICE]";

        private static byte[] llsdUndefAnswerBytes = Util.UTF8.GetBytes("<llsd><undef /></llsd>"); 
        private bool _MessageDetails = false;

        // Control info
        private static bool m_Enabled = false;

        private IConfig m_Config;

        // ISharedRegionModule.Initialize
        public void Initialise(IConfigSource config)
        {
            m_Config = config.Configs["WebRtcVoice"];
            if (m_Config is not null)
            {
                m_Enabled = m_Config.GetBoolean("Enabled", false);
                if (m_Enabled)
                {
                    _MessageDetails = m_Config.GetBoolean("MessageDetails", false);

                    m_log.Info($"{logHeader}: enabled");
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
            // todo register module to get parcels changes etc
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
                $"{logHeader}: OnRegisterCaps called with agentID {agentID} caps {caps} in scene {scene.Name}");

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
            // Get the voice service. If it doesn't exist, return an error.
            IWebRtcVoiceService voiceService = scene.RequestModuleInterface<IWebRtcVoiceService>();
            if (voiceService is null)
            {
                m_log.Error($"{logHeader}[ProvisionVoice]: voice service not loaded");
                response.StatusCode = (int)HttpStatusCode.NotFound;
                return;
            }

            if(request.HttpMethod != "POST")
            {
                m_log.Debug($"[{logHeader}][ProvisionVoice]: Not a POST request. Agent={agentID}");
                response.StatusCode = (int)HttpStatusCode.NotFound;
                return;
            }

            // Deserialize the request. Convert the LLSDXml to OSD for our use
            OSDMap map = BodyToMap(request, "ProvisionVoiceAccountRequest");
            if (map is null)
            {
                m_log.Error($"{logHeader}[ProvisionVoice]: No request data found. Agent={agentID}");
                response.StatusCode = (int)HttpStatusCode.NoContent;
                return;
            }

            // Make sure the request is for WebRtc voice
            if (map.TryGetValue("voice_server_type", out OSD vstosd))
            {
                if (vstosd is OSDString vst && !((string)vst).Equals("webrtc", StringComparison.OrdinalIgnoreCase))
                {
                    m_log.Warn($"{logHeader}[ProvisionVoice]: voice_server_type is not 'webrtc'. Request: {map}");
                    response.RawBuffer = llsdUndefAnswerBytes;
                    response.StatusCode = (int)HttpStatusCode.OK;
                    return;
                }
            }

            if (_MessageDetails) m_log.Debug($"{logHeader}[ProvisionVoice]: request: {map}");

            if (map.TryGetString("channel_type", out string channelType))
            {
                //do fully not trust viewers voice parcel requests
                if (channelType == "local")
                {
                    if (!scene.RegionInfo.EstateSettings.AllowVoice)
                    {
                        m_log.Debug($"{logHeader}[ProvisionVoice]:region \"{scene.Name}\": voice not enabled in estate settings");
                        response.RawBuffer = llsdUndefAnswerBytes;
                        response.StatusCode = (int)HttpStatusCode.NotImplemented;
                        return;
                    }
                    if (scene.LandChannel == null)
                    {
                        m_log.Error($"{logHeader}[ProvisionVoice] region \"{scene.Name}\" land data not yet available");
                        response.RawBuffer = llsdUndefAnswerBytes;
                        response.StatusCode = (int)HttpStatusCode.NotImplemented;
                        return;
                    }

                    if(!scene.TryGetScenePresence(agentID, out ScenePresence sp))
                    {
                        m_log.Debug($"{logHeader}[ProvisionVoice]:avatar not found");
                        response.RawBuffer = llsdUndefAnswerBytes;
                        response.StatusCode = (int)HttpStatusCode.NotFound;
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
                            m_log.Debug($"{logHeader}[ProvisionVoice]:parcel voice not allowed");
                            response.RawBuffer = llsdUndefAnswerBytes;
                            response.StatusCode = (int)HttpStatusCode.Forbidden;
                            return;
                        }

                        if ((land.Flags & (uint)ParcelFlags.UseEstateVoiceChan) != 0)
                        {
                            map.Remove("parcel_local_id"); // estate channel
                        }
                        else if(parcel.IsRestrictedFromLand(agentID) || parcel.IsBannedFromLand(agentID))
                        {
                            // check Z distance?
                            m_log.Debug($"{logHeader}[ProvisionVoice]:agent not allowed on parcel");
                            response.RawBuffer = llsdUndefAnswerBytes;
                            response.StatusCode = (int)HttpStatusCode.Forbidden;
                            return;
                        }
                    }
                }
            }

            // The checks passed. Send the request to the voice service.
            OSDMap resp = voiceService.ProvisionVoiceAccountRequest(map, agentID, scene.RegionInfo.RegionID);

            if(resp is not null)
            {
                if (_MessageDetails) m_log.Debug($"{logHeader}[ProvisionVoice]: response: {resp}");

                // TODO: check for errors and package the response

                // Convert the OSD to LLSDXml for the response
                string xmlResp = OSDParser.SerializeLLSDXmlString(resp);
                response.RawBuffer = Util.UTF8.GetBytes(xmlResp);
                response.StatusCode = (int)HttpStatusCode.OK;
            }
            else
            {
                m_log.Debug($"{logHeader}[ProvisionVoice]: got null response");
                response.StatusCode = (int)HttpStatusCode.OK;
            }
            return;
        }

        public void VoiceSignalingRequest(IOSHttpRequest request, IOSHttpResponse response, UUID agentID, Scene scene)
        {
            IWebRtcVoiceService voiceService = scene.RequestModuleInterface<IWebRtcVoiceService>();
            if (voiceService is null)
            {
                m_log.Error($"{logHeader}[VoiceSignalingRequest]: avatar \"{agentID}\": no voice service");
                response.StatusCode = (int)HttpStatusCode.NotFound;
                return;
            }

            if(request.HttpMethod != "POST")
            {
                m_log.Error($"[{logHeader}][VoiceSignaling]: Not a POST request. Agent={agentID}");
                response.StatusCode = (int)HttpStatusCode.NotFound;
                return;
            }

            // Deserialize the request. Convert the LLSDXml to OSD for our use
            OSDMap map = BodyToMap(request, "VoiceSignalingRequest");
            if (map is null)
            {
                m_log.Error($"{logHeader}[VoiceSignalingRequest]: No request data found. Agent={agentID}");
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

            OSDMap resp = voiceService.VoiceSignalingRequest(map, agentID, scene.RegionInfo.RegionID);

            if (_MessageDetails) m_log.Debug($"{logHeader}[VoiceSignalingRequest]: Response: {resp}");

            // TODO: check for errors and package the response

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
            m_log.Debug($"{logHeader}: ChatSessionRequest received for agent {agentID} in scene {scene.Name}");
            if (request.HttpMethod != "POST")
            {
                response.StatusCode = (int)HttpStatusCode.NotFound;
                return;
            }

            if (!scene.TryGetScenePresence(agentID, out ScenePresence sp) || sp.IsDeleted)
            {
                m_log.Warn($"{logHeader} ChatSessionRequest: scene presence not found or deleted for agent {agentID}");
                response.StatusCode = (int)HttpStatusCode.NotFound;
                return;
            }

            OSDMap reqmap = BodyToMap(request, "[ChatSessionRequest]");
            if (reqmap is null)
            {
                m_log.Warn($"{logHeader} ChatSessionRequest: message body not parsable in request for agent {agentID}");
                response.StatusCode = (int)HttpStatusCode.NoContent;
                return;
            }

            m_log.Debug($"{logHeader} ChatSessionRequest");

            if (!reqmap.TryGetString("method", out string method))
            {
                m_log.Warn($"{logHeader} ChatSessionRequest: missing required 'method' field in request for agent {agentID}");
                response.StatusCode = (int)HttpStatusCode.NotFound;
                return;
            }

            if (!reqmap.TryGetUUID("session-id", out UUID sessionID))
            {
                m_log.Warn($"{logHeader} ChatSessionRequest: missing required 'session-id' field in request for agent {agentID}");
                response.StatusCode = (int)HttpStatusCode.NotFound;
                return;
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
                        m_log.Error($"{logHeader}: no event queue for scene {scene.Name}");
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
                using Stream inputStream = request.InputStream;
                if (inputStream.Length > 0)
                {
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
