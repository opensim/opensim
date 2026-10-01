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
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using log4net;
using OpenSim.Framework.Security.DOSProtector.Core.Plugin.Zero;
using OpenSim.Framework.Security.DOSProtector.SDK;

namespace OpenSim.Framework.Security.DOSProtector.Core
{
    /// <summary>
    /// Builder class for creating DOS protector instances based on options type.
    /// Uses reflection and DOSProtectorOptionsAttribute to dynamically map options to implementations.
    /// Supports plugin assemblies loaded from configured paths.
    /// </summary>
    [SuppressMessage("ReSharper", "InconsistentlySynchronizedField")]
    public static class DOSProtectorBuilder
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod()?.DeclaringType);
        private static readonly Dictionary<Type, Type> _optionsToProtectorMap = new();
        private static readonly HashSet<string> _scannedAssemblies = [];
        private static readonly List<string> _pluginPaths = [];
        private static bool _initialized;
        private static readonly object _lock = new();

        /// <summary>
        /// Configures additional plugin paths to scan for DOS protector implementations.
        /// Must be called before first Build() call to take effect.
        /// </summary>
        /// <param name="paths">Paths to directories or DLL files containing DOS protector plugins</param>
        public static void ConfigurePluginPaths(params string[] paths)
        {
            lock (_lock)
            {
                foreach (var path in paths)
                {
                    if (string.IsNullOrEmpty(path) || _pluginPaths.Contains(path)) 
                        continue;
                    
                    _pluginPaths.Add(path);
                    m_log.Info($"[DOSProtectorBuilder]: Added plugin path: {path}");
                }

                // If already initialized, force re-scan
                if (!_initialized) 
                    return;
                
                m_log.Info("[DOSProtectorBuilder]: Plugin paths changed, triggering re-scan");
                RefreshCache();
            }
        }

        /// <summary>
        /// Clears the cache and re-scans all assemblies for DOS protector implementations.
        /// Useful for loading new plugins at runtime.
        /// </summary>
        public static void RefreshCache()
        {
            lock (_lock)
            {
                m_log.Info("[DOSProtectorBuilder]: Refreshing DOS protector cache");
                _optionsToProtectorMap.Clear();
                _scannedAssemblies.Clear();
                _initialized = false;
                Initialize();
            }
        }

        /// <summary>
        /// Returns information about discovered DOS protector implementations.
        /// </summary>
        public static Dictionary<string, string> GetDiscoveredProtectors()
        {
            lock (_lock)
            {
                Initialize();
                var result = new Dictionary<string, string>();
                foreach (var kvp in _optionsToProtectorMap)
                {
                    result[kvp.Key.Name] = kvp.Value.Name;
                }
                return result;
            }
        }

        /// <summary>
        /// Returns the full mapping of options types to protector types.
        /// Used internally by DOSProtectorConfigLoader for plugin resolution.
        /// </summary>
        internal static Dictionary<Type, Type> GetOptionsToProtectorMap()
        {
            lock (_lock)
            {
                Initialize();
                return new Dictionary<Type, Type>(_optionsToProtectorMap);
            }
        }

        /// <summary>
        /// Initializes the mapping between options types and protector implementations by scanning assemblies.
        /// </summary>
        private static void Initialize()
        {
            if (_initialized)
                return;

            lock (_lock)
            {
                if (_initialized)
                    return;

                m_log.Info("[DOSProtectorBuilder]: Initializing DOS protector registry");

                // 1. Scan current assembly (core implementations)
                ScanAssembly(Assembly.GetExecutingAssembly());

                // 2. Scan all currently loaded assemblies (for plugins already loaded)
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    // Skip system assemblies for performance
                    if (IsSystemAssembly(assembly))
                        continue;

                    ScanAssembly(assembly);
                }

                // 3. Scan configured plugin paths
                LoadPluginAssemblies();

                m_log.Info($"[DOSProtectorBuilder]: Discovered {_optionsToProtectorMap.Count} DOS protector implementations");

                _initialized = true;
            }
        }

        /// <summary>
        /// Scans a single assembly for DOS protector implementations.
        /// </summary>
        private static void ScanAssembly(Assembly assembly)
        {
            if (assembly == null)
                return;

            var assemblyName = assembly.FullName ?? assembly.GetName().Name ?? "Unknown";

            // Skip if already scanned
            if (_scannedAssemblies.Contains(assemblyName))
                return;

            _scannedAssemblies.Add(assemblyName);

            try
            {
                var protectorTypes = assembly.GetTypes()
                    .Where(t => typeof(IDOSProtector).IsAssignableFrom(t)
                                && !t.IsAbstract
                                && !t.IsInterface);

                foreach (var protectorType in protectorTypes)
                {
                    var attribute = protectorType.GetCustomAttribute<DOSProtectorOptionsAttribute>();
                    if (attribute is { OptionsType: not null })
                    {
                        // Register or update mapping
                        if (_optionsToProtectorMap.ContainsKey(attribute.OptionsType))
                        {
                            m_log.Warn($"[DOSProtectorBuilder]: Duplicate protector for {attribute.OptionsType.Name}, " +
                                      $"replacing {_optionsToProtectorMap[attribute.OptionsType].Name} with {protectorType.Name}");
                        }

                        _optionsToProtectorMap[attribute.OptionsType] = protectorType;
                        m_log.Debug($"[DOSProtectorBuilder]: Registered {protectorType.Name} for {attribute.OptionsType.Name}");
                    }
                }
            }
            catch (ReflectionTypeLoadException ex)
            {
                m_log.Warn($"[DOSProtectorBuilder]: Could not scan assembly {assemblyName}: {ex.Message}");
            }
            catch (Exception ex)
            {
                m_log.Error($"[DOSProtectorBuilder]: Error scanning assembly {assemblyName}: {ex.Message}");
            }
        }

        /// <summary>
        /// Loads plugin assemblies from configured paths.
        /// </summary>
        private static void LoadPluginAssemblies()
        {
            foreach (var path in _pluginPaths)
            {
                try
                {
                    if (File.Exists(path))
                    {
                        // Single DLL file
                        LoadPluginAssembly(path);
                    }
                    else if (Directory.Exists(path))
                    {
                        // Directory - scan for DLLs
                        var dllFiles = Directory.GetFiles(path, "*.dll", SearchOption.TopDirectoryOnly);
                        foreach (var dll in dllFiles)
                        {
                            LoadPluginAssembly(dll);
                        }
                    }
                    else
                    {
                        m_log.Warn($"[DOSProtectorBuilder]: Plugin path not found: {path}");
                    }
                }
                catch (Exception ex)
                {
                    m_log.Error($"[DOSProtectorBuilder]: Error loading plugins from {path}: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Loads a single plugin assembly and scans it.
        /// </summary>
        private static void LoadPluginAssembly(string path)
        {
            try
            {
                var assembly = Assembly.LoadFrom(path);
                m_log.Info($"[DOSProtectorBuilder]: Loaded plugin assembly: {Path.GetFileName(path)}");
                ScanAssembly(assembly);
            }
            catch (Exception ex)
            {
                m_log.Warn($"[DOSProtectorBuilder]: Could not load plugin assembly {path}: {ex.Message}");
            }
        }

        /// <summary>
        /// Checks if an assembly is a system assembly (to skip scanning for performance).
        /// </summary>
        private static bool IsSystemAssembly(Assembly assembly)
        {
            if (assembly == null)
                return true;

            var name = assembly.GetName().Name;
            if (name == null)
                return true;

            return name.StartsWith("System.") ||
                   name.StartsWith("Microsoft.") ||
                   name.StartsWith("mscorlib") ||
                   name.StartsWith("netstandard") ||
                   name == "log4net";
        }

        /// <summary>
        /// Builds a DOS protector instance based on identifier and options.
        /// If options is null or empty, attempts to load from configuration.
        /// Multiple options create a HybridDOSProtector that chains protectors in order.
        /// Uses DOSProtectorOptionsAttribute to determine the correct implementation.
        /// </summary>
        /// <param name="identifier">Unique identifier for this protector instance (e.g., "LoginService")</param>
        /// <param name="options">Optional: Pre-configured options. If null/empty, loads from DOSProtector.ini</param>
        /// <returns>Configured DOS protector instance</returns>
        public static IDOSProtector Build(string identifier, params IDOSProtectorOptions[] options)
        {
            if (string.IsNullOrWhiteSpace(identifier))
                throw new ArgumentException("Identifier cannot be null or empty", nameof(identifier));

            Initialize();

            // If no options provided, try to load from configuration
            if (options == null || options.Length == 0)
            {
                options = DOSProtectorConfigLoader.LoadServiceOptions(identifier);

                // If still no options found, use ZeroProtector
                if (options == null || options.Length == 0)
                {
                    options = new IDOSProtectorOptions[] { new ZeroOptions { ReportingName = identifier } };
                }
            }

            // Single protector
            if (options.Length == 1)
            {
                return BuildSingle(options[0]);
            }

            // Multiple protectors - create hybrid
            m_log.Info($"[DOSProtectorBuilder]: Creating hybrid protector with {options.Length} implementations for '{identifier}'");
            var protectors = new List<IDOSProtector>();

            foreach (var option in options)
            {
                if (option != null)
                {
                    protectors.Add(BuildSingle(option));
                }
            }

            return new HybridDOSProtector(protectors);
        }

        /// <summary>
        /// Builds a single DOS protector instance from options
        /// </summary>
        private static IDOSProtector BuildSingle(IDOSProtectorOptions options)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            var optionsType = options.GetType();

            // Try to find exact match
            if (_optionsToProtectorMap.TryGetValue(optionsType, out var protectorType))
            {
                return CreateInstance(protectorType, options);
            }

            // Try to find match for base types
            var currentType = optionsType.BaseType;
            while (currentType != null && currentType != typeof(object))
            {
                if (_optionsToProtectorMap.TryGetValue(currentType, out protectorType))
                {
                    return CreateInstance(protectorType, options);
                }
                currentType = currentType.BaseType;
            }
            
            throw new InvalidOperationException(
                $"No DOS protector implementation found for options type {optionsType.Name}");
        }

        private static IDOSProtector CreateInstance(Type protectorType, IDOSProtectorOptions options)
        {
            try
            {
                // Find constructor that takes IDOSProtectorOptions
                var constructor = protectorType.GetConstructor(new[] { options.GetType() });
                if (constructor == null)
                {
                    // Try to find constructor with base type
                    constructor = protectorType.GetConstructor(
                        BindingFlags.Public | BindingFlags.Instance,
                        null,
                        new[] { typeof(IDOSProtectorOptions) },
                        null);
                }

                if (constructor != null)
                {
                    return (IDOSProtector)constructor.Invoke(new object[] { options });
                }

                throw new InvalidOperationException(
                    $"No suitable constructor found for {protectorType.Name} accepting {options.GetType().Name}");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Failed to create instance of {protectorType.Name}", ex);
            }
        }
    }
}
