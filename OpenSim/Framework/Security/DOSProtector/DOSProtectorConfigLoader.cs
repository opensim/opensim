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
using System.Linq;
using System.Reflection;
using log4net;
using Nini.Config;
using OpenSim.Framework.Security.DOSProtector.Interfaces;
using OpenSim.Framework.Security.DOSProtector.Options;

namespace OpenSim.Framework.Security.DOSProtector
{
    /// <summary>
    /// Loads DOS Protector configuration from INI file and configures the builder
    /// </summary>
    public static class DOSProtectorConfigLoader
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod()?.DeclaringType);
        private static bool _loaded = false;
        private static readonly Dictionary<string, IDOSProtectorOptions> _optionsCache = new();

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
            _optionsCache.Clear();
        }

        /// <summary>
        /// Loads options for a specific service from INI configuration.
        /// Each service can have its own protector implementation(s) and settings.
        /// Supports multiple implementations for hybrid protectors (comma-separated).
        /// </summary>
        /// <param name="serviceIdentifier">Unique identifier for the service (e.g., "LoginService", "AssetService")</param>
        /// <param name="configPath">Optional path to config file. If null, uses DOSProtector.ini in bin/</param>
        /// <returns>Array of configured options instances, or null if not configured</returns>
        public static IDOSProtectorOptions[] LoadServiceOptions(string serviceIdentifier, string configPath = null)
        {
            if (string.IsNullOrWhiteSpace(serviceIdentifier))
            {
                m_log.Error("[DOSProtectorConfig]: Service identifier cannot be null or empty");
                return null;
            }

            string cacheKey = $"Service_{serviceIdentifier}";

            // Check if we already have cached options for this service
            if (_optionsCache.TryGetValue(cacheKey, out var cachedOptions))
            {
                // Options cache stores the first option, but we need to parse the full array
                // For now, we'll just re-parse (could be optimized with separate cache)
            }

            try
            {
                // Determine config file path
                if (string.IsNullOrEmpty(configPath))
                {
                    string binDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "./";
                    configPath = Path.Combine(binDir, "DOSProtector.ini");
                }

                if (!File.Exists(configPath))
                {
                    m_log.Debug($"[DOSProtectorConfig]: Config file not found for service {serviceIdentifier}");
                    return null;
                }

                var configSource = new IniConfigSource(configPath);

                // Look for service-specific section: [ServiceName.DOSProtector]
                string sectionName = $"{serviceIdentifier}.DOSProtector";
                var config = configSource.Configs[sectionName];

                if (config == null)
                {
                    m_log.Debug($"[DOSProtectorConfig]: No configuration section [{sectionName}] found");
                    return null;
                }

                // Read the Implementation property - supports comma-separated list for hybrid
                string implementationStr = config.GetString("Implementation", "BasicDOSProtector");
                var implementations = implementationStr.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim())
                    .Where(s => !string.IsNullOrEmpty(s))
                    .ToArray();

                if (implementations.Length == 0)
                {
                    m_log.Error($"[DOSProtectorConfig]: No valid implementations specified for service {serviceIdentifier}");
                    return null;
                }

                m_log.Info($"[DOSProtectorConfig]: Loading {implementations.Length} implementation(s) for service {serviceIdentifier}: {string.Join(", ", implementations)}");

                var optionsList = new List<IDOSProtectorOptions>();

                // For each implementation, look for implementation-specific section or use shared section
                foreach (var implementation in implementations)
                {
                    // Try implementation-specific section first: [ServiceName.Implementation]
                    string implSectionName = $"{serviceIdentifier}.{implementation}";
                    var implConfig = configSource.Configs[implSectionName];

                    // Fall back to shared service section
                    if (implConfig == null)
                        implConfig = config;

                    IDOSProtectorOptions options = CreateOptionsForImplementation(implementation);

                    if (options == null)
                    {
                        m_log.Warn($"[DOSProtectorConfig]: Unknown implementation '{implementation}' for service {serviceIdentifier}, skipping");
                        continue;
                    }

                    // Populate options from config
                    PopulateOptionsFromConfig(options, implConfig);
                    optionsList.Add(options);

                    m_log.Debug($"[DOSProtectorConfig]: Loaded {implementation} configuration for {serviceIdentifier}");
                }

                if (optionsList.Count == 0)
                {
                    m_log.Error($"[DOSProtectorConfig]: No valid options loaded for service {serviceIdentifier}");
                    return null;
                }

                var result = optionsList.ToArray();

                // Cache the first option (for backward compatibility)
                if (result.Length > 0)
                    _optionsCache[cacheKey] = result[0];

                return result;
            }
            catch (Exception ex)
            {
                m_log.Error($"[DOSProtectorConfig]: Error loading options for service {serviceIdentifier}: {ex.Message}", ex);
                return null;
            }
        }

        /// <summary>
        /// Creates an options instance for the specified implementation name.
        /// Supports both built-in and external plugin implementations.
        /// </summary>
        private static IDOSProtectorOptions CreateOptionsForImplementation(string implementation)
        {
            // Normalize implementation name
            string normalizedName = implementation.Replace("DOSProtector", "").Replace("Protector", "");

            // 1. Try built-in types first (fast path)
            var builtIn = normalizedName.ToLowerInvariant() switch
            {
                "basic" => new Options.BasicDosProtectorOptions(),
                "advanced" => new Options.AdvancedDosProtectorOptions(),
                "tokenbucket" => new Options.TokenBucketDosProtectorOptions(),
                "authenticated" => new Options.AuthenticatedDosProtectorOptions(),
                "ipfilter" => new Options.IPFilterDosProtectorOptions(),
                "honeypot" => new Options.HoneypotDosProtectorOptions(),
                _ => null
            };

            if (builtIn != null)
                return builtIn;

            // 2. Try to find external plugin via DOSProtectorBuilder
            // Get all discovered protectors (includes external plugins)
            var optionsToProtectorMap = DOSProtectorBuilder.GetOptionsToProtectorMap();

            foreach (var kvp in optionsToProtectorMap)
            {
                Type optionsType = kvp.Key;
                Type protectorType = kvp.Value;

                // Match by protector name (case-insensitive)
                string protectorShortName = protectorType.Name
                    .Replace("DOSProtector", "")
                    .Replace("Protector", "");

                if (protectorShortName.Equals(normalizedName, StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        return (IDOSProtectorOptions)Activator.CreateInstance(optionsType);
                    }
                    catch (Exception ex)
                    {
                        m_log.Error($"[DOSProtectorConfig]: Failed to create instance of {optionsType.Name}: {ex.Message}");
                        return null;
                    }
                }
            }

            m_log.Warn($"[DOSProtectorConfig]: Unknown implementation '{implementation}' - not found in built-in or discovered plugins");
            return null;
        }

        /// <summary>
        /// Populates options object properties from INI config section using reflection
        /// </summary>
        private static void PopulateOptionsFromConfig(IDOSProtectorOptions options, IConfig config)
        {
            var type = options.GetType();
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var prop in properties)
            {
                if (!prop.CanWrite)
                    continue;

                string key = prop.Name;

                try
                {
                    if (prop.PropertyType == typeof(int))
                    {
                        int value = config.GetInt(key, (int)prop.GetValue(options));
                        prop.SetValue(options, value);
                    }
                    else if (prop.PropertyType == typeof(bool))
                    {
                        bool value = config.GetBoolean(key, (bool)prop.GetValue(options));
                        prop.SetValue(options, value);
                    }
                    else if (prop.PropertyType == typeof(string))
                    {
                        string value = config.GetString(key, (string)prop.GetValue(options) ?? "");
                        prop.SetValue(options, value);
                    }
                    else if (prop.PropertyType == typeof(double))
                    {
                        double value = config.GetDouble(key, (double)prop.GetValue(options));
                        prop.SetValue(options, value);
                    }
                    else if (prop.PropertyType == typeof(float))
                    {
                        float value = config.GetFloat(key, (float)prop.GetValue(options));
                        prop.SetValue(options, value);
                    }
                    else if (prop.PropertyType == typeof(List<string>))
                    {
                        string value = config.GetString(key, "");
                        if (!string.IsNullOrWhiteSpace(value))
                        {
                            var list = value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                .Select(s => s.Trim())
                                .Where(s => !string.IsNullOrEmpty(s))
                                .ToList();
                            prop.SetValue(options, list);
                        }
                    }
                    else if (prop.PropertyType.IsEnum)
                    {
                        string value = config.GetString(key, prop.GetValue(options)?.ToString() ?? "");
                        if (!string.IsNullOrEmpty(value) && Enum.TryParse(prop.PropertyType, value, true, out var enumValue))
                        {
                            prop.SetValue(options, enumValue);
                        }
                    }
                }
                catch (Exception ex)
                {
                    m_log.Warn($"[DOSProtectorConfig]: Failed to set property {key}: {ex.Message}");
                }
            }
        }
    }
}
