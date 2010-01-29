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
using System.Reflection;
using Nini.Config;
using log4net;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using Mono.Addins;
using OpenMetaverse;

namespace OpenSim.Region.OptionalModules.Scripting.ScriptModuleComms
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "ScriptModuleCommsModule")]
    class ScriptModuleCommsModule : INonSharedRegionModule, IScriptModuleComms
    {
        private static readonly ILog m_log =
            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private IScriptModule m_scriptModule = null;

        public event ScriptCommand OnScriptCommand;

        public void Initialise(IConfigSource config)
        {
        }

        public void AddRegion(Scene scene)
        {
            scene.RegisterModuleInterface<IScriptModuleComms>(this);
        }

        public void RemoveRegion(Scene scene)
        {
        }

        public void RegionLoaded(Scene scene)
        {
            m_scriptModule = scene.RequestModuleInterface<IScriptModule>();
            
            if (m_scriptModule != null)
                m_log.Info("[MODULE COMMANDS]: Script engine found, module active");
        }

        public string Name
        {
            get { return "ScriptModuleCommsModule"; }
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public void Close()
        {
        }

        public void RaiseEvent(UUID script, string id, string module, string command, string k)
        {
            ScriptCommand c = OnScriptCommand;

            if (c == null)
                return;

            c(script, id, module, command, k);
        }

        public void DispatchReply(UUID script, int code, string text, string k)
        {
            if (m_scriptModule == null)
                return;

            Object[] args = new Object[] {-1, code, text, k};

            m_scriptModule.PostScriptEvent(script, "link_message", args);
        }
    }
}
