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
