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

namespace OpenSim.Tools.Configger
{
    /// <summary>
    /// Loads the Configuration files into nIni
    /// </summary>
    public class ConfigurationLoader
    {
        /// <summary>
        /// A source of Configuration data
        /// </summary>
        protected IConfigSource m_config;

        /// <summary>
        /// Console logger
        /// </summary>
        private static readonly ILog m_log =
                LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType);

        public ConfigurationLoader()
        {
        }

        /// <summary>
        /// Loads the region configuration
        /// </summary>
        /// <param name="argvSource">Parameters passed into the process when started</param>
        /// <param name="configSettings"></param>
        /// <param name="networkInfo"></param>
        /// <returns>A configuration that gets passed to modules</returns>
        public IConfigSource LoadConfigSettings(IConfig startupConfig)
        {
            bool iniFileExists = false;

            List<string> sources = new List<string>();

            string iniFileName = startupConfig.GetString("inifile", Path.Combine(".", "OpenSim.ini"));

            if (IsUri(iniFileName))
            {
                if (!sources.Contains(iniFileName))
                    sources.Add(iniFileName);
            }
            else
            {
                if (File.Exists(iniFileName))
                {
                    if (!sources.Contains(iniFileName))
                        sources.Add(iniFileName);
                }
            }

            m_config = new IniConfigSource();
            m_config.Merge(DefaultConfig());

            m_log.Info("[CONFIG] Reading configuration settings");

            if (sources.Count == 0)
            {
                m_log.FatalFormat("[CONFIG] Could not load any configuration");
                m_log.FatalFormat("[CONFIG] Did you copy the OpenSim.ini.example file to OpenSim.ini?");
                Environment.Exit(1);
            }

            for (int i = 0 ; i < sources.Count ; i++)
            {
                if (ReadConfig(sources[i]))
                    iniFileExists = true;
                AddIncludes(sources);
            }

            if (!iniFileExists)
            {
                m_log.FatalFormat("[CONFIG] Could not load any configuration");
                m_log.FatalFormat("[CONFIG] Configuration exists, but there was an error loading it!");
                Environment.Exit(1);
            }

            return m_config;
        }

        /// <summary>
        /// Adds the included files as ini configuration files
        /// </summary>
        /// <param name="sources">List of URL strings or filename strings</param>
        private void AddIncludes(List<string> sources)
        {
            //loop over config sources
            foreach (IConfig config in m_config.Configs)
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
                            string basepath = Path.GetFullPath(".");
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
                m_log.InfoFormat("[CONFIG] Reading configuration file {0}",
                        Path.GetFullPath(iniPath));

                m_config.Merge(new IniConfigSource(iniPath));
                success = true;
            }
            else
            {
                m_log.InfoFormat("[CONFIG] {0} is a http:// URI, fetching ...",
                        iniPath);

                // The ini file path is a http URI
                // Try to read it
                //
                try
                {
                    XmlReader r = XmlReader.Create(iniPath);
                    XmlConfigSource cs = new XmlConfigSource(r);
                    m_config.Merge(cs);

                    success = true;
                }
                catch (Exception e)
                {
                    m_log.FatalFormat("[CONFIG] Exception reading config from URI {0}\n" + e.ToString(), iniPath);
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
                config.Set("allow_regionless", false);

                config.Set("gridmode", false);
                config.Set("physics", "OpenDynamicsEngine");
                config.Set("meshing", "Meshmerizer");
                config.Set("physical_prim", true);
                config.Set("serverside_object_permissions", true);
                config.Set("storage_prim_inventories", true);
                config.Set("startup_console_commands_file", String.Empty);
                config.Set("shutdown_console_commands_file", String.Empty);
                config.Set("DefaultScriptEngine", "XEngine");
                config.Set("clientstack_plugin", "OpenSim.Region.ClientStack.LindenUDP.dll");
                // life doesn't really work without this
                config.Set("EventQueue", true);
            }

            return defaultConfig;
        }
    }
}