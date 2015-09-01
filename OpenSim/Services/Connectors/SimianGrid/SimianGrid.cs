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
using System.Reflection;
using log4net;
using Mono.Addins;
using Nini.Config;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;
using OpenMetaverse;
using OpenMetaverse.StructuredData;

[assembly: Addin("SimianGrid", OpenSim.VersionInfo.VersionNumber)]
[assembly: AddinDependency("OpenSim.Region.Framework", OpenSim.VersionInfo.VersionNumber)]

namespace OpenSim.Services.Connectors.SimianGrid
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "SimianExternalCapsModule")]
    public class SimianGrid : ISharedRegionModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private IConfig m_config = null;

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
                m_config = config.Configs["SimianGrid"];
               
                if (m_config != null)
                {
                    m_simianURL = m_config.GetString("SimianServiceURL");
                    if (String.IsNullOrEmpty(m_simianURL))
                    {
                        // m_log.DebugFormat("[SimianGrid] service URL is not defined");
                        return;
                    }
                    
                    InitialiseSimCap();
                    SimulatorCapability = SimulatorCapability.Trim();
                    m_log.InfoFormat("[SimianExternalCaps] using {0} as simulator capability",SimulatorCapability);
                }
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[SimianExternalCaps] initialization error: {0}",e.Message);
                return;
            }
        }

        public void PostInitialise() { }
        public void Close() { }
        public void AddRegion(Scene scene) { }
        public void RemoveRegion(Scene scene) { }
        public void RegionLoaded(Scene scene) { }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        ///<summary>
        /// Try a variety of methods for finding the simian simulator capability; first check the
        /// configuration itself, then look for a file that contains the cap, then finally look 
        /// for an environment variable that contains it.
        ///</summary>
        private void InitialiseSimCap()
        {
            if (m_config.Contains("SimulatorCapability"))
            {
                SimulatorCapability = m_config.GetString("SimulatorCapability");
                return;
            }
            
            if (m_config.Contains("SimulatorCapabilityFile"))
            {
                String filename = m_config.GetString("SimulatorCapabilityFile");
                if (System.IO.File.Exists(filename))
                {
                    SimulatorCapability = System.IO.File.ReadAllText(filename);
                    return;
                }
            }
            
            if (m_config.Contains("SimulatorCapabilityVariable"))
            {
                String envname = m_config.GetString("SimulatorCapabilityVariable");
                String envvalue = System.Environment.GetEnvironmentVariable(envname);
                if (envvalue != null)
                {
                    SimulatorCapability = envvalue;
                    return;
                }
            }

            m_log.WarnFormat("[SimianExternalCaps] no method specified for simulator capability");
        }
        
#endregion

        public static String SimulatorCapability = UUID.Zero.ToString();
        public static OSDMap PostToService(string url, NameValueCollection data)
        {
            data["cap"] = SimulatorCapability;
            return WebUtil.PostToService(url, data);
        }
    }
}