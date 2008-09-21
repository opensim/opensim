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
using Nini.Config;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Scenes;

namespace OpenSim.ApplicationPlugins.ScriptEngine
{
    public class RegionScriptEnginePlugin : IRegionModule
    {
        // This is a region module.
        // This means: Every time a new region is created, a new instance of this module is also created.
        // This module is responsible for starting the script engine for this region.

        private string tempScriptEngineName = "DotNetEngine";
        public RegionScriptEngineBase scriptEngine;
        public void Initialise(Scene scene, IConfigSource source)
        {
            // New region is being created
            // Create a new script engine
            try
            {
                scriptEngine =
                    Activator.CreateInstance(ComponentRegistry.scriptEngines[tempScriptEngineName]) as
                    RegionScriptEngineBase;
                scriptEngine.Initialize(scene, source);
            }
            catch (Exception ex)
            {
                scriptEngine.m_log.Error("[ScriptEngine]: Unable to load engine \"" + tempScriptEngineName + "\": " + ex.ToString());
            }
        }

        public void PostInitialise()
        {
            // Nothing
        }

        public void Close()
        {
            scriptEngine.Close();
        }

        public string Name
        {
            get { return "ScriptEngine Region Loader"; }
        }

        public bool IsSharedModule
        {
            get { return true; }
        }
    }
}
