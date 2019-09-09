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
using System.Reflection;
using System.IO;
using log4net;
using Mono.Addins;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using OpenSim.Framework.Console;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;
using Caps = OpenSim.Framework.Capabilities.Caps;
using OpenSim.Capabilities.Handlers;

namespace OpenSim.Region.ClientStack.LindenCaps
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "AgentPreferencesModule")]
    public class AgentPreferencesModule : ISharedRegionModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private List<Scene> m_scenes = new List<Scene>();

        public void Initialise(IConfigSource source)
        {

        }

        #region Region module

        public void AddRegion(Scene scene)
        {
            lock (m_scenes) m_scenes.Add(scene);
        }

        public void RemoveRegion(Scene scene)
        {
            lock (m_scenes) m_scenes.Remove(scene);
            scene.EventManager.OnRegisterCaps -= RegisterCaps;
            scene = null;
        }

        public void RegionLoaded(Scene scene)
        {
            scene.EventManager.OnRegisterCaps += delegate(UUID agentID, OpenSim.Framework.Capabilities.Caps caps)
            {
                RegisterCaps(agentID, caps);
            };
/*
            ISimulatorFeaturesModule simFeatures = scene.RequestModuleInterface<ISimulatorFeaturesModule>();
            if(simFeatures != null)
                simFeatures.AddFeature("AvatarHoverHeightEnabled",OSD.FromBoolean(true));
*/
        }

        public void PostInitialise() {}

        public void Close() {}

        public string Name { get { return "AgentPreferencesModule"; } }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public void RegisterCaps(UUID agent, Caps caps)
        {
            UUID capId = UUID.Random();
            caps.RegisterHandler("AgentPreferences",
                new RestStreamHandler("POST", "/CAPS/" + capId,
                    delegate(string request, string path, string param,
                        IOSHttpRequest httpRequest, IOSHttpResponse httpResponse)
                    {
                        return UpdateAgentPreferences(request, path, param, agent);
                    }));
            caps.RegisterHandler("UpdateAgentLanguage",
                new RestStreamHandler("POST", "/CAPS/" + capId,
                    delegate(string request, string path, string param,
                        IOSHttpRequest httpRequest, IOSHttpResponse httpResponse)
                    {
                        return UpdateAgentPreferences(request, path, param, agent);
                    }));
            caps.RegisterHandler("UpdateAgentInformation",
                new RestStreamHandler("POST", "/CAPS/" + capId,
                    delegate(string request, string path, string param,
                        IOSHttpRequest httpRequest, IOSHttpResponse httpResponse)
                    {
                        return UpdateAgentPreferences(request, path, param, agent);
                    }));
        }

        public string UpdateAgentPreferences(string request, string path, string param, UUID agent)
        {
            OSDMap resp = new OSDMap();
            // if there is no preference service,
            // we'll return a null llsd block for debugging purposes. This may change if someone knows what the
            // correct server response would be here.
            if (m_scenes[0].AgentPreferencesService == null)
            {
                return OSDParser.SerializeLLSDXmlString(resp);
            }
            m_log.DebugFormat("[AgentPrefs]: UpdateAgentPreferences for {0}", agent.ToString());
            OSDMap req = (OSDMap)OSDParser.DeserializeLLSDXml(request);
            AgentPrefs data = m_scenes[0].AgentPreferencesService.GetAgentPreferences(agent);
            if (data == null)
            {
                data = new AgentPrefs(agent);
            }

            if (req.ContainsKey("access_prefs"))
            {
                OSDMap accessPrefs = (OSDMap)req["access_prefs"];  // We could check with ContainsKey...
                data.AccessPrefs = accessPrefs["max"].AsString();
            }
            if (req.ContainsKey("default_object_perm_masks"))
            {
                OSDMap permsMap = (OSDMap)req["default_object_perm_masks"];
                data.PermEveryone = permsMap["Everyone"].AsInteger();
                data.PermGroup = permsMap["Group"].AsInteger();
                data.PermNextOwner = permsMap["NextOwner"].AsInteger();
            }
            if (req.ContainsKey("hover_height"))
            {
                //data.HoverHeight = (float)req["hover_height"].AsReal();
                data.HoverHeight = req["hover_height"].AsReal();
            }
            if (req.ContainsKey("language"))
            {
                data.Language = req["language"].AsString();
            }
            if (req.ContainsKey("language_is_public"))
            {
                data.LanguageIsPublic = req["language_is_public"].AsBoolean();
            }
            m_scenes[0].AgentPreferencesService.StoreAgentPreferences(data);
            OSDMap respAccessPrefs = new OSDMap();
            respAccessPrefs["max"] = data.AccessPrefs;
            resp["access_prefs"] = respAccessPrefs;
            OSDMap respDefaultPerms = new OSDMap();
            respDefaultPerms["Everyone"] = data.PermEveryone;
            respDefaultPerms["Group"] = data.PermGroup;
            respDefaultPerms["NextOwner"] = data.PermNextOwner;
            resp["default_object_perm_masks"] = respDefaultPerms;
            resp["god_level"] = 0; // *TODO: Add this
            resp["hover_height"] = data.HoverHeight;
            resp["language"] = data.Language;
            resp["language_is_public"] = data.LanguageIsPublic;
/*
            IAvatarFactoryModule afm = m_scenes[0].RequestModuleInterface<IAvatarFactoryModule>();
            afm?.SetPreferencesHoverZ(agent, (float)data.HoverHeight);
*/
            string response = OSDParser.SerializeLLSDXmlString(resp);
            return response;
        }

        #endregion Region module
    }
}

