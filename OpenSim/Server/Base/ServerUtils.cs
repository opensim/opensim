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
using OpenMetaverse.StructuredData; // LitJson is hidden on this

[assembly:AddinRoot("Robust", OpenSim.VersionInfo.VersionNumber)]
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
            //suppress_console_output_(true);
            AddinManager.Initialize(registryPath);
            //suppress_console_output_(false);
            AddinManager.Registry.Update();
            CommandManager commandmanager = new CommandManager(Registry);
            AddinManager.AddExtensionNodeHandler("/Robust/Connector", OnExtensionChanged);
        }

        private static TextWriter prev_console_;
        // Temporarily masking the errors reported on start
        // This is caused by a non-managed dll in the ./bin dir
        // when the registry is initialized. The dll belongs to
        // libomv, which has a hard-coded path to "." for pinvoke
        // to load the openjpeg dll
        //
        // Will look for a way to fix, but for now this keeps the
        // confusion to a minimum. this was copied from our region
        // plugin loader, we have been doing this in there for a long time.
        //
        public void suppress_console_output_(bool save)
        {
            if (save)
            {
                prev_console_ = System.Console.Out;
                System.Console.SetOut(new StreamWriter(Stream.Null));
            }
            else
            {
                if (prev_console_ != null)
                    System.Console.SetOut(prev_console_);
            }
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
                        m_log.InfoFormat("[SERVER UTILS]: Adding {0} from registry", a.Name);
                        connector.PluginPath = System.IO.Path.Combine(Registry.DefaultAddinsFolder,a.Name.Replace(',', '.'));                    }
                    else
                    {
                        m_log.InfoFormat("[SERVER UTILS]: Adding {0} from ./bin", a.Name);
                        connector.PluginPath = a.AddinFile;
                    }
                    LoadPlugin(connector);
                    break;
                case ExtensionChange.Remove:
                    m_log.InfoFormat("[SERVER UTILS]: Removing {0}", a.Name);
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
                m_log.InfoFormat("[SERVER UTILS]: {0} Disabled.", connector.ConfigName);
            }
        }

        private void UnloadPlugin(IRobustConnector connector)
        {
            m_log.InfoFormat("[SERVER UTILS]: Unloading {0}", connector.ConfigName);

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
            using (MemoryStream ms = new MemoryStream())
            using (XmlTextWriter xw = new XmlTextWriter(ms, Util.UTF8))
            {
                xw.Formatting = Formatting.Indented;
                xs.Serialize(xw, data);
                xw.Flush();

                ms.Seek(0, SeekOrigin.Begin);
                byte[] ret = ms.ToArray();

                return ret;
            }
        }

        /// <summary>
        /// Load a plugin from a dll with the given class or interface
        /// </summary>
        /// <param name="dllName"></param>
        /// <param name="args">The arguments which control which constructor is invoked on the plugin</param>
        /// <returns></returns>
        public static T LoadPlugin<T> (string dllName, Object[] args) where T:class
        {
            // This is good to debug configuration problems
            //if (dllName == string.Empty)
            //    Util.PrintCallStack();

            string className = String.Empty;

            // The path for a dynamic plugin will contain ":" on Windows
            string[] parts = dllName.Split (new char[] {':'});

            if (parts.Length < 3)
            {
                // Linux. There will be ':' but the one we're looking for
                dllName = parts [0];
                if (parts.Length > 1)
                    className = parts[1];
            }
            else
            {
                // This is Windows - we must replace the ":" in the path
                dllName = String.Format ("{0}:{1}", parts [0], parts [1]);
                if (parts.Length > 2)
                    className = parts[2];
            }

            // Handle extra string arguments in a more generic way
            if (dllName.Contains("@"))
            {
                string[] dllNameParts = dllName.Split(new char[] {'@'});
                dllName = dllNameParts[dllNameParts.Length - 1];
                List<Object> argList = new List<Object>(args);
                for (int i = 0 ; i < dllNameParts.Length - 1 ; ++i)
                    argList.Add(dllNameParts[i]);

                args = argList.ToArray();
            }

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

                        Type typeInterface = pluginType.GetInterface(interfaceName);

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
                                    m_log.Error(string.Format("[SERVER UTILS]: Error loading plugin {0} from {1}. Exception: {2}",
                                        interfaceName,
                                        dllName,
                                        e.InnerException == null ? e.Message : e.InnerException.Message),
                                            e);
                                }
                                m_log.ErrorFormat("[SERVER UTILS]: Error loading plugin {0}: {1} args.Length {2}", dllName, e.Message, args.Length);
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
                m_log.Error(string.Format("[SERVER UTILS]: Error loading plugin from {0}:\n{1}", dllName,
                    String.Join("\n", Array.ConvertAll(rtle.LoaderExceptions, e => e.ToString()))),
                    rtle);
                return null;
            }
            catch (Exception e)
            {
                m_log.Error(string.Format("[SERVER UTILS]: Error loading plugin from {0}", dllName), e);
                return null;
            }
        }

        public static Dictionary<string, object> ParseQueryString(string query)
        {
            string[] terms = query.Split(new char[] {'&'});

            int nterms = terms.Length;
            if (nterms == 0)
                return new Dictionary<string, object>();

            Dictionary<string, object> result = new Dictionary<string, object>(nterms);
            string name;

            for(int i = 0; i < nterms; ++i)
            {
                string[] elems = terms[i].Split(new char[] {'='});

                if (elems.Length == 0)
                    continue;

                if(String.IsNullOrWhiteSpace(elems[0]))
                    continue;

                name = System.Web.HttpUtility.UrlDecode(elems[0]);

                if (name.EndsWith("[]"))
                {
                    name = name.Substring(0, name.Length - 2);
                    if(String.IsNullOrWhiteSpace(name))
                        continue;
                    if (result.ContainsKey(name))
                    {
                        if (!(result[name] is List<string>))
                            continue;

                        List<string> l = (List<string>)result[name];
                        if (elems.Length > 1 && !String.IsNullOrWhiteSpace(elems[1]))
                            l.Add(System.Web.HttpUtility.UrlDecode(elems[1]));
                        else
                            l.Add(String.Empty);
                    }
                    else
                    {
                        List<string> newList = new List<string>();
                        if (elems.Length > 1 && !String.IsNullOrWhiteSpace(elems[1]))
                            newList.Add(System.Web.HttpUtility.UrlDecode(elems[1]));
                        else
                            newList.Add(String.Empty);
                        result[name] = newList;
                    }
                }
                else
                {
                    if (!result.ContainsKey(name))
                    {
                        if (elems.Length > 1 && !String.IsNullOrWhiteSpace(elems[1]))
                            result[name] = System.Web.HttpUtility.UrlDecode(elems[1]);
                        else
                            result[name] = String.Empty;
                    }
                }
            }

            return result;
        }

        public static string BuildQueryString(Dictionary<string, object> data)
        {
            // this is not conform to html url encoding
            // can only be used on Body of POST or PUT
            StringBuilder sb = new StringBuilder(4096);

            string pvalue;

            foreach (KeyValuePair<string, object> kvp in data)
            {
                if (kvp.Value is List<string>)
                {
                    List<string> l = (List<String>)kvp.Value;
                    int llen = l.Count;
                    string nkey = System.Web.HttpUtility.UrlEncode(kvp.Key);
                    for(int i = 0; i < llen; ++i)
                    {
                        if (sb.Length != 0)
                            sb.Append("&");
                        sb.Append(nkey);
                        sb.Append("[]=");
                        sb.Append(System.Web.HttpUtility.UrlEncode(l[i]));
                    }
                }
                else if(kvp.Value is Dictionary<string, object>)
                {
                    // encode complex structures as JSON
                    // needed for estate bans with the encoding used on xml
                    // encode can be here because object does contain the structure information
                    // but decode needs to be on estateSettings (or other user)
                    string js;
                    try
                    {
                        // bypass libovm, we dont need even more useless high level maps
                        // this should only be called once.. but no problem, i hope
                        // (other uses may need more..)
                        LitJson.JsonMapper.RegisterExporter<UUID>((uuid, writer) => writer.Write(uuid.ToString()) );
                        js = LitJson.JsonMapper.ToJson(kvp.Value);
                    }
 //                   catch(Exception e)
                    catch
                    {
                        continue;
                    }
                    if (sb.Length != 0)
                        sb.Append("&");
                    sb.Append(System.Web.HttpUtility.UrlEncode(kvp.Key));
                    sb.Append("=");
                    sb.Append(System.Web.HttpUtility.UrlEncode(js));
                }
                else
                {
                    if (sb.Length != 0)
                        sb.Append("&");
                    sb.Append(System.Web.HttpUtility.UrlEncode(kvp.Key));
 
                    pvalue = kvp.Value.ToString();
                    if (!String.IsNullOrEmpty(pvalue))
                    {
                        sb.Append("=");
                        sb.Append(System.Web.HttpUtility.UrlEncode(pvalue));
                    }
                }
            }

            return sb.ToString();
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

            try
            {
                doc.LoadXml(data);
                XmlNodeList rootL = doc.GetElementsByTagName("ServerResponse");

                if (rootL.Count != 1)
                    return ret;

                XmlNode rootNode = rootL[0];

                ret = ParseElement(rootNode);
            }
            catch (Exception e)
            {
                m_log.DebugFormat("[serverUtils.ParseXmlResponse]: failed error: {0} \n --- string: {1} - ",e.Message, data);
            }
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
            m_log.InfoFormat("[SERVER UTILS]: {0} is a http:// URI, fetching ...", url);

            // The ini file path is a http URI
            // Try to read it
            try
            {
                IConfigSource cs;
                using( XmlReader r = XmlReader.Create(url))
                {
                    cs = new XmlConfigSource(r);
                    source.Merge(cs);
                }
            }
            catch (Exception e)
            {
                m_log.FatalFormat("[SERVER UTILS]: Exception reading config from URI {0}\n" + e.ToString(), url);
                Environment.Exit(1);
            }

            return source;
        }
    }
}
