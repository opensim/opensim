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
using System.Linq;
using System.Reflection;
using log4net;
using Nini.Config;

namespace OpenSim.Framework.Security.DOSProtector
{
    /// <summary>
    /// Loads DOS Protector configuration from INI file and configures the builder
    /// </summary>
    public static class DOSProtectorConfigLoader
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod()?.DeclaringType);
        private static bool _loaded = false;

        /// <summary>
        /// Loads DOS Protector configuration from DOSProtector.ini file.
        /// Should be called during application startup.
        /// </summary>
        /// <param name="configPath">Optional path to config file. If null, searches in bin/ directory</param>
        public static void LoadConfig(string configPath = null)
        {
            if (_loaded)
            {
                m_log.Debug("[DOSProtectorConfig]: Configuration already loaded, skipping");
                return;
            }

            try
            {
                // Determine config file path
                if (string.IsNullOrEmpty(configPath))
                {
                    // Default: look for DOSProtector.ini in bin directory
                    string binDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "./";
                    configPath = Path.Combine(binDir, "DOSProtector.ini");
                }

                if (!File.Exists(configPath))
                {
                    m_log.Info($"[DOSProtectorConfig]: No configuration file found at {configPath}, using defaults");
                    _loaded = true;
                    return;
                }

                m_log.Info($"[DOSProtectorConfig]: Loading DOS Protector configuration from {configPath}");

                var configSource = new IniConfigSource(configPath);
                var config = configSource.Configs["DOSProtector"];

                if (config == null)
                {
                    m_log.Warn("[DOSProtectorConfig]: [DOSProtector] section not found in config file");
                    _loaded = true;
                    return;
                }

                // Read configuration
                bool enablePlugins = config.GetBoolean("EnablePlugins", true);
                string pluginPathsStr = config.GetString("PluginPaths", "");
                bool verboseLogging = config.GetBoolean("VerbosePluginLoading", false);

                if (!enablePlugins)
                {
                    m_log.Info("[DOSProtectorConfig]: Plugin loading disabled in configuration");
                    _loaded = true;
                    return;
                }

                // Parse and configure plugin paths
                if (!string.IsNullOrWhiteSpace(pluginPathsStr))
                {
                    var paths = pluginPathsStr.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(p => p.Trim())
                        .Where(p => !string.IsNullOrEmpty(p))
                        .ToArray();

                    if (paths.Length > 0)
                    {
                        // Resolve relative paths
                        string binDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "./";
                        var resolvedPaths = paths.Select(p =>
                        {
                            if (Path.IsPathRooted(p))
                                return p;
                            return Path.GetFullPath(Path.Combine(binDir, p));
                        }).ToArray();

                        m_log.Info($"[DOSProtectorConfig]: Configuring {resolvedPaths.Length} plugin path(s)");
                        DOSProtectorBuilder.ConfigurePluginPaths(resolvedPaths);
                    }
                }

                _loaded = true;
                m_log.Info("[DOSProtectorConfig]: Configuration loaded successfully");
            }
            catch (Exception ex)
            {
                m_log.Error($"[DOSProtectorConfig]: Error loading configuration: {ex.Message}", ex);
                _loaded = true; // Mark as loaded to prevent retry loop
            }
        }

        /// <summary>
        /// Resets the loaded state, allowing configuration to be reloaded.
        /// </summary>
        public static void Reset()
        {
            _loaded = false;
        }
    }
}
