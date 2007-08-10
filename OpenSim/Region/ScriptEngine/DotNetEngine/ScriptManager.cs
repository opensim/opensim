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

namespace OpenSim.Region.ScriptEngine.DotNetEngine
{
    class ScriptManager
    {

        private ScriptEngine myScriptEngine;
        public ScriptManager(ScriptEngine _ScriptEngine)
        {
            myScriptEngine = _ScriptEngine;
            Common.SendToDebug("ScriptManager Start");
        }


        // Object<string, Script<string, script>>
        internal Dictionary<string, Dictionary<string, OpenSim.Region.ScriptEngine.DotNetEngine.Compiler.LSO.LSL_BaseClass>> Scripts = new Dictionary<string, Dictionary<string, OpenSim.Region.ScriptEngine.DotNetEngine.Compiler.LSO.LSL_BaseClass>>();


        internal Dictionary<string, OpenSim.Region.ScriptEngine.DotNetEngine.Compiler.LSO.LSL_BaseClass>.KeyCollection GetScriptKeys(string ObjectID)
        {
            if (Scripts.ContainsKey(ObjectID) == false)
                return null;

            Dictionary<string, OpenSim.Region.ScriptEngine.DotNetEngine.Compiler.LSO.LSL_BaseClass> Obj;
            Scripts.TryGetValue(ObjectID, out Obj);

            return Obj.Keys;

        }

        internal OpenSim.Region.ScriptEngine.DotNetEngine.Compiler.LSO.LSL_BaseClass GetScript(string ObjectID, string ScriptID)
        {
            if (Scripts.ContainsKey(ObjectID) == false)
                return null;

            Dictionary<string, OpenSim.Region.ScriptEngine.DotNetEngine.Compiler.LSO.LSL_BaseClass> Obj;
            Scripts.TryGetValue(ObjectID, out Obj);
            if (Obj.ContainsKey(ScriptID) == false)
                return null;

            // Get script
            OpenSim.Region.ScriptEngine.DotNetEngine.Compiler.LSO.LSL_BaseClass Script;
            Obj.TryGetValue(ScriptID, out Script);

            return Script;

        }
        internal void SetScript(string ObjectID, string ScriptID, OpenSim.Region.ScriptEngine.DotNetEngine.Compiler.LSO.LSL_BaseClass Script)
        {
            // Create object if it doesn't exist
            if (Scripts.ContainsKey(ObjectID) == false)
                Scripts.Add(ObjectID, new Dictionary<string, OpenSim.Region.ScriptEngine.DotNetEngine.Compiler.LSO.LSL_BaseClass>());

            // Delete script if it exists
            Dictionary<string, OpenSim.Region.ScriptEngine.DotNetEngine.Compiler.LSO.LSL_BaseClass> Obj;
            Scripts.TryGetValue(ObjectID, out Obj);
            if (Obj.ContainsKey(ScriptID) == true)
                Obj.Remove(ScriptID);

            // Add to object
            Obj.Add(ScriptID, Script);

        }

        /// <summary>
        /// Fetches, loads and hooks up a script to an objects events
        /// </summary>
        /// <param name="ScriptID"></param>
        /// <param name="ObjectID"></param>
        public void StartScript(string ScriptID, string ObjectID)
        {
            Common.SendToDebug("ScriptManager StartScript: ScriptID: " + ScriptID + ", ObjectID: " + ObjectID);

            // We will initialize and start the script.
            // It will be up to the script itself to hook up the correct events.
            string FileName;

            // * Fetch script from server
            // DEBUG - ScriptID is an actual filename during debug
            //  (therefore we can also check type by looking at extension)
            FileName = ScriptID;

            // * Does script need compile? Send it to LSL compiler first. (TODO: Use (and clean) compiler cache)
            if (FileName.ToLower().EndsWith(".lso"))
            {
                Common.SendToDebug("ScriptManager Script is LSO, compiling to .Net Assembly");
                // Create a new instance of the compiler (currently we don't want reuse)
                OpenSim.Region.ScriptEngine.DotNetEngine.Compiler.LSO.Engine LSLCompiler = new OpenSim.Region.ScriptEngine.DotNetEngine.Compiler.LSO.Engine();
                // Compile
                FileName = LSLCompiler.Compile(FileName);
            }

            // * Insert yield into code
            FileName = ProcessYield(FileName);

            // * Find next available AppDomain to put it in
            AppDomain FreeAppDomain = GetFreeAppDomain();

            // * Load and start script
            OpenSim.Region.ScriptEngine.DotNetEngine.Compiler.LSO.LSL_BaseClass Script = LoadAndInitAssembly(FreeAppDomain, FileName);
            string FullScriptID = ScriptID + "." + ObjectID;
            // Add it to our temporary active script keeper
            //Scripts.Add(FullScriptID, Script);
            SetScript(ObjectID, ScriptID, Script);
            // We need to give (untrusted) assembly a private instance of BuiltIns
            //  this private copy will contain Read-Only FullScriptID so that it can bring that on to the server whenever needed.
            OpenSim.Region.ScriptEngine.DotNetEngine.Compiler.LSO.LSL_BuiltIn_Commands_Interface LSLB = new OpenSim.Region.ScriptEngine.DotNetEngine.Compiler.LSO.LSL_BuiltIn_Commands_TestImplementation(FullScriptID);
            // Start the script - giving it BuiltIns
            Common.SendToDebug("ScriptManager initializing script, handing over private builtin command interface");
            Script.Start(LSLB);


        }
        private string ProcessYield(string FileName)
        {
            // TODO: Create a new assembly and copy old but insert Yield Code
            return FileName;
        }

        private AppDomain GetFreeAppDomain()
        {
            // TODO: Find an available AppDomain - if none, create one and add default security
            return Thread.GetDomain();
        }

        /// <summary>
        /// Does actual loading and initialization of script Assembly
        /// </summary>
        /// <param name="FreeAppDomain">AppDomain to load script into</param>
        /// <param name="FileName">FileName of script assembly (.dll)</param>
        /// <returns></returns>
        private OpenSim.Region.ScriptEngine.DotNetEngine.Compiler.LSO.LSL_BaseClass LoadAndInitAssembly(AppDomain FreeAppDomain, string FileName)
        {
            Common.SendToDebug("ScriptManager Loading Assembly " + FileName);
            // Load .Net Assembly (.dll)
            // Initialize and return it

            // TODO: Add error handling
            // Script might not follow our rules since users can upload -anything-

            Assembly a;
            //try
            //{


            // Load to default appdomain (temporary)
            a = Assembly.LoadFrom(FileName);
            // Load to specified appdomain
            // TODO: Insert security
            //a = FreeAppDomain.Load(FileName);
            //}
            //catch (Exception e)
            //{
            //}


            foreach (Type _t in a.GetTypes())
            {
                Console.WriteLine("Type: " + _t.ToString());
            }

            Type t;
            //try
            //{
            t = a.GetType("LSL_ScriptObject", true);
            //}
            //catch (Exception e)
            //{
            //}

            return (OpenSim.Region.ScriptEngine.DotNetEngine.Compiler.LSO.LSL_BaseClass)Activator.CreateInstance(t);


        }

        internal void ExecuteFunction(string ObjectID, string ScriptID, string FunctionName, object[] args)
        {
            Common.SendToDebug("Executing Function ObjectID: " + ObjectID + ", ScriptID: " + ScriptID + ", FunctionName: " + FunctionName);
            OpenSim.Region.ScriptEngine.DotNetEngine.Compiler.LSO.LSL_BaseClass Script = myScriptEngine.myScriptManager.GetScript(ObjectID, ScriptID);

            Type type = Script.GetType();
            //object o = (object)Script;

            //System.Collections.Generic.List<string> Functions = (System.Collections.Generic.List<string>)
            //Type type = typeof(OpenSim.Region.ScriptEngine.DotNetEngine.Compiler.LSO.LSL_BaseClass);
            Common.SendToDebug("Invoke: \"" + Script.State + "_event_" + FunctionName + "\"");
            type.InvokeMember(Script.State + "_event_" + FunctionName, BindingFlags.InvokeMethod, null, Script, args);
            //System.Collections.Generic.List<string> Functions = (System.Collections.Generic.List<string>)type.InvokeMember("GetFunctions", BindingFlags.InvokeMethod, null, Script, null);


            //foreach (MemberInfo mi in type.GetMembers())
            //{
            //    Common.SendToDebug("Member found: " + mi.ToString());
            //}

        }

    }
}
