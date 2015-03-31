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
using System.IO;
using System.Reflection;
using System.Text;
using System.Collections.Generic;
using System.Threading;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using OpenSim;
using OpenSim.Region;
using OpenSim.Region.Framework;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Framework;
//using OpenSim.Framework.Capabilities;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;
using Nini.Config;
using log4net;
using Mono.Addins;
using OSDMap = OpenMetaverse.StructuredData.OSDMap;
using TeleportFlags = OpenSim.Framework.Constants.TeleportFlags;

namespace OpenSim.Region.OptionalModules.ViewerSupport
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "SpecialUI")]
    public class SpecialUIModule : INonSharedRegionModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private const string VIEWER_SUPPORT_DIR = "ViewerSupport";

        private Scene m_scene;
        private SimulatorFeaturesHelper m_Helper;
        private bool m_Enabled;
        private int m_UserLevel;

        public string Name
        {
            get { return "SpecialUIModule"; }
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public void Initialise(IConfigSource config)
        {
            IConfig moduleConfig = config.Configs["SpecialUIModule"];
            if (moduleConfig != null)
            {
                m_Enabled = moduleConfig.GetBoolean("enabled", false);
                if (m_Enabled)
                {
                    m_UserLevel = moduleConfig.GetInt("UserLevel", 0);
                    m_log.Info("[SPECIAL UI]: SpecialUIModule enabled");
                }

            }
        }

        public void Close()
        {
        }

        public void AddRegion(Scene scene)
        {
            if (m_Enabled)
            {
                m_scene = scene;
            }
        }

        public void RegionLoaded(Scene scene)
        {
            if (m_Enabled)
            {
                IEntityTransferModule et = m_scene.RequestModuleInterface<IEntityTransferModule>();
                m_Helper = new SimulatorFeaturesHelper(scene, et);

                ISimulatorFeaturesModule featuresModule = m_scene.RequestModuleInterface<ISimulatorFeaturesModule>();
                if (featuresModule != null)
                    featuresModule.OnSimulatorFeaturesRequest += OnSimulatorFeaturesRequest;
            }
        }

        public void RemoveRegion(Scene scene)
        {
        }

        private void OnSimulatorFeaturesRequest(UUID agentID, ref OSDMap features)
        {
            m_log.DebugFormat("[SPECIAL UI]: OnSimulatorFeaturesRequest in {0}", m_scene.RegionInfo.RegionName);
            if (m_Helper.ShouldSend(agentID) && m_Helper.UserLevel(agentID) <= m_UserLevel)
            {
                OSDMap extrasMap;
                OSDMap specialUI = new OSDMap();
                using (StreamReader s = new StreamReader(Path.Combine(VIEWER_SUPPORT_DIR, "panel_toolbar.xml")))
                {
                    if (features.ContainsKey("OpenSimExtras"))
                        extrasMap = (OSDMap)features["OpenSimExtras"];
                    else
                    {
                        extrasMap = new OSDMap();
                        features["OpenSimExtras"] = extrasMap;
                    }

                    specialUI["toolbar"] = OSDMap.FromString(s.ReadToEnd());
                    extrasMap["special-ui"] = specialUI;
                }
                m_log.DebugFormat("[SPECIAL UI]: Sending panel_toolbar.xml in {0}", m_scene.RegionInfo.RegionName);

                if (Directory.Exists(Path.Combine(VIEWER_SUPPORT_DIR, "Floaters")))
                {
                    OSDMap floaters = new OSDMap();
                    uint n = 0;
                    foreach (String name in Directory.GetFiles(Path.Combine(VIEWER_SUPPORT_DIR, "Floaters"), "*.xml"))
                    {
                        using (StreamReader s = new StreamReader(name))
                        {
                            string simple_name = Path.GetFileNameWithoutExtension(name);
                            OSDMap floater = new OSDMap();
                            floaters[simple_name] = OSDMap.FromString(s.ReadToEnd());
                            n++;
                        }
                    }
                    specialUI["floaters"] = floaters;
                    m_log.DebugFormat("[SPECIAL UI]: Sending {0} floaters", n);
                }
            }
            else
                m_log.DebugFormat("[SPECIAL UI]: NOT Sending panel_toolbar.xml in {0}", m_scene.RegionInfo.RegionName);

        }

    }

}
