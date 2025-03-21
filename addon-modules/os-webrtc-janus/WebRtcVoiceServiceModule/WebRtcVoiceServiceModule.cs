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
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;

using OpenSim.Framework;
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

namespace WebRtcVoice
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
                    string spatialDllName = moduleConfig.GetString("SpatialVoiceService", String.Empty);
                    string nonSpatialDllName = moduleConfig.GetString("NonSpatialVoiceService", String.Empty);
                    if (String.IsNullOrEmpty(spatialDllName) && String.IsNullOrEmpty(nonSpatialDllName))
                    {
                        m_log.ErrorFormat("{0} No SpatialVoiceService or NonSpatialVoiceService specified in configuration", LogHeader);
                        m_Enabled = false;
                    }

                    // Default non-spatial to spatial if not specified
                    if (String.IsNullOrEmpty(nonSpatialDllName))
                    {
                        m_log.DebugFormat("{0} nonSpatialDllName not specified. Defaulting to spatialDllName", LogHeader);
                        nonSpatialDllName = spatialDllName;
                    }

                    // Load the two voice services
                    m_log.DebugFormat("{0} Loading SpatialVoiceService from {1}", LogHeader, spatialDllName);
                    m_spatialVoiceService = ServerUtils.LoadPlugin<IWebRtcVoiceService>(spatialDllName, new object[] { m_Config });
                    if (m_spatialVoiceService is null)
                    {
                        m_log.ErrorFormat("{0} Could not load SpatialVoiceService from {1}", LogHeader, spatialDllName);
                        m_Enabled = false;
                    }

                    m_log.DebugFormat("{0} Loading NonSpatialVoiceService from {1}", LogHeader, nonSpatialDllName);
                    if (spatialDllName == nonSpatialDllName)
                    {
                        m_log.DebugFormat("{0} NonSpatialVoiceService is same as SpatialVoiceService", LogHeader);
                        m_nonSpatialVoiceService = m_spatialVoiceService;
                    }
                    else
                    {
                        m_nonSpatialVoiceService = ServerUtils.LoadPlugin<IWebRtcVoiceService>(nonSpatialDllName, new object[] { m_Config });
                        if (m_nonSpatialVoiceService is null)
                        {
                            m_log.ErrorFormat("{0} Could not load NonSpatialVoiceService from {1}", LogHeader, nonSpatialDllName);
                            m_Enabled = false;
                        }
                    }

                    if (m_Enabled)
                    {
                        m_log.InfoFormat("{0} WebRtcVoiceService enabled", LogHeader);
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
                m_log.DebugFormat("{0} Adding WebRtcVoiceService to region {1}", LogHeader, scene.Name);
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
        public async Task<OSDMap> ProvisionVoiceAccountRequest(OSDMap pRequest, UUID pUserID, UUID pSceneID)
        {
            OSDMap response = null;
            IVoiceViewerSession vSession = null;
            if (pRequest.ContainsKey("viewer_session"))
            {
                // request has a viewer session. Use that to find the voice service
                string viewerSessionId = pRequest["viewer_session"].AsString();
                if (!VoiceViewerSession.TryGetViewerSession(viewerSessionId, out vSession))
                {
                    m_log.ErrorFormat("{0} ProvisionVoiceAccountRequest: viewer session {1} not found", LogHeader, viewerSessionId);
                }
            }   
            else
            {
                // the request does not have a viewer session. See if it's an initial request
                if (pRequest.ContainsKey("channel_type"))
                {
                    string channelType = pRequest["channel_type"].AsString();
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
                    m_log.ErrorFormat("{0} ProvisionVoiceAccountRequest: no channel_type in request", LogHeader);
                }
            }
            if (vSession is not null)
            {
                response = await vSession.VoiceService.ProvisionVoiceAccountRequest(vSession, pRequest, pUserID, pSceneID);
            }
            return response;
        }

        // IWebRtcVoiceService.VoiceSignalingRequest
        public async Task<OSDMap> VoiceSignalingRequest(OSDMap pRequest, UUID pUserID, UUID pSceneID)
        {
            OSDMap response = null;
            IVoiceViewerSession vSession = null;
            if (pRequest.ContainsKey("viewer_session"))
            {
                // request has a viewer session. Use that to find the voice service
                string viewerSessionId = pRequest["viewer_session"].AsString();
                if (VoiceViewerSession.TryGetViewerSession(viewerSessionId, out vSession))
                {
                    response = await vSession.VoiceService.VoiceSignalingRequest(vSession, pRequest, pUserID, pSceneID);
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
        public Task<OSDMap> ProvisionVoiceAccountRequest(IVoiceViewerSession pVSession, OSDMap pRequest, UUID pUserID, UUID pSceneID)
        {
            throw new NotImplementedException();
        }

        // This module should never be called with this signature
        public Task<OSDMap> VoiceSignalingRequest(IVoiceViewerSession pVSession, OSDMap pRequest, UUID pUserID, UUID pSceneID)
        {
            throw new NotImplementedException();
        }

        public IVoiceViewerSession CreateViewerSession(OSDMap pRequest, UUID pUserID, UUID pSceneID)
        {
            throw new NotImplementedException();
        }
    }
}
