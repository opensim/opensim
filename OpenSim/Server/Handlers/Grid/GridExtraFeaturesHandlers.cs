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
using System.IO;
using System.Net;
using System.Reflection;
using System.Security;
using System.Text;
using log4net;
using Nini.Config;
using Nwc.XmlRpc;
using OpenSim.Framework;
using OpenSim.Framework.Servers.HttpServer;
using OpenMetaverse.StructuredData;


namespace OpenSim.Server.Handlers.Grid
{
    /// <summary>
    /// Grid extra features handlers.
    /// <para>Allows grid level configuration of OpenSimExtra items.</para>
    /// <para>Option to control region override of these settings.</para>
    /// </summary>
    public class GridExtraFeaturesHandlers
    {
        private static readonly ILog m_Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private Hashtable m_ExtraFeatures = new Hashtable();
        private bool m_AllowRegionOverride = true;

        public GridExtraFeaturesHandlers(IConfigSource configSource)
        {
            try
            {
                IConfig featuresCfg = configSource.Configs["GridExtraFeatures"];

                foreach( string key in featuresCfg.GetKeys())
                {
                    if(key != "AllowRegionOverride")
                    {
                        string value = featuresCfg.GetString(key);

                        // map the value to the viewer supported extra features
                        // add additional ones here as support is added in the viewer
                        // and place the configuration option in [GridExtraFeatures].
                        switch(key)
                        {
                            case "SearchServerURI":
                                m_ExtraFeatures["search-server-url"] = value;
                                break;
                            case "MapImageServerURI":
                                m_ExtraFeatures["map-server-url"] = value;
                                break;
                            case "DestinationGuideURI":
                                m_ExtraFeatures["destination-guide-url"] = value;
                                break;
                            case "ExportSupported":
                                m_ExtraFeatures["ExportSupported"] = value;          
                                break;
                            default:
                                m_Log.InfoFormat("{0} not yet supported.");
                                break;
                        }
                    }
                    else
                        m_AllowRegionOverride = featuresCfg.GetBoolean(key);
                }
            }
            catch (Exception)
            {
                m_Log.Warn("[GRID EXTRA FEATURES SERVICE]: Cannot get grid features from config source, allowing region override");
            }
        }

        public bool JsonGetGridFeaturesMethod(OSDMap json, ref JsonRpcResponse response)
        {
            OSDMap features = new OSDMap();
            OSDMap json_map = new OSDMap();
            
            foreach (string key in m_ExtraFeatures.Keys)
            {
                features[key] = OSD.FromString(m_ExtraFeatures[key].ToString());
            }

            json_map["extra_features"] = features;
            json_map["region_override"] = m_AllowRegionOverride.ToString();

            response.Result = json_map;

            return true;
        }
    }
}