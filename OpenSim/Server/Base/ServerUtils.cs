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
using OpenMetaverse;

namespace OpenSim.Server.Base
{
    public static class ServerUtils
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        #region SL / file extension / content-type conversions

        public static string SLAssetTypeToContentType(int assetType)
        {
            switch ((AssetType)assetType)
            {
                case AssetType.Texture:
                    return "image/x-j2c";
                case AssetType.Sound:
                    return "application/ogg";
                case AssetType.CallingCard:
                    return "application/vnd.ll.callingcard";
                case AssetType.Landmark:
                    return "application/vnd.ll.landmark";
                case AssetType.Clothing:
                    return "application/vnd.ll.clothing";
                case AssetType.Object:
                    return "application/vnd.ll.primitive";
                case AssetType.Notecard:
                    return "application/vnd.ll.notecard";
                case AssetType.Folder:
                    return "application/vnd.ll.folder";
                case AssetType.RootFolder:
                    return "application/vnd.ll.rootfolder";
                case AssetType.LSLText:
                    return "application/vnd.ll.lsltext";
                case AssetType.LSLBytecode:
                    return "application/vnd.ll.lslbyte";
                case AssetType.TextureTGA:
                case AssetType.ImageTGA:
                    return "image/tga";
                case AssetType.Bodypart:
                    return "application/vnd.ll.bodypart";
                case AssetType.TrashFolder:
                    return "application/vnd.ll.trashfolder";
                case AssetType.SnapshotFolder:
                    return "application/vnd.ll.snapshotfolder";
                case AssetType.LostAndFoundFolder:
                    return "application/vnd.ll.lostandfoundfolder";
                case AssetType.SoundWAV:
                    return "audio/x-wav";
                case AssetType.ImageJPEG:
                    return "image/jpeg";
                case AssetType.Animation:
                    return "application/vnd.ll.animation";
                case AssetType.Gesture:
                    return "application/vnd.ll.gesture";
                case AssetType.Simstate:
                    return "application/x-metaverse-simstate";
                case AssetType.Unknown:
                default:
                    return "application/octet-stream";
            }
        }

        public static sbyte ContentTypeToSLAssetType(string contentType)
        {
            switch (contentType)
            {
                case "image/x-j2c":
                case "image/jp2":
                    return (sbyte)AssetType.Texture;
                case "application/ogg":
                    return (sbyte)AssetType.Sound;
                case "application/vnd.ll.callingcard":
                case "application/x-metaverse-callingcard":
                    return (sbyte)AssetType.CallingCard;
                case "application/vnd.ll.landmark":
                case "application/x-metaverse-landmark":
                    return (sbyte)AssetType.Landmark;
                case "application/vnd.ll.clothing":
                case "application/x-metaverse-clothing":
                    return (sbyte)AssetType.Clothing;
                case "application/vnd.ll.primitive":
                case "application/x-metaverse-primitive":
                    return (sbyte)AssetType.Object;
                case "application/vnd.ll.notecard":
                case "application/x-metaverse-notecard":
                    return (sbyte)AssetType.Notecard;
                case "application/vnd.ll.folder":
                    return (sbyte)AssetType.Folder;
                case "application/vnd.ll.rootfolder":
                    return (sbyte)AssetType.RootFolder;
                case "application/vnd.ll.lsltext":
                case "application/x-metaverse-lsl":
                    return (sbyte)AssetType.LSLText;
                case "application/vnd.ll.lslbyte":
                case "application/x-metaverse-lso":
                    return (sbyte)AssetType.LSLBytecode;
                case "image/tga":
                    // Note that AssetType.TextureTGA will be converted to AssetType.ImageTGA
                    return (sbyte)AssetType.ImageTGA;
                case "application/vnd.ll.bodypart":
                case "application/x-metaverse-bodypart":
                    return (sbyte)AssetType.Bodypart;
                case "application/vnd.ll.trashfolder":
                    return (sbyte)AssetType.TrashFolder;
                case "application/vnd.ll.snapshotfolder":
                    return (sbyte)AssetType.SnapshotFolder;
                case "application/vnd.ll.lostandfoundfolder":
                    return (sbyte)AssetType.LostAndFoundFolder;
                case "audio/x-wav":
                    return (sbyte)AssetType.SoundWAV;
                case "image/jpeg":
                    return (sbyte)AssetType.ImageJPEG;
                case "application/vnd.ll.animation":
                case "application/x-metaverse-animation":
                    return (sbyte)AssetType.Animation;
                case "application/vnd.ll.gesture":
                case "application/x-metaverse-gesture":
                    return (sbyte)AssetType.Gesture;
                case "application/x-metaverse-simstate":
                    return (sbyte)AssetType.Simstate;
                case "application/octet-stream":
                default:
                    return (sbyte)AssetType.Unknown;
            }
        }

        public static sbyte ContentTypeToSLInvType(string contentType)
        {
            switch (contentType)
            {
                case "image/x-j2c":
                case "image/jp2":
                case "image/tga":
                case "image/jpeg":
                    return (sbyte)InventoryType.Texture;
                case "application/ogg":
                case "audio/x-wav":
                    return (sbyte)InventoryType.Sound;
                case "application/vnd.ll.callingcard":
                case "application/x-metaverse-callingcard":
                    return (sbyte)InventoryType.CallingCard;
                case "application/vnd.ll.landmark":
                case "application/x-metaverse-landmark":
                    return (sbyte)InventoryType.Landmark;
                case "application/vnd.ll.clothing":
                case "application/x-metaverse-clothing":
                case "application/vnd.ll.bodypart":
                case "application/x-metaverse-bodypart":
                    return (sbyte)InventoryType.Wearable;
                case "application/vnd.ll.primitive":
                case "application/x-metaverse-primitive":
                    return (sbyte)InventoryType.Object;
                case "application/vnd.ll.notecard":
                case "application/x-metaverse-notecard":
                    return (sbyte)InventoryType.Notecard;
                case "application/vnd.ll.folder":
                    return (sbyte)InventoryType.Folder;
                case "application/vnd.ll.rootfolder":
                    return (sbyte)InventoryType.RootCategory;
                case "application/vnd.ll.lsltext":
                case "application/x-metaverse-lsl":
                case "application/vnd.ll.lslbyte":
                case "application/x-metaverse-lso":
                    return (sbyte)InventoryType.LSL;
                case "application/vnd.ll.trashfolder":
                case "application/vnd.ll.snapshotfolder":
                case "application/vnd.ll.lostandfoundfolder":
                    return (sbyte)InventoryType.Folder;
                case "application/vnd.ll.animation":
                case "application/x-metaverse-animation":
                    return (sbyte)InventoryType.Animation;
                case "application/vnd.ll.gesture":
                case "application/x-metaverse-gesture":
                    return (sbyte)InventoryType.Gesture;
                case "application/x-metaverse-simstate":
                    return (sbyte)InventoryType.Snapshot;
                case "application/octet-stream":
                default:
                    return (sbyte)InventoryType.Unknown;
            }
        }

        #endregion SL / file extension / content-type conversions

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

        public static Dictionary<string, object> ParseQueryString(string query)
        {
            Dictionary<string, object> result = new Dictionary<string, object>();
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

                if (name.EndsWith("[]"))
                {
                    if (result.ContainsKey(name))
                    {
                        if (!(result[name] is List<string>))
                            continue;

                        List<string> l = (List<string>)result[name];

                        l.Add(value);
                    }
                    else
                    {
                        List<string> newList = new List<string>();

                        newList.Add(value);

                        result[name] = newList;
                    }
                }
                else
                {
                    if (!result.ContainsKey(name))
                        result[name] = value;
                }
            }

            return result;
        }

        public static string BuildQueryString(Dictionary<string, object> data)
        {
            string qstring = String.Empty;

            string part;

            foreach (KeyValuePair<string, object> kvp in data)
            {
                if (kvp.Value is List<string>)
                {
                    List<string> l = (List<String>)kvp.Value;

                    foreach (string s in l)
                    {
                        part = System.Web.HttpUtility.UrlEncode(kvp.Key) +
                                "[]=" + System.Web.HttpUtility.UrlEncode(s);

                        if (qstring != String.Empty)
                            qstring += "&";

                        qstring += part;
                    }
                }
                else
                {
                    if (kvp.Value.ToString() != String.Empty)
                    {
                        part = System.Web.HttpUtility.UrlEncode(kvp.Key) +
                                "=" + System.Web.HttpUtility.UrlEncode(kvp.Value.ToString());
                    }
                    else
                    {
                        part = System.Web.HttpUtility.UrlEncode(kvp.Key);
                    }

                    if (qstring != String.Empty)
                        qstring += "&";

                    qstring += part;
                }
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
                if (kvp.Value == null)
                    continue;

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
            //m_log.DebugFormat("[XXX]: received xml string: {0}", data);

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
