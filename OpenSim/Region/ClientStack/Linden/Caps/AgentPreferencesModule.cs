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
using System.Net;
using System.Reflection;
using System.Text;
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
            lock (m_scenes)
                m_scenes.Add(scene);
        }

        public void RemoveRegion(Scene scene)
        {
            lock (m_scenes)
                m_scenes.Remove(scene);
            scene.EventManager.OnRegisterCaps -= RegisterCaps;
            scene = null;
        }

        public void RegionLoaded(Scene scene)
        {
            scene.EventManager.OnRegisterCaps += RegisterCaps;

            ISimulatorFeaturesModule simFeatures = scene.RequestModuleInterface<ISimulatorFeaturesModule>();
            if(simFeatures != null)
                simFeatures.AddFeature("AvatarHoverHeightEnabled",OSD.FromBoolean(true));

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
            string capPath = "/" + UUID.Random().ToString();
            caps.RegisterSimpleHandler("AgentPreferences",
                new SimpleStreamHandler(capPath, delegate(IOSHttpRequest httpRequest, IOSHttpResponse httpResponse)
                    {
                        UpdateAgentPreferences(httpRequest, httpResponse, agent);
                    }));
            caps.RegisterSimpleHandler("UpdateAgentLanguage",
                new SimpleStreamHandler( capPath, delegate(IOSHttpRequest httpRequest, IOSHttpResponse httpResponse)
                    {
                        UpdateAgentPreferences(httpRequest, httpResponse, agent);
                    }), false);
            caps.RegisterSimpleHandler("UpdateAgentInformation",
                new SimpleStreamHandler(capPath, delegate(IOSHttpRequest httpRequest, IOSHttpResponse httpResponse)
                    {
                        UpdateAgentPreferences(httpRequest, httpResponse, agent);
                    }), false);
        }

        public void UpdateAgentPreferences(IOSHttpRequest httpRequest, IOSHttpResponse httpResponse, UUID agent)
        {
            if (httpRequest.HttpMethod != "POST")
            {
                httpResponse.StatusCode = (int)HttpStatusCode.NotFound;
                return;
            }

            //m_log.DebugFormat("[AgentPrefs]: UpdateAgentPreferences for {0}", agent.ToString());
            OSDMap req;
            try
            {
                req = (OSDMap)OSDParser.DeserializeLLSDXml(httpRequest.InputStream);
            }
            catch
            {
                httpResponse.StatusCode = (int)HttpStatusCode.BadRequest;
                return;
            }

            IAgentPreferencesService aps = m_scenes[0].AgentPreferencesService;
            AgentPrefs data = null;
            if(aps != null)
                data = aps.GetAgentPreferences(agent);

            if (data == null)
                data = new AgentPrefs(agent);

            bool changed = false;
            OSD tmp;
            if (req.TryGetValue("access_prefs", out tmp) && tmp is OSDMap)
            {
                OSDMap accessPrefs = (OSDMap)tmp;  // We could check with ContainsKey...
                data.AccessPrefs = accessPrefs["max"].AsString();
                changed = true;
            }
            if (req.TryGetValue("default_object_perm_masks", out tmp) && tmp is OSDMap)
            {
                OSDMap permsMap = (OSDMap)tmp;
                data.PermEveryone = permsMap["Everyone"].AsInteger();
                data.PermGroup = permsMap["Group"].AsInteger();
                data.PermNextOwner = permsMap["NextOwner"].AsInteger();
                changed = true;
            }
            if (req.TryGetValue("hover_height", out tmp))
            {
                data.HoverHeight = (float)tmp.AsReal();
                changed = true;
            }
            if (req.TryGetValue("language", out tmp))
            {
                data.Language = tmp.AsString();
                changed = true;
            }
            if (req.TryGetValue("language_is_public", out tmp))
            {
                data.LanguageIsPublic = tmp.AsBoolean();
                changed = true;
            }

            if(changed)
                aps?.StoreAgentPreferences(data);

            IAvatarFactoryModule afm = m_scenes[0].RequestModuleInterface<IAvatarFactoryModule>();
            afm?.SetPreferencesHoverZ(agent, (float)data.HoverHeight);

            OSDMap resp = new OSDMap();
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

            httpResponse.RawBuffer = OSDParser.SerializeLLSDXmlBytes(resp);
            httpResponse.StatusCode = (int)HttpStatusCode.OK;
        }
        #endregion Region module
    }
}

