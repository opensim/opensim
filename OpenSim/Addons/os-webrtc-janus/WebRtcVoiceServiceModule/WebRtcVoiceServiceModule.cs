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

using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Server.Base;

using OpenMetaverse;
using OpenMetaverse.StructuredData;

using Mono.Addins;

using log4net;
using Nini.Config;

[assembly: Addin("WebRtcVoiceServiceModule", "1.0")]
[assembly: AddinDependency("OpenSim.Region.Framework", OpenSim.VersionInfo.VersionNumber)]

namespace osWebRtcVoice
{
    /// <summary>
    /// Interface for the WebRtcVoiceService.
    /// An instance of this is registered as the IWebRtcVoiceService for this region.
    /// The function here is to direct the capability requests to the appropriate voice service.
    /// For the moment, there are separate voice services for spatial and non-spatial voice
    /// with the idea that a region could have a pre-region spatial voice service while
    /// the grid could have a non-spatial voice service for group chat, etc.
    /// Fancier configurations are possible.
    /// </summary>
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "WebRtcVoiceServiceModule")]
    public class WebRtcVoiceServiceModule : ISharedRegionModule, IWebRtcVoiceService
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private static string LogHeader = "[WEBRTC VOICE SERVICE MODULE]";

        private static bool m_Enabled = false;
        private IConfigSource m_Config;

        private IWebRtcVoiceService m_spatialVoiceService;
        private IWebRtcVoiceService m_nonSpatialVoiceService;

        // =====================================================================

        // ISharedRegionModule.Initialize
        // Get configuration and load the modules that will handle spatial and non-spatial voice.
        public void Initialise(IConfigSource pConfig)
        {
//            WebRtcDebugControl.ApplyFromConfig(pConfig);

            m_Config = pConfig;
            IConfig moduleConfig = m_Config.Configs["WebRtcVoice"];

            if (moduleConfig is not null)
            {
                m_Enabled = moduleConfig.GetBoolean("Enabled", false);
                if (m_Enabled)
                {
                    // Get the DLLs for the two voice services
                    // TODO: spacial/nonspacial names are wrong
                    // spacial here means service for region parcels, that can be spacial or not
                    // non spacial means for other uses like IMs, that just happen to be non spacial
                    // in fact this needs more consideration than just this 2 options

                    string spatialDllName = moduleConfig.GetString("SpatialVoiceService", string.Empty);
                    string nonSpatialDllName = moduleConfig.GetString("NonSpatialVoiceService", string.Empty);
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
                    m_spatialVoiceService = ServerUtils.LoadPlugin<IWebRtcVoiceService>(spatialDllName, [m_Config]);
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
                            m_log.Error("{LogHeader} Could not load NonSpatialVoiceService from {nonSpatialDllName}");
                            m_Enabled = false;
                        }
                    }

                    if (m_Enabled)
                    {
                        m_log.Info($"{LogHeader} WebRtcVoiceService enabled");
                    }
                }
            }
        }

        // ISharedRegionModule.PostInitialize
        public void PostInitialise()
        {
        }

        // ISharedRegionModule.Close
        public void Close()
        {
        }

        // ISharedRegionModule.ReplaceableInterface
        public Type ReplaceableInterface
        {
            get { return null; }
        }

        // ISharedRegionModule.Name
        public string Name
        {
            get { return "WebRtcVoiceServiceModule"; }
        }

        // ISharedRegionModule.AddRegion
        public void AddRegion(Scene scene)
        {
            if (m_Enabled)
            {
                m_log.Debug($"{LogHeader} Adding WebRtcVoiceService to region {scene.Name}");
                scene.RegisterModuleInterface<IWebRtcVoiceService>(this);

                // TODO: figure out what events we care about
                // When new client (child or root) is added to scene, before OnClientLogin
                // scene.EventManager.OnNewClient         += Event_OnNewClient;
                // When client is added on login.
                // scene.EventManager.OnClientLogin       += Event_OnClientLogin;
                // New presence is added to scene. Child, root, and NPC. See Scene.AddNewAgent()
                // scene.EventManager.OnNewPresence       += Event_OnNewPresence;
                // scene.EventManager.OnRemovePresence    += Event_OnRemovePresence;
                // update to client position (either this or 'significant')
                // scene.EventManager.OnClientMovement    += Event_OnClientMovement;
                // "significant" update to client position
                // scene.EventManager.OnSignificantClientMovement += Event_OnSignificantClientMovement;
            }

        }

        // ISharedRegionModule.RemoveRegion
        public void RemoveRegion(Scene scene)
        {
            if (m_Enabled)
            {
                scene.UnregisterModuleInterface<IWebRtcVoiceService>(this);
            }
        }

        // ISharedRegionModule.RegionLoaded
        public void RegionLoaded(Scene scene)
        {
        }

        // =====================================================================
        // Thought about doing this but currently relying on the voice service
        //     event ("hangup") to remove the viewer session.
        private void Event_OnRemovePresence(UUID pAgentID)
        {
            // When a presence is removed, remove the viewer sessions for that agent
            IEnumerable<KeyValuePair<string, IVoiceViewerSession>> vSessions;
            if (VoiceViewerSession.TryGetViewerSessionByAgentId(pAgentID, out vSessions))
            {
                foreach(KeyValuePair<string, IVoiceViewerSession> v in vSessions)
                {
                    m_log.DebugFormat("{0} Event_OnRemovePresence: removing viewer session {1}", LogHeader, v.Key);
                    VoiceViewerSession.RemoveViewerSession(v.Key);
                    v.Value.Shutdown();
                }
            }
        }

        private static bool TryGetViewerSessionByAgentAndScene(UUID pAgentID, UUID pSceneID, out IVoiceViewerSession pViewerSession)
        {
            if (VoiceViewerSession.TryGetViewerSessionByAgentId(pAgentID, out IEnumerable<KeyValuePair<string, IVoiceViewerSession>> vSessions))
            {
                foreach (KeyValuePair<string, IVoiceViewerSession> v in vSessions)
                {
                    if (v.Value.RegionId == pSceneID)
                    {
                        pViewerSession = v.Value;
                        return true;
                    }
                }
            }
            pViewerSession = null;
            return false;
        }

        private static List<KeyValuePair<string, IVoiceViewerSession>> GetViewerSessionsByAgentAndScene(UUID pAgentID, UUID pSceneID)
        {
            List<KeyValuePair<string, IVoiceViewerSession>> matches = [];
            if (VoiceViewerSession.TryGetViewerSessionByAgentId(pAgentID, out IEnumerable<KeyValuePair<string, IVoiceViewerSession>> vSessions))
            {
                foreach (KeyValuePair<string, IVoiceViewerSession> v in vSessions)
                {
                    if (v.Value.RegionId == pSceneID)
                    {
                        matches.Add(v);
                    }
                }
            }
            return matches;
        }

        private static object TryGetPropertyValue(object pSource, string pPropertyName)
        {
            if (pSource is null || string.IsNullOrEmpty(pPropertyName))
                return null;

            PropertyInfo propertyInfo = pSource.GetType().GetProperty(pPropertyName);
            if (propertyInfo is null)
                return null;

            return propertyInfo.GetValue(pSource);
        }

        private static bool IsViewerSessionReusable(IVoiceViewerSession pViewerSession)
        {
            if (pViewerSession is null)
                return false;

            if (string.IsNullOrEmpty(pViewerSession.ViewerSessionID) || string.IsNullOrEmpty(pViewerSession.VoiceServiceSessionId))
                return false;

            object disconnectReason = TryGetPropertyValue(pViewerSession, "DisconnectReason");
            if (disconnectReason is string reason && !string.IsNullOrEmpty(reason))
                return false;

            object sessionObj = TryGetPropertyValue(pViewerSession, "Session");
            if (sessionObj is not null)
            {
                object isConnectedObj = TryGetPropertyValue(sessionObj, "IsConnected");
                if (isConnectedObj is bool isConnected && !isConnected)
                    return false;
            }

            return true;
        }

        private void CleanupDuplicateSessions(UUID pAgentID, UUID pSceneID, string pKeepViewerSessionId)
        {
            List<KeyValuePair<string, IVoiceViewerSession>> candidates = GetViewerSessionsByAgentAndScene(pAgentID, pSceneID);
            foreach (KeyValuePair<string, IVoiceViewerSession> candidate in candidates)
            {
                if (!string.IsNullOrEmpty(pKeepViewerSessionId) && candidate.Key == pKeepViewerSessionId)
                    continue;

                m_log.Warn(
                    $"{LogHeader} CleanupDuplicateSessions: removing stale viewer_session {candidate.Key} for agent {pAgentID}, scene {pSceneID}");

                VoiceViewerSession.RemoveViewerSession(candidate.Key);
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await candidate.Value.Shutdown();
                    }
                    catch (Exception ex)
                    {
                        m_log.Debug(
                            $"{LogHeader} CleanupDuplicateSessions: shutdown failed for viewer_session {candidate.Key}: {ex.Message}");
                    }
                });
            }
        }
        private bool TryGetReusableViewerSession(UUID pAgentID, UUID pSceneID, out IVoiceViewerSession pViewerSession)
        {
            List<KeyValuePair<string, IVoiceViewerSession>> sessions = GetViewerSessionsByAgentAndScene(pAgentID, pSceneID);
            foreach (KeyValuePair<string, IVoiceViewerSession> candidate in sessions)
            {
                if (IsViewerSessionReusable(candidate.Value))
                {
                    pViewerSession = candidate.Value;
                    CleanupDuplicateSessions(pAgentID, pSceneID, candidate.Key);
                    return true;
                }
            }

            if (sessions.Count > 0)
            {
                // No reusable session found: remove all stale sessions to force a clean create path.
                CleanupDuplicateSessions(pAgentID, pSceneID, null);
            }

            pViewerSession = null;
            return false;
        }

        // =====================================================================
        // IWebRtcVoiceService

        // IWebRtcVoiceService.ProvisionVoiceAccountRequest
        public OSDMap ProvisionVoiceAccountRequest(OSDMap pRequest, UUID pUserID, UUID pSceneID)
        {
            IVoiceViewerSession vSession = null;
            if (pRequest.TryGetString("viewer_session", out string viewerSessionId))
            {
                // request has a viewer session. Use that to find the voice service
                if (VoiceViewerSession.TryGetViewerSession(viewerSessionId, out vSession))
                {
                    CleanupDuplicateSessions(pUserID, pSceneID, viewerSessionId);
                }
                else
                {
                    if (TryGetReusableViewerSession(pUserID, pSceneID, out vSession))
                        m_log.Info(
                            $"{LogHeader} ProvisionVoiceAccountRequest: viewer session {viewerSessionId} not found, reconnect fallback reused {vSession.ViewerSessionID}");
                    else
                        m_log.Error($"{LogHeader} ProvisionVoiceAccountRequest: viewer session {viewerSessionId} not found");
                }
            }
            else
            {
                // the request does not have a viewer session. See if it's an initial request
                if (pRequest.TryGetString("channel_type", out string channelType))
                {
                    if (TryGetReusableViewerSession(pUserID, pSceneID, out vSession))
                    {
                        m_log.Info(
                            $"{LogHeader} ProvisionVoiceAccountRequest: reconnect reuse for agent {pUserID}, scene {pSceneID}, viewer_session {vSession.ViewerSessionID}");
                    }
                    else
                    {
                        // Ensure stale sessions are cleared before creating a new one.
                        CleanupDuplicateSessions(pUserID, pSceneID, null);
                        if (channelType == "local")
                        {
                            // TODO: check if this userId is making a new session (case that user is reconnecting)
                            vSession = m_spatialVoiceService.CreateViewerSession(pRequest, pUserID, pSceneID);
                            VoiceViewerSession.AddViewerSession(vSession);
                        }
                        else
                        {
                            // TODO: check if this userId is making a new session (case that user is reconnecting)
                            vSession = m_nonSpatialVoiceService.CreateViewerSession(pRequest, pUserID, pSceneID);
                            VoiceViewerSession.AddViewerSession(vSession);
                        }
                    }
                }
                else
                {
                    if (TryGetReusableViewerSession(pUserID, pSceneID, out vSession))
                    {
                        m_log.Info(
                            $"{LogHeader} ProvisionVoiceAccountRequest: missing channel_type, reused existing session for agent {pUserID}, scene {pSceneID}, viewer_session {vSession.ViewerSessionID}");
                    }
                    else
                    {
                        m_log.Error(
                            $"{LogHeader} ProvisionVoiceAccountRequest: no channel_type in request and no existing session for agent {pUserID}, scene {pSceneID}");
                    }
                }
            }

            OSDMap response = null;

            if (vSession is not null)
            {
                response = vSession.VoiceService.ProvisionVoiceAccountRequest(vSession, pRequest, pUserID, pSceneID);
            }

            if (response is null)
            {
                return new OSDMap
                {
                    { "response", "error" },
                    { "message", "Unable to provision voice session (missing viewer_session/channel_type or session not found)" }
                };
             }

            return response;
        }

        // IWebRtcVoiceService.VoiceSignalingRequest
        public OSDMap VoiceSignalingRequest(OSDMap pRequest, UUID pUserID, UUID pSceneID)
        {
            OSDMap response = null;
            IVoiceViewerSession vSession = null;
            if (pRequest.TryGetString("viewer_session", out string viewerSessionId))
            {
                // request has a viewer session. Use that to find the voice service
                if (VoiceViewerSession.TryGetViewerSession(viewerSessionId, out vSession))
                {
                    response = vSession.VoiceService.VoiceSignalingRequest(vSession, pRequest, pUserID, pSceneID);
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
            return response;
        }

        // This module should never be called with this signature
        public OSDMap ProvisionVoiceAccountRequest(IVoiceViewerSession pVSession, OSDMap pRequest, UUID pUserID, UUID pSceneID)
        {
            throw new NotImplementedException();
        }

        // This module should never be called with this signature
        public OSDMap VoiceSignalingRequest(IVoiceViewerSession pVSession, OSDMap pRequest, UUID pUserID, UUID pSceneID)
        {
            throw new NotImplementedException();
        }

        public IVoiceViewerSession CreateViewerSession(OSDMap pRequest, UUID pUserID, UUID pSceneID)
        {
            throw new NotImplementedException();
        }
    }
}
