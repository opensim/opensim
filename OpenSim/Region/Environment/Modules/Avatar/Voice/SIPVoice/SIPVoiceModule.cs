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
 *     * Neither the name of the OpenSim Project nor the
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
using System.Collections;
using System.Reflection;
using OpenMetaverse;
using log4net;
using Nini.Config;
using OpenSim.Framework;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Framework.Communications.Capabilities;
using OpenSim.Framework.Servers;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Scenes;
using Caps=OpenSim.Framework.Communications.Capabilities.Caps;

namespace OpenSim.Region.Environment.Modules.Avatar.Voice.SIPVoice
{
    public class SIPVoiceModule : IRegionModule
    {
        private static readonly ILog m_log =
            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private static readonly string m_parcelVoiceInfoRequestPath = "0007/";
        private static readonly string m_provisionVoiceAccountRequestPath = "0008/";
        private IConfig m_config;
        private Scene m_scene;
        private string m_sipDomain;

        #region IRegionModule Members

        public void Initialise(Scene scene, IConfigSource config)
        {
            m_scene = scene;
            m_config = config.Configs["Voice"];

            if (null == m_config || !m_config.GetBoolean("enabled", false))
            {
                m_log.Info("[VOICE] plugin disabled");
                return;
            }
            m_log.Info("[VOICE] plugin enabled");

            m_sipDomain = m_config.GetString("sip_domain", String.Empty);
            if (String.IsNullOrEmpty(m_sipDomain))
            {
                m_log.Error("[VOICE] plugin mis-configured: missing sip_domain configuration");
                m_log.Info("[VOICE] plugin disabled");
                return;
            }
            m_log.InfoFormat("[VOICE] using SIP domain {0}", m_sipDomain);

            scene.EventManager.OnRegisterCaps += OnRegisterCaps;
        }

        public void PostInitialise()
        {
        }

        public void Close()
        {
        }

        public string Name
        {
            get { return "VoiceModule"; }
        }

        public bool IsSharedModule
        {
            get { return false; }
        }

        #endregion

        public void OnRegisterCaps(UUID agentID, Caps caps)
        {
            m_log.DebugFormat("[VOICE] OnRegisterCaps: agentID {0} caps {1}", agentID, caps);
            string capsBase = "/CAPS/" + caps.CapsObjectPath;
            caps.RegisterHandler("ParcelVoiceInfoRequest",
                                 new RestStreamHandler("POST", capsBase + m_parcelVoiceInfoRequestPath,
                                                       delegate(string request, string path, string param,
                                                                OSHttpRequest httpRequest, OSHttpResponse httpResponse)
                                                           {
                                                               return ParcelVoiceInfoRequest(request, path, param,
                                                                                             agentID, caps);
                                                           }));
            caps.RegisterHandler("ProvisionVoiceAccountRequest",
                                 new RestStreamHandler("POST", capsBase + m_provisionVoiceAccountRequestPath,
                                                       delegate(string request, string path, string param,
                                                                OSHttpRequest httpRequest, OSHttpResponse httpResponse)
                                                           {
                                                               return ProvisionVoiceAccountRequest(request, path, param,
                                                                                                   agentID, caps);
                                                           }));
        }

        /// <summary>
        /// Callback for a client request for ParcelVoiceInfo
        /// </summary>
        /// <param name="request"></param>
        /// <param name="path"></param>
        /// <param name="param"></param>
        /// <param name="agentID"></param>
        /// <param name="caps"></param>
        /// <returns></returns>
        public string ParcelVoiceInfoRequest(string request, string path, string param,
                                             UUID agentID, Caps caps)
        {
            try
            {
                m_log.DebugFormat("[VOICE][PARCELVOICE]: request: {0}, path: {1}, param: {2}", request, path, param);

                // FIXME: get the creds from region file or from config
                Hashtable creds = new Hashtable();

                creds["channel_uri"] = String.Format("sip:{0}@{1}", agentID, m_sipDomain);

                string regionName = m_scene.RegionInfo.RegionName;
                ScenePresence avatar = m_scene.GetScenePresence(agentID);
                if (null == m_scene.LandChannel) throw new Exception("land data not yet available");
                LandData land = m_scene.GetLandData(avatar.AbsolutePosition.X, avatar.AbsolutePosition.Y);

                LLSDParcelVoiceInfoResponse parcelVoiceInfo =
                    new LLSDParcelVoiceInfoResponse(regionName, land.LocalID, creds);

                string r = LLSDHelpers.SerialiseLLSDReply(parcelVoiceInfo);
                m_log.DebugFormat("[VOICE][PARCELVOICE]: {0}", r);

                return r;
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[CAPS]: {0}, try again later", e.ToString());
            }

            return null;
        }

        /// <summary>
        /// Callback for a client request for Voice Account Details
        /// </summary>
        /// <param name="request"></param>
        /// <param name="path"></param>
        /// <param name="param"></param>
        /// <param name="agentID"></param>
        /// <param name="caps"></param>
        /// <returns></returns>
        public string ProvisionVoiceAccountRequest(string request, string path, string param,
                                                   UUID agentID, Caps caps)
        {
            try
            {
                m_log.DebugFormat("[VOICE][PROVISIONVOICE]: request: {0}, path: {1}, param: {2}",
                                  request, path, param);

                string voiceUser = "x" + Convert.ToBase64String(agentID.GetBytes());
                voiceUser = voiceUser.Replace('+', '-').Replace('/', '_');

                CachedUserInfo userInfo = m_scene.CommsManager.UserProfileCacheService.GetUserDetails(agentID);
                if (null == userInfo) throw new Exception("cannot get user details");

                LLSDVoiceAccountResponse voiceAccountResponse =
                    new LLSDVoiceAccountResponse(voiceUser, "$1$" + userInfo.UserProfile.PasswordHash);
                string r = LLSDHelpers.SerialiseLLSDReply(voiceAccountResponse);
                m_log.DebugFormat("[CAPS][PROVISIONVOICE]: {0}", r);
                return r;
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[CAPS][PROVISIONVOICE]: {0}, retry later", e.Message);
            }

            return null;
        }
    }
}
