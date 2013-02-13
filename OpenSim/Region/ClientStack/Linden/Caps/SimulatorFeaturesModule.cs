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
using System.Reflection;
using log4net;
using Nini.Config;
using Mono.Addins;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using OpenSim.Framework;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;
using Caps = OpenSim.Framework.Capabilities.Caps;

namespace OpenSim.Region.ClientStack.Linden
{
    /// <summary>
    /// SimulatorFeatures capability.
    /// </summary>
    /// <remarks>
    /// This is required for uploading Mesh.
    /// Since is accepts an open-ended response, we also send more information
    /// for viewers that care to interpret it.
    /// 
    /// NOTE: Part of this code was adapted from the Aurora project, specifically
    /// the normal part of the response in the capability handler.
    /// </remarks>
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "SimulatorFeaturesModule")]
    public class SimulatorFeaturesModule : ISharedRegionModule, ISimulatorFeaturesModule
    {
//        private static readonly ILog m_log =
//            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private Scene m_scene;

        /// <summary>
        /// Simulator features
        /// </summary>
        private OSDMap m_features = new OSDMap();

        private string m_MapImageServerURL = string.Empty;
        private string m_SearchURL = string.Empty;

        #region ISharedRegionModule Members

        public void Initialise(IConfigSource source)
        {
            IConfig config = source.Configs["SimulatorFeatures"];
            if (config != null)
            {
                m_MapImageServerURL = config.GetString("MapImageServerURI", string.Empty);
                if (m_MapImageServerURL != string.Empty)
                {
                    m_MapImageServerURL = m_MapImageServerURL.Trim();
                    if (!m_MapImageServerURL.EndsWith("/"))
                        m_MapImageServerURL = m_MapImageServerURL + "/";
                }
    
                m_SearchURL = config.GetString("SearchServerURI", string.Empty);
            }

            AddDefaultFeatures();
        }

        public void AddRegion(Scene s)
        {
            m_scene = s;
            m_scene.EventManager.OnRegisterCaps += RegisterCaps;

            m_scene.RegisterModuleInterface<ISimulatorFeaturesModule>(this);
        }

        public void RemoveRegion(Scene s)
        {
            m_scene.EventManager.OnRegisterCaps -= RegisterCaps;
        }

        public void RegionLoaded(Scene s)
        {
        }

        public void PostInitialise()
        {
        }

        public void Close() { }

        public string Name { get { return "SimulatorFeaturesModule"; } }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        #endregion

        /// <summary>
        /// Add default features
        /// </summary>
        /// <remarks>
        /// TODO: These should be added from other modules rather than hardcoded.
        /// </remarks>
        private void AddDefaultFeatures()
        {
            lock (m_features)
            {
                m_features["MeshRezEnabled"] = true;
                m_features["MeshUploadEnabled"] = true;
                m_features["MeshXferEnabled"] = true;
                m_features["PhysicsMaterialsEnabled"] = true;
    
                OSDMap typesMap = new OSDMap();
                typesMap["convex"] = true;
                typesMap["none"] = true;
                typesMap["prim"] = true;
                m_features["PhysicsShapeTypes"] = typesMap;
    
                // Extra information for viewers that want to use it
                OSDMap gridServicesMap = new OSDMap();
                if (m_MapImageServerURL != string.Empty)
                    gridServicesMap["map-server-url"] = m_MapImageServerURL;
                if (m_SearchURL != string.Empty)
                    gridServicesMap["search"] = m_SearchURL;
                m_features["GridServices"] = gridServicesMap;
            }
        }

        public void RegisterCaps(UUID agentID, Caps caps)
        {
            IRequestHandler reqHandler
                = new RestHTTPHandler(
                    "GET", "/CAPS/" + UUID.Random(),
                    HandleSimulatorFeaturesRequest, "SimulatorFeatures", agentID.ToString());

            caps.RegisterHandler("SimulatorFeatures", reqHandler);
        }

        public void AddFeature(string name, OSD value)
        {
            lock (m_features)
                m_features[name] = value;
        }

        public bool RemoveFeature(string name)
        {
            lock (m_features)
                return m_features.Remove(name);
        }

        public bool TryGetFeature(string name, out OSD value)
        {
            lock (m_features)
                return m_features.TryGetValue(name, out value);
        }

        public OSDMap GetFeatures()
        {
            lock (m_features)
                return new OSDMap(m_features);
        }

        private Hashtable HandleSimulatorFeaturesRequest(Hashtable mDhttpMethod)
        {
//            m_log.DebugFormat("[SIMULATOR FEATURES MODULE]: SimulatorFeatures request");

            //Send back data
            Hashtable responsedata = new Hashtable();
            responsedata["int_response_code"] = 200; 
            responsedata["content_type"] = "text/plain";
            responsedata["keepalive"] = false;

            lock (m_features)
                responsedata["str_response_string"] = OSDParser.SerializeLLSDXmlString(m_features);

            return responsedata;
        }
    }
}
