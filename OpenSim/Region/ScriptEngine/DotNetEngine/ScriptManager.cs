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
* 
*/
/* Original code: Tedd Hansen */
using System;
using libsecondlife;
using OpenSim.Framework;
using OpenSim.Region.Environment.Scenes;
using OpenSim.Region.ScriptEngine.Common;
using OpenSim.Region.ScriptEngine.Common.ScriptEngineBase;

namespace OpenSim.Region.ScriptEngine.DotNetEngine
{
    public class ScriptManager : OpenSim.Region.ScriptEngine.Common.ScriptEngineBase.ScriptManager
    {
        public ScriptManager(Common.ScriptEngineBase.ScriptEngine scriptEngine)
            : base(scriptEngine)
        {
            base.m_scriptEngine = scriptEngine;

        }

        // KEEP TRACK OF SCRIPTS <int id, whatever script>
        //internal Dictionary<uint, Dictionary<LLUUID, LSL_BaseClass>> Scripts = new Dictionary<uint, Dictionary<LLUUID, LSL_BaseClass>>();
        // LOAD SCRIPT
        // UNLOAD SCRIPT
        // PROVIDE SCRIPT WITH ITS INTERFACE TO OpenSim

        private Compiler.LSL.Compiler LSLCompiler = new Compiler.LSL.Compiler();

        public override void _StartScript(uint localID, LLUUID itemID, string Script)
        {
            //IScriptHost root = host.GetRoot();
            Console.WriteLine("ScriptManager StartScript: localID: " + localID + ", itemID: " + itemID);

            // We will initialize and start the script.
            // It will be up to the script itself to hook up the correct events.
            string ScriptSource = String.Empty;

            SceneObjectPart m_host = World.GetSceneObjectPart(localID);

            try
            {
                // Compile (We assume LSL)
                ScriptSource = LSLCompiler.CompileFromLSLText(Script);

#if DEBUG
                long before;
                before = GC.GetTotalMemory(true);
#endif

                IScript CompiledScript;
                CompiledScript = m_scriptEngine.m_AppDomainManager.LoadScript(ScriptSource);

#if DEBUG
                Console.WriteLine("Script " + itemID + " occupies {0} bytes", GC.GetTotalMemory(true) - before);
#endif

                CompiledScript.Source = Script;
                // Add it to our script memstruct
                SetScript(localID, itemID, CompiledScript);

                // We need to give (untrusted) assembly a private instance of BuiltIns
                //  this private copy will contain Read-Only FullitemID so that it can bring that on to the server whenever needed.


                LSL_BuiltIn_Commands LSLB = new LSL_BuiltIn_Commands(m_scriptEngine, m_host, localID, itemID);

                // Start the script - giving it BuiltIns
                CompiledScript.Start(LSLB);

                // Fire the first start-event
                m_scriptEngine.m_EventQueueManager.AddToScriptQueue(localID, itemID, "state_entry", EventQueueManager.llDetectNull, new object[] { });
            }
            catch (Exception e)
            {
                //m_scriptEngine.Log.Error("ScriptEngine", "Error compiling script: " + e.ToString());
                try
                {
                    // DISPLAY ERROR INWORLD
                    string text = "Error compiling script:\r\n" + e.Message.ToString();
                    if (text.Length > 1500)
                        text = text.Substring(0, 1500);
                    World.SimChat(Helpers.StringToField(text), ChatTypeEnum.Say, 0, m_host.AbsolutePosition,
                                  m_host.Name, m_host.UUID);
                }
                catch (Exception e2)
                {
                    m_scriptEngine.Log.Error("ScriptEngine", "Error displaying error in-world: " + e2.ToString());
                    m_scriptEngine.Log.Error("ScriptEngine",
                                             "Errormessage: Error compiling script:\r\n" + e.Message.ToString());
                }
            }
        }

        public override void _StopScript(uint localID, LLUUID itemID)
        {
            // Stop script
            Console.WriteLine("Stop script localID: " + localID + " LLUID: " + itemID.ToString());


            // Stop long command on script
            m_scriptEngine.m_ASYNCLSLCommandManager.RemoveScript(localID, itemID);

            IScript LSLBC = GetScript(localID, itemID);
            if (LSLBC == null)
                return;

            // TEMP: First serialize it
            //GetSerializedScript(localID, itemID);


            try
            {
                // Get AppDomain
                AppDomain ad = LSLBC.Exec.GetAppDomain();
                // Tell script not to accept new requests
                GetScript(localID, itemID).Exec.StopScript();
                // Remove from internal structure
                RemoveScript(localID, itemID);
                // Tell AppDomain that we have stopped script
                m_scriptEngine.m_AppDomainManager.StopScript(ad);
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception stopping script localID: " + localID + " LLUID: " + itemID.ToString() +
                                  ": " + e.ToString());
            }
        }

    }
}