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
using Nini.Config;
using OpenSim.Framework;
using OpenMetaverse;
using Mono.Addins;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Framework.Servers;


[assembly:AddinRoot("Robust", "0.1")]
namespace OpenSim.Server.Base
{
    [TypeExtensionPoint(Path="/Robust/Connector", Name="RobustConnector")]
    public interface IRobustConnector
    {
        string ConfigName
        {
            get;
        }

        bool Enabled
        {
            get;
        }

        string PluginPath
        {
            get;
            set;
        }

        uint Configure(IConfigSource config);
        void Initialize(IHttpServer server);
        void Unload();
    }

    public class PluginLoader
    {
        static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public AddinRegistry Registry
        {
            get;
            private set;
        }

        public IConfigSource Config
        {
            get;
            private set;
        }

        public PluginLoader(IConfigSource config, string registryPath)
        {
            Config = config;

            Registry = new AddinRegistry(registryPath, ".");
            AddinManager.Initialize(registryPath);
            AddinManager.Registry.Update();
            CommandManager commandmanager = new CommandManager(Registry);
            AddinManager.AddExtensionNodeHandler("/Robust/Connector", OnExtensionChanged);
        }

        private void OnExtensionChanged(object s, ExtensionNodeEventArgs args)
        {
            IRobustConnector connector = (IRobustConnector)args.ExtensionObject;
            Addin a = Registry.GetAddin(args.ExtensionNode.Addin.Id);

            if(a == null)
            {
                Registry.Rebuild(null);
                a = Registry.GetAddin(args.ExtensionNode.Addin.Id);
            }

            switch(args.Change)
            {
                case ExtensionChange.Add:
                    if (a.AddinFile.Contains(Registry.DefaultAddinsFolder))
                    {
                        m_log.InfoFormat("[SERVER]: Adding {0} from registry", a.Name);
                        connector.PluginPath = String.Format("{0}/{1}", Registry.DefaultAddinsFolder, a.Name.Replace(',', '.'));
                    }
                    else
                    {
                        m_log.InfoFormat("[SERVER]: Adding {0} from ./bin", a.Name);
                        connector.PluginPath = a.AddinFile;
                    }
                    LoadPlugin(connector);
                    break;
                case ExtensionChange.Remove:
                    m_log.InfoFormat("[SERVER]: Removing {0}", a.Name);
                    UnloadPlugin(connector);
                    break;
            }
        }

        private void LoadPlugin(IRobustConnector connector)
        {
            IHttpServer server = null;
            uint port = connector.Configure(Config);

            if(connector.Enabled)
            {
                server = GetServer(connector, port);
                connector.Initialize(server);
            }
            else
            {
                m_log.InfoFormat("[SERVER]: {0} Disabled.", connector.ConfigName);
            }
        }

        private void UnloadPlugin(IRobustConnector connector)
        {
            m_log.InfoFormat("[Server]: Unloading {0}", connector.ConfigName);

            connector.Unload();
        }

        private IHttpServer GetServer(IRobustConnector connector, uint port)
        {
            IHttpServer server;

            if(port != 0)
                server = MainServer.GetHttpServer(port);
            else    
                server = MainServer.Instance;

            return server;
        }
    }

    public static class ServerUtils
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

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

        /// <summary>
        /// Load a plugin from a dll with the given class or interface
        /// </summary>
        /// <param name="dllName"></param>
        /// <param name="args">The arguments which control which constructor is invoked on the plugin</param>
        /// <returns></returns>
        public static T LoadPlugin<T>(string dllName, Object[] args) where T:class
        {
            // This is good to debug configuration problems
            //if (dllName == string.Empty)
            //    Util.PrintCallStack();

            string[] parts = dllName.Split(new char[] {':'});

            dllName = parts[0];

            string className = String.Empty;

            if (parts.Length > 1)
                className = parts[1];

            return LoadPlugin<T>(dllName, className, args);
        }

        /// <summary>
        /// Load a plugin from a dll with the given class or interface
        /// </summary>
        /// <param name="dllName"></param>
        /// <param name="className"></param>
        /// <param name="args">The arguments which control which constructor is invoked on the plugin</param>
        /// <returns></returns>
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
                        if (className != String.Empty 
                            && pluginType.ToString() != pluginType.Namespace + "." + className)
                            continue;
                        
                        Type typeInterface = pluginType.GetInterface(interfaceName, true);

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
                                {
                                    m_log.ErrorFormat("Error loading plugin {0} from {1}. Exception: {2}",
                                        interfaceName, dllName, e.InnerException == null ? e.Message : e.InnerException.Message);
                                }
                                return null;
                            }

                            return plug;
                        }
                    }
                }

                return null;
            }
            catch (ReflectionTypeLoadException rtle)
            {
                m_log.Error(string.Format("Error loading plugin from {0}:\n{1}", dllName,
                    String.Join("\n", Array.ConvertAll(rtle.LoaderExceptions, e => e.ToString()))),
                    rtle);
                return null;
            }
            catch (Exception e)
            {
                m_log.Error(string.Format("Error loading plugin from {0}", dllName), e);
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
                    string cleanName = name.Substring(0, name.Length - 2);
                    if (result.ContainsKey(cleanName))
                    {
                        if (!(result[cleanName] is List<string>))
                            continue;

                        List<string> l = (List<string>)result[cleanName];

                        l.Add(value);
                    }
                    else
                    {
                        List<string> newList = new List<string>();

                        newList.Add(value);

                        result[cleanName] = newList;
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
                        XmlConvert.EncodeLocalName(kvp.Key), "");

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
                    ret[XmlConvert.DecodeName(part.Name)] = part.InnerText;
                }
                else
                {
                    ret[XmlConvert.DecodeName(part.Name)] = ParseElement(part);
                }
            }

            return ret;
        }

        public static IConfig GetConfig(string configFile, string configName)
        {
            IConfig config;

            if (File.Exists(configFile))
            {
                IConfigSource configsource = new IniConfigSource(configFile);
                config = configsource.Configs[configName];
            }
            else
                config = null;

            return config;
        }

        public static IConfigSource LoadInitialConfig(string url)
        {
            IConfigSource source = new XmlConfigSource();
            m_log.InfoFormat("[CONFIG]: {0} is a http:// URI, fetching ...", url);

            // The ini file path is a http URI
            // Try to read it
            try
            {
                XmlReader r = XmlReader.Create(url);
                IConfigSource cs = new XmlConfigSource(r);
                source.Merge(cs);
            }
            catch (Exception e)
            {
                m_log.FatalFormat("[CONFIG]: Exception reading config from URI {0}\n" + e.ToString(), url);
                Environment.Exit(1);
            }

            return source;
        }
    }
}
