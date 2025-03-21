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

using OpenMetaverse;
using OpenMetaverse.StructuredData;

using log4net;
using Nini.Config;
using OSHttpServer;

namespace WebRtcVoice
{
    // Class that provides the local IWebRtcVoiceService interface to the JsonRPC Robust
    //     server. This is used by the region servers to talk to the Robust server.
    public class WebRtcVoiceServiceConnector : IWebRtcVoiceService
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private static readonly string LogHeader = "[WEBRTC VOICE SERVICE CONNECTOR]";
        private bool m_Enabled = false;
        private bool m_MessageDetails = false;
        private IConfigSource m_Config;

        string m_serverURI = "http://localhost:8080";

        public WebRtcVoiceServiceConnector(IConfigSource pConfig)
        {
            m_Config = pConfig;
            IConfig moduleConfig = m_Config.Configs["WebRtcVoice"];

            if (moduleConfig is not null)
            {
                m_Enabled = moduleConfig.GetBoolean("Enabled", false);
                if (m_Enabled)
                {
                    m_serverURI = moduleConfig.GetString("WebRtcVoiceServerURI", string.Empty);
                    if (string.IsNullOrWhiteSpace(m_serverURI))
                    {
                        m_log.ErrorFormat("{0} WebRtcVoiceServiceConnector enabled but no WebRtcVoiceServerURI specified", LogHeader);
                        m_Enabled = false;
                    }
                    else
                    {
                        m_log.InfoFormat("{0} WebRtcVoiceServiceConnector enabled", LogHeader);
                    }

                    m_MessageDetails = moduleConfig.GetBoolean("MessageDetails", false);
                }
            }

        }

        // Create a local viewer session. This gets a local viewer session ID that is
        //    later changed when the ProvisionVoiceAccountRequest response is returned
        //    so that the viewer session ID is the same here as from the WebRTC service.
        public IVoiceViewerSession CreateViewerSession(OSDMap pRequest, UUID pUserID, UUID pSceneID)
        {
            m_log.DebugFormat("{0} CreateViewerSession", LogHeader);
            return new VoiceViewerSession(this, pUserID, pSceneID);   
        }

        public Task<OSDMap> ProvisionVoiceAccountRequest(OSDMap pRequest, UUID pUserID, UUID pSceneID)
        {
            m_log.DebugFormat("{0} ProvisionVoiceAccountRequest without ViewerSession. uID={1}, sID={2}", LogHeader, pUserID, pSceneID);
            return null;
        }

        // Received a ProvisionVoiceAccountRequest from a viewer. Forward it to the WebRTC service.
        public async Task<OSDMap> ProvisionVoiceAccountRequest(IVoiceViewerSession pVSession, OSDMap pRequest, UUID pUserID, UUID pSceneID)
        {
            m_log.DebugFormat("{0} VoiceSignalingRequest. uID={1}, sID={2}", LogHeader, pUserID, pSceneID);
            OSDMap req = new OSDMap()
            {
                { "request", pRequest },
                { "userID", pUserID.ToString() },
                { "scene", pSceneID.ToString() }
            };
            var resp = await JsonRpcRequest("provision_voice_account_request", m_serverURI, req);

            // Kludge to sync the viewer session number in our IVoiceViewerSession with the one from the WebRTC service.
            if (resp.ContainsKey("viewer_session"))
            {
                string otherViewerSessionId = resp["viewer_session"].AsString();
                m_log.DebugFormat("{0} ProvisionVoiceAccountRequest: syncing viewSessionID. old={1}, new={2}",
                                LogHeader, pVSession.ViewerSessionID, otherViewerSessionId);
                VoiceViewerSession.UpdateViewerSessionId(pVSession, otherViewerSessionId);
            }

            return resp;
        }

        public Task<OSDMap> VoiceSignalingRequest(OSDMap pRequest, UUID pUserID, UUID pSceneID)
        {
            m_log.DebugFormat("{0} VoiceSignalingRequest without ViewerSession. uID={1}, sID={2}", LogHeader, pUserID, pSceneID);
            return null;
        }

        public Task<OSDMap> VoiceSignalingRequest(IVoiceViewerSession pVSession, OSDMap pRequest, UUID pUserID, UUID pSceneID)
        {
            m_log.DebugFormat("{0} VoiceSignalingRequest. uID={1}, sID={2}", LogHeader, pUserID, pSceneID);
            OSDMap req = new OSDMap()
            {
                { "request", pRequest },
                { "userID", pUserID.ToString() },
                { "scene", pSceneID.ToString() }
            };
            return JsonRpcRequest("voice_signaling_request", m_serverURI, req);
        }

        public Task<OSDMap> JsonRpcRequest(string method, string uri, OSDMap pParams)
        {
            string jsonId = UUID.Random().ToString();

            if(string.IsNullOrWhiteSpace(uri))
                return null;

            TaskCompletionSource<OSDMap> tcs = new TaskCompletionSource<OSDMap>();
            _ = Task.Run(() =>
            {
                OSDMap request = new()
                {
                    { "jsonrpc", OSD.FromString("2.0") },
                    { "id", OSD.FromString(jsonId) },
                    { "method", OSD.FromString(method) },
                    { "params", pParams }
                };

                OSDMap outerResponse = null;
                try
                {
                    if (m_MessageDetails) m_log.DebugFormat("{0}: request: {1}", LogHeader, request);
                    outerResponse = WebUtil.PostToService(uri, request, 10000, true);
                    if (m_MessageDetails) m_log.DebugFormat("{0}: response: {1}", LogHeader, outerResponse);
                }
                catch (Exception e)
                {
                    m_log.ErrorFormat("{0}: JsonRpc request '{1}' to {2} failed: {3}", LogHeader, method, uri, e);
                    m_log.DebugFormat("{0}: request: {1}", LogHeader, request);
                    tcs.SetResult(new OSDMap()
                    {
                        { "error", OSD.FromString(e.Message) }
                    });
                }

                OSD osdtmp;
                if (!outerResponse.TryGetValue("_Result", out osdtmp) || (osdtmp is not OSDMap))
                {
                    string errm = String.Format("JsonRpc request '{0}' to {1} returned an invalid response: {2}",
                        method, uri, OSDParser.SerializeJsonString(outerResponse));
                    m_log.ErrorFormat(errm);
                    tcs.SetResult(new OSDMap()
                    {
                        { "error", errm }
                    });
                }

                OSDMap response = osdtmp as OSDMap;
                if (response.TryGetValue("error", out osdtmp))
                {
                    string errm = String.Format("JsonRpc request '{0}' to {1} returned an error: {2}",
                        method, uri, OSDParser.SerializeJsonString(osdtmp));
                    m_log.ErrorFormat(errm);
                    tcs.SetResult(new OSDMap()
                    {
                        { "error", errm }
                    });
                }

                OSDMap resultmap = null;
                if (!response.TryGetValue("result", out osdtmp) || (osdtmp is not OSDMap))
                {
                    string errm = String.Format("JsonRpc request '{0}' to {1} returned result as non-OSDMap: {2}",
                        method, uri, OSDParser.SerializeJsonString(outerResponse));
                    m_log.ErrorFormat(errm);
                    tcs.SetResult(new OSDMap()
                    {
                        { "error", errm }
                    });
                }
                resultmap = osdtmp as OSDMap;

                tcs.SetResult(resultmap);
            });

            return tcs.Task;
        }

    }
}