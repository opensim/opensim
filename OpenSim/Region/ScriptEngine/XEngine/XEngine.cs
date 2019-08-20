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
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security;
using System.Security.Policy;
using System.Text;
using System.Threading;
using System.Xml;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using log4net;
using Nini.Config;
using Amib.Threading;
using Mono.Addins;
using OpenSim.Framework;
using OpenSim.Framework.Console;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.ScriptEngine.Interfaces;
using OpenSim.Region.ScriptEngine.Shared;
using OpenSim.Region.ScriptEngine.Shared.CodeTools;
using OpenSim.Region.ScriptEngine.Shared.Instance;
using OpenSim.Region.ScriptEngine.Shared.Api;
using OpenSim.Region.ScriptEngine.Shared.Api.Plugins;
using OpenSim.Region.ScriptEngine.Shared.ScriptBase;
using OpenSim.Region.ScriptEngine.XEngine.ScriptBase;
using Timer = OpenSim.Region.ScriptEngine.Shared.Api.Plugins.Timer;

using ScriptCompileQueue = OpenSim.Framework.LocklessQueue<object[]>;

namespace OpenSim.Region.ScriptEngine.XEngine
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "XEngine")]
    public class XEngine : INonSharedRegionModule, IScriptModule, IScriptEngine
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// Control the printing of certain debug messages.
        /// </summary>
        /// <remarks>
        /// If DebugLevel >= 1, then we log every time that a script is started.
        /// </remarks>
        public int DebugLevel { get; set; }

        /// <summary>
        /// A parameter to allow us to notify the log if at least one script has a compilation that is not compatible
        /// with ScriptStopStrategy.
        /// </summary>
        public bool HaveNotifiedLogOfScriptStopMismatch { get; private set; }

        private SmartThreadPool m_ThreadPool;
        private int m_MaxScriptQueue;
        private Scene m_Scene;
        private IConfig m_ScriptConfig = null;
        private IConfigSource m_ConfigSource = null;
        private ICompiler m_Compiler;
        private int m_MinThreads;
        private int m_MaxThreads;

        /// <summary>
        /// Amount of time to delay before starting.
        /// </summary>
        private int m_StartDelay;

        /// <summary>
        /// Are we stopping scripts co-operatively by inserting checks in them at C# compile time (true) or aborting
        /// their threads (false)?
        /// </summary>
        private bool m_coopTermination;

        private int m_IdleTimeout;
        private int m_StackSize;
        private int m_SleepTime;
        private int m_SaveTime;
        private ThreadPriority m_Prio;
        private bool m_Enabled = false;
        private bool m_InitialStartup = true;
        private int m_ScriptFailCount; // Number of script fails since compile queue was last empty
        private string m_ScriptErrorMessage;
        private bool m_AppDomainLoading;
        private bool m_CompactMemOnLoad;
        private Dictionary<UUID,ArrayList> m_ScriptErrors =
                new Dictionary<UUID,ArrayList>();

        // disable warning: need to keep a reference to XEngine.EventManager
        // alive to avoid it being garbage collected
#pragma warning disable 414
        private EventManager m_EventManager;
#pragma warning restore 414
        private IXmlRpcRouter m_XmlRpcRouter;
        private int m_EventLimit;
        private bool m_KillTimedOutScripts;

        /// <summary>
        /// Number of milliseconds we will wait for a script event to complete on script stop before we forcibly abort
        /// its thread.
        /// </summary>
        /// <remarks>
        /// It appears that if a script thread is aborted whilst it is holding ReaderWriterLockSlim (possibly the write
        /// lock) then the lock is not properly released.  This causes mono 2.6, 2.10 and possibly
        /// later to crash, sometimes with symptoms such as a leap to 100% script usage and a vm thead dump showing
        /// all threads waiting on release of ReaderWriterLockSlim write thread which none of the threads listed
        /// actually hold.
        ///
        /// Pausing for event completion reduces the risk of this happening.  However, it may be that aborting threads
        /// is not a mono issue per se but rather a risky activity in itself in an AppDomain that is not immediately
        /// shutting down.
        /// </remarks>
        private int m_WaitForEventCompletionOnScriptStop = 1000;

        private string m_ScriptEnginesPath = null;

        private ExpiringCache<UUID, bool> m_runFlags = new ExpiringCache<UUID, bool>();

        /// <summary>
        /// Is the entire simulator in the process of shutting down?
        /// </summary>
        private bool m_SimulatorShuttingDown;

        private static List<XEngine> m_ScriptEngines =
                new List<XEngine>();

        // Maps the local id to the script inventory items in it

        private Dictionary<uint, List<UUID> > m_PrimObjects =
                new Dictionary<uint, List<UUID> >();

        // Maps the UUID above to the script instance

        private Dictionary<UUID, IScriptInstance> m_Scripts =
                new Dictionary<UUID, IScriptInstance>();

        // Maps the asset ID to the assembly

        private Dictionary<UUID, string> m_Assemblies =
                new Dictionary<UUID, string>();

        private Dictionary<string, int> m_AddingAssemblies =
                new Dictionary<string, int>();

        // This will list AppDomains by script asset

        private Dictionary<UUID, AppDomain> m_AppDomains =
                new Dictionary<UUID, AppDomain>();

        // List the scripts running in each appdomain

        private Dictionary<UUID, List<UUID> > m_DomainScripts =
                new Dictionary<UUID, List<UUID> >();

        private ScriptCompileQueue m_CompileQueue = new ScriptCompileQueue();
        IWorkItemResult m_CurrentCompile = null;
        private Dictionary<UUID, ScriptCompileInfo> m_CompileDict = new Dictionary<UUID, ScriptCompileInfo>();

        private ScriptEngineConsoleCommands m_consoleCommands;

        public string ScriptEngineName
        {
            get { return "XEngine"; }
        }

        public string ScriptClassName { get; private set; }

        public string ScriptBaseClassName { get; private set; }

        public ParameterInfo[] ScriptBaseClassParameters { get; private set; }

        public string[] ScriptReferencedAssemblies { get; private set; }

        public Scene World
        {
            get { return m_Scene; }
        }

        public static List<XEngine> ScriptEngines
        {
            get { return m_ScriptEngines; }
        }

        public IScriptModule ScriptModule
        {
            get { return this; }
        }

        // private struct RezScriptParms
        // {
        //     uint LocalID;
        //     UUID ItemID;
        //     string Script;
        // }

        public IConfig Config
        {
            get { return m_ScriptConfig; }
        }

        public string ScriptEnginePath
        {
            get { return m_ScriptEnginesPath; }
        }

        public IConfigSource ConfigSource
        {
            get { return m_ConfigSource; }
        }

        private class ScriptCompileInfo
        {
            public List<EventParams> eventList = new List<EventParams>();
        }

        /// <summary>
        /// Event fired after the script engine has finished removing a script.
        /// </summary>
        public event ScriptRemoved OnScriptRemoved;

        /// <summary>
        /// Event fired after the script engine has finished removing a script from an object.
        /// </summary>
        public event ObjectRemoved OnObjectRemoved;

        public void Initialise(IConfigSource configSource)
        {
            if (configSource.Configs["XEngine"] == null)
                return;

            m_ScriptConfig = configSource.Configs["XEngine"];
            m_ConfigSource = configSource;

            string rawScriptStopStrategy = m_ScriptConfig.GetString("ScriptStopStrategy", "co-op");

            m_log.InfoFormat("[XEngine]: Script stop strategy is {0}", rawScriptStopStrategy);

            if (rawScriptStopStrategy == "co-op")
            {
                m_coopTermination = true;
                ScriptClassName = "XEngineScript";
                ScriptBaseClassName = typeof(XEngineScriptBase).FullName;
                ScriptBaseClassParameters = typeof(XEngineScriptBase).GetConstructor(new Type[] { typeof(WaitHandle) }).GetParameters();
                ScriptReferencedAssemblies = new string[] { Path.GetFileName(typeof(XEngineScriptBase).Assembly.Location) };
            }
            else
            {
                ScriptClassName = "Script";
                ScriptBaseClassName = typeof(ScriptBaseClass).FullName;
            }

//            Console.WriteLine("ASSEMBLY NAME: {0}", ScriptReferencedAssemblies[0]);
        }

        public void AddRegion(Scene scene)
        {
            if (m_ScriptConfig == null)
                return;

            m_ScriptFailCount = 0;
            m_ScriptErrorMessage = String.Empty;

            m_Enabled = m_ScriptConfig.GetBoolean("Enabled", true);

            if (!m_Enabled)
                return;

            AppDomain.CurrentDomain.AssemblyResolve +=
                OnAssemblyResolve;

            m_Scene = scene;
            m_log.InfoFormat("[XEngine]: Initializing scripts in region {0}", m_Scene.RegionInfo.RegionName);

            m_MinThreads = m_ScriptConfig.GetInt("MinThreads", 2);
            m_MaxThreads = m_ScriptConfig.GetInt("MaxThreads", 100);
            m_IdleTimeout = m_ScriptConfig.GetInt("IdleTimeout", 60);
            string priority = m_ScriptConfig.GetString("Priority", "BelowNormal");
            m_StartDelay = m_ScriptConfig.GetInt("StartDelay", 15000);
            m_MaxScriptQueue = m_ScriptConfig.GetInt("MaxScriptEventQueue",300);
            m_StackSize = m_ScriptConfig.GetInt("ThreadStackSize", 262144);
            m_SleepTime = m_ScriptConfig.GetInt("MaintenanceInterval", 10) * 1000;
            m_AppDomainLoading = m_ScriptConfig.GetBoolean("AppDomainLoading", false);
            m_CompactMemOnLoad = m_ScriptConfig.GetBoolean("CompactMemOnLoad", false);
            m_EventLimit = m_ScriptConfig.GetInt("EventLimit", 30);
            m_KillTimedOutScripts = m_ScriptConfig.GetBoolean("KillTimedOutScripts", false);
            m_SaveTime = m_ScriptConfig.GetInt("SaveInterval", 120) * 1000;
            m_WaitForEventCompletionOnScriptStop
                = m_ScriptConfig.GetInt("WaitForEventCompletionOnScriptStop", m_WaitForEventCompletionOnScriptStop);

            m_ScriptEnginesPath = m_ScriptConfig.GetString("ScriptEnginesPath", "ScriptEngines");

            m_Prio = ThreadPriority.BelowNormal;
            switch (priority)
            {
                case "Lowest":
                    m_Prio = ThreadPriority.Lowest;
                    break;
                case "BelowNormal":
                    m_Prio = ThreadPriority.BelowNormal;
                    break;
                case "Normal":
                    m_Prio = ThreadPriority.Normal;
                    break;
                case "AboveNormal":
                    m_Prio = ThreadPriority.AboveNormal;
                    break;
                case "Highest":
                    m_Prio = ThreadPriority.Highest;
                    break;
                default:
                    m_log.ErrorFormat("[XEngine] Invalid thread priority: '{0}'. Assuming BelowNormal", priority);
                    break;
            }

            lock (m_ScriptEngines)
            {
                m_ScriptEngines.Add(this);
            }

            // Needs to be here so we can queue the scripts that need starting
            //
            m_Scene.EventManager.OnRezScript += OnRezScript;

            // Complete basic setup of the thread pool
            //
            SetupEngine(m_MinThreads, m_MaxThreads, m_IdleTimeout, m_Prio,
                        m_MaxScriptQueue, m_StackSize);

            m_Scene.StackModuleInterface<IScriptModule>(this);

            m_XmlRpcRouter = m_Scene.RequestModuleInterface<IXmlRpcRouter>();
            if (m_XmlRpcRouter != null)
            {
                OnScriptRemoved += m_XmlRpcRouter.ScriptRemoved;
                OnObjectRemoved += m_XmlRpcRouter.ObjectRemoved;
            }

            m_consoleCommands = new ScriptEngineConsoleCommands(this);
            m_consoleCommands.RegisterCommands();

            MainConsole.Instance.Commands.AddCommand(
                "Scripts", false, "xengine status", "xengine status", "Show status information",
                "Show status information on the script engine.",
                HandleShowStatus);

            MainConsole.Instance.Commands.AddCommand(
                "Scripts", false, "scripts show", "scripts show [<script-item-uuid>+]", "Show script information",
                "Show information on all scripts known to the script engine.\n"
                    + "If one or more <script-item-uuid>s are given then only information on that script will be shown.",
                HandleShowScripts);

            MainConsole.Instance.Commands.AddCommand(
                "Scripts", false, "show scripts", "show scripts [<script-item-uuid>+]", "Show script information",
                "Synonym for scripts show command", HandleShowScripts);

            MainConsole.Instance.Commands.AddCommand(
                "Scripts", false, "scripts suspend", "scripts suspend [<script-item-uuid>+]", "Suspends all running scripts",
                "Suspends all currently running scripts.  This only suspends event delivery, it will not suspend a"
                    + " script that is currently processing an event.\n"
                    + "Suspended scripts will continue to accumulate events but won't process them.\n"
                    + "If one or more <script-item-uuid>s are given then only that script will be suspended.  Otherwise, all suitable scripts are suspended.",
                 (module, cmdparams) => HandleScriptsAction(cmdparams, HandleSuspendScript));

            MainConsole.Instance.Commands.AddCommand(
                "Scripts", false, "scripts resume", "scripts resume [<script-item-uuid>+]", "Resumes all suspended scripts",
                "Resumes all currently suspended scripts.\n"
                    + "Resumed scripts will process all events accumulated whilst suspended.\n"
                    + "If one or more <script-item-uuid>s are given then only that script will be resumed.  Otherwise, all suitable scripts are resumed.",
                (module, cmdparams) => HandleScriptsAction(cmdparams, HandleResumeScript));

            MainConsole.Instance.Commands.AddCommand(
                "Scripts", false, "scripts stop", "scripts stop [<script-item-uuid>+]", "Stops all running scripts",
                "Stops all running scripts.\n"
                    + "If one or more <script-item-uuid>s are given then only that script will be stopped.  Otherwise, all suitable scripts are stopped.",
                (module, cmdparams) => HandleScriptsAction(cmdparams, HandleStopScript));

            MainConsole.Instance.Commands.AddCommand(
                "Scripts", false, "scripts start", "scripts start [<script-item-uuid>+]", "Starts all stopped scripts",
                "Starts all stopped scripts.\n"
                    + "If one or more <script-item-uuid>s are given then only that script will be started.  Otherwise, all suitable scripts are started.",
                (module, cmdparams) => HandleScriptsAction(cmdparams, HandleStartScript));

            MainConsole.Instance.Commands.AddCommand(
                "Debug", false, "debug scripts log", "debug scripts log <item-id> <log-level>", "Extra debug logging for a particular script.",
                "Activates or deactivates extra debug logging for the given script.\n"
                    + "Level == 0, deactivate extra debug logging.\n"
                    + "Level >= 1, log state changes.\n"
                    + "Level >= 2, log event invocations.\n",
                HandleDebugScriptLogCommand);

            MainConsole.Instance.Commands.AddCommand(
                "Debug", false, "debug xengine log", "debug xengine log [<level>]",
                "Turn on detailed xengine debugging.",
                  "If level <= 0, then no extra logging is done.\n"
                + "If level >= 1, then we log every time that a script is started.",
                HandleDebugLevelCommand);
        }

        private void HandleDebugScriptLogCommand(string module, string[] args)
        {
            if (!(MainConsole.Instance.ConsoleScene == null || MainConsole.Instance.ConsoleScene == m_Scene))
                return;

            if (args.Length != 5)
            {
                MainConsole.Instance.Output("Usage: debug script log <item-id> <log-level>");
                return;
            }

            UUID itemId;

            if (!ConsoleUtil.TryParseConsoleUuid(MainConsole.Instance, args[3], out itemId))
                return;

            int newLevel;

            if (!ConsoleUtil.TryParseConsoleInt(MainConsole.Instance, args[4], out newLevel))
                return;

            IScriptInstance si;

            lock (m_Scripts)
            {
                // XXX: We can't give the user feedback on a bad item id because this may apply to a different script
                // engine
                if (!m_Scripts.TryGetValue(itemId, out si))
                    return;
            }

            si.DebugLevel = newLevel;
            MainConsole.Instance.Output("Set debug level of {0} {1} to {2}", null, si.ScriptName, si.ItemID, newLevel);
        }

        /// <summary>
        /// Change debug level
        /// </summary>
        /// <param name="module"></param>
        /// <param name="args"></param>
        private void HandleDebugLevelCommand(string module, string[] args)
        {
            if (args.Length >= 4)
            {
                int newDebug;
                if (ConsoleUtil.TryParseConsoleNaturalInt(MainConsole.Instance, args[3], out newDebug))
                {
                    DebugLevel = newDebug;
                    MainConsole.Instance.Output("Debug level set to {0} in XEngine for region {1}", null, newDebug, m_Scene.Name);
                }
            }
            else if (args.Length == 3)
            {
                MainConsole.Instance.Output("Current debug level is {0}", null, DebugLevel);
            }
            else
            {
                MainConsole.Instance.Output("Usage: debug xengine log <level>");
            }
        }

        /// <summary>
        /// Parse the raw item id into a script instance from the command params if it's present.
        /// </summary>
        /// <param name="cmdparams"></param>
        /// <param name="instance"></param>
        /// <param name="comparer">Basis on which to sort output.  Can be null if no sort needs to take place</param>
        private void HandleScriptsAction(string[] cmdparams, Action<IScriptInstance> action)
        {
            HandleScriptsAction<object>(cmdparams, action, null);
        }

        /// <summary>
        /// Parse the raw item id into a script instance from the command params if it's present.
        /// </summary>
        /// <param name="cmdparams"></param>
        /// <param name="instance"></param>
        /// <param name="keySelector">Basis on which to sort output.  Can be null if no sort needs to take place</param>
        private void HandleScriptsAction<TKey>(
            string[] cmdparams, Action<IScriptInstance> action, System.Func<IScriptInstance, TKey> keySelector)
        {
            if (!(MainConsole.Instance.ConsoleScene == null || MainConsole.Instance.ConsoleScene == m_Scene))
                return;

            lock (m_Scripts)
            {
                string rawItemId;
                UUID itemId = UUID.Zero;

                if (cmdparams.Length == 2)
                {
                    IEnumerable<IScriptInstance> scripts = m_Scripts.Values;

                    if (keySelector != null)
                        scripts = scripts.OrderBy<IScriptInstance, TKey>(keySelector);

                    foreach (IScriptInstance instance in scripts)
                        action(instance);

                    return;
                }

                for (int i = 2; i < cmdparams.Length; i++)
                {
                    rawItemId = cmdparams[i];

                    if (!UUID.TryParse(rawItemId, out itemId))
                    {
                        MainConsole.Instance.Output("ERROR: {0} is not a valid UUID", null, rawItemId);
                        continue;
                    }

                    if (itemId != UUID.Zero)
                    {
                        IScriptInstance instance = GetInstance(itemId);
                        if (instance == null)
                        {
                            // Commented out for now since this will cause false reports on simulators with more than
                            // one scene where the current command line set region is 'root' (which causes commands to
                            // go to both regions... (sigh)
    //                        MainConsole.Instance.OutputFormat("Error - No item found with id {0}", itemId);
                            continue;
                        }
                        else
                        {
                            action(instance);
                        }
                    }
                }
            }
        }

        private void HandleShowStatus(string module, string[] cmdparams)
        {
            if (!(MainConsole.Instance.ConsoleScene == null || MainConsole.Instance.ConsoleScene == m_Scene))
                return;

            MainConsole.Instance.Output(GetStatusReport());
        }

        public string GetStatusReport()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendFormat("Status of XEngine instance for {0}\n", m_Scene.RegionInfo.RegionName);

            long scriptsLoaded, eventsQueued = 0, eventsProcessed = 0;

            lock (m_Scripts)
            {
                scriptsLoaded = m_Scripts.Count;

                foreach (IScriptInstance si in m_Scripts.Values)
                {
                    eventsQueued += si.EventsQueued;
                    eventsProcessed += si.EventsProcessed;
                }
            }

            sb.AppendFormat("Scripts loaded             : {0}\n", scriptsLoaded);
            sb.AppendFormat("Scripts waiting for load   : {0}\n", m_CompileQueue.Count);
            sb.AppendFormat("Max threads                : {0}\n", m_ThreadPool.MaxThreads);
            sb.AppendFormat("Min threads                : {0}\n", m_ThreadPool.MinThreads);
            sb.AppendFormat("Allocated threads          : {0}\n", m_ThreadPool.ActiveThreads);
            sb.AppendFormat("In use threads             : {0}\n", m_ThreadPool.InUseThreads);
            sb.AppendFormat("Work items waiting         : {0}\n", m_ThreadPool.WaitingCallbacks);
//            sb.AppendFormat("Assemblies loaded          : {0}\n", m_Assemblies.Count);
            sb.AppendFormat("Events queued              : {0}\n", eventsQueued);
            sb.AppendFormat("Events processed           : {0}\n", eventsProcessed);

            SensorRepeat sr = AsyncCommandManager.GetSensorRepeatPlugin(this);
            sb.AppendFormat("Sensors                    : {0}\n", sr != null ? sr.SensorsCount : 0);

            Dataserver ds = AsyncCommandManager.GetDataserverPlugin(this);
            sb.AppendFormat("Dataserver requests        : {0}\n", ds != null ? ds.DataserverRequestsCount : 0);

            Timer t = AsyncCommandManager.GetTimerPlugin(this);
            sb.AppendFormat("Timers                     : {0}\n", t != null ? t.TimersCount : 0);

            Listener l = AsyncCommandManager.GetListenerPlugin(this);
            sb.AppendFormat("Listeners                  : {0}\n", l != null ? l.ListenerCount : 0);

            return sb.ToString();
        }

        public void HandleShowScripts(string module, string[] cmdparams)
        {
            if (!(MainConsole.Instance.ConsoleScene == null || MainConsole.Instance.ConsoleScene == m_Scene))
                return;

            if (cmdparams.Length == 2)
            {
                lock (m_Scripts)
                {
                    MainConsole.Instance.Output(
                        "Showing {0} scripts in {1}", null, m_Scripts.Count, m_Scene.RegionInfo.RegionName);
                }
            }

            HandleScriptsAction<long>(cmdparams, HandleShowScript, si => si.EventsProcessed);
        }

        private void HandleShowScript(IScriptInstance instance)
        {
            SceneObjectPart sop = m_Scene.GetSceneObjectPart(instance.ObjectID);
            string status;

            if (instance.ShuttingDown)
            {
                status = "shutting down";
            }
            else if (instance.Suspended)
            {
                status = "suspended";
            }
            else if (!instance.Running)
            {
                status = "stopped";
            }
            else
            {
                status = "running";
            }

            StringBuilder sb = new StringBuilder();

            sb.AppendFormat("Script name         : {0}\n", instance.ScriptName);
            sb.AppendFormat("Status              : {0}\n", status);
            sb.AppendFormat("Queued events       : {0}\n", instance.EventsQueued);
            sb.AppendFormat("Processed events    : {0}\n", instance.EventsProcessed);
            sb.AppendFormat("Item UUID           : {0}\n", instance.ItemID);
            sb.AppendFormat("Asset UUID          : {0}\n", instance.AssetID);
            sb.AppendFormat("Containing part name: {0}\n", instance.PrimName);
            sb.AppendFormat("Containing part UUID: {0}\n", instance.ObjectID);
            sb.AppendFormat("Position            : {0}\n", sop.AbsolutePosition);

            MainConsole.Instance.Output(sb.ToString());
        }

        private void HandleSuspendScript(IScriptInstance instance)
        {
            if (!instance.Suspended)
            {
                instance.Suspend();

                SceneObjectPart sop = m_Scene.GetSceneObjectPart(instance.ObjectID);
                MainConsole.Instance.Output(
                    "Suspended {0}.{1}, item UUID {2}, prim UUID {3} @ {4}",
                    null,
                    instance.PrimName, instance.ScriptName, instance.ItemID, instance.ObjectID, sop.AbsolutePosition);
            }
        }

        private void HandleResumeScript(IScriptInstance instance)
        {
            if (instance.Suspended)
            {
                instance.Resume();

                SceneObjectPart sop = m_Scene.GetSceneObjectPart(instance.ObjectID);
                MainConsole.Instance.Output(
                    "Resumed {0}.{1}, item UUID {2}, prim UUID {3} @ {4}",
                    null,
                    instance.PrimName, instance.ScriptName, instance.ItemID, instance.ObjectID, sop.AbsolutePosition);
            }
        }

        private void HandleStartScript(IScriptInstance instance)
        {
            if (!instance.Running)
            {
                instance.Start();

                SceneObjectPart sop = m_Scene.GetSceneObjectPart(instance.ObjectID);
                MainConsole.Instance.Output(
                    "Started {0}.{1}, item UUID {2}, prim UUID {3} @ {4}",
                    null,
                    instance.PrimName, instance.ScriptName, instance.ItemID, instance.ObjectID, sop.AbsolutePosition);
            }
        }

        private void HandleStopScript(IScriptInstance instance)
        {
            if (instance.Running)
            {
                instance.StayStopped = true;    // the script was stopped explicitly

                instance.Stop(0);

                SceneObjectPart sop = m_Scene.GetSceneObjectPart(instance.ObjectID);
                MainConsole.Instance.Output(
                    "Stopped {0}.{1}, item UUID {2}, prim UUID {3} @ {4}",
                    null,
                    instance.PrimName, instance.ScriptName, instance.ItemID, instance.ObjectID, sop.AbsolutePosition);
            }
        }

        public void RemoveRegion(Scene scene)
        {
            if (!m_Enabled)
                return;

            lock (m_Scripts)
            {
                m_log.InfoFormat(
                    "[XEngine]: Shutting down {0} scripts in {1}", m_Scripts.Count, m_Scene.RegionInfo.RegionName);

                foreach (IScriptInstance instance in m_Scripts.Values)
                {
                    // Force a final state save
                    //
                    try
                    {
                        if (instance.StatePersistedHere)
                            instance.SaveState();
                    }
                    catch (Exception e)
                    {
                        m_log.Error(
                            string.Format(
                                "[XEngine]: Failed final state save for script {0}.{1}, item UUID {2}, prim UUID {3} in {4}.  Exception ",
                                instance.PrimName, instance.ScriptName, instance.ItemID, instance.ObjectID, World.Name)
                            , e);
                    }

                    // Clear the event queue and abort the instance thread
                    //
                    instance.Stop(0, true);

                    // Release events, timer, etc
                    //
                    instance.DestroyScriptInstance();

                    // Unload scripts and app domains.
                    // Must be done explicitly because they have infinite
                    // lifetime.
                    // However, don't bother to do this if the simulator is shutting
                    // down since it takes a long time with many scripts.
                    if (!m_SimulatorShuttingDown)
                    {
                        m_DomainScripts[instance.AppDomain].Remove(instance.ItemID);
                        if (m_DomainScripts[instance.AppDomain].Count == 0)
                        {
                            m_DomainScripts.Remove(instance.AppDomain);
                            UnloadAppDomain(instance.AppDomain);
                        }
                    }
                }

                m_Scripts.Clear();
                m_PrimObjects.Clear();
                m_Assemblies.Clear();
                m_DomainScripts.Clear();
            }
            lock (m_ScriptEngines)
            {
                m_ScriptEngines.Remove(this);
            }
        }

        public void RegionLoaded(Scene scene)
        {
            if (!m_Enabled)
                return;

            m_EventManager = new EventManager(this);

            m_Compiler = new Compiler(this);

            m_Scene.EventManager.OnRemoveScript += OnRemoveScript;
            m_Scene.EventManager.OnScriptReset += OnScriptReset;
            m_Scene.EventManager.OnStartScript += OnStartScript;
            m_Scene.EventManager.OnStopScript += OnStopScript;
            m_Scene.EventManager.OnGetScriptRunning += OnGetScriptRunning;
            m_Scene.EventManager.OnShutdown += OnShutdown;

            // If region ready has been triggered, then the region had no scripts to compile and completed its other
            // work.
            m_Scene.EventManager.OnRegionReadyStatusChange += s => { if (s.Ready) m_InitialStartup = false; };

            if (m_SleepTime > 0)
            {
                m_ThreadPool.QueueWorkItem(new WorkItemCallback(this.DoMaintenance),
                                           new Object[]{ m_SleepTime });
            }

            if (m_SaveTime > 0)
            {
                m_ThreadPool.QueueWorkItem(new WorkItemCallback(this.DoBackup),
                                           new Object[] { m_SaveTime });
            }
        }

        public void StartProcessing()
        {
            m_ThreadPool.Start();
        }

        public void Close()
        {
            if (!m_Enabled)
                return;

            lock (m_ScriptEngines)
            {
                if (m_ScriptEngines.Contains(this))
                    m_ScriptEngines.Remove(this);
            }

            lock(m_Scripts)
                m_ThreadPool.Shutdown();
        }

        public object DoBackup(object o)
        {
            Object[] p = (Object[])o;
            int saveTime = (int)p[0];

            if (saveTime > 0)
                System.Threading.Thread.Sleep(saveTime);

//            m_log.Debug("[XEngine] Backing up script states");

            List<IScriptInstance> instances = new List<IScriptInstance>();

            lock (m_Scripts)
            {
                foreach (IScriptInstance instance in m_Scripts.Values)
                {
                    if (instance.StatePersistedHere)
                    {
//                        m_log.DebugFormat(
//                            "[XEngine]: Adding script {0}.{1}, item UUID {2}, prim UUID {3} in {4} for state persistence",
//                            instance.PrimName, instance.ScriptName, instance.ItemID, instance.ObjectID, World.Name);

                        instances.Add(instance);
                    }
                }
            }

            foreach (IScriptInstance i in instances)
            {
                try
                {
                    i.SaveState();
                }
                catch (Exception e)
                {
                    m_log.Error(
                        string.Format(
                            "[XEngine]: Failed to save state of script {0}.{1}, item UUID {2}, prim UUID {3} in {4}.  Exception ",
                            i.PrimName, i.ScriptName, i.ItemID, i.ObjectID, World.Name)
                        , e);
                }
            }

            if (saveTime > 0)
                m_ThreadPool.QueueWorkItem(new WorkItemCallback(this.DoBackup),
                                           new Object[] { saveTime });

            return 0;
        }

        public void SaveAllState()
        {
            DoBackup(new object[] { 0 });
        }

        public object DoMaintenance(object p)
        {
            object[] parms = (object[])p;
            int sleepTime = (int)parms[0];

            foreach (IScriptInstance inst in m_Scripts.Values)
            {
                if (inst.EventTime() > m_EventLimit)
                {
                    inst.Stop(100);
                    if (!m_KillTimedOutScripts)
                        inst.Start();
                }
            }

            System.Threading.Thread.Sleep(sleepTime);

            m_ThreadPool.QueueWorkItem(new WorkItemCallback(this.DoMaintenance),
                                       new Object[]{ sleepTime });

            return 0;
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public string Name
        {
            get { return "XEngine"; }
        }

        public void OnRezScript(uint localID, UUID itemID, string script, int startParam, bool postOnRez, string engine, int stateSource)
        {
//            m_log.DebugFormat(
//                "[XEngine]: OnRezScript event triggered for script {0}, startParam {1}, postOnRez {2}, engine {3}, stateSource {4}, script\n{5}",
//                 itemID, startParam, postOnRez, engine, stateSource, script);

            if (script.StartsWith("//MRM:"))
                return;

            List<IScriptModule> engines = new List<IScriptModule>(m_Scene.RequestModuleInterfaces<IScriptModule>());

            List<string> names = new List<string>();
            foreach (IScriptModule m in engines)
                names.Add(m.ScriptEngineName);

            int lineEnd = script.IndexOf('\n');

            if (lineEnd > 1)
            {
                string firstline = script.Substring(0, lineEnd).Trim();

                int colon = firstline.IndexOf(':');
                if (firstline.Length > 2 && firstline.Substring(0, 2) == "//" && colon != -1)
                {
                    string engineName = firstline.Substring(2, colon - 2);

                    if (names.Contains(engineName))
                    {
                        engine = engineName;
                        script = "//" + script.Substring(colon + 1);
                    }
                    else
                    {
                        if (engine == ScriptEngineName)
                        {
                            // If we are falling back on XEngine as the default engine, then only complain to the user
                            // if a script language has been explicitly set and it's one that we recognize or there are
                            // no non-whitespace characters after the colon.
                            //
                            // If the script is
                            // explicitly not allowed or the script is not in LSL then the user will be informed by a later compiler message.
                            //
                            // If the colon ends the line then we'll risk the false positive as this is more likely
                            // to signal a real scriptengine line where the user wants to use the default compile language.
                            //
                            // This avoids the overwhelming number of false positives where we're in this code because
                            // there's a colon in a comment in the first line of a script for entirely
                            // unrelated reasons (e.g. vim settings).
                            //
                            // TODO: A better fix would be to deprecate simple : detection and look for some less likely
                            // string to begin the comment (like #! in unix shell scripts).
                            bool warnRunningInXEngine = false;
                            string restOfFirstLine = firstline.Substring(colon + 1);

                            // FIXME: These are hardcoded because they are currently hardcoded in Compiler.cs
                            if (restOfFirstLine.StartsWith("c#")
                                || restOfFirstLine.StartsWith("vb")
                                || restOfFirstLine.StartsWith("lsl")
                                || restOfFirstLine.Length == 0)
                                warnRunningInXEngine = true;

                            if (warnRunningInXEngine)
                            {
                                SceneObjectPart part =
                                        m_Scene.GetSceneObjectPart(
                                        localID);

                                TaskInventoryItem item =
                                        part.Inventory.GetInventoryItem(itemID);

                                ScenePresence presence =
                                        m_Scene.GetScenePresence(
                                        item.OwnerID);

                                if (presence != null)
                                {
                                   presence.ControllingClient.SendAgentAlertMessage(
                                            "Selected engine unavailable. "+
                                            "Running script on "+
                                            ScriptEngineName,
                                            false);
                                }
                            }
                        }
                    }
                }
            }

            if (engine != ScriptEngineName)
                return;

            Object[] parms = new Object[]{localID, itemID, script, startParam, postOnRez, (StateSource)stateSource};

            if (stateSource == (int)StateSource.ScriptedRez)
            {
                lock (m_CompileDict)
                {
//                    m_log.DebugFormat("[XENGINE]: Set compile dict for {0}", itemID);
                    m_CompileDict[itemID] = new ScriptCompileInfo();
                }

                DoOnRezScript(parms);
            }
            else
            {
                lock (m_CompileDict)
                    m_CompileDict[itemID] = new ScriptCompileInfo();
//                m_log.DebugFormat("[XENGINE]: Set compile dict for {0} delayed", itemID);

                // This must occur after the m_CompileDict so that an existing compile thread cannot hit the check
                // in DoOnRezScript() before m_CompileDict has been updated.
                m_CompileQueue.Enqueue(parms);

//                m_log.DebugFormat("[XEngine]: Added script {0} to compile queue", itemID);

                // NOTE: Although we use a lockless queue, the lock here
                // is required. It ensures that there are never two
                // compile threads running, which, due to a race
                // conndition, might otherwise happen
                //
                lock (m_CompileQueue)
                {
                    if (m_CurrentCompile == null)
                        m_CurrentCompile = m_ThreadPool.QueueWorkItem(DoOnRezScriptQueue, null);
                }
            }
        }

        public Object DoOnRezScriptQueue(Object dummy)
        {
            try
            {
                if (m_InitialStartup)
                {
                    // This delay exists to stop mono problems where script compilation and startup would stop the sim
                    // working properly for the session.
                    System.Threading.Thread.Sleep(m_StartDelay);

                    m_log.InfoFormat("[XEngine]: Performing initial script startup on {0}", m_Scene.Name);
                }

                object[] o;

                int scriptsStarted = 0;

                while (m_CompileQueue.Dequeue(out o))
                {
                    try
                    {
                        if (DoOnRezScript(o))
                        {
                            scriptsStarted++;

                            if (m_InitialStartup)
                                if (scriptsStarted % 50 == 0)
                                    m_log.InfoFormat(
                                        "[XEngine]: Started {0} scripts in {1}", scriptsStarted, m_Scene.Name);
                        }
                    }
                    catch (System.Threading.ThreadAbortException) { }
                    catch (Exception e)
                    {
                        m_log.Error(
                            string.Format(
                                "[XEngine]: Failure in DoOnRezScriptQueue() for item {0} in {1}.  Continuing.  Exception  ",
                                o[1], m_Scene.Name),
                            e);
                    }
                }

                if (m_InitialStartup)
                    m_log.InfoFormat(
                        "[XEngine]: Completed starting {0} scripts on {1}", scriptsStarted, m_Scene.Name);

            }
            catch (Exception e)
            {
                m_log.Error(
                    string.Format("[XEngine]: Failure in DoOnRezScriptQueue() in {0}.  Exception  ", m_Scene.Name), e);
            }
            finally
            {
                // FIXME: On failure we must trigger this even if the compile queue is not actually empty so that the
                // RegionReadyModule is not forever waiting.  This event really needs a different name.
                m_Scene.EventManager.TriggerEmptyScriptCompileQueue(m_ScriptFailCount,
                                                                    m_ScriptErrorMessage);

                m_ScriptFailCount = 0;
                m_InitialStartup = false;

                // NOTE: Despite having a lockless queue, this lock is required
                // to make sure there is never no compile thread while there
                // are still scripts to compile. This could otherwise happen
                // due to a race condition
                //
                lock (m_CompileQueue)
                {
                    m_CurrentCompile = null;

                    // This is to avoid a situation where the m_CompileQueue while loop above could complete but
                    // OnRezScript() place a new script on the queue and check m_CurrentCompile = null before we hit
                    // this section.
                    if (m_CompileQueue.Count > 0)
                        m_CurrentCompile = m_ThreadPool.QueueWorkItem(DoOnRezScriptQueue, null);
                }
            }

            return null;
        }

        private bool DoOnRezScript(object[] parms)
        {
            Object[] p = parms;
            uint localID = (uint)p[0];
            UUID itemID = (UUID)p[1];
            string script =(string)p[2];
            int startParam = (int)p[3];
            bool postOnRez = (bool)p[4];
            StateSource stateSource = (StateSource)p[5];

//            m_log.DebugFormat("[XEngine]: DoOnRezScript called for script {0}", itemID);

            lock (m_CompileDict)
            {
                if (!m_CompileDict.ContainsKey(itemID))
                    return false;
            }

            // Get the asset ID of the script, so we can check if we
            // already have it.

            // We must look for the part outside the m_Scripts lock because GetSceneObjectPart later triggers the
            // m_parts lock on SOG.  At the same time, a scene object that is being deleted will take the m_parts lock
            // and then later on try to take the m_scripts lock in this class when it calls OnRemoveScript()
            SceneObjectPart part = m_Scene.GetSceneObjectPart(localID);
            if (part == null)
            {
                m_log.ErrorFormat("[Script]: SceneObjectPart with localID {0} unavailable. Script NOT started.", localID);
                m_ScriptErrorMessage += "SceneObjectPart unavailable. Script NOT started.\n";
                m_ScriptFailCount++;
                lock (m_CompileDict)
                    m_CompileDict.Remove(itemID);
                return false;
            }

            TaskInventoryItem item = part.Inventory.GetInventoryItem(itemID);
            if (item == null)
            {
                m_ScriptErrorMessage += "Can't find script inventory item.\n";
                m_ScriptFailCount++;
                lock (m_CompileDict)
                    m_CompileDict.Remove(itemID);
                return false;
            }

            if (DebugLevel > 0)
                m_log.DebugFormat(
                    "[XEngine]: Loading script {0}.{1}, item UUID {2}, prim UUID {3} @ {4}.{5}",
                    part.ParentGroup.RootPart.Name, item.Name, itemID, part.UUID,
                    part.ParentGroup.RootPart.AbsolutePosition, part.ParentGroup.Scene.RegionInfo.RegionName);

            UUID assetID = item.AssetID;

            ScenePresence presence = m_Scene.GetScenePresence(item.OwnerID);

            string assemblyPath = "";

            Culture.SetCurrentCulture();

            Dictionary<KeyValuePair<int, int>, KeyValuePair<int, int>> linemap;

            lock (m_ScriptErrors)
            {
                try
                {
                    lock (m_AddingAssemblies)
                    {
                        m_Compiler.PerformScriptCompile(script, assetID.ToString(), item.OwnerID, out assemblyPath, out linemap);

//                        m_log.DebugFormat(
//                            "[XENGINE]: Found assembly path {0} onrez {1} in {2}",
//                            assemblyPath, item.ItemID, World.Name);

                        if (!m_AddingAssemblies.ContainsKey(assemblyPath)) {
                            m_AddingAssemblies[assemblyPath] = 1;
                        } else {
                            m_AddingAssemblies[assemblyPath]++;
                        }
                    }

                    string[] warnings = m_Compiler.GetWarnings();

                    if (warnings != null && warnings.Length != 0)
                    {
                        foreach (string warning in warnings)
                        {
                            if (!m_ScriptErrors.ContainsKey(itemID))
                                m_ScriptErrors[itemID] = new ArrayList();

                            m_ScriptErrors[itemID].Add(warning);
    //                        try
    //                        {
    //                            // DISPLAY WARNING INWORLD
    //                            string text = "Warning:\n" + warning;
    //                            if (text.Length > 1000)
    //                                text = text.Substring(0, 1000);
    //                            if (!ShowScriptSaveResponse(item.OwnerID,
    //                                    assetID, text, true))
    //                            {
    //                                if (presence != null && (!postOnRez))
    //                                    presence.ControllingClient.SendAgentAlertMessage("Script saved with warnings, check debug window!", false);
    //
    //                                World.SimChat(Utils.StringToBytes(text),
    //                                              ChatTypeEnum.DebugChannel, 2147483647,
    //                                              part.AbsolutePosition,
    //                                              part.Name, part.UUID, false);
    //                            }
    //                        }
    //                        catch (Exception e2) // LEGIT: User Scripting
    //                        {
    //                            m_log.Error("[XEngine]: " +
    //                                    "Error displaying warning in-world: " +
    //                                    e2.ToString());
    //                            m_log.Error("[XEngine]: " +
    //                                    "Warning:\r\n" +
    //                                    warning);
    //                        }
                        }
                    }
                }
                catch (Exception e)
                {
//                    m_log.ErrorFormat(
//                        "[XEngine]: Exception when rezzing script with item ID {0}, {1}{2}",
//                        itemID, e.Message, e.StackTrace);

    //                try
    //                {
                        if (!m_ScriptErrors.ContainsKey(itemID))
                            m_ScriptErrors[itemID] = new ArrayList();
                        // DISPLAY ERROR INWORLD
    //                    m_ScriptErrorMessage += "Failed to compile script in object: '" + part.ParentGroup.RootPart.Name + "' Script name: '" + item.Name + "' Error message: " + e.Message.ToString();
    //
                        m_ScriptFailCount++;
                        m_ScriptErrors[itemID].Add(e.Message.ToString());
    //                    string text = "Error compiling script '" + item.Name + "':\n" + e.Message.ToString();
    //                    if (text.Length > 1000)
    //                        text = text.Substring(0, 1000);
    //                    if (!ShowScriptSaveResponse(item.OwnerID,
    //                            assetID, text, false))
    //                    {
    //                        if (presence != null && (!postOnRez))
    //                            presence.ControllingClient.SendAgentAlertMessage("Script saved with errors, check debug window!", false);
    //                        World.SimChat(Utils.StringToBytes(text),
    //                                      ChatTypeEnum.DebugChannel, 2147483647,
    //                                      part.AbsolutePosition,
    //                                      part.Name, part.UUID, false);
    //                    }
    //                }
    //                catch (Exception e2) // LEGIT: User Scripting
    //                {
    //                    m_log.Error("[XEngine]: "+
    //                            "Error displaying error in-world: " +
    //                            e2.ToString());
    //                    m_log.Error("[XEngine]: " +
    //                            "Errormessage: Error compiling script:\r\n" +
    //                            e.Message.ToString());
    //                }

                    lock (m_CompileDict)
                        m_CompileDict.Remove(itemID);
                    return false;
                }
            }

            // optionaly do not load a assembly on top of a lot of to release memory
            // only if logins disable since causes a lot of rubber banding
            if(m_CompactMemOnLoad && !m_Scene.LoginsEnabled)
                GC.Collect(2);

            ScriptInstance instance = null;
            lock (m_Scripts)
            {
                // Create the object record
                if ((!m_Scripts.ContainsKey(itemID)) ||
                    (m_Scripts[itemID].AssetID != assetID))
                {
//                    UUID appDomain = assetID;

//                    if (part.ParentGroup.IsAttachment)
//                        appDomain = part.ParentGroup.RootPart.UUID;
                    UUID appDomain = part.ParentGroup.RootPart.UUID;

                    if (!m_AppDomains.ContainsKey(appDomain))
                    {
                        try
                        {
                            AppDomain sandbox;
                            if (m_AppDomainLoading)
                            {
                                AppDomainSetup appSetup = new AppDomainSetup();
                                appSetup.PrivateBinPath = Path.Combine(
                                    m_ScriptEnginesPath,
                                    m_Scene.RegionInfo.RegionID.ToString());

                                Evidence baseEvidence = AppDomain.CurrentDomain.Evidence;
                                Evidence evidence = new Evidence(baseEvidence);

                                sandbox = AppDomain.CreateDomain(
                                                m_Scene.RegionInfo.RegionID.ToString(),
                                                evidence, appSetup);
                                sandbox.AssemblyResolve +=
                                    new ResolveEventHandler(
                                        AssemblyResolver.OnAssemblyResolve);
                            }
                            else
                            {
                                sandbox = AppDomain.CurrentDomain;
                            }

                            //PolicyLevel sandboxPolicy = PolicyLevel.CreateAppDomainLevel();
                            //AllMembershipCondition sandboxMembershipCondition = new AllMembershipCondition();
                            //PermissionSet sandboxPermissionSet = sandboxPolicy.GetNamedPermissionSet("Internet");
                            //PolicyStatement sandboxPolicyStatement = new PolicyStatement(sandboxPermissionSet);
                            //CodeGroup sandboxCodeGroup = new UnionCodeGroup(sandboxMembershipCondition, sandboxPolicyStatement);
                            //sandboxPolicy.RootCodeGroup = sandboxCodeGroup;
                            //sandbox.SetAppDomainPolicy(sandboxPolicy);

                            m_AppDomains[appDomain] = sandbox;

                            m_DomainScripts[appDomain] = new List<UUID>();
                        }
                        catch (Exception e)
                        {
                            m_log.ErrorFormat("[XEngine] Exception creating app domain:\n {0}", e.ToString());
                            m_ScriptErrorMessage += "Exception creating app domain:\n";
                            m_ScriptFailCount++;
                            lock (m_AddingAssemblies)
                            {
                                m_AddingAssemblies[assemblyPath]--;
                            }
                            lock (m_CompileDict)
                                m_CompileDict.Remove(itemID);
                            return false;
                        }
                    }
                    m_DomainScripts[appDomain].Add(itemID);

                    IScript scriptObj = null;
                    EventWaitHandle coopSleepHandle;
                    bool coopTerminationForThisScript;

                    // Set up assembly name to point to the appropriate scriptEngines directory
                    AssemblyName assemblyName = new AssemblyName(Path.GetFileNameWithoutExtension(assemblyPath));
                    assemblyName.CodeBase = Path.GetDirectoryName(assemblyPath);

                    if (m_coopTermination)
                    {
                        try
                        {
                            coopSleepHandle = new XEngineEventWaitHandle(false, EventResetMode.AutoReset);

                            scriptObj
                                = (IScript)m_AppDomains[appDomain].CreateInstanceAndUnwrap(
                                    assemblyName.FullName,
                                    "SecondLife.XEngineScript",
                                    false,
                                    BindingFlags.Default,
                                    null,
                                    new object[] { coopSleepHandle },
                                    null,
                                    null);

                            coopTerminationForThisScript = true;
                        }
                        catch (TypeLoadException)
                        {
                            coopSleepHandle = null;

                            try
                            {
                                scriptObj
                                    = (IScript)m_AppDomains[appDomain].CreateInstanceAndUnwrap(
                                        assemblyName.FullName,
                                        "SecondLife.Script",
                                        false,
                                        BindingFlags.Default,
                                        null,
                                        null,
                                        null,
                                        null);
                            }
                            catch (Exception e2)
                            {
                                m_log.Error(
                                    string.Format(
                                        "[XENGINE]: Could not load previous SecondLife.Script from assembly {0} in {1}.  Not starting.  Exception  ",
                                        assemblyName.FullName, World.Name),
                                    e2);

                                lock (m_CompileDict)
                                    m_CompileDict.Remove(itemID);
                                return false;
                            }

                            coopTerminationForThisScript = false;
                        }
                    }
                    else
                    {
                        try
                        {
                            scriptObj
                                = (IScript)m_AppDomains[appDomain].CreateInstanceAndUnwrap(
                                    assemblyName.FullName,
                                    "SecondLife.Script",
                                    false,
                                    BindingFlags.Default,
                                    null,
                                    null,
                                    null,
                                    null);

                            coopSleepHandle = null;
                            coopTerminationForThisScript = false;
                        }
                        catch (TypeLoadException)
                        {
                            coopSleepHandle = new XEngineEventWaitHandle(false, EventResetMode.AutoReset);

                            try
                            {
                                scriptObj
                                    = (IScript)m_AppDomains[appDomain].CreateInstanceAndUnwrap(
                                        assemblyName.FullName,
                                        "SecondLife.XEngineScript",
                                        false,
                                        BindingFlags.Default,
                                        null,
                                        new object[] { coopSleepHandle },
                                        null,
                                        null);
                            }
                            catch (Exception e2)
                            {
                                m_log.Error(
                                    string.Format(
                                        "[XENGINE]: Could not load previous SecondLife.XEngineScript from assembly {0} in {1}.  Not starting.  Exception  ",
                                        assemblyName.FullName, World.Name),
                                    e2);

                                lock (m_CompileDict)
                                    m_CompileDict.Remove(itemID);
                                return false;
                            }

                            coopTerminationForThisScript = true;
                        }
                    }

                    if (m_coopTermination != coopTerminationForThisScript && !HaveNotifiedLogOfScriptStopMismatch)
                    {
                        // Notify the log that there is at least one script compile that doesn't match the
                        // ScriptStopStrategy.  Operator has to manually delete old DLLs - we can't do this on Windows
                        // once the assembly has been loaded evne if the instantiation of a class was unsuccessful.
                        m_log.WarnFormat(
                            "[XEngine]: At least one existing compiled script DLL in {0} has {1} as ScriptStopStrategy whereas config setting is {2}."
                            + "\nContinuing with script compiled strategy but to remove this message please set [XEngine] DeleteScriptsOnStartup = true for one simulator session to remove old script DLLs (script state will not be lost).",
                            World.Name, coopTerminationForThisScript ? "co-op" : "abort", m_coopTermination ? "co-op" : "abort");

                        HaveNotifiedLogOfScriptStopMismatch = true;
                    }

                    instance = new ScriptInstance(this, part,
                                                  item,
                                                  startParam, postOnRez,
                                                  m_MaxScriptQueue);

                    if(!instance.Load(scriptObj, coopSleepHandle, assemblyPath,
                            Path.Combine(ScriptEnginePath, World.RegionInfo.RegionID.ToString()), stateSource, coopTerminationForThisScript))
                    {
                        lock (m_CompileDict)
                            m_CompileDict.Remove(itemID);
                        return false;
                    }

//                    if (DebugLevel >= 1)
//                    m_log.DebugFormat(
//                        "[XEngine] Loaded script {0}.{1}, item UUID {2}, prim UUID {3} @ {4}.{5}",
//                        part.ParentGroup.RootPart.Name, item.Name, itemID, part.UUID,
//                        part.ParentGroup.RootPart.AbsolutePosition, part.ParentGroup.Scene.RegionInfo.RegionName);

                    if (presence != null)
                    {
                        ShowScriptSaveResponse(item.OwnerID,
                                assetID, "Compile successful", true);
                    }

                    instance.AppDomain = appDomain;
                    instance.LineMap = linemap;

                    m_Scripts[itemID] = instance;
                }
            }

            lock (m_PrimObjects)
            {
                if (!m_PrimObjects.ContainsKey(localID))
                    m_PrimObjects[localID] = new List<UUID>();

                if (!m_PrimObjects[localID].Contains(itemID))
                    m_PrimObjects[localID].Add(itemID);
            }


            lock (m_AddingAssemblies)
            {
                if (!m_Assemblies.ContainsKey(assetID))
                    m_Assemblies[assetID] = assemblyPath;

                m_AddingAssemblies[assemblyPath]--;
            }

            if (instance != null)
            {
                instance.Init();
                lock (m_CompileDict)
                {
                    foreach (EventParams pp in m_CompileDict[itemID].eventList)
                        instance.PostEvent(pp);
                }
            }
            lock (m_CompileDict)
                m_CompileDict.Remove(itemID);

            bool runIt;
            if (m_runFlags.TryGetValue(itemID, out runIt))
            {
                if (!runIt)
                    StopScript(itemID);
                m_runFlags.Remove(itemID);
            }

            return true;
        }

        public void OnRemoveScript(uint localID, UUID itemID)
        {
            // If it's not yet been compiled, make sure we don't try
            lock (m_CompileDict)
            {
                if (m_CompileDict.ContainsKey(itemID))
                    m_CompileDict.Remove(itemID);
            }

            IScriptInstance instance = null;

            lock (m_Scripts)
            {
                // Do we even have it?
                if (!m_Scripts.ContainsKey(itemID))
                    return;

                instance = m_Scripts[itemID];
                m_Scripts.Remove(itemID);
            }

            instance.Stop(m_WaitForEventCompletionOnScriptStop, true);

            lock (m_PrimObjects)
            {
                // Remove the script from it's prim
                if (m_PrimObjects.ContainsKey(localID))
                {
                    // Remove inventory item record
                    if (m_PrimObjects[localID].Contains(itemID))
                        m_PrimObjects[localID].Remove(itemID);

                    // If there are no more scripts, remove prim
                    if (m_PrimObjects[localID].Count == 0)
                        m_PrimObjects.Remove(localID);
                }
            }

            if (instance.StatePersistedHere)
                instance.RemoveState();

            instance.DestroyScriptInstance();

            m_DomainScripts[instance.AppDomain].Remove(instance.ItemID);
            if (m_DomainScripts[instance.AppDomain].Count == 0)
            {
                m_DomainScripts.Remove(instance.AppDomain);
                UnloadAppDomain(instance.AppDomain);
            }

            ObjectRemoved handlerObjectRemoved = OnObjectRemoved;
            if (handlerObjectRemoved != null)
                handlerObjectRemoved(instance.ObjectID);

            ScriptRemoved handlerScriptRemoved = OnScriptRemoved;
            if (handlerScriptRemoved != null)
                handlerScriptRemoved(itemID);
        }

        public void OnScriptReset(uint localID, UUID itemID)
        {
            ResetScript(itemID);
        }

        public void OnStartScript(uint localID, UUID itemID)
        {
            StartScript(itemID);
        }

        public void OnStopScript(uint localID, UUID itemID)
        {
            StopScript(itemID);
        }

        private void CleanAssemblies()
        {
            List<UUID> assetIDList = new List<UUID>(m_Assemblies.Keys);

            foreach (IScriptInstance i in m_Scripts.Values)
            {
                if (assetIDList.Contains(i.AssetID))
                    assetIDList.Remove(i.AssetID);
            }

            lock (m_AddingAssemblies)
            {
                foreach (UUID assetID in assetIDList)
                {
                    // Do not remove assembly files if another instance of the script
                    // is currently initialising
                    if (!m_AddingAssemblies.ContainsKey(m_Assemblies[assetID])
                        || m_AddingAssemblies[m_Assemblies[assetID]] == 0)
                    {
//                        m_log.DebugFormat("[XEngine] Removing unreferenced assembly {0}", m_Assemblies[assetID]);
                        try
                        {
                            if (File.Exists(m_Assemblies[assetID]))
                                File.Delete(m_Assemblies[assetID]);

                            if (File.Exists(m_Assemblies[assetID]+".text"))
                                File.Delete(m_Assemblies[assetID]+".text");

                            if (File.Exists(m_Assemblies[assetID]+".mdb"))
                                File.Delete(m_Assemblies[assetID]+".mdb");

                            if (File.Exists(m_Assemblies[assetID]+".map"))
                                File.Delete(m_Assemblies[assetID]+".map");
                        }
                        catch (Exception)
                        {
                        }
                        m_Assemblies.Remove(assetID);
                    }
                }
            }
        }

        private void UnloadAppDomain(UUID id)
        {
            if (m_AppDomains.ContainsKey(id))
            {
                AppDomain domain = m_AppDomains[id];
                m_AppDomains.Remove(id);

                if (domain != AppDomain.CurrentDomain)
                    AppDomain.Unload(domain);
                domain = null;
                // m_log.DebugFormat("[XEngine] Unloaded app domain {0}", id.ToString());
            }
        }

        //
        // Start processing
        //
        private void SetupEngine(int minThreads, int maxThreads,
                                 int idleTimeout, ThreadPriority threadPriority,
                                 int maxScriptQueue, int stackSize)
        {
            m_MaxScriptQueue = maxScriptQueue;

            STPStartInfo startInfo = new STPStartInfo();
            startInfo.ThreadPoolName = "XEngine";
            startInfo.IdleTimeout = idleTimeout * 1000; // convert to seconds as stated in .ini
            startInfo.MaxWorkerThreads = maxThreads;
            startInfo.MinWorkerThreads = minThreads;
            startInfo.ThreadPriority = threadPriority;;
            startInfo.MaxStackSize = stackSize;
            startInfo.StartSuspended = true;

            m_ThreadPool = new SmartThreadPool(startInfo);
        }

        //
        // Used by script instances to queue event handler jobs
        //
        public IScriptWorkItem QueueEventHandler(object parms)
        {
            return new XWorkItem(m_ThreadPool.QueueWorkItem(
                                     new WorkItemCallback(this.ProcessEventHandler),
                                     parms));
        }

        /// <summary>
        /// Process a previously posted script event.
        /// </summary>
        /// <param name="parms"></param>
        /// <returns></returns>
        private object ProcessEventHandler(object parms)
        {
            Culture.SetCurrentCulture();

            IScriptInstance instance = (ScriptInstance) parms;

//            m_log.DebugFormat("[XEngine]: Processing event for {0}", instance);

            return instance.EventProcessor();
        }

        /// <summary>
        /// Post event to an entire prim
        /// </summary>
        /// <param name="localID"></param>
        /// <param name="p"></param>
        /// <returns></returns>
        public bool PostObjectEvent(uint localID, EventParams p)
        {
            bool result = false;
            List<UUID> uuids = null;

            lock (m_PrimObjects)
            {
                if (!m_PrimObjects.ContainsKey(localID))
                    return false;

                uuids = m_PrimObjects[localID];

                foreach (UUID itemID in uuids)
                {
                    IScriptInstance instance = null;
                    try
                    {
                        if (m_Scripts.ContainsKey(itemID))
                            instance = m_Scripts[itemID];
                    }
                    catch { /* ignore race conditions */ }

                    if (instance != null)
                    {
                        instance.PostEvent(p);
                        result = true;
                    }
                    else
                    {
                        lock (m_CompileDict)
                        {
                            if (m_CompileDict.ContainsKey(itemID))
                            {
                                m_CompileDict[itemID].eventList.Add(p);
                                result = true;
                            }
                        }
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Post an event to a single script
        /// </summary>
        /// <param name="itemID"></param>
        /// <param name="p"></param>
        /// <returns></returns>
        public bool PostScriptEvent(UUID itemID, EventParams p)
        {
            if (m_Scripts.ContainsKey(itemID))
            {
                IScriptInstance instance = m_Scripts[itemID];
                if (instance != null)
                    instance.PostEvent(p);
                return true;
            }
            lock (m_CompileDict)
            {
                if (m_CompileDict.ContainsKey(itemID))
                {
                    m_CompileDict[itemID].eventList.Add(p);
                    return true;
                }
            }
            return false;
        }

        public bool PostScriptEvent(UUID itemID, string name, Object[] p)
        {
            Object[] lsl_p = new Object[p.Length];
            for (int i = 0; i < p.Length ; i++)
            {
                if (p[i] is int)
                    lsl_p[i] = new LSL_Types.LSLInteger((int)p[i]);
                else if (p[i] is string)
                    lsl_p[i] = new LSL_Types.LSLString((string)p[i]);
                else if (p[i] is Vector3)
                    lsl_p[i] = new LSL_Types.Vector3((Vector3)p[i]);
                else if (p[i] is Quaternion)
                    lsl_p[i] = new LSL_Types.Quaternion((Quaternion)p[i]);
                else if (p[i] is float)
                    lsl_p[i] = new LSL_Types.LSLFloat((float)p[i]);
                else
                    lsl_p[i] = p[i];
            }

            return PostScriptEvent(itemID, new EventParams(name, lsl_p, new DetectParams[0]));
        }

        public bool PostObjectEvent(UUID itemID, string name, Object[] p)
        {
            SceneObjectPart part = m_Scene.GetSceneObjectPart(itemID);
            if (part == null)
                return false;

            Object[] lsl_p = new Object[p.Length];
            for (int i = 0; i < p.Length ; i++)
            {
                if (p[i] is int)
                    lsl_p[i] = new LSL_Types.LSLInteger((int)p[i]);
                else if (p[i] is string)
                    lsl_p[i] = new LSL_Types.LSLString((string)p[i]);
                else if (p[i] is Vector3)
                    lsl_p[i] = new LSL_Types.Vector3((Vector3)p[i]);
                else if (p[i] is Quaternion)
                    lsl_p[i] = new LSL_Types.Quaternion((Quaternion)p[i]);
                else if (p[i] is float)
                    lsl_p[i] = new LSL_Types.LSLFloat((float)p[i]);
                else
                    lsl_p[i] = p[i];
            }

            return PostObjectEvent(part.LocalId, new EventParams(name, lsl_p, new DetectParams[0]));
        }

        public Assembly OnAssemblyResolve(object sender,
                                          ResolveEventArgs args)
        {
            if (!(sender is System.AppDomain))
                return null;

            string[] pathList = new string[] {"bin", m_ScriptEnginesPath,
                                              Path.Combine(m_ScriptEnginesPath,
                                                           m_Scene.RegionInfo.RegionID.ToString())};

            string assemblyName = args.Name;
            if (assemblyName.IndexOf(",") != -1)
                assemblyName = args.Name.Substring(0, args.Name.IndexOf(","));

            foreach (string s in pathList)
            {
                string path = Path.Combine(Directory.GetCurrentDirectory(),
                                           Path.Combine(s, assemblyName))+".dll";

//                Console.WriteLine("[XEngine]: Trying to resolve {0}", path);

                if (File.Exists(path))
                    return Assembly.LoadFrom(path);
            }

            return null;
        }

        private IScriptInstance GetInstance(UUID itemID)
        {
            IScriptInstance instance;
            lock (m_Scripts)
            {
                if (!m_Scripts.ContainsKey(itemID))
                    return null;
                instance = m_Scripts[itemID];
            }
            return instance;
        }

        public void SetScriptState(UUID itemID, bool running, bool self)
        {
            IScriptInstance instance = GetInstance(itemID);
            if (instance != null)
            {
                if (running)
                        instance.Start();
                else
                {
                    if(self)
                    {
                        instance.Running = false;
                        throw new EventAbortException();
                    }
                    else
                        instance.Stop(100);
                }
            }
        }

        public bool GetScriptState(UUID itemID)
        {
            IScriptInstance instance = GetInstance(itemID);
            return instance != null && instance.Running;
        }

        public void ApiResetScript(UUID itemID)
        {
            IScriptInstance instance = GetInstance(itemID);
            if (instance != null)
                instance.ApiResetScript();

            // Send the new number of threads that are in use by the thread
            // pool, I believe that by adding them to the locations where the
            // script is changing states that I will catch all changes to the
            // thread pool
            m_Scene.setThreadCount(m_ThreadPool.InUseThreads);
        }

        public void ResetScript(UUID itemID)
        {
            IScriptInstance instance = GetInstance(itemID);
            if (instance != null)
                instance.ResetScript(m_WaitForEventCompletionOnScriptStop);

            // Send the new number of threads that are in use by the thread
            // pool, I believe that by adding them to the locations where the
            // script is changing states that I will catch all changes to the
            // thread pool
            m_Scene.setThreadCount(m_ThreadPool.InUseThreads);
        }

        public void StartScript(UUID itemID)
        {
            IScriptInstance instance = GetInstance(itemID);
            if (instance != null)
                instance.Start();
            else
                m_runFlags.AddOrUpdate(itemID, true, 240);

            // Send the new number of threads that are in use by the thread
            // pool, I believe that by adding them to the locations where the
            // script is changing states that I will catch all changes to the
            // thread pool
            m_Scene.setThreadCount(m_ThreadPool.InUseThreads);
        }

        public void StopScript(UUID itemID)
        {
            IScriptInstance instance = GetInstance(itemID);

            if (instance != null)
            {
                lock (instance.EventQueue)
                    instance.StayStopped = true;    // the script was stopped explicitly

                instance.Stop(m_WaitForEventCompletionOnScriptStop);
            }
            else
            {
//                m_log.DebugFormat("[XENGINE]: Could not find script with ID {0} to stop in {1}", itemID, World.Name);
                m_runFlags.AddOrUpdate(itemID, false, 240);
            }

            // Send the new number of threads that are in use by the thread
            // pool, I believe that by adding them to the locations where the
            // script is changing states that I will catch all changes to the
            // thread pool
            m_Scene.setThreadCount(m_ThreadPool.InUseThreads);
        }

        public DetectParams GetDetectParams(UUID itemID, int idx)
        {
            IScriptInstance instance = GetInstance(itemID);
            return instance != null ? instance.GetDetectParams(idx) : null;
        }

        public void SetMinEventDelay(UUID itemID, double delay)
        {
            IScriptInstance instance = GetInstance(itemID);
            if (instance != null)
                instance.MinEventDelay = delay;
        }

        public UUID GetDetectID(UUID itemID, int idx)
        {
            IScriptInstance instance = GetInstance(itemID);
            return instance != null ? instance.GetDetectID(idx) : UUID.Zero;
        }

        public void SetState(UUID itemID, string newState)
        {
            IScriptInstance instance = GetInstance(itemID);
            if (instance == null)
                return;
            instance.SetState(newState);
        }

        public int GetStartParameter(UUID itemID)
        {
            IScriptInstance instance = GetInstance(itemID);
            return instance == null ? 0 : instance.StartParam;
        }

        public void OnShutdown()
        {
            m_SimulatorShuttingDown = true;

            List<IScriptInstance> instances = new List<IScriptInstance>();

            lock (m_Scripts)
            {
                foreach (IScriptInstance instance in m_Scripts.Values)
                    instances.Add(instance);
            }

            foreach (IScriptInstance i in instances)
            {
                // Stop the script, even forcibly if needed. Then flag
                // it as shutting down and restore the previous run state
                // for serialization, so the scripts don't come back
                // dead after region restart
                //
                bool prevRunning = i.Running;
                i.Stop(50);
                i.ShuttingDown = true;
                i.Running = prevRunning;
            }

            DoBackup(new Object[] {0});
        }

        public IScriptApi GetApi(UUID itemID, string name)
        {
            IScriptInstance instance = GetInstance(itemID);
            return instance == null ? null : instance.GetApi(name);
        }

        public void OnGetScriptRunning(IClientAPI controllingClient, UUID objectID, UUID itemID)
        {
            IScriptInstance instance = GetInstance(itemID);
            if (instance == null)
                return;
            IEventQueue eq = World.RequestModuleInterface<IEventQueue>();
            if (eq == null)
            {
                controllingClient.SendScriptRunningReply(objectID, itemID,
                        GetScriptState(itemID));
            }
            else
            {
                eq.ScriptRunningEvent(objectID, itemID, GetScriptState(itemID), controllingClient.AgentId);
            }
        }

        public string GetXMLState(UUID itemID)
        {
//            m_log.DebugFormat("[XEngine]: Getting XML state for script instance {0}", itemID);

            IScriptInstance instance = GetInstance(itemID);
            if (instance == null)
            {
//                m_log.DebugFormat("[XEngine]: Found no script instance for {0}, returning empty string", itemID);
                return "";
            }

            string xml = instance.GetXMLState();

            XmlDocument sdoc = new XmlDocument();

            bool loadedState = true;
            try
            {
                sdoc.LoadXml(xml);
            }
            catch (System.Xml.XmlException)
            {
                loadedState = false;
            }

            XmlNodeList rootL = null;
            XmlNode rootNode = null;
            if (loadedState)
            {
                rootL = sdoc.GetElementsByTagName("ScriptState");
                rootNode = rootL[0];
            }

            // Create <State UUID="xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx">
            XmlDocument doc = new XmlDocument();
            XmlElement stateData = doc.CreateElement("", "State", "");
            XmlAttribute stateID = doc.CreateAttribute("", "UUID", "");
            stateID.Value = itemID.ToString();
            stateData.Attributes.Append(stateID);
            XmlAttribute assetID = doc.CreateAttribute("", "Asset", "");
            assetID.Value = instance.AssetID.ToString();
            stateData.Attributes.Append(assetID);
            XmlAttribute engineName = doc.CreateAttribute("", "Engine", "");
            engineName.Value = ScriptEngineName;
            stateData.Attributes.Append(engineName);
            doc.AppendChild(stateData);

            XmlNode xmlstate = null;

            // Add <ScriptState>...</ScriptState>
            if (loadedState)
            {
                xmlstate = doc.ImportNode(rootNode, true);
            }
            else
            {
                xmlstate = doc.CreateElement("", "ScriptState", "");
            }

            stateData.AppendChild(xmlstate);

            string assemName = instance.GetAssemblyName();

            string fn = Path.GetFileName(assemName);

            string assem = String.Empty;
            string assemNameText = assemName + ".text";

            if (File.Exists(assemNameText))
            {
                FileInfo tfi = new FileInfo(assemNameText);

                if (tfi != null)
                {
                    Byte[] tdata = new Byte[tfi.Length];

                    try
                    {
                        using (FileStream tfs = File.Open(assemNameText,
                                FileMode.Open, FileAccess.Read))
                        {
                            tfs.Read(tdata, 0, tdata.Length);
                        }

                        assem = Encoding.ASCII.GetString(tdata);
                    }
                    catch (Exception e)
                    {
                         m_log.ErrorFormat(
                            "[XEngine]: Unable to open script textfile {0}{1}, reason: {2}",
                            assemName, ".text", e.Message);
                    }
                }
            }
            else
            {
                FileInfo fi = new FileInfo(assemName);

                if (fi != null)
                {
                    Byte[] data = new Byte[fi.Length];

                    try
                    {
                        using (FileStream fs = File.Open(assemName, FileMode.Open, FileAccess.Read))
                        {
                            fs.Read(data, 0, data.Length);
                        }

                        assem = System.Convert.ToBase64String(data);
                    }
                    catch (Exception e)
                    {
                        m_log.ErrorFormat(
                            "[XEngine]: Unable to open script assembly {0}, reason: {1}", assemName, e.Message);
                    }
                }
            }

            string map = String.Empty;

            if (File.Exists(fn + ".map"))
            {
                using (FileStream mfs = File.Open(fn + ".map", FileMode.Open, FileAccess.Read))
                {
                    using (StreamReader msr = new StreamReader(mfs))
                    {
                        map = msr.ReadToEnd();
                    }
                }
            }

            XmlElement assemblyData = doc.CreateElement("", "Assembly", "");
            XmlAttribute assemblyName = doc.CreateAttribute("", "Filename", "");

            assemblyName.Value = fn;
            assemblyData.Attributes.Append(assemblyName);

            assemblyData.InnerText = assem;

            stateData.AppendChild(assemblyData);

            XmlElement mapData = doc.CreateElement("", "LineMap", "");
            XmlAttribute mapName = doc.CreateAttribute("", "Filename", "");

            mapName.Value = fn + ".map";
            mapData.Attributes.Append(mapName);

            mapData.InnerText = map;

            stateData.AppendChild(mapData);

            // m_log.DebugFormat("[XEngine]: Got XML state for {0}", itemID);

            return doc.InnerXml;
        }

        private bool ShowScriptSaveResponse(UUID ownerID, UUID assetID, string text, bool compiled)
        {
            return false;
        }

        public bool SetXMLState(UUID itemID, string xml)
        {
//            m_log.DebugFormat("[XEngine]: Writing state for script item with ID {0}", itemID);

            if (xml == String.Empty)
                return false;

            XmlDocument doc = new XmlDocument();

            try
            {
                doc.LoadXml(xml);
            }
            catch (Exception)
            {
                m_log.Error("[XEngine]: Exception decoding XML data from region transfer");
                return false;
            }

            XmlNodeList rootL = doc.GetElementsByTagName("State");
            if (rootL.Count < 1)
                return false;

            XmlElement rootE = (XmlElement)rootL[0];

            if (rootE.GetAttribute("Engine") != ScriptEngineName)
                return false;

//          On rez from inventory, that ID will have changed. It was only
//          advisory anyway. So we don't check it anymore.
//
//            if (rootE.GetAttribute("UUID") != itemID.ToString())
//                return;

            XmlNodeList stateL = rootE.GetElementsByTagName("ScriptState");

            if (stateL.Count != 1)
                return false;

            XmlElement stateE = (XmlElement)stateL[0];

            if (World.m_trustBinaries)
            {
                XmlNodeList assemL = rootE.GetElementsByTagName("Assembly");

                if (assemL.Count != 1)
                    return false;

                XmlElement assemE = (XmlElement)assemL[0];

                string fn = assemE.GetAttribute("Filename");
                string base64 = assemE.InnerText;

                string path = Path.Combine(m_ScriptEnginesPath, World.RegionInfo.RegionID.ToString());
                path = Path.Combine(path, fn);

                if (!File.Exists(path))
                {
                    Byte[] filedata = Convert.FromBase64String(base64);

                    try
                    {
                        using (FileStream fs = File.Create(path))
                        {
//                            m_log.DebugFormat("[XEngine]: Writing assembly file {0}", path);

                            fs.Write(filedata, 0, filedata.Length);
                        }
                    }
                    catch (IOException ex)
                    {
                        // if there already exists a file at that location, it may be locked.
                        m_log.ErrorFormat("[XEngine]: Error whilst writing assembly file {0}, {1}", path, ex.Message);
                    }

                    string textpath = path + ".text";
                    try
                    {
                        using (FileStream fs = File.Create(textpath))
                        {
                            using (StreamWriter sw = new StreamWriter(fs))
                            {
//                                m_log.DebugFormat("[XEngine]: Writing .text file {0}", textpath);

                                sw.Write(base64);
                            }
                        }
                    }
                    catch (IOException ex)
                    {
                        // if there already exists a file at that location, it may be locked.
                        m_log.ErrorFormat("[XEngine]: Error whilst writing .text file {0}, {1}", textpath, ex.Message);
                    }
                }

                XmlNodeList mapL = rootE.GetElementsByTagName("LineMap");
                if (mapL.Count > 0)
                {
                    XmlElement mapE = (XmlElement)mapL[0];

                    string mappath = Path.Combine(m_ScriptEnginesPath, World.RegionInfo.RegionID.ToString());
                    mappath = Path.Combine(mappath, mapE.GetAttribute("Filename"));

                    try
                    {
                        using (FileStream mfs = File.Create(mappath))
                        {
                            using (StreamWriter msw = new StreamWriter(mfs))
                            {
    //                            m_log.DebugFormat("[XEngine]: Writing linemap file {0}", mappath);

                                msw.Write(mapE.InnerText);
                            }
                        }
                    }
                    catch (IOException ex)
                    {
                        // if there already exists a file at that location, it may be locked.
                        m_log.Error(
                            string.Format("[XEngine]: Linemap file {0} could not be written.  Exception  ", mappath), ex);
                    }
                }
            }

            string statepath = Path.Combine(m_ScriptEnginesPath, World.RegionInfo.RegionID.ToString());
            statepath = Path.Combine(statepath, itemID.ToString() + ".state");

            try
            {
                using (FileStream sfs = File.Create(statepath))
                {
                    using (StreamWriter ssw = new StreamWriter(sfs))
                    {
//                        m_log.DebugFormat("[XEngine]: Writing state file {0}", statepath);

                        ssw.Write(stateE.OuterXml);
                    }
                }
            }
            catch (IOException ex)
            {
                // if there already exists a file at that location, it may be locked.
                m_log.ErrorFormat("[XEngine]: Error whilst writing state file {0}, {1}", statepath, ex.Message);
            }

//            m_log.DebugFormat(
//                "[XEngine]: Wrote state for script item with ID {0} at {1} in {2}", itemID, statepath, m_Scene.Name);

            return true;
        }

        public ArrayList GetScriptErrors(UUID itemID)
        {
            System.Threading.Thread.Sleep(1000);

            lock (m_ScriptErrors)
            {
                if (m_ScriptErrors.ContainsKey(itemID))
                {
                    ArrayList ret = m_ScriptErrors[itemID];
                    m_ScriptErrors.Remove(itemID);
                    return ret;
                }
                return new ArrayList();
            }
        }

        public Dictionary<uint, float> GetObjectScriptsExecutionTimes()
        {
            Dictionary<uint, float> topScripts = new Dictionary<uint, float>();

            lock (m_Scripts)
            {
                foreach (IScriptInstance si in m_Scripts.Values)
                {
                    if (!topScripts.ContainsKey(si.LocalID))
                        topScripts[si.RootLocalID] = 0;

                    topScripts[si.RootLocalID] += GetExectionTime(si);
                }
            }

            return topScripts;
        }

        public float GetScriptExecutionTime(List<UUID> itemIDs)
        {
            if (itemIDs == null|| itemIDs.Count == 0)
            {
                return 0.0f;
            }
            float time = 0.0f;
            IScriptInstance si;
            // Calculate the time for all scripts that this engine is executing
            // Ignore any others
            foreach (UUID id in itemIDs)
            {
                si = GetInstance(id);
                if (si != null && si.Running)
                {
                    time += GetExectionTime(si);
                }
            }
            return time;
        }

        private float GetExectionTime(IScriptInstance si)
        {
            return (float)si.ExecutionTime.GetSumTime().TotalMilliseconds;
        }

        public void SuspendScript(UUID itemID)
        {
//            m_log.DebugFormat("[XEngine]: Received request to suspend script with ID {0}", itemID);

            IScriptInstance instance = GetInstance(itemID);
            if (instance != null)
                instance.Suspend();
//            else
//                m_log.DebugFormat("[XEngine]: Could not find script with ID {0} to resume", itemID);

            // Send the new number of threads that are in use by the thread
            // pool, I believe that by adding them to the locations where the
            // script is changing states that I will catch all changes to the
            // thread pool
            m_Scene.setThreadCount(m_ThreadPool.InUseThreads);
        }

        public void ResumeScript(UUID itemID)
        {
//            m_log.DebugFormat("[XEngine]: Received request to resume script with ID {0}", itemID);

            IScriptInstance instance = GetInstance(itemID);
            if (instance != null)
                instance.Resume();
//            else
//                m_log.DebugFormat("[XEngine]: Could not find script with ID {0} to resume", itemID);

            // Send the new number of threads that are in use by the thread
            // pool, I believe that by adding them to the locations where the
            // script is changing states that I will catch all changes to the
            // thread pool
            m_Scene.setThreadCount(m_ThreadPool.InUseThreads);
        }

        public bool HasScript(UUID itemID, out bool running)
        {
            running = true;

            IScriptInstance instance = GetInstance(itemID);
            if (instance == null)
                return false;

            running = instance.Running;
            return true;
        }

        public void SleepScript(UUID itemID, int delay)
        {
            IScriptInstance instance = GetInstance(itemID);
            if (instance == null)
                return;

            instance.ExecutionTimer.Stop();
            try
            {
                if (instance.CoopWaitHandle != null)
                {
                    if (instance.CoopWaitHandle.WaitOne(delay))
                        throw new ScriptCoopStopException();
                }
                else
                {
                    Thread.Sleep(delay);
                }
            }
            finally
            {
                instance.ExecutionTimer.Start();
            }
        }
    }
}
