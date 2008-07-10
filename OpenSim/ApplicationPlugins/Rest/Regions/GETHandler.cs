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
 *     * Neither the name of the OpenSim Project nor the
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
using System.Threading;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Timers;
using System.Xml;
using System.Xml.Serialization;
using libsecondlife;
using Nwc.XmlRpc;
using Nini.Config;
using OpenSim.Framework;
using OpenSim.Framework.Console;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Communications;
using OpenSim.Region.Environment.Scenes;
using OpenSim.ApplicationPlugins.Rest;

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
            XmlWriter.WriteStartElement(String.Empty, "regions", String.Empty);
            foreach (Scene s in App.SceneManager.Scenes)
            {
                XmlWriter.WriteStartElement(String.Empty, "uuid", String.Empty);
                XmlWriter.WriteString(s.RegionInfo.RegionID.ToString());
                XmlWriter.WriteEndElement();
            }
            XmlWriter.WriteEndElement();

            return XmlWriterResult;
        }

        protected string ShortRegionInfo(string key, string value)
        {
            if (String.IsNullOrEmpty(value) ||
                String.IsNullOrEmpty(key)) return null;

            XmlWriter.WriteStartElement(String.Empty, "region", String.Empty);
            XmlWriter.WriteStartElement(String.Empty, key, String.Empty);
            XmlWriter.WriteString(value);
            XmlWriter.WriteEndDocument();

            return XmlWriterResult;
        }

        public string GetHandlerRegion(OSHttpResponse httpResponse, string param)
        {
            // be resilient and don't get confused by a terminating '/'
            param = param.TrimEnd(new char[]{'/'});
            string[] comps = param.Split('/');
            LLUUID regionID = (LLUUID)comps[0];

            m_log.DebugFormat("{0} GET region UUID {1}", MsgID, regionID.ToString());

            if (LLUUID.Zero == regionID) throw new Exception("missing region ID");

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
                XmlSerializer xs = new XmlSerializer(typeof(RegionDetails));
                xs.Serialize(XmlWriter, details, _xmlNs);
                return XmlWriterResult;
            }

            if (2 == comps.Length) {
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
                    return RegionPrims(httpResponse, scene);
                }
            }
            return Failure(httpResponse, OSHttpStatusCode.ClientErrorBadRequest,
                           "GET", "too many parameters {0}", param);
        }
        #endregion GET methods

        protected string RegionTerrain(OSHttpResponse httpResponse, Scene scene)
        {
            return Failure(httpResponse, OSHttpStatusCode.ServerErrorNotImplemented,
                           "GET", "terrain not implemented");
        }

        protected string RegionStats(OSHttpResponse httpResponse, Scene scene)
        {
            int users = scene.GetAvatars().Count;
            int objects = scene.Entities.Count - users;

            XmlWriter.WriteStartElement(String.Empty, "region", String.Empty);
            XmlWriter.WriteStartElement(String.Empty, "stats", String.Empty);

            XmlWriter.WriteStartElement(String.Empty, "users", String.Empty);
            XmlWriter.WriteString(users.ToString());
            XmlWriter.WriteEndElement();

            XmlWriter.WriteStartElement(String.Empty, "objects", String.Empty);
            XmlWriter.WriteString(objects.ToString());
            XmlWriter.WriteEndElement();

            XmlWriter.WriteEndDocument();

            return XmlWriterResult;
        }

        protected string RegionPrims(OSHttpResponse httpResponse, Scene scene)
        {
            return Failure(httpResponse, OSHttpStatusCode.ServerErrorNotImplemented,
                           "GET", "prims not implemented");
        }
    }
}
