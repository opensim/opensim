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
using System.Xml.Serialization;
using OpenMetaverse;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.ApplicationPlugins.Rest.Regions
{
    public partial class RestRegionPlugin : RestPlugin
    {
        #region GET methods
        public string GetHandler(string request, string path, string param,
                                 OSHttpRequest httpRequest, OSHttpResponse httpResponse)
        {
            // foreach (string h in httpRequest.Headers.AllKeys)
            //     foreach (string v in httpRequest.Headers.GetValues(h))
            //         m_log.DebugFormat("{0} IsGod: {1} -> {2}", MsgID, h, v);

            MsgID = RequestID;
            m_log.DebugFormat("{0} GET path {1} param {2}", MsgID, path, param);

            try
            {
                // param empty: regions list
                if (String.IsNullOrEmpty(param)) return GetHandlerRegions(httpResponse);

                // param not empty: specific region
                return GetHandlerRegion(httpResponse, param);
            }
            catch (Exception e)
            {
                return Failure(httpResponse, OSHttpStatusCode.ServerErrorInternalError, "GET", e);
            }
        }

        public string GetHandlerRegions(OSHttpResponse httpResponse)
        {
            RestXmlWriter rxw = new RestXmlWriter(new StringWriter());

            rxw.WriteStartElement(String.Empty, "regions", String.Empty);
            foreach (Scene s in App.SceneManager.Scenes)
            {
                rxw.WriteStartElement(String.Empty, "uuid", String.Empty);
                rxw.WriteString(s.RegionInfo.RegionID.ToString());
                rxw.WriteEndElement();
            }
            rxw.WriteEndElement();

            return rxw.ToString();
        }

        protected string ShortRegionInfo(string key, string value)
        {
            RestXmlWriter rxw = new RestXmlWriter(new StringWriter());

            if (String.IsNullOrEmpty(value) ||
                String.IsNullOrEmpty(key)) return null;

            rxw.WriteStartElement(String.Empty, "region", String.Empty);
            rxw.WriteStartElement(String.Empty, key, String.Empty);
            rxw.WriteString(value);
            rxw.WriteEndDocument();

            return rxw.ToString();
        }

        public string GetHandlerRegion(OSHttpResponse httpResponse, string param)
        {
            // be resilient and don't get confused by a terminating '/'
            param = param.TrimEnd(new char[]{'/'});
            string[] comps = param.Split('/');
            UUID regionID = (UUID)comps[0];

            m_log.DebugFormat("{0} GET region UUID {1}", MsgID, regionID.ToString());

            if (UUID.Zero == regionID) throw new Exception("missing region ID");

            Scene scene = null;
            App.SceneManager.TryGetScene(regionID, out scene);
            if (null == scene) return Failure(httpResponse, OSHttpStatusCode.ClientErrorNotFound,
                                              "GET", "cannot find region {0}", regionID.ToString());

            RegionDetails details = new RegionDetails(scene.RegionInfo);

            // m_log.DebugFormat("{0} GET comps {1}", MsgID, comps.Length);
            // for (int i = 0; i < comps.Length; i++)  m_log.DebugFormat("{0} GET comps[{1}] >{2}<", MsgID, i, comps[i]);

            if (1 == comps.Length)
            {
                // complete region details requested
                RestXmlWriter rxw = new RestXmlWriter(new StringWriter());
                XmlSerializer xs = new XmlSerializer(typeof(RegionDetails));
                xs.Serialize(rxw, details, _xmlNs);
                return rxw.ToString();
            }

            if (2 == comps.Length)
            {
                string resp = ShortRegionInfo(comps[1], details[comps[1]]);
                if (null != resp) return resp;

                // m_log.DebugFormat("{0} GET comps advanced: >{1}<", MsgID, comps[1]);

                // check for {terrain,stats,prims}
                switch (comps[1].ToLower())
                {
                case "terrain":
                    return RegionTerrain(httpResponse, scene);

                case "stats":
                    return RegionStats(httpResponse, scene);

                case "prims":
                    return RegionPrims(httpResponse, scene, Vector3.Zero, Vector3.Zero);
                }
            }

            if (3 == comps.Length)
            {
                switch (comps[1].ToLower())
                {
                case "prims":
                    string[] subregion = comps[2].Split(',');
                    if (subregion.Length == 6)
                    {
                        Vector3 min, max;
                        try
                        {
                            min = new Vector3((float)Double.Parse(subregion[0]), (float)Double.Parse(subregion[1]), (float)Double.Parse(subregion[2]));
                            max = new Vector3((float)Double.Parse(subregion[3]), (float)Double.Parse(subregion[4]), (float)Double.Parse(subregion[5]));
                        }
                        catch (Exception)
                        {
                            return Failure(httpResponse, OSHttpStatusCode.ClientErrorBadRequest,
                                           "GET", "invalid subregion parameter");
                        }
                        return RegionPrims(httpResponse, scene, min, max);
                    }
                    else
                    {
                        return Failure(httpResponse, OSHttpStatusCode.ClientErrorBadRequest,
                                       "GET", "invalid subregion parameter");
                    }
                }
            }

            return Failure(httpResponse, OSHttpStatusCode.ClientErrorBadRequest,
                           "GET", "too many parameters {0}", param);
        }
        #endregion GET methods

        protected string RegionTerrain(OSHttpResponse httpResponse, Scene scene)
        {
            httpResponse.SendChunked = true;
            httpResponse.ContentType = "text/xml";

            return scene.Heightmap.SaveToXmlString();
            //return Failure(httpResponse, OSHttpStatusCode.ServerErrorNotImplemented,
            //               "GET", "terrain not implemented");
        }

        protected string RegionStats(OSHttpResponse httpResponse, Scene scene)
        {
            int users = scene.GetAvatars().Count;
            int objects = scene.Entities.Count - users;

            RestXmlWriter rxw = new RestXmlWriter(new StringWriter());

            rxw.WriteStartElement(String.Empty, "region", String.Empty);
            rxw.WriteStartElement(String.Empty, "stats", String.Empty);

            rxw.WriteStartElement(String.Empty, "users", String.Empty);
            rxw.WriteString(users.ToString());
            rxw.WriteEndElement();

            rxw.WriteStartElement(String.Empty, "objects", String.Empty);
            rxw.WriteString(objects.ToString());
            rxw.WriteEndElement();

            rxw.WriteEndDocument();

            return rxw.ToString();
        }

        protected string RegionPrims(OSHttpResponse httpResponse, Scene scene, Vector3 min, Vector3 max)
        {
            httpResponse.SendChunked = true;
            httpResponse.ContentType = "text/xml";
            
            IRegionSerialiserModule serialiser = scene.RequestModuleInterface<IRegionSerialiserModule>();
            if (serialiser != null)
                serialiser.SavePrimsToXml2(scene, new StreamWriter(httpResponse.OutputStream), min, max);
            
            return "";
        }
    }
}
