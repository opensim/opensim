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
using System.Reflection;
using log4net;
using Mono.Addins;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using Caps = OpenSim.Framework.Capabilities.Caps;

namespace OpenSim.Region.ClientStack.LindenCaps
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "ServerReleaseNotesModule")]
    class ServerReleaseNotesModule : ISharedRegionModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private Dictionary<string, Scene> m_Scenes = new Dictionary<string, Scene>();
        private Scene m_scene;

        private bool m_enabled;
        private static string m_ServerReleaseNotesURL = string.Empty;

        public string Name { get { return "ServerReleaseNotesModule"; } }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public void AddRegion(Scene scene)
        {
            if (!m_enabled)
                return;

            m_scene = scene;

            if (m_enabled == true)
            {
                if (m_Scenes.ContainsKey(scene.RegionInfo.RegionName))
                {
                    lock (m_Scenes)
                    {
                        m_Scenes[scene.RegionInfo.RegionName] = scene;
                    }
                }
            }
            else
            {
                lock (m_Scenes)
                {
                    m_Scenes.Add(scene.RegionInfo.RegionName, scene);
                }
            }

            m_scene.EventManager.OnRegisterCaps += RegisterCaps;
        }

        public void Close() { }

        public void Initialise(IConfigSource source)
        {
            IConfig ServerReleaseNote = source.Configs["ServerReleaseNotes"];
            m_ServerReleaseNotesURL = ServerReleaseNote.GetString("ServerReleaseNotesURL", m_ServerReleaseNotesURL);
            m_enabled = ServerReleaseNote.GetBoolean("enabled", false);

            if (m_ServerReleaseNotesURL == null)
            {
                m_enabled = false;
                m_log.Info("[ServerReleaseNotes]: No Configuration Found, module has been disabled");
                return;
            }

            if (m_enabled == false)
            {
                m_log.InfoFormat("[ServerReleaseNotes]: Module is disabled");
            }
        }

        public void PostInitialise() { }

        public void RegionLoaded(Scene scene)
        {
            if (!m_enabled)
            {
                return;
            }
        }

        public void RemoveRegion(Scene scene)
        {
            if (!m_enabled)
            {
                return;
            }
            m_scene.EventManager.OnRegisterCaps -= RegisterCaps;
        }

        public void RegisterCaps(UUID agentID, Caps caps)
        {
            UUID capId = UUID.Random();

            IRequestHandler ServerReleaseNote
                = new RestHTTPHandler(
                    "GET", "/CAPS/" + capId + "/",
                    delegate (Hashtable request)
                    {
                        return ProcessServerReleaseNotes(request, agentID, capId);
                    });
            caps.RegisterHandler("ServerReleaseNotes", ServerReleaseNote);
        }

        private Hashtable ProcessServerReleaseNotes(Hashtable request, UUID agentID, UUID capUUID)
        {
            Hashtable responsedata = new Hashtable();
            responsedata["int_response_code"] = 301;
            responsedata["str_redirect_location"] = m_ServerReleaseNotesURL;
            responsedata["content_type"] = "text/plain";
            responsedata["keepalive"] = false;

            OSDMap osd = new OSDMap();
            osd.Add("ServerReleaseNotes", new OSDString(GetServerReleaseNotesURL()));

            string response = OSDParser.SerializeLLSDXmlString(osd);
            responsedata["str_response_string"] = response;
            return responsedata;
        }

        private string GetServerReleaseNotesURL()
        {
            return "Set the ReleaseNotesUrl in OpenSim.ini under [ServerReleaseNotesURL] section";
        }
    }
}
