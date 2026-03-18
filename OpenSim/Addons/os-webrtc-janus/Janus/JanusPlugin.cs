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
using System.Threading.Tasks;

using OpenMetaverse.StructuredData;
using OpenMetaverse;

using Nini.Config;
using log4net;

namespace osWebRtcVoice
{
    // Encapsulization of a Session to the Janus server
    public class JanusPlugin : IDisposable
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private static readonly string LogHeader = "[JANUS PLUGIN]";

        protected IConfigSource _Config;
        protected JanusSession _JanusSession;

        public string PluginName { get; private set; }
        public string PluginId { get; private set; }
        public string PluginUri { get ; private set ; }

        public bool IsConnected => !String.IsNullOrEmpty(PluginId);

        // Wrapper around the session connection to Janus-gateway
        public JanusPlugin(JanusSession pSession, string pPluginName)
        {
            _JanusSession = pSession;
            PluginName = pPluginName;
        }

        public virtual void Dispose()
        {
            if (IsConnected)
            {
                // Close the handle

            }
        }

        public Task<JanusMessageResp> SendPluginMsg(OSDMap pParams)
        {
            return _JanusSession.SendToJanus(new PluginMsgReq(pParams), PluginUri);
        }
        public Task<JanusMessageResp> SendPluginMsg(PluginMsgReq pJMsg)
        {
            return _JanusSession.SendToJanus(pJMsg, PluginUri);
        }

        /// <summary>
        /// Make the create a handle to a plugin within the session.
        /// </summary>
        /// <returns>TRUE if handle was created successfully</returns>
        public async Task<bool> Activate(IConfigSource pConfig)
        {
            _Config = pConfig;

            bool ret = false;
            try
            {
                JanusMessageResp resp = await _JanusSession.SendToSession(new AttachPluginReq(PluginName)).ConfigureAwait(false);
                if (resp is not null && resp.isSuccess)
                {
                    AttachPluginResp handleResp = new(resp);
                    PluginId = handleResp.pluginId;
                    PluginUri = _JanusSession.SessionUri + "/" + PluginId;
                    m_log.Debug($"{LogHeader} Activate. Plugin attached. ID={PluginId}, URL={PluginUri}");
                    _JanusSession.PluginId = PluginId;
                    _JanusSession.OnEvent += Handle_Event;
                    _JanusSession.OnMessage += Handle_Message;
                    ret = true;
                }
                else
                {
                    m_log.Error($"{LogHeader} Activate: failed to attach to plugin {PluginName}");
                }
            }
            catch (Exception e)
            {
                m_log.Error($"{LogHeader} Activate: exception attaching to plugin {PluginName}:", e);
            }

            return ret;
        }

        public virtual async Task<bool> Detach()
        {
            bool ret = false;
            if (!IsConnected || _JanusSession is null)
            {
                m_log.Warn($"{LogHeader} Detach. Not connected");
                return ret;
            }
            try
            {
                _JanusSession.OnEvent -= Handle_Event;
                _JanusSession.OnMessage -= Handle_Message;
                // We send the 'detach' message to the plugin URI
                JanusMessageResp resp = await _JanusSession.SendToJanus(new DetachPluginReq(), PluginUri).ConfigureAwait(false);
                if (resp is not null && resp.isSuccess)
                {
                    m_log.Debug($"{LogHeader} Detach. Detached");
                    ret = true;
                }
                else
                {
                    m_log.Error($"{LogHeader} Detach: failed");
                }
            }
            catch (Exception e)
            {
                m_log.Error($"{LogHeader} Detach: exception", e);
            }

            return ret;
        }   

        public virtual void Handle_Event(JanusMessageResp pResp)
        {
            m_log.Debug($"{LogHeader} Handle_Event: {pResp}");
        }
        public virtual void Handle_Message(JanusMessageResp pResp)
        {
            m_log.Debug($"{LogHeader} Handle_Message: {pResp}");
        }
    }
}