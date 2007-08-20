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

        private ScriptEngine m_scriptEngine;
        public ScriptManager(ScriptEngine scriptEngine)
        {
            m_scriptEngine = scriptEngine;
            m_scriptEngine.Log.Verbose("ScriptEngine", "ScriptManager Start");
            AppDomain.CurrentDomain.AssemblyResolve += new ResolveEventHandler(CurrentDomain_AssemblyResolve);
        }

        private static Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {

            //Console.WriteLine("ScriptManager.CurrentDomain_AssemblyResolve: " + args.Name);
            return Assembly.GetExecutingAssembly().FullName == args.Name ? Assembly.GetExecutingAssembly() : null;

        }


        // Object<string, Script<string, script>>
        // IMPORTANT: Types and MemberInfo-derived objects require a LOT of memory.
        // Instead use RuntimeTypeHandle, RuntimeFieldHandle and RunTimeHandle (IntPtr) instead!
        internal Dictionary<IScriptHost, Dictionary<string, LSL_BaseClass>> Scripts = new Dictionary<IScriptHost, Dictionary<string, LSL_BaseClass>>();
        public Scene World
        {
            get
            {
                return m_scriptEngine.World;
            }
        }


        internal Dictionary<string, LSL_BaseClass>.KeyCollection GetScriptKeys(IScriptHost ObjectID)
        {
            if (Scripts.ContainsKey(ObjectID) == false)
                return null;

            Dictionary<string, LSL_BaseClass> Obj;
            Scripts.TryGetValue(ObjectID, out Obj);

            return Obj.Keys;

        }

        internal LSL_BaseClass GetScript(IScriptHost ObjectID, string ScriptID)
        {
            if (Scripts.ContainsKey(ObjectID) == false)
                return null;

            Dictionary<string, LSL_BaseClass> Obj;
            Scripts.TryGetValue(ObjectID, out Obj);
            if (Obj.ContainsKey(ScriptID) == false)
                return null;

            // Get script
            LSL_BaseClass Script;
            Obj.TryGetValue(ScriptID, out Script);

            return Script;

        }
        internal void SetScript(IScriptHost ObjectID, string ScriptID, LSL_BaseClass Script)
        {
            // Create object if it doesn't exist
            if (Scripts.ContainsKey(ObjectID) == false)
            {
                Scripts.Add(ObjectID, new Dictionary<string, LSL_BaseClass>());
            }

            // Delete script if it exists
            Dictionary<string, LSL_BaseClass> Obj;
            Scripts.TryGetValue(ObjectID, out Obj);
            if (Obj.ContainsKey(ScriptID) == true)
                Obj.Remove(ScriptID);

            // Add to object
            Obj.Add(ScriptID, Script);

        }
        internal void RemoveScript(IScriptHost ObjectID, string ScriptID)
        {
            // Don't have that object?
            if (Scripts.ContainsKey(ObjectID) == false)
                return;

            // Delete script if it exists
            Dictionary<string, LSL_BaseClass> Obj;
            Scripts.TryGetValue(ObjectID, out Obj);
            if (Obj.ContainsKey(ScriptID) == true)
                Obj.Remove(ScriptID);

        }
        /// <summary>
        /// Fetches, loads and hooks up a script to an objects events
        /// </summary>
        /// <param name="ScriptID"></param>
        /// <param name="ObjectID"></param>
        public void StartScript(string ScriptID, IScriptHost ObjectID)
        {
            //IScriptHost root = host.GetRoot();
            m_scriptEngine.Log.Verbose("ScriptEngine", "ScriptManager StartScript: ScriptID: " + ScriptID + ", ObjectID: " + ObjectID);

            // We will initialize and start the script.
            // It will be up to the script itself to hook up the correct events.
            string FileName = "";

            try
            {


                // * Fetch script from server
                // DEBUG - ScriptID is an actual filename during debug
                //  (therefore we can also check type by looking at extension)
                FileName = ScriptID;

                // * Does script need compile? Send it to LSL compiler first. (TODO: Use (and clean) compiler cache)
                //myScriptEngine.m_logger.Verbose("ScriptEngine", "ScriptManager Script extension: " + System.IO.Path.GetExtension(FileName).ToLower());
                switch (System.IO.Path.GetExtension(FileName).ToLower())
                {
                    case ".txt":
                    case ".lsl":
                    case ".cs":
                        m_scriptEngine.Log.Verbose("ScriptEngine", "ScriptManager Script is CS/LSL, compiling to .Net Assembly");
                        // Create a new instance of the compiler (currently we don't want reuse)
                        OpenSim.Region.ScriptEngine.DotNetEngine.Compiler.LSL.Compiler LSLCompiler = new OpenSim.Region.ScriptEngine.DotNetEngine.Compiler.LSL.Compiler();
                        // Compile
                        FileName = LSLCompiler.Compile(FileName);
                        break;
                    default:
                        throw new Exception("Unknown script type.");
                }



                m_scriptEngine.Log.Verbose("ScriptEngine", "Compilation done");
                // * Insert yield into code
                FileName = ProcessYield(FileName);


                //OpenSim.Region.ScriptEngine.DotNetEngine.Compiler.LSO.LSL_BaseClass Script = LoadAndInitAssembly(FreeAppDomain, FileName);
                
                //OpenSim.Region.ScriptEngine.DotNetEngine.Compiler.LSL.LSL_BaseClass Script = LoadAndInitAssembly(FreeAppDomain, FileName, ObjectID);
                long before;
                before = GC.GetTotalMemory(true);
                LSL_BaseClass Script = m_scriptEngine.myAppDomainManager.LoadScript(FileName);
                Console.WriteLine("Script occupies {0} bytes", GC.GetTotalMemory(true) - before);
                before = GC.GetTotalMemory(true);
                Script = m_scriptEngine.myAppDomainManager.LoadScript(FileName);
                Console.WriteLine("Script occupies {0} bytes", GC.GetTotalMemory(true) - before);
                //before = GC.GetTotalMemory(true);
                //Script = m_scriptEngine.myAppDomainManager.LoadScript(FileName);
                //Console.WriteLine("Script occupies {0} bytes", GC.GetTotalMemory(true) - before);
                

                // Add it to our temporary active script keeper
                //Scripts.Add(FullScriptID, Script);
                SetScript(ObjectID, ScriptID, Script);
                // We need to give (untrusted) assembly a private instance of BuiltIns
                //  this private copy will contain Read-Only FullScriptID so that it can bring that on to the server whenever needed.
                LSL_BuiltIn_Commands LSLB = new LSL_BuiltIn_Commands(this, ObjectID);

                // Start the script - giving it BuiltIns
                Script.Start(LSLB);

            }
            catch (Exception e)
            {
                m_scriptEngine.Log.Error("ScriptEngine", "Exception loading script \"" + FileName + "\": " + e.ToString());
            }


        }
        public void StopScript(string ScriptID, IScriptHost ObjectID)
        {
            // Stop script

            // Get AppDomain
            AppDomain ad = GetScript(ObjectID, ScriptID).Exec.GetAppDomain();
            // Tell script not to accept new requests
            GetScript(ObjectID, ScriptID).Exec.StopScript();
            // Remove from internal structure
            RemoveScript(ObjectID, ScriptID);
            // Tell AppDomain that we have stopped script
            m_scriptEngine.myAppDomainManager.StopScript(ad);
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
        /// <param name="ObjectID">Object the script is located in</param>
        /// <param name="ScriptID">Script ID</param>
        /// <param name="FunctionName">Name of function</param>
        /// <param name="args">Arguments to pass to function</param>
        internal void ExecuteEvent(IScriptHost ObjectID, string ScriptID, string FunctionName, object[] args)
        {

            // Execute a function in the script
            m_scriptEngine.Log.Verbose("ScriptEngine", "Executing Function ObjectID: " + ObjectID + ", ScriptID: " + ScriptID + ", FunctionName: " + FunctionName);
            LSL_BaseClass Script = m_scriptEngine.myScriptManager.GetScript(ObjectID, ScriptID);

            // Must be done in correct AppDomain, so leaving it up to the script itself
            Script.Exec.ExecuteEvent(FunctionName, args);

        }

        public string RegionName
        {
            get
            {
                return World.RegionInfo.RegionName;
            }
        }
    }
}
