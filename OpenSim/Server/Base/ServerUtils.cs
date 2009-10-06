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
using System.Xml;
using System.Xml.Serialization;
using System.Text;
using System.Collections.Generic;
using log4net;
using OpenSim.Framework;

namespace OpenSim.Server.Base
{
    public static class ServerUtils
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        
        public static string SLAssetTypeToContentType(int assetType)
        {
            switch (assetType)
            {
                case 0:
                    return "image/jp2";
                case 1:
                    return "application/ogg";
                case 2:
                    return "application/x-metaverse-callingcard";
                case 3:
                    return "application/x-metaverse-landmark";
                case 5:
                    return "application/x-metaverse-clothing";
                case 6:
                    return "application/x-metaverse-primitive";
                case 7:
                    return "application/x-metaverse-notecard";
                case 8:
                    return "application/x-metaverse-folder";
                case 10:
                    return "application/x-metaverse-lsl";
                case 11:
                    return "application/x-metaverse-lso";
                case 12:
                    return "image/tga";
                case 13:
                    return "application/x-metaverse-bodypart";
                case 17:
                    return "audio/x-wav";
                case 19:
                    return "image/jpeg";
                case 20:
                    return "application/x-metaverse-animation";
                case 21:
                    return "application/x-metaverse-gesture";
                case 22:
                    return "application/x-metaverse-simstate";
                default:
                    return "application/octet-stream";
            }
        }

        public static  byte[] SerializeResult(XmlSerializer xs, object data)
        {
            MemoryStream ms = new MemoryStream();
            XmlTextWriter xw = new XmlTextWriter(ms, Util.UTF8);
            xw.Formatting = Formatting.Indented;
            xs.Serialize(xw, data);
            xw.Flush();

            ms.Seek(0, SeekOrigin.Begin);
            byte[] ret = ms.GetBuffer();
            Array.Resize(ref ret, (int)ms.Length);

            return ret;
        }

        public static T LoadPlugin<T>(string dllName, Object[] args) where T:class
        {
            string[] parts = dllName.Split(new char[] {':'});

            dllName = parts[0];

            string className = String.Empty;

            if (parts.Length > 1)
                className = parts[1];

            return LoadPlugin<T>(dllName, className, args);
        }

        public static T LoadPlugin<T>(string dllName, string className, Object[] args) where T:class
        {
            string interfaceName = typeof(T).ToString();

            try
            {
                Assembly pluginAssembly = Assembly.LoadFrom(dllName);

                foreach (Type pluginType in pluginAssembly.GetTypes())
                {
                    if (pluginType.IsPublic)
                    {
                        if (className != String.Empty &&
                                pluginType.ToString() !=
                                pluginType.Namespace + "." + className)
                            continue;
                        Type typeInterface =
                                pluginType.GetInterface(interfaceName, true);
                        if (typeInterface != null)
                        {
                            T plug = null;
                            try
                            {
                                plug = (T)Activator.CreateInstance(pluginType,
                                        args);
                            }
                            catch (Exception e)
                            {
                                if (!(e is System.MissingMethodException))
                                    m_log.ErrorFormat("Error loading plugin from {0}, exception {1}", dllName, e.InnerException);
                                return null;
                            }

                            return plug;
                        }
                    }
                }

                return null;
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("Error loading plugin from {0}, exception {1}", dllName, e);
                return null;
            }
        }

        public static Dictionary<string, string> ParseQueryString(string query)
        {
            Dictionary<string, string> result = new Dictionary<string, string>();
            string[] terms = query.Split(new char[] {'&'});

            if (terms.Length == 0)
                return result;

            foreach (string t in terms)
            {
                string[] elems = t.Split(new char[] {'='});
                if (elems.Length == 0)
                    continue;

                string name = System.Web.HttpUtility.UrlDecode(elems[0]);
                string value = String.Empty;

                if (elems.Length > 1)
                    value = System.Web.HttpUtility.UrlDecode(elems[1]);

                result[name] = value;
            }

            return result;
        }

        public static string BuildQueryString(Dictionary<string, string> data)
        {
            string qstring = String.Empty;

            foreach (KeyValuePair<string, string> kvp in data)
            {
                string part;
                if (kvp.Value != String.Empty)
                {
                    part = System.Web.HttpUtility.UrlEncode(kvp.Key) +
                            "=" + System.Web.HttpUtility.UrlEncode(kvp.Value);
                }
                else
                {
                    part = System.Web.HttpUtility.UrlEncode(kvp.Key);
                }

                if (qstring != String.Empty)
                    qstring += "&";

                qstring += part;
            }

            return qstring;
        }

        public static string BuildXmlResponse(Dictionary<string, object> data)
        {
            XmlDocument doc = new XmlDocument();

            XmlNode xmlnode = doc.CreateNode(XmlNodeType.XmlDeclaration,
                    "", "");

            doc.AppendChild(xmlnode);

            XmlElement rootElement = doc.CreateElement("", "ServerResponse",
                    "");

            doc.AppendChild(rootElement);

            BuildXmlData(rootElement, data);

            return doc.InnerXml;
        }

        private static void BuildXmlData(XmlElement parent, Dictionary<string, object> data)
        {
            foreach (KeyValuePair<string, object> kvp in data)
            {
                XmlElement elem = parent.OwnerDocument.CreateElement("",
                        kvp.Key, "");

                if (kvp.Value is Dictionary<string, object>)
                {
                    XmlAttribute type = parent.OwnerDocument.CreateAttribute("",
                        "type", "");
                    type.Value = "List";

                    elem.Attributes.Append(type);

                    BuildXmlData(elem, (Dictionary<string, object>)kvp.Value);
                }
                else
                {
                    elem.AppendChild(parent.OwnerDocument.CreateTextNode(
                            kvp.Value.ToString()));
                }

                parent.AppendChild(elem);
            }
        }

        public static Dictionary<string, object> ParseXmlResponse(string data)
        {
            m_log.DebugFormat("[XXX]: received xml string: {0}", data);

            Dictionary<string, object> ret = new Dictionary<string, object>();

            XmlDocument doc = new XmlDocument();

            doc.LoadXml(data);
            
            XmlNodeList rootL = doc.GetElementsByTagName("ServerResponse");

            if (rootL.Count != 1)
                return ret;

            XmlNode rootNode = rootL[0];

            ret = ParseElement(rootNode);

            return ret;
        }

        private static Dictionary<string, object> ParseElement(XmlNode element)
        {
            Dictionary<string, object> ret = new Dictionary<string, object>();

            XmlNodeList partL = element.ChildNodes;

            foreach (XmlNode part in partL)
            {
                XmlNode type = part.Attributes.GetNamedItem("type");
                if (type == null || type.Value != "List")
                {
                    ret[part.Name] = part.InnerText;
                }
                else
                {
                    ret[part.Name] = ParseElement(part);
                }
            }

            return ret;
        }
    }
}
