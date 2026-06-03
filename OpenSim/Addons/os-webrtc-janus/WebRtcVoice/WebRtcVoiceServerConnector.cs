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

namespace osWebRtcVoice
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

                    m_log.Debug($"{LogHeader} loading {localServiceModule}");
                    m_WebRtcVoiceService = ServerUtils.LoadPlugin<IWebRtcVoiceService>(localServiceModule, [pConfig]);

                    // The WebRtcVoiceServiceModule is both an IWebRtcVoiceService and a ISharedRegionModule
                    //     so we can initialize it as if it was the region module.
                    if (m_WebRtcVoiceService is not IWebRtcVoiceService voiceservice)
                    {
                        m_log.ErrorFormat("{0} local service module does not implement ISharedRegionModule", LogHeader);
                        m_Enabled = false;
                        return;
                    }

                    // Now that we have someone to handle the requests, we can set up the handlers
                    pServer.AddJsonRPCHandler("provision_voice_account_request", Handle_ProvisionVoiceAccountRequest);
                    pServer.AddJsonRPCHandler("voice_signaling_request", Handle_VoiceSignalingRequest);
                }
            }
        }

        private bool Handle_ProvisionVoiceAccountRequest(OSDMap pJson, ref JsonRpcResponse pResponse)
        {
            bool ret = false;
            m_log.Debug($"{LogHeader} Handle_ProvisionVoiceAccountRequest");
            if (m_MessageDetails) m_log.DebugFormat("{0} PVAR: req={1}", LogHeader, pJson.ToString());

            if (pJson.TryGetOSDMap("params", out OSDMap paramsMap))
            {
                OSDMap request = paramsMap.TryGetOSDMap("request", out OSDMap treq) ? treq : null;
                if(request is null)
                {
                    m_log.Error($"{LogHeader} PVAR: invalid parameter 'request'");
                    return false;
                }

                UUID userID = paramsMap.TryGetUUID("userID", out UUID tuserid) ? tuserid : UUID.Zero;
                UUID sceneID = paramsMap.TryGetUUID("scene", out UUID tsceneid) ? tsceneid : UUID.Zero;

                try
                {
                    if (m_WebRtcVoiceService is null)
                    {
                        m_log.ErrorFormat("{0} PVAR: no local service", LogHeader);
                        return false;
                    }

                    OSDMap resp = m_WebRtcVoiceService.ProvisionVoiceAccountRequest(request, userID, sceneID);

                    pResponse = new JsonRpcResponse
                    {
                        Result = resp
                    };

                    if (m_MessageDetails) m_log.DebugFormat("{0} PVAR: resp={1}", LogHeader, resp.ToString());
                    ret = true;
                }
                catch (Exception e)
                {
                    m_log.Error($"{LogHeader} PVAR: exception ", e);
                }   
            }
            else
            {
                m_log.Error($"{LogHeader} PVAR: missing parameters");
            }
            return ret;
        }

        private bool Handle_VoiceSignalingRequest(OSDMap pJson, ref JsonRpcResponse pResponse)
        {
            if (pJson.TryGetOSDMap("params", out OSDMap paramsMap))
            {
                m_log.Debug($"{LogHeader} Handle_VoiceSignalingRequest");
                if (m_MessageDetails) m_log.Debug($"{LogHeader} VSR: req={paramsMap}");

                OSDMap request = paramsMap.TryGetOSDMap("request", out OSDMap treq) ? treq : null;
                if(request is null)
                {
                    m_log.Error($"{LogHeader} VSR: null parameter 'request'");
                    return false;
                }

                UUID userID = paramsMap.TryGetUUID("userID", out UUID tuserid) ? tuserid : UUID.Zero;
                UUID sceneID = paramsMap.TryGetUUID("scene", out UUID tsceneid) ? tsceneid : UUID.Zero;

                try
                {
                    OSDMap resp = m_WebRtcVoiceService.VoiceSignalingRequest(request, userID, sceneID);

                    pResponse = new JsonRpcResponse
                    {
                        Result = resp
                    };
                    if (m_MessageDetails) m_log.Debug($"{LogHeader} VSR: resp={resp}");

                    return true;
                }
                catch (Exception e)
                {
                    m_log.Error($"{LogHeader} VSR: exception ", e);
                }
            }
            else
            {
                m_log.Error($"{LogHeader} VSR: missing parameters");
            }

            return false;
        }
    }
}
