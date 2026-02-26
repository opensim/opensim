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
            m_Config = pConfig;
            IConfig moduleConfig = m_Config.Configs["WebRtcVoice"];

            if (moduleConfig is not null)
            {
                m_Enabled = moduleConfig.GetBoolean("Enabled", false);
                if (m_Enabled)
                {
                    // Get the DLLs for the two voice services
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
        // =====================================================================
        // IWebRtcVoiceService

        // IWebRtcVoiceService.ProvisionVoiceAccountRequest
        public OSDMap ProvisionVoiceAccountRequest(OSDMap pRequest, UUID pUserID, UUID pSceneID)
        {
            OSDMap response = null;
            IVoiceViewerSession vSession = null;
            if (pRequest.TryGetString("viewer_session", out string viewerSessionId))
            {
                // request has a viewer session. Use that to find the voice service
                if (!VoiceViewerSession.TryGetViewerSession(viewerSessionId, out vSession))
                {
                    m_log.Error($"{0} ProvisionVoiceAccountRequest: viewer session {viewerSessionId} not found");
                }
            }   
            else
            {
                // the request does not have a viewer session. See if it's an initial request
                if (pRequest.TryGetString("channel_type", out string channelType))
                {
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
                else
                {
                    m_log.Error($"{LogHeader} ProvisionVoiceAccountRequest: no channel_type in request");
                }
            }
            if (vSession is not null)
            {
                response = vSession.VoiceService.ProvisionVoiceAccountRequest(vSession, pRequest, pUserID, pSceneID);
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
                    m_log.ErrorFormat("{0} VoiceSignalingRequest: viewer session {1} not found", LogHeader, viewerSessionId);
                }
            }   
            else
            {
                m_log.ErrorFormat("{0} VoiceSignalingRequest: no viewer_session in request", LogHeader);
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
