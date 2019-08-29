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
using System.IO;
using System.Reflection;
using System.Text;
using log4net;
using Nini.Config;
using Mono.Addins;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using OpenSim.Framework;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
// using OpenSim.Services.Interfaces;
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
    public class SimulatorFeaturesModule : INonSharedRegionModule, ISimulatorFeaturesModule
    {
        private static readonly ILog m_log =
            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public event SimulatorFeaturesRequestDelegate OnSimulatorFeaturesRequest;

        private Scene m_scene;

        /// <summary>
        /// Simulator features
        /// </summary>
        private OSDMap m_features = new OSDMap();

        private string m_SearchURL = string.Empty;
        private string m_DestinationGuideURL = string.Empty;
        private bool m_ExportSupported = false;
        private string m_GridName = string.Empty;
        private string m_GridURL = string.Empty;

        private bool m_doScriptSyntax;
        private bool m_BoMSupported = false;

        static private object m_scriptSyntaxLock = new object();
        static private UUID m_scriptSyntaxID = UUID.Zero;
        static private string m_scriptSyntaxXML;

        #region ISharedRegionModule Members

        public void Initialise(IConfigSource source)
        {
            IConfig config = source.Configs["SimulatorFeatures"];
            m_doScriptSyntax = true;
            if (config != null)
            {
                //
                // All this is obsolete since getting these features from the grid service!!
                // Will be removed after the next release
                //
                m_SearchURL = config.GetString("SearchServerURI", m_SearchURL);

                m_DestinationGuideURL = config.GetString ("DestinationGuideURI", m_DestinationGuideURL);

                if (m_DestinationGuideURL == string.Empty) // Make this consistent with the variable in the LoginService config
                    m_DestinationGuideURL = config.GetString("DestinationGuide", m_DestinationGuideURL);

                m_ExportSupported = config.GetBoolean("ExportSupported", m_ExportSupported);

                m_GridURL = Util.GetConfigVarFromSections<string>(
                    source, "GatekeeperURI", new string[] { "Startup", "Hypergrid", "SimulatorFeatures" }, String.Empty);

                m_GridName = config.GetString("GridName", string.Empty);
                if (m_GridName == string.Empty)
                    m_GridName = Util.GetConfigVarFromSections<string>(
                        source, "gridname", new string[] { "GridInfo", "SimulatorFeatures" }, String.Empty);
                m_doScriptSyntax = config.GetBoolean("ScriptSyntax", m_doScriptSyntax);
                m_BoMSupported = config.GetBoolean("BoMSupported", m_BoMSupported);

            }

            ReadScriptSyntax();
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
            GetGridExtraFeatures(s);
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

                if(m_BoMSupported)
                    m_features["BakesOnMeshEnabled"] = true;

                m_features["PhysicsMaterialsEnabled"] = true;

                OSDMap typesMap = new OSDMap();
                typesMap["convex"] = true;
                typesMap["none"] = true;
                typesMap["prim"] = true;
                m_features["PhysicsShapeTypes"] = typesMap;

                if(m_doScriptSyntax && m_scriptSyntaxID != UUID.Zero)
                    m_features["LSLSyntaxId"] = OSD.FromUUID(m_scriptSyntaxID);

                OSDMap meshAnim = new OSDMap();
                meshAnim["AnimatedObjectMaxTris"] = OSD.FromInteger(10000);
                meshAnim["MaxAgentAnimatedObjectAttachments"] = OSD.FromInteger(2);
                m_features["AnimatedObjects"] = meshAnim;

                // Extra information for viewers that want to use it
                // TODO: Take these out of here into their respective modules, like map-server-url
                OSDMap extrasMap;
                if(m_features.ContainsKey("OpenSimExtras"))
                {
                    extrasMap = (OSDMap)m_features["OpenSimExtras"];
                }
                else
                    extrasMap = new OSDMap();

                extrasMap["AvatarSkeleton"] = true;
                extrasMap["AnimationSet"] = true;

                // TODO: Take these out of here into their respective modules, like map-server-url
                if (m_SearchURL != string.Empty)
                    extrasMap["search-server-url"] = m_SearchURL;
                if (!string.IsNullOrEmpty(m_DestinationGuideURL))
                    extrasMap["destination-guide-url"] = m_DestinationGuideURL;
                if (m_ExportSupported)
                    extrasMap["ExportSupported"] = true;
                if (m_GridURL != string.Empty)
                    extrasMap["GridURL"] = m_GridURL;
                if (m_GridName != string.Empty)
                    extrasMap["GridName"] = m_GridName;

                if (extrasMap.Count > 0)
                    m_features["OpenSimExtras"] = extrasMap;
            }
        }

        public void RegisterCaps(UUID agentID, Caps caps)
        {
            IRequestHandler reqHandler = new RestHTTPHandler(
                    "GET", "/CAPS/" + UUID.Random(),
                    x => { return HandleSimulatorFeaturesRequest(x, agentID); },
                    "SimulatorFeatures", agentID.ToString());
            caps.RegisterHandler("SimulatorFeatures", reqHandler);

            if (m_doScriptSyntax && m_scriptSyntaxID != UUID.Zero && !String.IsNullOrEmpty(m_scriptSyntaxXML))
            {
                IRequestHandler sreqHandler = new RestHTTPHandler(
                        "GET", "/CAPS/" + UUID.Random(),
                        x => { return HandleSyntaxRequest(x, agentID); },
                        "LSLSyntax", agentID.ToString());
                caps.RegisterHandler("LSLSyntax", sreqHandler);
            }
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

        private OSDMap DeepCopy()
        {
            // This isn't the cheapest way of doing this but the rate
            // of occurrence is low (on sim entry only) and it's a sure
            // way to get a true deep copy.
            OSD copy = OSDParser.DeserializeLLSDXml(OSDParser.SerializeLLSDXmlString(m_features));

            return (OSDMap)copy;
        }

        private Hashtable HandleSimulatorFeaturesRequest(Hashtable mDhttpMethod, UUID agentID)
        {
            //            m_log.DebugFormat("[SIMULATOR FEATURES MODULE]: SimulatorFeatures request");

            OSDMap copy = DeepCopy();

            // Let's add the agentID to the destination guide, if it is expecting that.
            if (copy.ContainsKey("OpenSimExtras") && ((OSDMap)(copy["OpenSimExtras"])).ContainsKey("destination-guide-url"))
                ((OSDMap)copy["OpenSimExtras"])["destination-guide-url"] = Replace(((OSDMap)copy["OpenSimExtras"])["destination-guide-url"], "[USERID]", agentID.ToString());

            SimulatorFeaturesRequestDelegate handlerOnSimulatorFeaturesRequest = OnSimulatorFeaturesRequest;

            if (handlerOnSimulatorFeaturesRequest != null)
                handlerOnSimulatorFeaturesRequest(agentID, ref copy);

            //Send back data
            Hashtable responsedata = new Hashtable();
            responsedata["int_response_code"] = 200;
            responsedata["content_type"] = "text/plain";

            responsedata["str_response_string"] = OSDParser.SerializeLLSDXmlString(copy);

            return responsedata;
        }

        private Hashtable HandleSyntaxRequest(Hashtable mDhttpMethod, UUID agentID)
        {
            Hashtable responsedata = new Hashtable();
            responsedata["int_response_code"] = 200;
            responsedata["str_response_string"] = m_scriptSyntaxXML;
            return responsedata;
        }

        /// <summary>
        /// Gets the grid extra features.
        /// </summary>
        /// <param name='featuresURI'>
        /// The URI Robust uses to handle the get_extra_features request
        /// </param>

        private void GetGridExtraFeatures(Scene scene)
        {
            Dictionary<string, object> extraFeatures = scene.GridService.GetExtraFeatures();
            if (extraFeatures.ContainsKey("Result") && extraFeatures["Result"] != null && extraFeatures["Result"].ToString() == "Failure")
            {
                m_log.WarnFormat("[SIMULATOR FEATURES MODULE]: Unable to retrieve grid-wide features");
                return;
            }

            lock (m_features)
            {
                OSDMap extrasMap = new OSDMap();

                foreach(string key in extraFeatures.Keys)
                {
                    extrasMap[key] = (string)extraFeatures[key];

                    if (key == "ExportSupported")
                    {
                        bool.TryParse(extraFeatures[key].ToString(), out m_ExportSupported);
                    }
                }
                m_features["OpenSimExtras"] = extrasMap;

            }
        }

        private string Replace(string url, string substring, string replacement)
        {
            if (!String.IsNullOrEmpty(url) && url.Contains(substring))
                return url.Replace(substring, replacement);

            return url;
        }

        private void ReadScriptSyntax()
        {
            lock(m_scriptSyntaxLock)
            {
                if(!m_doScriptSyntax || m_scriptSyntaxID != UUID.Zero)
                    return;

                if(!File.Exists("ScriptSyntax.xml"))
                    return;

                try
                {
                    using (StreamReader sr = File.OpenText("ScriptSyntax.xml"))
                    {
                        StringBuilder sb = new StringBuilder(400*1024);

                        string s="";
                        char[] trimc = new char[] {' ','\t', '\n', '\r'};

                        s = sr.ReadLine();
                        if(s == null)
                            return;
                        s = s.Trim(trimc);
                        UUID id;
                        if(!UUID.TryParse(s,out id))
                            return;

                        while ((s = sr.ReadLine()) != null)
                        {
                            s = s.Trim(trimc);
                            if (String.IsNullOrEmpty(s) || s.StartsWith("<!--"))
                                continue;
                            sb.Append(s);
                        }
                        m_scriptSyntaxXML = sb.ToString();
                        m_scriptSyntaxID = id;
                    }
                }
                catch
                {
                    m_log.Error("[SIMULATOR FEATURES MODULE] fail read ScriptSyntax.xml file");
                    m_scriptSyntaxID = UUID.Zero;
                    m_scriptSyntaxXML = "";
                }
            }
        }
    }
}
