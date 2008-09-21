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
using System.Text;
using OpenSim.ApplicationPlugins.ScriptEngine;
using OpenSim.ApplicationPlugins.ScriptEngine.Components;

namespace OpenSim.ApplicationPlugins.ScriptEngine
{
    /// <summary>
    /// Component providers
    /// This is where any component (any part) of the Script Engine Component System (SECS) registers.
    /// Nothing is instanciated at this point. The modules just need to register here to be available for any script engine.
    /// </summary>
    public static class ComponentRegistry
    {
        // Component providers are registered here wit a name (string)
        // When a script engine is created the components are instanciated
        public static Dictionary<string, Type> providers = new Dictionary<string, Type>();
        public static Dictionary<string, Type> scriptEngines = new Dictionary<string, Type>();

        ///// <summary>
        ///// Returns a list of ProviderBase objects which has been instanciated by their name
        ///// </summary>
        ///// <param name="Providers">List of Script Engine Components</param>
        ///// <returns></returns>
        //public static List<ComponentBase> GetComponents(string[] Providers)
        //{
        //    List<ComponentBase> pbl = new List<ComponentBase>();
        //    if (Providers != null)
        //    {
        //        foreach (string p in Providers)
        //        {
        //            if (providers.ContainsKey(p))
        //                pbl.Add(Activator.CreateInstance(providers[p]) as ComponentBase);
        //        }
        //    }

        //    return pbl;
        //}

    }
}