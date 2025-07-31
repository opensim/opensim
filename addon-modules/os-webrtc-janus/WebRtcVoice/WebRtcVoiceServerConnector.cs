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
using System.Net;
using System.Reflection;

using OpenSim.Framework;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Services.Interfaces;
using OpenSim.Server.Base;
using OpenSim.Server.Handlers.Base;
using System.Threading.Tasks;

using OpenMetaverse;
using OpenMetaverse.StructuredData;

using log4net;
using Nini.Config;

namespace WebRtcVoice
{
    // Class that provides the network interface to the WebRTC voice server.
    // This is used by the Robust server to receive requests from the region servers
    //     and do the voice stuff on the WebRTC service (see WebRtcVoiceServiceConnector).
    public class WebRtcVoiceServerConnector : IServiceConnector
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private static readonly string LogHeader = "[WEBRTC VOICE SERVER CONNECTOR]";

        private bool m_Enabled = false;
        private bool m_MessageDetails = false;
        private IWebRtcVoiceService m_WebRtcVoiceService;

        public WebRtcVoiceServerConnector(IConfigSource pConfig, IHttpServer pServer, string pConfigName)
        {
            IConfig moduleConfig = pConfig.Configs["WebRtcVoice"];

            if (moduleConfig is not null)
            {
                m_Enabled = moduleConfig.GetBoolean("Enabled", false);
                if (m_Enabled)
                {
                    m_log.InfoFormat("{0} WebRtcVoiceServerConnector enabled", LogHeader);
                    m_MessageDetails = moduleConfig.GetBoolean("MessageDetails", false);

                    // This creates the local service that handles the requests.
                    // The local service provides  the IWebRtcVoiceService interface and directs the requests
                    //   to the WebRTC service.
                    string localServiceModule = moduleConfig.GetString("LocalServiceModule", "WebRtcVoiceServiceModule.dll:WebRtcVoiceServiceModule");
                    m_log.DebugFormat("{0} loading {1}", LogHeader, localServiceModule);

                    object[] args = new object[0];
                    m_WebRtcVoiceService = ServerUtils.LoadPlugin<IWebRtcVoiceService>(localServiceModule, args); 

                    // The WebRtcVoiceServiceModule is both an IWebRtcVoiceService and a ISharedRegionModule
                    //     so we can initialize it as if it was the region module.
                    ISharedRegionModule sharedModule = m_WebRtcVoiceService as ISharedRegionModule;
                    if (sharedModule is null)
                    {
                        m_log.ErrorFormat("{0} local service module does not implement ISharedRegionModule", LogHeader);
                        m_Enabled = false;
                        return;
                    }
                    sharedModule.Initialise(pConfig);

                    // Now that we have someone to handle the requests, we can set up the handlers
                    pServer.AddJsonRPCHandler("provision_voice_account_request", Handle_ProvisionVoiceAccountRequest);
                    pServer.AddJsonRPCHandler("voice_signaling_request", Handle_VoiceSignalingRequest);
                }
            }
        }

        private bool Handle_ProvisionVoiceAccountRequest(OSDMap pJson, ref JsonRpcResponse pResponse)
        {
            bool ret = false;
            m_log.DebugFormat("{0} Handle_ProvisionVoiceAccountRequest", LogHeader);
            if (m_MessageDetails) m_log.DebugFormat("{0} PVAR: req={1}", LogHeader, pJson.ToString());

            if (pJson.ContainsKey("params") && pJson["params"] is OSDMap paramsMap)
            {
                OSDMap request = paramsMap.ContainsKey("request") ? paramsMap["request"] as OSDMap : null;
                UUID userID = paramsMap.ContainsKey("userID") ? paramsMap["userID"].AsUUID() : UUID.Zero;
                UUID sceneID = paramsMap.ContainsKey("scene") ? paramsMap["scene"].AsUUID() : UUID.Zero;

                try
                {
                    if (m_WebRtcVoiceService is null)
                    {
                        m_log.ErrorFormat("{0} PVAR: no local service", LogHeader);
                        return false;
                    }
                    OSDMap resp = m_WebRtcVoiceService.ProvisionVoiceAccountRequest(request, userID, sceneID).Result;

                    pResponse = new JsonRpcResponse();
                    pResponse.Result = resp;
                    if (m_MessageDetails) m_log.DebugFormat("{0} PVAR: resp={1}", LogHeader, resp.ToString());
                    ret = true;
                }
                catch (Exception e)
                {
                    m_log.ErrorFormat("{0} PVAR: exception {1}", LogHeader, e);
                }   
            }
            else
            {
                m_log.ErrorFormat("{0} PVAR: missing parameters", LogHeader);
            }
            return ret;
        }

        private bool Handle_VoiceSignalingRequest(OSDMap pJson, ref JsonRpcResponse pResponse)
        {
            bool ret = false;
            if (pJson.ContainsKey("params") && pJson["params"] is OSDMap paramsMap)
            {
                m_log.DebugFormat("{0} Handle_VoiceSignalingRequest", LogHeader);
                if (m_MessageDetails) m_log.DebugFormat("{0} VSR: req={1}", LogHeader, paramsMap.ToString());

                OSDMap request = paramsMap.ContainsKey("request") ? paramsMap["request"] as OSDMap : null;
                UUID userID = paramsMap.ContainsKey("userID") ? paramsMap["userID"].AsUUID() : UUID.Zero;
                UUID sceneID = paramsMap.ContainsKey("scene") ? paramsMap["scene"].AsUUID() : UUID.Zero;

                try
                {
                    OSDMap resp = m_WebRtcVoiceService.VoiceSignalingRequest(request, userID, sceneID).Result;

                    pResponse = new JsonRpcResponse();
                    pResponse.Result = resp;
                    if (m_MessageDetails) m_log.DebugFormat("{0} VSR: resp={1}", LogHeader, resp.ToString());

                    ret = true;
                }
                catch (Exception e)
                {
                    m_log.ErrorFormat("{0} VSR: exception {1}", LogHeader, e);
                }
            }
            else
            {
                m_log.ErrorFormat("{0} VSR: missing parameters", LogHeader);
            }

            return ret;
        }
    }
}
