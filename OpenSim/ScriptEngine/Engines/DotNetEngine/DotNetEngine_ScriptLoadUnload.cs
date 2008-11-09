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
using System.Text;
using OpenMetaverse;
using OpenSim.ScriptEngine.Components.DotNetEngine.Events;
using OpenSim.ScriptEngine.Shared;

namespace OpenSim.ScriptEngine.Engines.DotNetEngine
{
    public partial class DotNetEngine
    {

        //internal Dictionary<int, IScriptScheduler> ScriptMapping = new Dictionary<int, IScriptScheduler>();


        //
        // HANDLE EVENTS FROM SCRIPTS
        // We will handle script add, change and remove events outside of command pipeline
        //
        #region Script Add/Change/Remove
        void Events_RezScript(uint localID, UUID itemID, string script, int startParam, bool postOnRez, string engine)
        {
            // ###
            // # New script created
            // ###
            m_log.DebugFormat(
                "[{0}] NEW SCRIPT: localID: {1}, itemID: {2}, startParam: {3}, postOnRez: {4}, engine: {5}",
                Name, localID, itemID, startParam, postOnRez, engine);

            // Make a script object
            ScriptStructure scriptObject = new ScriptStructure();
            scriptObject.RegionInfo = RegionInfo;
            scriptObject.LocalID = localID;
            scriptObject.ItemID = itemID;
            scriptObject.Source = script;

            //
            // Get MetaData from script header
            //
            ScriptMetaData scriptMetaData = ScriptMetaData.Extract(ref script);
            scriptObject.ScriptMetaData = scriptMetaData;
            foreach (string key in scriptObject.ScriptMetaData.Keys)
            {
                m_log.DebugFormat("[{0}] Script metadata: Key: \"{1}\", Value: \"{2}\".", Name, key, scriptObject.ScriptMetaData[key]);
            }

            //
            // Load this assembly
            //
            // TODO: Use Executor to send a command instead?
            m_log.DebugFormat("[{0}] Adding script to scheduler", Name);
            RegionInfo.FindScheduler(scriptObject.ScriptMetaData).AddScript(scriptObject);
            // Add to our internal mapping
            //ScriptMapping.Add(itemID, Schedulers[scheduler]);
        }

        private void Events_RemoveScript(uint localID, UUID itemID)
        {
            // Tell all schedulers to remove this item
            foreach (IScriptScheduler scheduler in RegionInfo.Schedulers.Values)
            {
                scheduler.Removecript(localID, itemID);
            }
        }
        #endregion

    }
}
