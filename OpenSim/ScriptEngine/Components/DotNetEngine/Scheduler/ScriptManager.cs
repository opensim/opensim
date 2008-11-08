using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Threading;
using log4net;
using OpenMetaverse;
using OpenSim.Region.ScriptEngine.Shared;
using OpenSim.ScriptEngine.Shared;
using EventParams=OpenSim.ScriptEngine.Shared.EventParams;

namespace OpenSim.ScriptEngine.Components.DotNetEngine.Scheduler
{
    public partial class ScriptManager: IScriptExecutor
    {
        private const int NoWorkSleepMs = 50;
        private const int NoWorkSleepMsInc = 1;                 // How much time to increase wait with on every iteration
        private const int NoWorkSleepMsIncMax = 300;            // Max time to wait

        internal static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        public string Name { get { return "SECS.DotNetEngine.ScriptManager"; } }
        private static Thread ScriptLoadUnloadThread;
        public Dictionary<uint, Dictionary<UUID, ScriptStructure>> Scripts = new Dictionary<uint, Dictionary<UUID, ScriptStructure>>();

        private RegionInfoStructure CurrentRegion;
        public void Initialize(RegionInfoStructure currentRegion)
        {
            CurrentRegion = currentRegion;
        }

        public ScriptManager()
        {
            ScriptLoadUnloadThread = new Thread(LoadUnloadLoop);
            ScriptLoadUnloadThread.Name = "ScriptLoadUnloadThread";
            ScriptLoadUnloadThread.IsBackground = true;
            ScriptLoadUnloadThread.Start();
        }
        public void Close() { }

        private void LoadUnloadLoop ()
        {
            int _NoWorkSleepMsInc = 0;
            while (true)
            {
                if (DoScriptLoadUnload())
                {
                    // We found work, reset counter
                    _NoWorkSleepMsInc = NoWorkSleepMs;
                } else
                {
                    // We didn't find work
                    // Sleep
                    Thread.Sleep(NoWorkSleepMs + NoWorkSleepMsInc);
                    // Increase sleep delay
                    _NoWorkSleepMsInc += NoWorkSleepMsInc;
                    // Make sure we don't exceed max
                    if (_NoWorkSleepMsInc > NoWorkSleepMsIncMax)
                        _NoWorkSleepMsInc = NoWorkSleepMsIncMax;
                }
            }
        }

        #region Add/Remove/Find script functions for our Script memory structure
        private void MemAddScript(ScriptStructure script)
        {
            lock (scriptLock)
            {
                // Create object if it doesn't exist
                if (!Scripts.ContainsKey(script.LocalID))
                    Scripts.Add(script.LocalID, new Dictionary<UUID, ScriptStructure>());

                // Delete script if it exists
                Dictionary<UUID, ScriptStructure> Obj;
                if (Scripts.TryGetValue(script.LocalID, out Obj))
                    if (Obj.ContainsKey(script.ItemID) == true)
                        Obj.Remove(script.ItemID);

                // Add to object
                Obj.Add(script.ItemID, script);
            }
        }
        private void MemRemoveScript(uint LocalID, UUID ItemID)
        {
            // TODO: Also clean up command queue and async commands for object
            lock (scriptLock)
            {
                // Create object if it doesn't exist
                if (!Scripts.ContainsKey(LocalID))
                    return;

                // Delete script if it exists
                Dictionary<UUID, ScriptStructure> Obj;
                if (Scripts.TryGetValue(LocalID, out Obj))
                    if (Obj.ContainsKey(ItemID) == true)
                        Obj.Remove(ItemID);

                // Empty?
                if (Obj.Count == 0)
                    Scripts.Remove(LocalID);

            }
        }
        public bool TryGetScript(uint localID, UUID itemID, ref ScriptStructure script)
        {
            lock (scriptLock)
            {

                if (Scripts.ContainsKey(localID) == false)
                    return false;

                Dictionary<UUID, ScriptStructure> Obj;
                if (Scripts.TryGetValue(localID, out Obj))
                    if (Obj.ContainsKey(itemID) == false)
                        return false;

                // Get script
                return Obj.TryGetValue(itemID, out script);
            }
        }
        public ScriptStructure GetScript(uint localID, UUID itemID)
        {
            lock (scriptLock)
            {

                if (Scripts.ContainsKey(localID) == false)
                    throw new Exception("No script with LocalID " + localID + " was found.");

                Dictionary<UUID, ScriptStructure> Obj;
                if (Scripts.TryGetValue(localID, out Obj))
                    if (Obj.ContainsKey(itemID) == false)
                        throw new Exception("No script with ItemID " + itemID + " was found.");

                // Get script
                return Obj[itemID];
            }
        }
        public bool TryGetScripts(uint localID, ref Dictionary<UUID, ScriptStructure> returnList)
        {
            Dictionary<UUID, ScriptStructure> getList = GetScripts(localID);
                if (getList != null)
                {
                    returnList = getList;
                    return true;
                }
            return false;
        }
        public Dictionary<UUID, ScriptStructure> GetScripts(uint localID)
        {
            lock (scriptLock)
            {

                if (Scripts.ContainsKey(localID) == false)
                    return null;
                return Scripts[localID];
            }
        }
        #endregion

        public void ExecuteCommand(EventParams p)
        {
            ScriptStructure ss = new ScriptStructure();
            if (TryGetScript(p.LocalID, p.ItemID, ref ss))
                ExecuteCommand(ref ss, p);
        }

        public void ExecuteCommand(ref ScriptStructure scriptContainer, EventParams p)
        {
            m_log.DebugFormat("[{0}] ######################################################", Name);
            m_log.DebugFormat("[{0}] Command execution ItemID {1}: \"{2}\".", Name, scriptContainer.ItemID, p.EventName);
            scriptContainer.ExecuteEvent(p);
            m_log.DebugFormat("[{0}] ######################################################", Name);
        }

    }
}
