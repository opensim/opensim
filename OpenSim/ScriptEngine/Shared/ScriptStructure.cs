using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using OpenMetaverse;
using OpenSim.Region.ScriptEngine.Interfaces;
using OpenSim.Region.ScriptEngine.Shared.ScriptBase;
using OpenSim.ScriptEngine.Shared;

namespace OpenSim.ScriptEngine.Shared
{
    public struct ScriptStructure
    {
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
                    //RegionInfo.Scene.
                    RegionInfo.Logger.ErrorFormat("[{0}] Script function \"{1}\" was not found.", Name, FunctionName);
                    return;
                }

                // Execute script function
                try
                {
                    InternalFunctions[FunctionName].DynamicInvoke(p.Params);
                }
                catch (Exception e)
                {
                    RegionInfo.Logger.ErrorFormat("[{0}] Execute \"{1}\" failed: {2}", Name, FunctionName, e.ToString());
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
                        RegionInfo.Logger.ErrorFormat("[{0}] Error: Script function \"{1}\" is already added. We do not support overloading.",
                            Name, mi.Name);
                }
            }
        }

    }
}