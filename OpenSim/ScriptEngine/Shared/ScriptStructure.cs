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
using System.Reflection;
using System.Text;
using log4net;
using OpenMetaverse;
using OpenSim.Region.ScriptEngine.Interfaces;
using OpenSim.Region.ScriptEngine.Shared.ScriptBase;
using OpenSim.ScriptEngine.Shared;

namespace OpenSim.ScriptEngine.Shared
{
    public struct ScriptStructure
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public RegionInfoStructure RegionInfo;
        public ScriptMetaData ScriptMetaData;

        public ScriptAssemblies.IScript ScriptObject;
        public string State;
        public bool Running;
        public bool Disabled;
        public string Source;
        public int StartParam;
        public AppDomain AppDomain;
        public Dictionary<string, IScriptApi> Apis;
        public Dictionary<KeyValuePair<int, int>, KeyValuePair<int, int>> LineMap;
        public uint LocalID;
        public UUID ItemID;
        public string AssemblyFileName;

        public string ScriptID { get { return LocalID.ToString() + "." + ItemID.ToString(); } }
        public string Name { get { return "Script:" + ScriptID; } }
        private bool Initialized;
        private Dictionary<string, Delegate> InternalFunctions;
        public string AssemblyName;

        public void ExecuteEvent(EventParams p)
        {
            ExecuteMethod(p, true);
        }

        public void ExecuteMethod(EventParams p)
        {
            ExecuteMethod(p, false);
        }
        private void ExecuteMethod(EventParams p, bool isEvent)
        {
            // First time initialization?
            if (!Initialized)
            {
                Initialized = true;
                CacheInternalFunctions();
            }

            lock (InternalFunctions)
            {
                // Make function name
                string FunctionName;
                if (isEvent)
                    FunctionName = State + "_event_" + p.EventName;
                else
                    FunctionName = p.EventName;

                // Check if this function exist
                if (!InternalFunctions.ContainsKey(FunctionName))
                {
                    // TODO: Send message in-world
                    m_log.ErrorFormat("[{0}] Script function \"{1}\" was not found.", Name, FunctionName);
                    return;
                }

                // Execute script function
                try
                {
                    InternalFunctions[FunctionName].DynamicInvoke(p.Params);
                }
                catch (Exception e)
                {
                    m_log.ErrorFormat("[{0}] Execute \"{1}\" failed: {2}", Name, FunctionName, e.ToString());
                }
            }
        }

        /// <summary>
        /// Cache functions into a dictionary with delegates. Should be faster than reflection.
        /// </summary>
        private void CacheInternalFunctions()
        {
            Type scriptObjectType = ScriptObject.GetType();
            InternalFunctions = new Dictionary<string, Delegate>();

            MethodInfo[] methods = scriptObjectType.GetMethods();
            lock (InternalFunctions)
            {
                // Read all methods into a dictionary
                foreach (MethodInfo mi in methods)
                {
                    // TODO: We don't support overloading
                    if (!InternalFunctions.ContainsKey(mi.Name))
                        InternalFunctions.Add(mi.Name, Delegate.CreateDelegate(scriptObjectType, ScriptObject, mi));
                    else
                        m_log.ErrorFormat("[{0}] Error: Script function \"{1}\" is already added. We do not support overloading.",
                                          Name, mi.Name);
                }
            }
        }
    }
}
