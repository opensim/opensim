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
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Xml;
using log4net;
using Nini.Config;
using OpenSim.Framework;

namespace OpenSim
{
    /// <summary>
    /// Loads the Configuration files into nIni
    /// </summary>
    public class ConfigurationLoader
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        
        /// <summary>
        /// Various Config settings the region needs to start
        /// Physics Engine, Mesh Engine, GridMode, PhysicsPrim allowed, Neighbor, 
        /// StorageDLL, Storage Connection String, Estate connection String, Client Stack
        /// Standalone settings.
        /// </summary>
        protected ConfigSettings m_configSettings;

        /// <summary>
        /// A source of Configuration data
        /// </summary>
        protected OpenSimConfigSource m_config;

        /// <summary>
        /// Grid Service Information.  This refers to classes and addresses of the grid service
        /// </summary>
        protected NetworkServersInfo m_networkServersInfo;

        /// <summary>
        /// Loads the region configuration
        /// </summary>
        /// <param name="argvSource">Parameters passed into the process when started</param>
        /// <param name="configSettings"></param>
        /// <param name="networkInfo"></param>
        /// <returns>A configuration that gets passed to modules</returns>
        public OpenSimConfigSource LoadConfigSettings(
                IConfigSource argvSource, EnvConfigSource envConfigSource, out ConfigSettings configSettings,
                out NetworkServersInfo networkInfo)
        {
            m_configSettings = configSettings = new ConfigSettings();
            m_networkServersInfo = networkInfo = new NetworkServersInfo();

            bool iniFileExists = false;

            IConfig startupConfig = argvSource.Configs["Startup"];

            List<string> sources = new List<string>();

            string masterFileName =
                    startupConfig.GetString("inimaster", "OpenSimDefaults.ini");

            if (masterFileName == "none")
                masterFileName = String.Empty;

            if (IsUri(masterFileName))
            {
                if (!sources.Contains(masterFileName))
                    sources.Add(masterFileName);
            }
            else
            {
                string masterFilePath = Path.GetFullPath(
                        Path.Combine(Util.configDir(), masterFileName));

                if (masterFileName != String.Empty)
                {
                    if (File.Exists(masterFilePath))
                    {
                        if (!sources.Contains(masterFilePath))
                            sources.Add(masterFilePath);
                    }
                    else
                    {
                        m_log.ErrorFormat("Master ini file {0} not found", Path.GetFullPath(masterFilePath));
                        Environment.Exit(1);
                    }
                }
            }

            string iniFileName = startupConfig.GetString("inifile", "OpenSim.ini");

            if (IsUri(iniFileName))
            {
                if (!sources.Contains(iniFileName))
                    sources.Add(iniFileName);
                Application.iniFilePath = iniFileName;
            }
            else
            {
                Application.iniFilePath = Path.GetFullPath(
                        Path.Combine(Util.configDir(), iniFileName));

                if (!File.Exists(Application.iniFilePath))
                {
                    iniFileName = "OpenSim.xml";
                    Application.iniFilePath = Path.GetFullPath(Path.Combine(Util.configDir(), iniFileName));
                }

                if (File.Exists(Application.iniFilePath))
                {
                    if (!sources.Contains(Application.iniFilePath))
                        sources.Add(Application.iniFilePath);
                }
            }

            string iniDirName = startupConfig.GetString("inidirectory", "config");
            string iniDirPath = Path.Combine(Util.configDir(), iniDirName);

            if (Directory.Exists(iniDirPath))
            {
                m_log.InfoFormat("Searching folder {0} for config ini files", iniDirPath);

                string[] fileEntries = Directory.GetFiles(iniDirName);
                foreach (string filePath in fileEntries)
                {
                    if (Path.GetExtension(filePath).ToLower() == ".ini")
                    {
                        if (!sources.Contains(Path.GetFullPath(filePath)))
                            sources.Add(Path.GetFullPath(filePath));
                    }
                }
            }

            m_config = new OpenSimConfigSource();
            m_config.Source = new IniConfigSource();
            m_config.Source.Merge(DefaultConfig());

            m_log.Info("[CONFIG]: Reading configuration settings");

            if (sources.Count == 0)
            {
                m_log.FatalFormat("[CONFIG]: Could not load any configuration");
                Environment.Exit(1);
            }

            for (int i = 0 ; i < sources.Count ; i++)
            {
                if (ReadConfig(sources[i]))
                {
                    iniFileExists = true;
                    AddIncludes(sources);
                }
            }

            if (!iniFileExists)
            {
                m_log.FatalFormat("[CONFIG]: Could not load any configuration");
                m_log.FatalFormat("[CONFIG]: Configuration exists, but there was an error loading it!");
                Environment.Exit(1);
            }

            // Make sure command line options take precedence
            m_config.Source.Merge(argvSource);

            IConfig enVars = m_config.Source.Configs["Environment"];

            if( enVars != null )
            {
                string[] env_keys = enVars.GetKeys();

                foreach ( string key in env_keys )
                {
                    envConfigSource.AddEnv(key, string.Empty);
                }

                envConfigSource.LoadEnv();
                m_config.Source.Merge(envConfigSource);
                m_config.Source.ExpandKeyValues();
            }


            ReadConfigSettings();

            return m_config;
        }

        /// <summary>
        /// Adds the included files as ini configuration files
        /// </summary>
        /// <param name="sources">List of URL strings or filename strings</param>
        private void AddIncludes(List<string> sources)
        {
            //loop over config sources
            foreach (IConfig config in m_config.Source.Configs)
            {
                // Look for Include-* in the key name
                string[] keys = config.GetKeys();
                foreach (string k in keys)
                {
                    if (k.StartsWith("Include-"))
                    {
                        // read the config file to be included.
                        string file = config.GetString(k);
                        if (IsUri(file))
                        {
                            if (!sources.Contains(file))
                                sources.Add(file);
                        }
                        else
                        {
                            string basepath = Path.GetFullPath(Util.configDir());
                            // Resolve relative paths with wildcards
                            string chunkWithoutWildcards = file;
                            string chunkWithWildcards = string.Empty;
                            int wildcardIndex = file.IndexOfAny(new char[] { '*', '?' });
                            if (wildcardIndex != -1)
                            {
                                chunkWithoutWildcards = file.Substring(0, wildcardIndex);
                                chunkWithWildcards = file.Substring(wildcardIndex);
                            }
                            string path = Path.Combine(basepath, chunkWithoutWildcards);
                            path = Path.GetFullPath(path) + chunkWithWildcards;
                            string[] paths = Util.Glob(path);
                            
                            // If the include path contains no wildcards, then warn the user that it wasn't found.
                            if (wildcardIndex == -1 && paths.Length == 0)
                            {
                                m_log.WarnFormat("[CONFIG]: Could not find include file {0}", path);
                            }
                            else
                            {                            
                                foreach (string p in paths)
                                {
                                    if (!sources.Contains(p))
                                        sources.Add(p);
                                }
                            }
                        }
                    }
                }
            }
        }
        /// <summary>
        /// Check if we can convert the string to a URI
        /// </summary>
        /// <param name="file">String uri to the remote resource</param>
        /// <returns>true if we can convert the string to a Uri object</returns>
        bool IsUri(string file)
        {
            Uri configUri;

            return Uri.TryCreate(file, UriKind.Absolute,
                    out configUri) && configUri.Scheme == Uri.UriSchemeHttp;
        }

        /// <summary>
        /// Provide same ini loader functionality for standard ini and master ini - file system or XML over http
        /// </summary>
        /// <param name="iniPath">Full path to the ini</param>
        /// <returns></returns>
        private bool ReadConfig(string iniPath)
        {
            bool success = false;

            if (!IsUri(iniPath))
            {
                m_log.InfoFormat("[CONFIG]: Reading configuration file {0}", Path.GetFullPath(iniPath));

                m_config.Source.Merge(new IniConfigSource(iniPath));
                success = true;
            }
            else
            {
                m_log.InfoFormat("[CONFIG]: {0} is a http:// URI, fetching ...", iniPath);

                // The ini file path is a http URI
                // Try to read it
                try
                {
                    XmlReader r = XmlReader.Create(iniPath);
                    XmlConfigSource cs = new XmlConfigSource(r);
                    m_config.Source.Merge(cs);

                    success = true;
                }
                catch (Exception e)
                {
                    m_log.FatalFormat("[CONFIG]: Exception reading config from URI {0}\n" + e.ToString(), iniPath);
                    Environment.Exit(1);
                }
            }
            return success;
        }

        /// <summary>
        /// Setup a default config values in case they aren't present in the ini file
        /// </summary>
        /// <returns>A Configuration source containing the default configuration</returns>
        private static IConfigSource DefaultConfig()
        {
            IConfigSource defaultConfig = new IniConfigSource();

            {
                IConfig config = defaultConfig.Configs["Startup"];

                if (null == config)
                    config = defaultConfig.AddConfig("Startup");

                config.Set("region_info_source", "filesystem");

                config.Set("physics", "OpenDynamicsEngine");
                config.Set("meshing", "Meshmerizer");
                config.Set("physical_prim", true);
                config.Set("serverside_object_permissions", true);
                config.Set("storage_plugin", "OpenSim.Data.SQLite.dll");
                config.Set("storage_connection_string", "URI=file:OpenSim.db,version=3");
                config.Set("storage_prim_inventories", true);
                config.Set("startup_console_commands_file", String.Empty);
                config.Set("shutdown_console_commands_file", String.Empty);
                config.Set("DefaultScriptEngine", "XEngine");
                config.Set("clientstack_plugin", "OpenSim.Region.ClientStack.LindenUDP.dll");
                // life doesn't really work without this
                config.Set("EventQueue", true);
            }

            {
                IConfig config = defaultConfig.Configs["Network"];

                if (null == config)
                    config = defaultConfig.AddConfig("Network");

                config.Set("http_listener_port", ConfigSettings.DefaultRegionHttpPort);
            }

            return defaultConfig;
        }

        /// <summary>
        /// Read initial region settings from the ConfigSource
        /// </summary>
        protected virtual void ReadConfigSettings()
        {
            IConfig startupConfig = m_config.Source.Configs["Startup"];
            if (startupConfig != null)
            {
                m_configSettings.PhysicsEngine = startupConfig.GetString("physics");
                m_configSettings.MeshEngineName = startupConfig.GetString("meshing");
                m_configSettings.StorageDll = startupConfig.GetString("storage_plugin");

                m_configSettings.ClientstackDll 
                    = startupConfig.GetString("clientstack_plugin", "OpenSim.Region.ClientStack.LindenUDP.dll");
            }

            m_networkServersInfo.loadFromConfiguration(m_config.Source);
        }
    }
}