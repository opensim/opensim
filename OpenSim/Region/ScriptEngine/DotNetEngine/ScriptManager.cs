/*
* Copyright (c) Contributors, http://www.openmetaverse.org/
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
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS AND ANY
* EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
* WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
* DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
* DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
* (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
* LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
* ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
* (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
* SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
* 
*/
/* Original code: Tedd Hansen */
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Reflection;
using System.Runtime.Remoting;
using OpenSim.Region.Environment.Scenes;
using OpenSim.Region.Environment.Scenes.Scripting;
using OpenSim.Region.ScriptEngine.DotNetEngine.Compiler;
using OpenSim.Region.ScriptEngine.DotNetEngine.Compiler.LSL;
using OpenSim.Region.ScriptEngine.Common;
using libsecondlife;

namespace OpenSim.Region.ScriptEngine.DotNetEngine
{
    /// <summary>
    /// Loads scripts
    /// Compiles them if necessary
    /// Execute functions for EventQueueManager (Sends them to script on other AppDomain for execution)
    /// </summary>
    [Serializable]
    public class ScriptManager
    {

        private Thread ScriptLoadUnloadThread;
        private int ScriptLoadUnloadThread_IdleSleepms = 100;
        private Queue<LoadStruct> LoadQueue = new Queue<LoadStruct>();
        private Queue<UnloadStruct> UnloadQueue = new Queue<UnloadStruct>();
        private struct LoadStruct
        {
            public uint localID;
            public LLUUID itemID;
            public string Script;
        }
        private struct UnloadStruct
        {
            public uint localID;
            public LLUUID itemID;
        }

        private ScriptEngine m_scriptEngine;
        public ScriptManager(ScriptEngine scriptEngine)
        {
            m_scriptEngine = scriptEngine;
            AppDomain.CurrentDomain.AssemblyResolve += new ResolveEventHandler(CurrentDomain_AssemblyResolve);
            ScriptLoadUnloadThread = new Thread(ScriptLoadUnloadThreadLoop);
            ScriptLoadUnloadThread.Name = "ScriptLoadUnloadThread";
            ScriptLoadUnloadThread.IsBackground = true;
            ScriptLoadUnloadThread.Priority = ThreadPriority.BelowNormal;
            ScriptLoadUnloadThread.Start();

        }
        ~ScriptManager ()
        {
            // Abort load/unload thread
            try
            {
                if (ScriptLoadUnloadThread != null)
                {
                    if (ScriptLoadUnloadThread.IsAlive == true)
                    {
                        ScriptLoadUnloadThread.Abort();
                        ScriptLoadUnloadThread.Join();
                    }
                }
            }
            catch
            {
            }
        }
        private void ScriptLoadUnloadThreadLoop()
        {
            try
            {
                while (true)
                {
                    if (LoadQueue.Count == 0 && UnloadQueue.Count == 0)
                        Thread.Sleep(ScriptLoadUnloadThread_IdleSleepms);

                    if (LoadQueue.Count > 0)
                    {
                        LoadStruct item = LoadQueue.Dequeue();
                        _StartScript(item.localID, item.itemID, item.Script);
                    }

                    if (UnloadQueue.Count > 0)
                    {
                        UnloadStruct item = UnloadQueue.Dequeue();
                        _StopScript(item.localID, item.itemID);
                    }
                    
                    

                }
            }
            catch (ThreadAbortException tae)
            {
                // Expected
            }

        }

        private static Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {

            //Console.WriteLine("ScriptManager.CurrentDomain_AssemblyResolve: " + args.Name);
            return Assembly.GetExecutingAssembly().FullName == args.Name ? Assembly.GetExecutingAssembly() : null;

        }


        // Object<string, Script<string, script>>
        // IMPORTANT: Types and MemberInfo-derived objects require a LOT of memory.
        // Instead use RuntimeTypeHandle, RuntimeFieldHandle and RunTimeHandle (IntPtr) instead!
        internal Dictionary<uint, Dictionary<LLUUID, LSL_BaseClass>> Scripts = new Dictionary<uint, Dictionary<LLUUID, LSL_BaseClass>>();
        public Scene World
        {
            get
            {
                return m_scriptEngine.World;
            }
        }


        internal Dictionary<LLUUID, LSL_BaseClass>.KeyCollection GetScriptKeys(uint localID)
        {
            if (Scripts.ContainsKey(localID) == false)
                return null;

            Dictionary<LLUUID, LSL_BaseClass> Obj;
            Scripts.TryGetValue(localID, out Obj);

            return Obj.Keys;

        }

        internal LSL_BaseClass GetScript(uint localID, LLUUID itemID)
        {
            if (Scripts.ContainsKey(localID) == false)
                return null;

            Dictionary<LLUUID, LSL_BaseClass> Obj;
            Scripts.TryGetValue(localID, out Obj);
            if (Obj.ContainsKey(itemID) == false)
                return null;

            // Get script
            LSL_BaseClass Script;
            Obj.TryGetValue(itemID, out Script);

            return Script;

        }
        internal void SetScript(uint localID, LLUUID itemID, LSL_BaseClass Script)
        {
            // Create object if it doesn't exist
            if (Scripts.ContainsKey(localID) == false)
            {
                Scripts.Add(localID, new Dictionary<LLUUID, LSL_BaseClass>());
            }

            // Delete script if it exists
            Dictionary<LLUUID, LSL_BaseClass> Obj;
            Scripts.TryGetValue(localID, out Obj);
            if (Obj.ContainsKey(itemID) == true)
                Obj.Remove(itemID);

            // Add to object
            Obj.Add(itemID, Script);

        }
        internal void RemoveScript(uint localID, LLUUID itemID)
        {
            // Don't have that object?
            if (Scripts.ContainsKey(localID) == false)
                return;

            // Delete script if it exists
            Dictionary<LLUUID, LSL_BaseClass> Obj;
            Scripts.TryGetValue(localID, out Obj);
            if (Obj.ContainsKey(itemID) == true)
                Obj.Remove(itemID);

        }
        /// <summary>
        /// Fetches, loads and hooks up a script to an objects events
        /// </summary>
        /// <param name="itemID"></param>
        /// <param name="localID"></param>
        public void StartScript(uint localID, LLUUID itemID, string Script)
        {
            LoadStruct ls = new LoadStruct();
            ls.localID = localID;
            ls.itemID = itemID;
            ls.Script = Script;
            LoadQueue.Enqueue(ls);
        }
        /// <summary>
        /// Disables and unloads a script
        /// </summary>
        /// <param name="localID"></param>
        /// <param name="itemID"></param>
        public void StopScript(uint localID, LLUUID itemID)
        {
            UnloadStruct ls = new UnloadStruct();
            ls.localID = localID;
            ls.itemID = itemID;
            UnloadQueue.Enqueue(ls);
        }

        private void _StartScript(uint localID, LLUUID itemID, string Script)
        {
            //IScriptHost root = host.GetRoot();
            Console.WriteLine("ScriptManager StartScript: localID: " + localID + ", itemID: " + itemID);

            // We will initialize and start the script.
            // It will be up to the script itself to hook up the correct events.
            string FileName = "";

            IScriptHost m_host = World.GetSceneObjectPart(localID);

            try
            {


                

                // Create a new instance of the compiler (currently we don't want reuse)
                OpenSim.Region.ScriptEngine.DotNetEngine.Compiler.LSL.Compiler LSLCompiler = new OpenSim.Region.ScriptEngine.DotNetEngine.Compiler.LSL.Compiler();
                // Compile (We assume LSL)
                FileName = LSLCompiler.CompileFromLSLText(Script);
                Console.WriteLine("Compilation of " + FileName + " done");
                // * Insert yield into code
                FileName = ProcessYield(FileName);


#if DEBUG
                long before;
                before = GC.GetTotalMemory(false);
#endif
                LSL_BaseClass CompiledScript;
                    CompiledScript = m_scriptEngine.myAppDomainManager.LoadScript(FileName);
#if DEBUG
                Console.WriteLine("Script " + itemID + " occupies {0} bytes", GC.GetTotalMemory(false) - before);
#endif

                // Add it to our script memstruct
                SetScript(localID, itemID, CompiledScript);

                // We need to give (untrusted) assembly a private instance of BuiltIns
                //  this private copy will contain Read-Only FullitemID so that it can bring that on to the server whenever needed.


                LSL_BuiltIn_Commands LSLB = new LSL_BuiltIn_Commands(m_scriptEngine, m_host, localID, itemID);

                // Start the script - giving it BuiltIns
                CompiledScript.Start(LSLB);

                // Fire the first start-event
                m_scriptEngine.myEventQueueManager.AddToObjectQueue(localID, "state_entry", new object[] { });


            }
            catch (Exception e)
            {
                m_scriptEngine.Log.Error("ScriptEngine", "Error compiling script: " + e.ToString());
                try
                {
                    // DISPLAY ERROR INWORLD
                    string text = "Error compiling script:\r\n" + e.Message.ToString();
                    if (text.Length > 1500)
                        text = text.Substring(0, 1500);
                    World.SimChat(Helpers.StringToField(text), 1, m_host.AbsolutePosition, m_host.Name, m_host.UUID);
                }
                catch (Exception e2)
                {
                    m_scriptEngine.Log.Error("ScriptEngine", "Error displaying error in-world: " + e2.ToString());
                }
            }
            


        }

        private void _StopScript(uint localID, LLUUID itemID)
        {
            // Stop script
            Console.WriteLine("Stop script localID: " + localID + " LLUID: " + itemID.ToString());

            // Stop long command on script
            m_scriptEngine.myLSLLongCmdHandler.RemoveScript(localID, itemID);

            LSL_BaseClass LSLBC = GetScript(localID, itemID);
            if (LSLBC == null)
                return;

            try
            {
                // Get AppDomain
                AppDomain ad = LSLBC.Exec.GetAppDomain();
                // Tell script not to accept new requests
                GetScript(localID, itemID).Exec.StopScript();
                // Remove from internal structure
                RemoveScript(localID, itemID);
                // Tell AppDomain that we have stopped script
                m_scriptEngine.myAppDomainManager.StopScript(ad);
            }
            catch(Exception e)
            {
                Console.WriteLine("Exception stopping script localID: " + localID + " LLUID: " + itemID.ToString() + ": " + e.ToString());
            }
        }
            private string ProcessYield(string FileName)
        {
            // TODO: Create a new assembly and copy old but insert Yield Code
            //return TempDotNetMicroThreadingCodeInjector.TestFix(FileName);
            return FileName;
        }



        /// <summary>
        /// Execute a LL-event-function in Script
        /// </summary>
        /// <param name="localID">Object the script is located in</param>
        /// <param name="itemID">Script ID</param>
        /// <param name="FunctionName">Name of function</param>
        /// <param name="args">Arguments to pass to function</param>
        internal void ExecuteEvent(uint localID, LLUUID itemID, string FunctionName, object[] args)
        {

            // Execute a function in the script
            //m_scriptEngine.Log.Verbose("ScriptEngine", "Executing Function localID: " + localID + ", itemID: " + itemID + ", FunctionName: " + FunctionName);
            LSL_BaseClass Script = m_scriptEngine.myScriptManager.GetScript(localID, itemID);

            // Must be done in correct AppDomain, so leaving it up to the script itself
            Script.Exec.ExecuteEvent(FunctionName, args);

        }

    }
}
