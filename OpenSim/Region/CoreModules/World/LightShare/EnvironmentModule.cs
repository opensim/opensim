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
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Capabilities;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using log4net;
using Nini.Config;
using Mono.Addins;

using Caps = OpenSim.Framework.Capabilities.Caps;


namespace OpenSim.Region.CoreModules.World.LightShare
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "EnvironmentModule")]

    public class EnvironmentModule : INonSharedRegionModule, IEnvironmentModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private Scene m_scene = null;
        private UUID regionID = UUID.Zero;
        private static bool Enabled = false;

        private static readonly string capsName = "EnvironmentSettings";
        private static readonly string capsBase = "/CAPS/0020/";

        private LLSDEnvironmentSetResponse setResponse = null;

        #region INonSharedRegionModule
        public void Initialise(IConfigSource source)
        {
            IConfig config = source.Configs["ClientStack.LindenCaps"];

            if (null == config)
                return;

            if (config.GetString("Cap_EnvironmentSettings", String.Empty) != "localhost")
            {
                m_log.InfoFormat("[{0}]: Module is disabled.", Name);
                return;
            }

            Enabled = true;

            m_log.InfoFormat("[{0}]: Module is enabled.", Name);
        }

        public void Close()
        {
        }

        public string Name
        {
            get { return "EnvironmentModule"; }
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public void AddRegion(Scene scene)
        {
            if (!Enabled)
                return;

            scene.RegisterModuleInterface<IEnvironmentModule>(this);
            m_scene = scene;
            regionID = scene.RegionInfo.RegionID;
        }

        public void RegionLoaded(Scene scene)
        {
            if (!Enabled)
                return;

            setResponse = new LLSDEnvironmentSetResponse();
            scene.EventManager.OnRegisterCaps += OnRegisterCaps;
        }

        public void RemoveRegion(Scene scene)
        {
            if (Enabled)
                return;

            scene.EventManager.OnRegisterCaps -= OnRegisterCaps;
            m_scene = null;
        }
        #endregion

        #region IEnvironmentModule
        public void ResetEnvironmentSettings(UUID regionUUID)
        {
            if (!Enabled)
                return;

            m_scene.SimulationDataService.RemoveRegionEnvironmentSettings(regionUUID);
        }
        #endregion

        #region Events
        private void OnRegisterCaps(UUID agentID, Caps caps)
        {
            //            m_log.DebugFormat("[{0}]: Register capability for agentID {1} in region {2}",
            //                Name, agentID, caps.RegionName);

            string capsPath = capsBase + UUID.Random();

            // Get handler
            caps.RegisterHandler(
                capsName,
                new RestStreamHandler(
                    "GET",
                    capsPath,
                    (request, path, param, httpRequest, httpResponse)
                     => GetEnvironmentSettings(request, path, param, agentID, caps),
                     capsName,
                     agentID.ToString()));

            // Set handler
            caps.HttpListener.AddStreamHandler(
                new RestStreamHandler(
                "POST",
                capsPath,
                (request, path, param, httpRequest, httpResponse)
                 => SetEnvironmentSettings(request, path, param, agentID, caps),
                 capsName,
                 agentID.ToString()));
        }
        #endregion

        private string GetEnvironmentSettings(string request, string path, string param,
              UUID agentID, Caps caps)
        {
            //            m_log.DebugFormat("[{0}]: Environment GET handle for agentID {1} in region {2}",
            //                Name, agentID, caps.RegionName);

            string env = String.Empty;

            try
            {
                env = m_scene.SimulationDataService.LoadRegionEnvironmentSettings(regionID);
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[{0}]: Unable to load environment settings for region {1}, Exception: {2} - {3}",
                    Name, caps.RegionName, e.Message, e.StackTrace);
            }

            if (String.IsNullOrEmpty(env))
                env = EnvironmentSettings.EmptySettings(UUID.Zero, regionID);

            return env;
        }

        private string SetEnvironmentSettings(string request, string path, string param,
                              UUID agentID, Caps caps)
        {

            //            m_log.DebugFormat("[{0}]: Environment SET handle from agentID {1} in region {2}",
            //                Name, agentID, caps.RegionName);

            setResponse.regionID = regionID;
            setResponse.success = false;

            if (!m_scene.Permissions.CanIssueEstateCommand(agentID, false))
            {
                setResponse.fail_reason = "Insufficient estate permissions, settings has not been saved.";
                return LLSDHelpers.SerialiseLLSDReply(setResponse);
            }

            try
            {
                m_scene.SimulationDataService.StoreRegionEnvironmentSettings(regionID, request);
                setResponse.success = true;

                m_log.InfoFormat("[{0}]: New Environment settings has been saved from agentID {1} in region {2}",
                    Name, agentID, caps.RegionName);
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[{0}]: Environment settings has not been saved for region {1}, Exception: {2} - {3}",
                    Name, caps.RegionName, e.Message, e.StackTrace);

                setResponse.success = false;
                setResponse.fail_reason = String.Format("Environment Set for region {0} has failed, settings has not been saved.", caps.RegionName);
            }

            return LLSDHelpers.SerialiseLLSDReply(setResponse);
        }
    }
}

