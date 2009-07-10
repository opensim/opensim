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
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Collections.Generic;
using System.Text;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Client.VWoHTTP.ClientStack;
using OpenSim.Framework;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Client.VWoHTTP
{
    class VWoHTTPModule : IRegionModule, IHttpAgentHandler
    {
        private bool m_disabled = true;

        private IHttpServer m_httpd;

        private readonly List<Scene> m_scenes = new List<Scene>();

        private Dictionary<UUID, VWHClientView> m_clients = new Dictionary<UUID, VWHClientView>();

        #region Implementation of IRegionModule

        public void Initialise(Scene scene, IConfigSource source)
        {
            if (m_disabled)
                return;

            m_scenes.Add(scene);

            m_httpd = MainServer.Instance;
        }

        public void PostInitialise()
        {
            if (m_disabled)
                return;

            m_httpd.AddAgentHandler("vwohttp", this);
        }

        public void Close()
        {
            if (m_disabled)
                return;

            m_httpd.RemoveAgentHandler("vwohttp", this);
        }

        public string Name
        {
            get { return "VWoHTTP ClientStack"; }
        }

        public bool IsSharedModule
        {
            get { return true; }
        }

        #endregion

        #region Implementation of IHttpAgentHandler

        public bool Handle(OSHttpRequest req, OSHttpResponse resp)
        {
            string[] urlparts = req.Url.AbsolutePath.Split(new char[] {'/'}, StringSplitOptions.RemoveEmptyEntries);

            if (urlparts.Length < 2)
                return false;

            if (urlparts[1] == "connect")
            {
                UUID sessID = UUID.Random();

                VWHClientView client = new VWHClientView(sessID, UUID.Random(), "VWoHTTPClient", m_scenes[0]);

                m_clients.Add(sessID, client);

                return true;
            }
            else
            {
                if (urlparts.Length < 3)
                    return false;

                UUID sessionID;
                if (!UUID.TryParse(urlparts[1], out sessionID))
                    return false;

                if (!m_clients.ContainsKey(sessionID))
                    return false;

                return m_clients[sessionID].ProcessInMsg(req, resp);
            }
        }

        public bool Match(OSHttpRequest req, OSHttpResponse resp)
        {
            return req.Url.ToString().Contains("vwohttp");
        }

        #endregion
    }
}
