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
using System.IO;
using System.Web;

using log4net;
using Nini.Config;
using Mono.Addins;

using OpenMetaverse;
using OpenMetaverse.StructuredData;

using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;
using Caps = OpenSim.Framework.Capabilities.Caps;

namespace OpenSim.Services.Connectors.SimianGrid
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "SimianExternalCapsModule")]
    public class SimianExternalCapsModule : INonSharedRegionModule, IExternalCapsModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private bool m_enabled = true;
        private Scene m_scene;
        private String m_simianURL;

#region IRegionModule Members

        public string Name
        {
            get { return this.GetType().Name; }
        }

        public void Initialise(IConfigSource config)
        {
            try
            {
                IConfig m_config;

                if ((m_config = config.Configs["SimianExternalCaps"]) != null)
                {
                    m_enabled = m_config.GetBoolean("Enabled", m_enabled);
                    if ((m_config = config.Configs["SimianGrid"]) != null)
                    {
                        m_simianURL = m_config.GetString("SimianServiceURL");
                        if (String.IsNullOrEmpty(m_simianURL))
                        {
                            //m_log.DebugFormat("[SimianGrid] service URL is not defined");
                            m_enabled = false;
                            return;
                        }
                    }
                }
                else
                    m_enabled = false;
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[SimianExternalCaps] initialization error: {0}",e.Message);
                return;
            }
        }

        public void PostInitialise() { }
        public void Close() { }

        public void AddRegion(Scene scene)
        {
            if (! m_enabled)
                return;

            m_scene = scene;
            m_scene.RegisterModuleInterface<IExternalCapsModule>(this);
        }

        public void RemoveRegion(Scene scene)
        {
            if (! m_enabled)
                return;

            m_scene.EventManager.OnRegisterCaps -= RegisterCapsEventHandler;
            m_scene.EventManager.OnDeregisterCaps -= DeregisterCapsEventHandler;
        }

        public void RegionLoaded(Scene scene)
        {
            if (! m_enabled)
                return;

            m_scene.EventManager.OnRegisterCaps += RegisterCapsEventHandler;
            m_scene.EventManager.OnDeregisterCaps += DeregisterCapsEventHandler;
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

#endregion

#region IExternalCapsModule
        // Eg http://grid.sciencesim.com/GridPublic/%CAP%/%OP%/"
        public bool RegisterExternalUserCapsHandler(UUID agentID, Caps caps, String capName, String urlSkel)
        {
            UUID cap = UUID.Random();

            // Call to simian to register the cap we generated
            // NameValueCollection requestArgs = new NameValueCollection
            // {
            //     { "RequestMethod", "AddCapability" },
            //     { "Resource", "user" },
            //     { "Expiration", 0 },
            //     { "OwnerID", agentID.ToString() },
            //     { "CapabilityID", cap.ToString() }
            // };

            // OSDMap response = SimianGrid.PostToService(m_simianURL, requestArgs);

            Dictionary<String,String> subs = new Dictionary<String,String>();
            subs["%OP%"] = capName;
            subs["%USR%"] = agentID.ToString();
            subs["%CAP%"] = cap.ToString();
            subs["%SIM%"] = m_scene.RegionInfo.RegionID.ToString();

            caps.RegisterHandler(capName,ExpandSkeletonURL(urlSkel,subs));
            return true;
        }

#endregion

#region EventHandlers
        public void RegisterCapsEventHandler(UUID agentID, Caps caps) { }
        public void DeregisterCapsEventHandler(UUID agentID, Caps caps) { }
#endregion

        private String ExpandSkeletonURL(String urlSkel, Dictionary<String,String> subs)
        {
            String result = urlSkel;

            foreach (KeyValuePair<String,String> kvp in subs)
            {
                result = result.Replace(kvp.Key,kvp.Value);
            }

            return result;
        }
    }
}