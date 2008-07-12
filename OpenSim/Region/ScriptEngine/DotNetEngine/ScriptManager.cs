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
using System.Reflection;
using log4net;
using libsecondlife;
using OpenSim.Framework;
using OpenSim.Region.Environment.Scenes;
using OpenSim.Region.ScriptEngine.Common;
using OpenSim.Region.ScriptEngine.Common.ScriptEngineBase;

namespace OpenSim.Region.ScriptEngine.DotNetEngine
{
    public class ScriptManager : Common.ScriptEngineBase.ScriptManager
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        
        public ScriptManager(Common.ScriptEngineBase.ScriptEngine scriptEngine)
            : base(scriptEngine)
        {
            base.m_scriptEngine = scriptEngine;
        }
        private Compiler.LSL.Compiler LSLCompiler;        

        public override void Initialize()
        {
            // Create our compiler
            LSLCompiler = new Compiler.LSL.Compiler(m_scriptEngine);
        }

        // KEEP TRACK OF SCRIPTS <int id, whatever script>
        //internal Dictionary<uint, Dictionary<LLUUID, LSL_BaseClass>> Scripts = new Dictionary<uint, Dictionary<LLUUID, LSL_BaseClass>>();
        // LOAD SCRIPT
        // UNLOAD SCRIPT
        // PROVIDE SCRIPT WITH ITS INTERFACE TO OpenSim


        public override void _StartScript(uint localID, LLUUID itemID, string Script, int startParam, bool postOnRez)
        {
            m_log.DebugFormat(
                "[{0}]: ScriptManager StartScript: localID: {1}, itemID: {2}", 
                m_scriptEngine.ScriptEngineName, localID, itemID);

            //IScriptHost root = host.GetRoot();

            // We will initialize and start the script.
            // It will be up to the script itself to hook up the correct events.
            string CompiledScriptFile = String.Empty;

            SceneObjectPart m_host = World.GetSceneObjectPart(localID);
            
            if (null == m_host)
            {
                m_log.ErrorFormat(
                    "[{0}]: Could not get scene object part corresponding to localID {1} to start script", 
                    m_scriptEngine.ScriptEngineName, localID);
                
                return;
            }

            // Xantor 20080525: I need assetID here to see if we already compiled this one previously
            LLUUID assetID = LLUUID.Zero;
            TaskInventoryItem taskInventoryItem = new TaskInventoryItem();
            if (m_host.TaskInventory.TryGetValue(itemID, out taskInventoryItem))
                assetID = taskInventoryItem.AssetID;

            try
            {
                // Xantor 20080525 see if we already compiled this script this session, stop incessant recompiling on
                // scriptreset, spawning of objects with embedded scripts etc.

                if (scriptList.TryGetValue(assetID, out CompiledScriptFile))
                {
                    m_log.InfoFormat("[SCRIPT]: Found existing compile of assetID {0}: {1}", assetID, CompiledScriptFile);
                }
                else
                {
                    // Compile (We assume LSL)
                    CompiledScriptFile = LSLCompiler.PerformScriptCompile(Script);

                    // Xantor 20080525 Save compiled scriptfile for later use
                    m_log.InfoFormat("[SCRIPT]: Compiled assetID {0}: {1}", assetID, CompiledScriptFile);
                    scriptList.Add(assetID, CompiledScriptFile);
                }

//#if DEBUG
                //long before;
                //before = GC.GetTotalMemory(true); // This force a garbage collect that freezes some windows plateforms
//#endif

                IScript CompiledScript;
                CompiledScript = m_scriptEngine.m_AppDomainManager.LoadScript(CompiledScriptFile);

//#if DEBUG
                //m_scriptEngine.Log.DebugFormat("[" + m_scriptEngine.ScriptEngineName + "]: Script " + itemID + " occupies {0} bytes", GC.GetTotalMemory(true) - before);
//#endif

                CompiledScript.Source = Script;
                CompiledScript.StartParam = startParam;

                // Add it to our script memstruct
                m_scriptEngine.m_ScriptManager.SetScript(localID, itemID, CompiledScript);

                // We need to give (untrusted) assembly a private instance of BuiltIns
                //  this private copy will contain Read-Only FullitemID so that it can bring that on to the server whenever needed.


                BuilIn_Commands LSLB = new BuilIn_Commands(m_scriptEngine, m_host, localID, itemID);

                // Start the script - giving it BuiltIns
                CompiledScript.Start(LSLB);

                // Fire the first start-event
                int eventFlags = m_scriptEngine.m_ScriptManager.GetStateEventFlags(localID, itemID);
                m_host.SetScriptEvents(itemID, eventFlags);
                m_scriptEngine.m_EventQueueManager.AddToScriptQueue(localID, itemID, "state_entry", EventQueueManager.llDetectNull, new object[] { });
                if (postOnRez)
                {
                    m_scriptEngine.m_EventQueueManager.AddToScriptQueue(localID, itemID, "on_rez", EventQueueManager.llDetectNull, new object[] { new LSL_Types.LSLInteger(startParam) });
                }
            }
            catch (Exception e) // LEGIT: User Scripting
            {
                //m_scriptEngine.Log.Error("[ScriptEngine]: Error compiling script: " + e.ToString());
                try
                {
                    // DISPLAY ERROR INWORLD
                    string text = "Error compiling script:\r\n" + e.Message.ToString();
                    if (text.Length > 1500)
                        text = text.Substring(0, 1500);
                    World.SimChat(Helpers.StringToField(text), ChatTypeEnum.DebugChannel, 2147483647,
                                  m_host.AbsolutePosition, m_host.Name, m_host.UUID, false);
                }
                catch (Exception e2) // LEGIT: User Scripting
                {
                    m_scriptEngine.Log.Error("[" + m_scriptEngine.ScriptEngineName + "]: Error displaying error in-world: " + e2.ToString());
                    m_scriptEngine.Log.Error("[" + m_scriptEngine.ScriptEngineName + "]: " +
                                                "Errormessage: Error compiling script:\r\n" + e.Message.ToString());
                }
            }
        }

        public override void _StopScript(uint localID, LLUUID itemID)
        {
            // Stop script
#if DEBUG
            m_scriptEngine.Log.Debug("[" + m_scriptEngine.ScriptEngineName + "]: Stop script localID: " + localID + " LLUID: " + itemID.ToString());
#endif

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
                m_scriptEngine.m_ScriptManager.GetScript(localID, itemID).Exec.StopScript();
                // Remove from internal structure
                m_scriptEngine.m_ScriptManager.RemoveScript(localID, itemID);
                // Tell AppDomain that we have stopped script
                m_scriptEngine.m_AppDomainManager.StopScript(ad);
            }
            catch (Exception e) // LEGIT: User Scripting
            {
                m_scriptEngine.Log.Error("[" + m_scriptEngine.ScriptEngineName + "]: Exception stopping script localID: " + localID + " LLUID: " + itemID.ToString() +
                                            ": " + e.ToString());
            }
        }
    }
}
