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
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using log4net;
using OpenSim.ApplicationPlugins.ScriptEngine.Components;

namespace OpenSim.ApplicationPlugins.ScriptEngine
{
    /// <summary>
    /// Used to load ScriptEngine component .dll's
    /// </summary>
    internal class ComponentLoader
    {
        internal static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private ScriptEnginePlugin scriptEnginePlugin;
        public ComponentLoader(ScriptEnginePlugin sep)
        {
            scriptEnginePlugin = sep;
        }

        /// <summary>
        /// Load components from directory
        /// </summary>
        /// <param name="directory"></param>
        public void Load(string directory)
        {
            // We may want to change how this functions as currently it required unique class names for each component

            foreach (string file in Directory.GetFiles(directory, "*.dll"))
            {
                //m_log.DebugFormat("[ScriptEngine]: Loading: [{0}].", file);
                Assembly componentAssembly = Assembly.LoadFrom(file);
                if (componentAssembly != null)
                {
                    try
                    {
                        // Go through all types in the assembly
                        foreach (Type componentType in componentAssembly.GetTypes())
                        {
                            if (componentType.IsPublic
                                && !componentType.IsAbstract)
                            {
                                if (componentType.IsSubclassOf(typeof (ComponentBase)))
                                {
                                    // We have found an type which is derived from ProdiverBase, add it to provider list
                                    m_log.InfoFormat("[ScriptEngine]: Adding component: {0}", componentType.Name);
                                    ComponentRegistry.providers.Add(componentType.Name, componentType);
                                }
                                if (componentType.IsSubclassOf(typeof(RegionScriptEngineBase)))
                                {
                                    // We have found an type which is derived from RegionScriptEngineBase, add it to engine list
                                    m_log.InfoFormat("[ScriptEngine]: Adding script engine: {0}", componentType.Name);
                                    ComponentRegistry.scriptEngines.Add(componentType.Name, componentType);
                                }
                            }
                        }
                    }
                    catch
                        (ReflectionTypeLoadException)
                    {
                        m_log.InfoFormat("[ScriptEngine]: Could not load types for [{0}].", componentAssembly.FullName);
                    }
                } //if
            } //foreach
        }
    }
}