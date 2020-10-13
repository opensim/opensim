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
using System.Collections.Specialized;

using System.Net;
using log4net;
using Nini.Config;
using Mono.Addins;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Framework.Capabilities;
using Caps = OpenSim.Framework.Capabilities.Caps;

namespace OpenSim.Region.ClientStack.Linden
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "AvatarPickerSearchModule")]
    public class AvatarPickerSearchModule : ISharedRegionModule
    {
//        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private int m_nscenes;
        private IPeople m_People = null;
        private bool m_Enabled = false;

        private string m_URL;

        #region ISharedRegionModule Members

        public void Initialise(IConfigSource source)
        {
            IConfig config = source.Configs["ClientStack.LindenCaps"];
            if (config == null)
                return;

            m_URL = config.GetString("Cap_AvatarPickerSearch", string.Empty);
            // Cap doesn't exist
            if (m_URL != string.Empty)
                m_Enabled = true;
        }

        public void AddRegion(Scene s)
        {
            if (!m_Enabled)
                return;
        }

        public void RemoveRegion(Scene s)
        {
            if (!m_Enabled)
                return;

            s.EventManager.OnRegisterCaps -= RegisterCaps;
            --m_nscenes;
            if(m_nscenes >= 0)
                m_People = null;
        }

        public void RegionLoaded(Scene s)
        {
            if (!m_Enabled)
                return;

            if(m_People == null)
                m_People = s.RequestModuleInterface<IPeople>();
            s.EventManager.OnRegisterCaps += RegisterCaps;
            ++m_nscenes;
        }

        public void PostInitialise()
        {
        }

        public void Close() { }

        public string Name { get { return "AvatarPickerSearchModule"; } }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        #endregion

        public void RegisterCaps(UUID agentID, Caps caps)
        {
            UUID capID = UUID.Random();

            if (m_URL == "localhost")
            {
                // m_log.DebugFormat("[AVATAR PICKER SEARCH]: /CAPS/{0} in region {1}", capID, m_scene.RegionInfo.RegionName);
                if(m_People != null)
                    caps.RegisterSimpleHandler("AvatarPickerSearch",
                        new SimpleStreamHandler("/" + UUID.Random(), ProcessRequest));
            }
            else
            {
                // m_log.DebugFormat("[AVATAR PICKER SEARCH]: {0} in region {1}", m_URL, m_scene.RegionInfo.RegionName);
                caps.RegisterHandler("AvatarPickerSearch", m_URL);
            }
        }

        protected void ProcessRequest(IOSHttpRequest httpRequest, IOSHttpResponse httpResponse)
        {
            if(httpRequest.HttpMethod != "GET")
            {
                httpResponse.StatusCode = (int)HttpStatusCode.NotFound;
                return;
            }

            NameValueCollection query = httpRequest.QueryString;
            string names = query.GetOne("names");
            string psize = query.GetOne("page_size");
            string pnumber = query.GetOne("page");

            if (string.IsNullOrEmpty(names) || names.Length < 3)
            {
                httpResponse.StatusCode = (int)HttpStatusCode.BadRequest;
                return;
            }

            int page_size;
            int page_number;
            try
            {
                page_size = (string.IsNullOrEmpty(psize) ? 500 : Int32.Parse(psize));
                page_number = (string.IsNullOrEmpty(pnumber) ? 1 : Int32.Parse(pnumber));
            }
            catch
            {
                httpResponse.StatusCode = (int)HttpStatusCode.BadRequest;
                return;
            }
            // Full content request
            List<UserData> users = m_People.GetUserData(names, page_size, page_number);

            LLSDAvatarPicker osdReply = new LLSDAvatarPicker();
            osdReply.next_page_url = httpRequest.RawUrl;
            foreach (UserData u in users)
                osdReply.agents.Array.Add(ConvertUserData(u));

            string reply = LLSDHelpers.SerialiseLLSDReply(osdReply);
            httpResponse.RawBuffer = Util.UTF8.GetBytes(reply);
            httpResponse.StatusCode = (int)HttpStatusCode.OK;
            httpResponse.ContentType = "application/llsd+xml";
        }

        private LLSDPerson ConvertUserData(UserData user)
        {
            LLSDPerson p = new LLSDPerson();
            p.legacy_first_name = user.FirstName;
            p.legacy_last_name = user.LastName;
            p.display_name = user.FirstName + " " + user.LastName;
            if (user.LastName.StartsWith("@"))
                p.username = user.FirstName.ToLower() + user.LastName.ToLower();
            else
                p.username = user.FirstName.ToLower() + "." + user.LastName.ToLower();
            p.id = user.Id;
            p.is_display_name_default = false;
            return p;
        }
    }
}
