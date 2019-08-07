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
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Reflection;

using log4net;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using Mono.Addins;

using OpenSim.Framework;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using Caps=OpenSim.Framework.Capabilities.Caps;

namespace OpenSim.Region.ClientStack.Linden
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "EstateChangeInfoCapModule")]
    public class EstateChangeInfoCapModule : INonSharedRegionModule
    {
        private static readonly ILog m_log =
            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private Scene m_scene;
        private bool m_Enabled = false;
        private string m_capUrl;
        IEstateModule m_EstateModule;

        #region INonSharedRegionModule Members

        public void Initialise(IConfigSource pSource)
        {
            IConfig config = pSource.Configs["ClientStack.LindenCaps"];
            if (config == null)
                return;

            m_capUrl = config.GetString("Cap_EstateChangeInfo", string.Empty);
            if (!String.IsNullOrEmpty(m_capUrl) && m_capUrl.Equals("localhost"))
                m_Enabled = true;
        }

        public void AddRegion(Scene scene)
        {
            if (!m_Enabled)
                return;

            m_scene = scene;
        }

        public void RemoveRegion(Scene scene)
        {
            if (!m_Enabled)
                return;

            if (m_scene == scene)
            {
                m_scene.EventManager.OnRegisterCaps -= RegisterCaps;
                m_scene = null;
            }
        }

        public void RegionLoaded(Scene scene)
        {
            if (!m_Enabled)
                return;

            if (scene.RegionInfo == null || scene.RegionInfo.EstateSettings == null)
            {
                m_Enabled = false;
                return;
            }

            m_EstateModule = scene.RequestModuleInterface<IEstateModule>();
            if(m_EstateModule == null)
            {
                m_Enabled = false;
                return;
            }

            scene.EventManager.OnRegisterCaps += RegisterCaps;
        }

        public void Close()
        {
        }

        public string Name
        {
            get { return "EstateChangeInfoCapModule"; }
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        #endregion

        public void RegisterCaps(UUID agentID, Caps caps)
        {
            string capUrl = "/CAPS/" + UUID.Random() + "/";

            caps.RegisterHandler(
                "EstateChangeInfo",
                new RestHTTPHandler(
                    "POST",
                    capUrl,
                    httpMethod => ProcessRequest(httpMethod, agentID, caps),
                    "EstateChangeInfo",
                    agentID.ToString())); ;
        }

        public Hashtable ProcessRequest(Hashtable request, UUID AgentId, Caps cap)
        {
            Hashtable responsedata = new Hashtable();

            ScenePresence avatar;
            if (!m_scene.TryGetScenePresence(AgentId, out avatar) || !m_scene.Permissions.CanIssueEstateCommand(AgentId, false))
            {
                responsedata["int_response_code"] = 401;
                responsedata["str_response_string"] = LLSDxmlEncode.LLSDEmpty;
                responsedata["keepalive"] = false;
                return responsedata;
            }

            if (m_scene.RegionInfo == null 
                || m_scene.RegionInfo.EstateSettings == null)
            {
                responsedata["int_response_code"] = 501;
                responsedata["str_response_string"] = LLSDxmlEncode.LLSDEmpty;
                responsedata["keepalive"] = false;
                return responsedata;
            }

            OSDMap r;

            try
            {
                r = (OSDMap)OSDParser.Deserialize((string)request["requestbody"]);
            }
            catch (Exception ex)
            {
                m_log.Error("[UPLOAD OBJECT ASSET MODULE]: Error deserializing message " + ex.ToString());
                r = null;
            }

            if (r == null)
            {
                responsedata["int_response_code"] = 400; //501; //410; //404;
                responsedata["content_type"] = "text/plain";
                responsedata["keepalive"] = false;
                responsedata["str_response_string"] = LLSDxmlEncode.LLSDEmpty;
                return responsedata;
            }

            bool ok = true;
            try
            {
                string estateName = r["estate_name"].AsString();
                UUID invoice = r["invoice"].AsUUID();
                int sunHour = r["sun_hour"].AsInteger();
                bool sunFixed = r["is_sun_fixed"].AsBoolean();
                bool externallyVisible = r["is_externally_visible"].AsBoolean();
                bool allowDirectTeleport = r["allow_direct_teleport"].AsBoolean();
                bool denyAnonymous = r["deny_anonymous"].AsBoolean();
                bool denyAgeUnverified = r["deny_age_unverified"].AsBoolean();
                bool alloVoiceChat = r["allow_voice_chat"].AsBoolean();
                // taxfree is now AllowAccessOverride
                bool overridePublicAccess = m_scene.RegionInfo.EstateSettings.TaxFree;
                if (r.ContainsKey("override_public_access"))
                    overridePublicAccess = r["override_public_access"].AsBoolean();

                ok = m_EstateModule.handleEstateChangeInfoCap(estateName, invoice, sunHour, sunFixed,
                        externallyVisible, allowDirectTeleport, denyAnonymous, denyAgeUnverified,
                        alloVoiceChat, overridePublicAccess);
            }
            catch
            {
                ok = false;
            }

            if(ok)
            {
                responsedata["int_response_code"] = 200;
                responsedata["content_type"] = "text/plain";
                responsedata["str_response_string"] = LLSDxmlEncode.LLSDEmpty;
            }
            else
            {
                responsedata["int_response_code"] = 400; //501; //410; //404;
                responsedata["content_type"] = "text/plain";
                responsedata["keepalive"] = false;
                responsedata["str_response_string"] = LLSDxmlEncode.LLSDEmpty;
            }
            return responsedata;
        }
    }
}
