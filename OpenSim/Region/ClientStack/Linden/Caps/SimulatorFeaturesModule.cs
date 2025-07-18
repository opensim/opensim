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
using System.Net;
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
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public event SimulatorFeaturesRequestDelegate OnSimulatorFeaturesRequest;

        private Scene m_scene;

        /// <summary>
        /// Simulator features
        /// </summary>
        private readonly OSDMap m_features = new();

        private bool m_ExportSupported = false;

        private bool m_doScriptSyntax = true;

        static private readonly object m_scriptSyntaxLock = new();
        static private UUID m_scriptSyntaxID = UUID.Zero;
        static private byte[] m_scriptSyntaxXML = null;

        #region ISharedRegionModule Members

        public void Initialise(IConfigSource source)
        {
            IConfig config = source.Configs["SimulatorFeatures"];
            if (config != null)
            {
                m_ExportSupported = config.GetBoolean("ExportSupported", m_ExportSupported);
                m_doScriptSyntax = config.GetBoolean("ScriptSyntax", m_doScriptSyntax);
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
                m_features["AnimatedObjects"] = new OSDMap()
                {
                    ["AnimatedObjectMaxTris"] = OSD.FromInteger(150000),
                    ["MaxAgentAnimatedObjectAttachments"] = OSD.FromInteger(Constants.MaxAgentAnimatedObjectAttachments)
                };

                m_features["BakesOnMeshEnabled"] = true;

                if(m_doScriptSyntax && !m_scriptSyntaxID.IsZero())
                    m_features["LSLSyntaxId"] = OSD.FromUUID(m_scriptSyntaxID);

                m_features["MaxAgentAttachments"] = OSD.FromInteger(Constants.MaxAgentAttachments);
                m_features["MaxAgentGroups"] = OSD.FromInteger(Constants.MaxAgentGroups);
                m_features["MaxAgentGroupsBasic"] = OSD.FromInteger(Constants.MaxAgentGroups);
                m_features["MaxAgentGroupsPremium"] = OSD.FromInteger(Constants.MaxAgentGroups);

                m_features["MaxEstateAccessIds"] = OSD.FromInteger(Constants.MaxEstateAccessIds);
                m_features["MaxEstateManagers"] = OSD.FromInteger(Constants.MaxEstateManagers);

                m_features["MaxTextureResolution"] = OSD.FromInteger(Constants.MaxTextureResolution);

                m_features["MaxProfilePicks"] = OSD.FromInteger(Constants.MaxProfilePicks);

                m_features["MeshRezEnabled"] = true;
                m_features["MeshUploadEnabled"] = true;
                m_features["MeshXferEnabled"] = true;

                m_features["MirrorsEnabled"] = true;

                m_features["PhysicsMaterialsEnabled"] = true;

                m_features["PhysicsShapeTypes"] = new OSDMap()
                {
                    ["convex"] = true,
                    ["none"] = true,
                    ["prim"] = true
                };

                // Extra information for viewers that want to use it
                OSDMap extrasMap = m_features.TryGetValue("OpenSimExtras", out OSD oe) ? oe as OSDMap : new OSDMap();

                extrasMap["AvatarSkeleton"] = true;
                extrasMap["AnimationSet"] = true;

                extrasMap["MinSimHeight"] = Constants.MinSimulationHeight;
                extrasMap["MaxSimHeight"] = Constants.MaxSimulationHeight;
                extrasMap["MinHeightmap"] = Constants.MinTerrainHeightmap;
                extrasMap["MaxHeightmap"] = Constants.MaxTerrainHeightmap;

                if (m_ExportSupported)
                    extrasMap["ExportSupported"] = true;

                m_features["OpenSimExtras"] = extrasMap;
            }
        }

        public void RegisterCaps(UUID agentID, Caps caps)
        {
            caps.RegisterSimpleHandler("SimulatorFeatures",
                new SimpleStreamHandler("/" + UUID.Random(),
                    delegate (IOSHttpRequest request, IOSHttpResponse response)
                    {
                        HandleSimulatorFeaturesRequest(request, response, caps);
                    }));

            if (m_doScriptSyntax && !m_scriptSyntaxID.IsZero() && m_scriptSyntaxXML != null)
            {
                caps.RegisterSimpleHandler("LSLSyntax",
                    new SimpleStreamHandler("/" + UUID.Random(), HandleSyntaxRequest));
            }
        }

        public void AddFeature(string name, OSD value)
        {
            lock (m_features)
                m_features[name] = value;
        }

        public void AddOpenSimExtraFeature(string name, OSD value)
        {
            lock (m_features)
            {
                OSDMap extrasMap;
                if (m_features.TryGetValue("OpenSimExtras", out OSD extra))
                    extrasMap = extra as OSDMap;
                else
                {
                    extrasMap = new OSDMap();
                    m_features["OpenSimExtras"] = extrasMap;
                }
                extrasMap[name] = value;
            }
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

        public bool TryGetOpenSimExtraFeature(string name, out OSD value)
        {
            value = null;
            lock (m_features)
            {
                if (m_features.TryGetValue("OpenSimExtras", out OSD extra) && extra is OSDMap exm)
                    return exm.TryGetValue(name, out value);
            }
            return false;
        }
        public bool OpenSimExtraFeatureContains(string name)
        {
            lock (m_features)
            {
                if (m_features.TryGetValue("OpenSimExtras", out OSD extra) && extra is OSDMap exm)
                    return exm.ContainsKey(name);
            }
            return false;
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
            OSD copy = OSDParser.DeserializeLLSDXml(OSDParser.SerializeLLSDXmlToBytes(m_features));

            return (OSDMap)copy;
        }

        private void HandleSimulatorFeaturesRequest(IOSHttpRequest request, IOSHttpResponse response, Caps caps)
        {
            // m_log.DebugFormat("[SIMULATOR FEATURES MODULE]: SimulatorFeatures request");

            if (request.HttpMethod != "GET")
            {
                response.StatusCode = (int)HttpStatusCode.NotFound;
                return;
            }

            OSDMap copy = DeepCopy();

            if ((caps.Flags & Caps.CapsFlags.TPBR) != 0)
            {
                copy["PBRMaterialSwatchEnabled"] = true;
                copy["PBRTerrainEnabled"] = true;
            }

            // Let's add the agentID to the destination guide, if it is expecting that.
            if(copy.TryGetValue("OpenSimExtras", out OSD oe))
            {
                if(((OSDMap)oe).TryGetValue("destination-guide-url", out OSD dgl))
                {
                    ((OSDMap)oe)["destination-guide-url"] = Replace(dgl.AsString(), "[USERID]", caps.AgentID.ToString());
                }
            }

            if(OnSimulatorFeaturesRequest != null)
            {
                foreach(SimulatorFeaturesRequestDelegate sd in OnSimulatorFeaturesRequest.GetInvocationList())
                try
                {
                    sd?.Invoke(caps.AgentID, ref copy);
                }
                catch { }
            }

            //Send back data
            response.RawBuffer = OSDParser.SerializeLLSDXmlToBytes(copy);
            response.StatusCode = (int)HttpStatusCode.OK;
        }

        private void HandleSyntaxRequest(IOSHttpRequest request, IOSHttpResponse response)
        {
            if (request.HttpMethod != "GET" || m_scriptSyntaxXML == null)
            {
                response.StatusCode = (int)HttpStatusCode.NotFound;
                return;
            }
            response.RawBuffer = m_scriptSyntaxXML;
            response.StatusCode = (int)HttpStatusCode.OK;
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

            GridInfo ginfo = scene.SceneGridInfo;
            lock (m_features)
            {
                OSDMap extrasMap;
                if (m_features.TryGetValue("OpenSimExtras", out OSD extra))
                    extrasMap = extra as OSDMap;
                else
                {
                    extrasMap = new OSDMap();
                }

                foreach (string key in extraFeatures.Keys)
                {
                    string val = (string)extraFeatures[key];
                    switch(key)
                    {
                        case "GridName":
                            ginfo.GridName = val;
                            break;
                        case "GridNick":
                            ginfo.GridNick = val;
                            break;
                        case "GridURL":
                            ginfo.GridUrl = val;
                            break;
                        case "GridURLAlias":
                            string[] vals = val.Split(',');
                            if(vals.Length > 0)
                                ginfo.GridUrlAlias = vals;
                            break;
                        case "search-server-url":
                            ginfo.SearchURL = val;
                            break;
                        case "destination-guide-url":
                            ginfo.DestinationGuideURL = val;
                            break;
                        case "currency-base-uri":
                            // keep this local to avoid issues with diferent modules
                            // ginfo.EconomyURL = val;
                            break;
                        default:
                            if (key == "ExportSupported")
                            {
                                _ = bool.TryParse(val, out m_ExportSupported);
                                extrasMap[key] = m_ExportSupported;
                            }
                            else
                                extrasMap[key] = val;
                            break;
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
                if(!m_doScriptSyntax || !m_scriptSyntaxID.IsZero())
                    return;

                if(!File.Exists("ScriptSyntax.xml"))
                    return;

                try
                {
                    using (StreamReader sr = File.OpenText("ScriptSyntax.xml"))
                    {
                        StringBuilder sb = new(400*1024);
                        char[] trimc = new char[] {' ','\t', '\n', '\r'};

                        string s = sr.ReadLine();
                        if(s is null)
                            return;
                        s = s.Trim(trimc);

                        if(!UUID.TryParse(s, out UUID id))
                            return;

                        while ((s = sr.ReadLine()) is not null)
                        {
                            s = s.Trim(trimc);
                            if (string.IsNullOrEmpty(s) || s.StartsWith("<!--"))
                                continue;
                            sb.Append(s);
                        }
                        m_scriptSyntaxXML = Util.UTF8.GetBytes(sb.ToString());
                        m_scriptSyntaxID = id;
                    }
                }
                catch
                {
                    m_log.Error("[SIMULATOR FEATURES MODULE] fail read ScriptSyntax.xml file");
                    m_scriptSyntaxID = UUID.Zero;
                    m_scriptSyntaxXML = null;
                }
            }
        }
    }
}
