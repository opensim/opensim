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
using log4net;
using Mono.Addins;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.OptionalModules.ViewerSupport
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "GodNamesModule")]
    public class GodNamesModule : ISharedRegionModule
    {
        // Infrastructure
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        // Configuration
        private static bool m_enabled = false;
        private static List<String> m_lastNames = new List<String>();
        private static List<String> m_fullNames = new List<String>();

        public void Initialise(IConfigSource config)
        {
            IConfig moduleConfig = config.Configs["GodNames"];

            if (moduleConfig == null) {
                return;
            }

            if (!moduleConfig.GetBoolean("Enabled", false)) {
                m_log.Info("[GODNAMES]: Addon is disabled");
                return;
            }

            m_log.Info("[GODNAMES]: Enabled");
            m_enabled = true;
            string conf_str = moduleConfig.GetString("FullNames", String.Empty);
            foreach (string strl in conf_str.Split(',')) {
                string strlan = strl.Trim(" \t".ToCharArray());
                m_log.DebugFormat("[GODNAMES]: Adding {0} as a God name", strlan);
                m_fullNames.Add(strlan);
            }

            conf_str = moduleConfig.GetString("Surnames", String.Empty);
            foreach (string strl in conf_str.Split(',')) {
                string strlan = strl.Trim(" \t".ToCharArray());
                m_log.DebugFormat("[GODNAMES]: Adding {0} as a God last name", strlan);
                m_lastNames.Add(strlan);
            }
        }

        public void AddRegion(Scene scene) {
            /*no op*/
        }

        public void RemoveRegion(Scene scene) {
            /*no op*/
        }

        public void PostInitialise() {
            /*no op*/
        }

        public void Close() {
            /*no op*/
        }

        public Type ReplaceableInterface {
            get { return null; }
        }

        public string Name {
            get { return "Godnames"; }
        }

        public bool IsSharedModule {
            get { return true; }
        }

        public virtual void RegionLoaded(Scene scene)
        {
            if (!m_enabled)
                return;

            ISimulatorFeaturesModule featuresModule = scene.RequestModuleInterface<ISimulatorFeaturesModule>();

            if (featuresModule != null)
                featuresModule.OnSimulatorFeaturesRequest += OnSimulatorFeaturesRequest;

        }

        private void OnSimulatorFeaturesRequest(UUID agentID, ref OSDMap features)
        {
            OSD namesmap = new OSDMap();
            if (features.ContainsKey("god_names"))
                namesmap = features["god_names"];
            else
                features["god_names"] = namesmap;

            OSDArray fnames = new OSDArray();
            foreach (string name in m_fullNames) {
                fnames.Add(name);
            }
            ((OSDMap)namesmap)["full_names"] = fnames;

            OSDArray lnames = new OSDArray();
            foreach (string name in m_lastNames) {
                lnames.Add(name);
            }
            ((OSDMap)namesmap)["last_names"] = lnames;
        }
    }
}
