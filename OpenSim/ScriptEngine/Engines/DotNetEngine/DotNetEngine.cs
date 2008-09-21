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
using System.Reflection;
using System.Text;
using log4net;
using Nini.Config;
using OpenSim.ApplicationPlugins.ScriptEngine;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Scenes;
using ComponentProviders = OpenSim.ApplicationPlugins.ScriptEngine.ComponentRegistry;

namespace OpenSim.ScriptEngine.Engines.DotNetEngine
{
    // This is a sample engine
    public class DotNetEngine : RegionScriptEngineBase
    {
        

        // This will be the makeup of this script engine
        public string[] ComponentNames = new string[] {
                    "Commands_LSL",
                    "Commands_OSSL",
                    "Compiler_CS",
                    "Compiler_JS",
                    "Compiler_LSL",
                    "Compiler_VB",
                    "LSLEventProvider",
                    "Scheduler"
                };

        public override string Name
        {
            get { return "DotNetEngine"; }
        }

        public override void Initialize()
        {
            // We need to initialize the components we will be using. Our baseclass already has builtin functions for this.
            m_log.Info("[" + Name + "]: Initializing SECs (Script Engine Components)");
            InitializeComponents(ComponentNames);
        }

        public override void PreClose()
        {
            // Before 
        }
    }
}
