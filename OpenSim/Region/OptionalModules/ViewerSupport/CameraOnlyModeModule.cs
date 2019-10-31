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
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "CameraOnlyMode")]
    public class CameraOnlyModeModule : INonSharedRegionModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private Scene m_scene;
        private SimulatorFeaturesHelper m_Helper;
        private bool m_Enabled;
        private int m_UserLevel;

        public string Name
        {
            get { return "CameraOnlyModeModule"; }
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public void Initialise(IConfigSource config)
        {
            IConfig moduleConfig = config.Configs["CameraOnlyModeModule"];
            if (moduleConfig != null)
            {
                m_Enabled = moduleConfig.GetBoolean("enabled", false);
                if (m_Enabled)
                {
                    m_UserLevel = moduleConfig.GetInt("UserLevel", 0);
                    m_log.Info("[CAMERA-ONLY MODE]: CameraOnlyModeModule enabled");
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
                //m_scene.EventManager.OnMakeRootAgent += (OnMakeRootAgent);
            }
        }

        //private void OnMakeRootAgent(ScenePresence obj)
        //{
        //    throw new NotImplementedException();
        //}

        public void RegionLoaded(Scene scene)
        {
            if (m_Enabled)
            {
                m_Helper = new SimulatorFeaturesHelper(scene);

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
            if (!m_Enabled)
                return;

            m_log.DebugFormat("[CAMERA-ONLY MODE]: OnSimulatorFeaturesRequest in {0}", m_scene.RegionInfo.RegionName);
            if (m_Helper.UserLevel(agentID) <= m_UserLevel)
            {
                OSDMap extrasMap;
                if (features.ContainsKey("OpenSimExtras"))
                {
                    extrasMap = (OSDMap)features["OpenSimExtras"];
                }
                else
                {
                    extrasMap = new OSDMap();
                    features["OpenSimExtras"] = extrasMap;
                }
                extrasMap["camera-only-mode"] = OSDMap.FromString("true");
                m_log.DebugFormat("[CAMERA-ONLY MODE]: Sent in {0}", m_scene.RegionInfo.RegionName);
            }
            else
                m_log.DebugFormat("[CAMERA-ONLY MODE]: NOT Sending camera-only-mode in {0}", m_scene.RegionInfo.RegionName);
        }

        private void DetachAttachments(UUID agentID)
        {
            ScenePresence sp = m_scene.GetScenePresence(agentID);
            if ((sp.TeleportFlags & TeleportFlags.ViaLogin) != 0)
                // Wait a little, cos there's weird stuff going on at  login related to
                // the Current Outfit Folder
                Thread.Sleep(8000);

            if (sp != null && m_scene.AttachmentsModule != null)
            {
                List<SceneObjectGroup> attachs = sp.GetAttachments();
                if (attachs != null && attachs.Count > 0)
                {
                    foreach (SceneObjectGroup sog in attachs)
                    {
                        m_log.DebugFormat("[CAMERA-ONLY MODE]: Forcibly detaching attach {0} from {1} in {2}",
                            sog.Name, sp.Name, m_scene.RegionInfo.RegionName);

                        m_scene.AttachmentsModule.DetachSingleAttachmentToInv(sp, sog);
                    }
                }
            }
        }

    }

}
