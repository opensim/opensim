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
using System.Globalization;
using System.Text;

using log4net;
using Nini.Config;
using OpenMetaverse;
using Mono.Addins;
using OpenSim.Framework;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using Caps=OpenSim.Framework.Capabilities.Caps;

namespace OpenSim.Region.ClientStack.Linden
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "EstateAcessCapModule")]
    public class EstateAccessCapModule : INonSharedRegionModule
    {
//        private static readonly ILog m_log =
//            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private Scene m_scene;
        private bool m_Enabled = false;
        private string m_capUrl;
        //IEstateModule m_EstateModule;

        #region INonSharedRegionModule Members

        public void Initialise(IConfigSource pSource)
        {
            IConfig config = pSource.Configs["ClientStack.LindenCaps"];
            if (config == null)
                return;

            m_capUrl = config.GetString("Cap_EstateAccess", string.Empty);
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

            IEstateModule m_EstateModule = scene.RequestModuleInterface<IEstateModule>();
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
            get { return "EstateAccessCapModule"; }
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
                "EstateAccess",
                new RestHTTPHandler(
                    "GET",
                    capUrl,
                    httpMethod => ProcessRequest(httpMethod, agentID, caps),
                    "EstateAccess",
                    agentID.ToString())); ;
        }

        public Hashtable ProcessRequest(Hashtable request, UUID AgentId, Caps cap)
        {
            Hashtable responsedata = new Hashtable();
            responsedata["int_response_code"] = 200; //501; //410; //404;
            responsedata["content_type"] = "text/plain";

            ScenePresence avatar;
            if (!m_scene.TryGetScenePresence(AgentId, out avatar))
            {
                responsedata["str_response_string"] = "<llsd><array /></llsd>"; ;
                responsedata["keepalive"] = false;
                return responsedata;
            }

            if (m_scene.RegionInfo == null 
                || m_scene.RegionInfo.EstateSettings == null
                ||!m_scene.Permissions.CanIssueEstateCommand(AgentId, false))
            {
                responsedata["str_response_string"] = "<llsd><array /></llsd>"; ;
                return responsedata;
            }

            EstateSettings regionSettings = m_scene.RegionInfo.EstateSettings;
            UUID[] managers = regionSettings.EstateManagers;
            UUID[] allowed = regionSettings.EstateAccess;
            UUID[] groups = regionSettings.EstateGroups;
            EstateBan[] EstateBans = regionSettings.EstateBans;

            StringBuilder sb = LLSDxmlEncode.Start();
            LLSDxmlEncode.AddMap(sb);

            if (allowed != null && allowed.Length > 0)
            {
                LLSDxmlEncode.AddArray("AllowedAgents", sb);
                for (int i = 0; i < allowed.Length; ++i)
                {
                    UUID id = allowed[i];
                    if (id == UUID.Zero)
                        continue;
                    LLSDxmlEncode.AddMap(sb);
                        LLSDxmlEncode.AddElem("id", id, sb);
                    LLSDxmlEncode.AddEndMap(sb);
                }
                LLSDxmlEncode.AddEndArray(sb);
            }

            if (groups != null && groups.Length > 0)
            {
                LLSDxmlEncode.AddArray("AllowedGroups", sb);
                for (int i = 0; i < groups.Length; ++i)
                {
                    UUID id = groups[i];
                    if (id == UUID.Zero)
                        continue;
                    LLSDxmlEncode.AddMap(sb);
                        LLSDxmlEncode.AddElem("id", id, sb);
                    LLSDxmlEncode.AddEndMap(sb);
                }
                LLSDxmlEncode.AddEndArray(sb);
            }

            if (EstateBans != null && EstateBans.Length > 0)
            {
                LLSDxmlEncode.AddArray("BannedAgents", sb);
                for (int i = 0; i < EstateBans.Length; ++i)
                {
                    EstateBan ban = EstateBans[i];
                    UUID id = ban.BannedUserID;
                    if (id == UUID.Zero)
                        continue;
                    LLSDxmlEncode.AddMap(sb);
                        LLSDxmlEncode.AddElem("id", id, sb);
                        LLSDxmlEncode.AddElem("banning_id", ban.BanningUserID, sb);
                        LLSDxmlEncode.AddElem("last_login_date", "na", sb); // We will not have this. This information is far at grid
                        if (ban.BanTime == 0)
                            LLSDxmlEncode.AddElem("ban_date", "0000-00-00 00:00", sb);
                        else
                            LLSDxmlEncode.AddElem("ban_date", (Util.ToDateTime(ban.BanTime)).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture), sb);
                    LLSDxmlEncode.AddEndMap(sb);
                }
                LLSDxmlEncode.AddEndArray(sb);
            }

            if (managers != null && managers.Length > 0)
            {
                LLSDxmlEncode.AddArray("Managers", sb);
                for (int i = 0; i < managers.Length; ++i)
                {
                    LLSDxmlEncode.AddMap(sb);
                        LLSDxmlEncode.AddElem("agent_id", managers[i], sb);
                    LLSDxmlEncode.AddEndMap(sb);
                }
                LLSDxmlEncode.AddEndArray(sb);
            }

            LLSDxmlEncode.AddEndMap(sb);
            responsedata["str_response_string"] = LLSDxmlEncode.End(sb);

            return responsedata;
        }
    }
}
