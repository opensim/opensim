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
using System.IO;
using System.Reflection;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;
using OpenMetaverse;
using OpenSim.Region.Environment.Scenes;

namespace OpenSim.Region.ScriptEngine.Common.ScriptEngineBase
{
    /// <summary>
    /// Loads scripts
    /// Compiles them if necessary
    /// Execute functions for EventQueueManager (Sends them to script on other AppDomain for execution)
    /// </summary>
    ///

    // This class is as close as you get to the script without being inside script class. It handles all the dirty work for other classes.
    // * Keeps track of running scripts
    // * Compiles script if necessary (through "Compiler")
    // * Loads script (through "AppDomainManager" called from for example "EventQueueManager")
    // * Executes functions inside script (called from for example "EventQueueManager" class)
    // * Unloads script (through "AppDomainManager" called from for example "EventQueueManager")
    // * Dedicated load/unload thread, and queues loading/unloading.
    //   This so that scripts starting or stopping will not slow down other theads or whole system.
    //
    [Serializable]
    public abstract class ScriptManager : iScriptEngineFunctionModule
    {
        #region Declares

        private Thread scriptLoadUnloadThread;
        private static Thread staticScriptLoadUnloadThread;
        // private int scriptLoadUnloadThread_IdleSleepms;
        private Queue<LUStruct> LUQueue = new Queue<LUStruct>();
        private static bool PrivateThread;
        private int LoadUnloadMaxQueueSize;
        private Object scriptLock = new Object();

        // Load/Unload structure
        private struct LUStruct
        {
            public uint localID;
            public UUID itemID;
            public string script;
            public LUType Action;
            public int startParam;
            public bool postOnRez;
        }

        private enum LUType
        {
            Unknown = 0,
            Load = 1,
            Unload = 2
        }

        // Xantor 20080525: Keep a list of compiled scripts this session for reuse
        public Dictionary<UUID, String> scriptList = new Dictionary<UUID, string>();

        // Object<string, Script<string, script>>
        // IMPORTANT: Types and MemberInfo-derived objects require a LOT of memory.
        // Instead use RuntimeTypeHandle, RuntimeFieldHandle and RunTimeHandle (IntPtr) instead!
        public Dictionary<uint, Dictionary<UUID, IScript>> Scripts =
            new Dictionary<uint, Dictionary<UUID, IScript>>();


        public Scene World
        {
            get { return m_scriptEngine.World; }
        }

        #endregion

        public void ReadConfig()
        {
            // scriptLoadUnloadThread_IdleSleepms = m_scriptEngine.ScriptConfigSource.GetInt("ScriptLoadUnloadLoopms", 30);
            // TODO: Requires sharing of all ScriptManagers to single thread
            PrivateThread = true; // m_scriptEngine.ScriptConfigSource.GetBoolean("PrivateScriptLoadUnloadThread", false);
            LoadUnloadMaxQueueSize = m_scriptEngine.ScriptConfigSource.GetInt("LoadUnloadMaxQueueSize", 100);
        }

        #region Object init/shutdown

        public ScriptEngine m_scriptEngine;

        public ScriptManager(ScriptEngine scriptEngine)
        {
            m_scriptEngine = scriptEngine;
        }
        public abstract void Initialize();
        public void Start()
        {
            ReadConfig();
            Initialize();

            AppDomain.CurrentDomain.AssemblyResolve += new ResolveEventHandler(CurrentDomain_AssemblyResolve);

            //
            // CREATE THREAD
            // Private or shared
            //
            if (PrivateThread)
            {
                // Assign one thread per region
                //scriptLoadUnloadThread = StartScriptLoadUnloadThread();
            }
            else
            {
                // Shared thread - make sure one exist, then assign it to the private
                if (staticScriptLoadUnloadThread == null)
                {
                    //staticScriptLoadUnloadThread = StartScriptLoadUnloadThread();
                }
                scriptLoadUnloadThread = staticScriptLoadUnloadThread;
            }
        }

// TODO: unused
//        private static int privateThreadCount = 0;
//         private Thread StartScriptLoadUnloadThread()
//         {
//             Thread t = new Thread(ScriptLoadUnloadThreadLoop);
//             string name = "ScriptLoadUnloadThread:";
//             if (PrivateThread)
//             {
//                 name += "Private:" + privateThreadCount;
//                 privateThreadCount++;
//             }
//             else
//             {
//                 name += "Shared";
//             }
//             t.Name = name;
//             t.IsBackground = true;
//             t.Priority = ThreadPriority.Normal;
//             t.Start();
//             OpenSim.Framework.ThreadTracker.Add(t);
//             return t;
//         }

        ~ScriptManager()
        {
            // Abort load/unload thread
            try
            {
                //PleaseShutdown = true;
                //Thread.Sleep(100);
                if (scriptLoadUnloadThread != null && scriptLoadUnloadThread.IsAlive == true)
                {
                    scriptLoadUnloadThread.Abort();
                    //scriptLoadUnloadThread.Join();
                }
            }
            catch
            {
            }
        }

        #endregion

        #region Load / Unload scripts (Thread loop)

// TODO: unused
//         private void ScriptLoadUnloadThreadLoop()
//         {
//             try
//             {
//                 while (true)
//                 {
//                     if (LUQueue.Count == 0)
//                         Thread.Sleep(scriptLoadUnloadThread_IdleSleepms);
//                     //if (PleaseShutdown)
//                     //    return;
//                     DoScriptLoadUnload();
//                 }
//             }
//             catch (ThreadAbortException tae)
//             {
//                 string a = tae.ToString();
//                 a = String.Empty;
//                 // Expected
//             }
//         }

        public void DoScriptLoadUnload()
        {
            lock (LUQueue)
            {
                if (LUQueue.Count > 0)
                {
                    LUStruct item = LUQueue.Dequeue();

                    if (item.Action == LUType.Unload)
                    {
                        _StopScript(item.localID, item.itemID);
                    }
                    else if (item.Action == LUType.Load)
                    {
                        _StartScript(item.localID, item.itemID, item.script, item.startParam, item.postOnRez);
                    }
                }
            }
        }

        #endregion

        #region Helper functions

        private static Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            //Console.WriteLine("ScriptManager.CurrentDomain_AssemblyResolve: " + args.Name);
            return Assembly.GetExecutingAssembly().FullName == args.Name ? Assembly.GetExecutingAssembly() : null;
        }

        #endregion



        #region Start/Stop/Reset script

        // private readonly Object startStopLock = new Object();

        /// <summary>
        /// Fetches, loads and hooks up a script to an objects events
        /// </summary>
        /// <param name="itemID"></param>
        /// <param name="localID"></param>
        public void StartScript(uint localID, UUID itemID, string Script, int startParam, bool postOnRez)
        {
            lock (LUQueue)
            {
                if (LUQueue.Count >= LoadUnloadMaxQueueSize)
                {
                    m_scriptEngine.Log.Error("[" + m_scriptEngine.ScriptEngineName + "]: ERROR: Load/unload queue item count is at " + LUQueue.Count + ". Config variable \"LoadUnloadMaxQueueSize\" is set to " + LoadUnloadMaxQueueSize + ", so ignoring new script.");
                    return;
                }

                LUStruct ls = new LUStruct();
                ls.localID = localID;
                ls.itemID = itemID;
                ls.script = Script;
                ls.Action = LUType.Load;
                ls.startParam = startParam;
                ls.postOnRez = postOnRez;
                LUQueue.Enqueue(ls);
            }
        }

        /// <summary>
        /// Disables and unloads a script
        /// </summary>
        /// <param name="localID"></param>
        /// <param name="itemID"></param>
        public void StopScript(uint localID, UUID itemID)
        {
            LUStruct ls = new LUStruct();
            ls.localID = localID;
            ls.itemID = itemID;
            ls.Action = LUType.Unload;
            ls.startParam = 0;
            ls.postOnRez = false;
            lock (LUQueue)
            {
                LUQueue.Enqueue(ls);
            }
        }

        // Create a new instance of the compiler (reuse)
        //private Compiler.LSL.Compiler LSLCompiler = new Compiler.LSL.Compiler();

        public abstract void _StartScript(uint localID, UUID itemID, string Script, int startParam, bool postOnRez);
        public abstract void _StopScript(uint localID, UUID itemID);


        #endregion

        #region Perform event execution in script

        /// <summary>
        /// Execute a LL-event-function in Script
        /// </summary>
        /// <param name="localID">Object the script is located in</param>
        /// <param name="itemID">Script ID</param>
        /// <param name="FunctionName">Name of function</param>
        /// <param name="args">Arguments to pass to function</param>
        internal void ExecuteEvent(uint localID, UUID itemID, string FunctionName, EventQueueManager.Queue_llDetectParams_Struct qParams, object[] args)
        {
            //cfk 2-7-08 dont need this right now and the default Linux build has DEBUG defined
            ///#if DEBUG
            ///            Console.WriteLine("ScriptEngine: Inside ExecuteEvent for event " + FunctionName);
            ///#endif
            // Execute a function in the script
            //m_scriptEngine.Log.Info("[" + ScriptEngineName + "]: Executing Function localID: " + localID + ", itemID: " + itemID + ", FunctionName: " + FunctionName);
            //ScriptBaseInterface Script = (ScriptBaseInterface)GetScript(localID, itemID);
            IScript Script = GetScript(localID, itemID);
            if (Script == null)
            {
                return;
            }
            //cfk 2-7-08 dont need this right now and the default Linux build has DEBUG defined
            ///#if DEBUG
            ///            Console.WriteLine("ScriptEngine: Executing event: " + FunctionName);
            ///#endif
            // Must be done in correct AppDomain, so leaving it up to the script itself
            Script.llDetectParams = qParams;
            Script.Exec.ExecuteEvent(FunctionName, args);
        }

        public int GetStateEventFlags(uint localID, UUID itemID)
        {
            // Console.WriteLine("GetStateEventFlags for <" + localID + "," + itemID + ">");
            try
            {
                IScript Script = GetScript(localID, itemID);
                if (Script == null)
                {
                    return 0;
                }
                ExecutorBase.scriptEvents evflags = Script.Exec.GetStateEventFlags();
                return (int)evflags;
            }
            catch (Exception)
            {
            }

            return 0;
        }


        #endregion

        #region Internal functions to keep track of script

        public Dictionary<UUID, IScript>.KeyCollection GetScriptKeys(uint localID)
        {
            if (Scripts.ContainsKey(localID) == false)
                return null;

            Dictionary<UUID, IScript> Obj;
            Scripts.TryGetValue(localID, out Obj);

            return Obj.Keys;
        }

        public IScript GetScript(uint localID, UUID itemID)
        {
            lock (scriptLock)
            {
                if (Scripts.ContainsKey(localID) == false)
                    return null;

                Dictionary<UUID, IScript> Obj;
                Scripts.TryGetValue(localID, out Obj);
                if (Obj.ContainsKey(itemID) == false)
                    return null;

                // Get script
                IScript Script;
                Obj.TryGetValue(itemID, out Script);
                return Script;
            }
        }

        public void SetScript(uint localID, UUID itemID, IScript Script)
        {
            lock (scriptLock)
            {
                // Create object if it doesn't exist
                if (Scripts.ContainsKey(localID) == false)
                {
                    Scripts.Add(localID, new Dictionary<UUID, IScript>());
                }

                // Delete script if it exists
                Dictionary<UUID, IScript> Obj;
                Scripts.TryGetValue(localID, out Obj);
                if (Obj.ContainsKey(itemID) == true)
                    Obj.Remove(itemID);

                // Add to object
                Obj.Add(itemID, Script);
            }
        }

        public void RemoveScript(uint localID, UUID itemID)
        {
            // Don't have that object?
            if (Scripts.ContainsKey(localID) == false)
                return;

            // Delete script if it exists
            Dictionary<UUID, IScript> Obj;
            Scripts.TryGetValue(localID, out Obj);
            if (Obj.ContainsKey(itemID) == true)
                Obj.Remove(itemID);
        }

        #endregion


        public void ResetScript(uint localID, UUID itemID)
        {
            IScript s = GetScript(localID, itemID);
            string script = s.Source;
            StopScript(localID, itemID);
            SceneObjectPart part = World.GetSceneObjectPart(localID);
            part.GetInventoryItem(itemID).PermsMask = 0;
            part.GetInventoryItem(itemID).PermsGranter = UUID.Zero;
            StartScript(localID, itemID, script, s.StartParam, false);
        }


        #region Script serialization/deserialization

        public void GetSerializedScript(uint localID, UUID itemID)
        {
            // Serialize the script and return it
            // Should not be a problem
            FileStream fs = File.Create("SERIALIZED_SCRIPT_" + itemID);
            BinaryFormatter b = new BinaryFormatter();
            b.Serialize(fs, GetScript(localID, itemID));
            fs.Close();
        }

        public void PutSerializedScript(uint localID, UUID itemID)
        {
            // Deserialize the script and inject it into an AppDomain

            // How to inject into an AppDomain?
        }

        #endregion

        ///// <summary>
        ///// If set to true then threads and stuff should try to make a graceful exit
        ///// </summary>
        //public bool PleaseShutdown
        //{
        //    get { return _PleaseShutdown; }
        //    set { _PleaseShutdown = value; }
        //}
        //private bool _PleaseShutdown = false;
    }
}
