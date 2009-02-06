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
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Threading;
using log4net;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.ScriptEngine.Interfaces;
using OpenSim.Region.ScriptEngine.Shared.ScriptBase;
using OpenSim.ScriptEngine.Shared;

namespace OpenSim.ScriptEngine.Components.DotNetEngine.Scheduler
{
    public partial class ScriptManager
    {
        private Queue<LoadUnloadStructure> LUQueue = new Queue<LoadUnloadStructure>();
        private int LoadUnloadMaxQueueSize = 500;
        private Object scriptLock = new Object();
        //private Dictionary<InstanceData, DetectParams[]> detparms = new Dictionary<InstanceData, DetectParams[]>();

        // Load/Unload structure


        public void AddScript(ScriptStructure script)
        {
            lock (LUQueue)
            {
                if ((LUQueue.Count >= LoadUnloadMaxQueueSize))
                {
                    m_log.ErrorFormat("[{0}] ERROR: Load queue count is at {1} of max {2}. Ignoring load request for script LocalID: {3}, ItemID: {4}.", 
                        Name, LUQueue.Count, LoadUnloadMaxQueueSize, script.LocalID, script.ItemID);
                    return;
                }

                LoadUnloadStructure ls = new LoadUnloadStructure();
                ls.Script = script;
                ls.Action = LoadUnloadStructure.LUType.Load;
                LUQueue.Enqueue(ls);
            }

        }
        public void RemoveScript(uint localID, UUID itemID)
        {
            LoadUnloadStructure ls = new LoadUnloadStructure();

            // See if we can find script
            if (!TryGetScript(localID, itemID, ref ls.Script))
            {
                // Set manually
                ls.Script.LocalID = localID;
                ls.Script.ItemID = itemID;
            }
            ls.Script.StartParam = 0;

            ls.Action = LoadUnloadStructure.LUType.Unload;
            ls.PostOnRez = false;

            lock (LUQueue)
            {
                LUQueue.Enqueue(ls);
            }
        }

        internal bool DoScriptLoadUnload()
        {
            bool ret = false;
            //            if (!m_started)
            //                return;

            lock (LUQueue)
            {
                if (LUQueue.Count > 0)
                {
                    LoadUnloadStructure item = LUQueue.Dequeue();
                    ret = true;

                    if (item.Action == LoadUnloadStructure.LUType.Unload)
                    {
                         _StopScript(item.Script.LocalID, item.Script.ItemID);
                        RemoveScript(item.Script.LocalID, item.Script.ItemID);
                    }
                    else if (item.Action == LoadUnloadStructure.LUType.Load)
                    {
                        m_log.DebugFormat("[{0}] Loading script", Name);
                        _StartScript(item);
                    }
                }
            }
            return ret;
        }

        //public void _StartScript(uint localID, UUID itemID, string Script, int startParam, bool postOnRez)
        private void _StartScript(LoadUnloadStructure ScriptObject)
        {
            m_log.DebugFormat(
                "[{0}]: ScriptManager StartScript: localID: {1}, itemID: {2}",
                Name, ScriptObject.Script.LocalID, ScriptObject.Script.ItemID);

            // We will initialize and start the script.
            // It will be up to the script itself to hook up the correct events.

            SceneObjectPart m_host = ScriptObject.Script.RegionInfo.Scene.GetSceneObjectPart(ScriptObject.Script.LocalID);

            if (null == m_host)
            {
                m_log.ErrorFormat(
                    "[{0}]: Could not find scene object part corresponding " +
                    "to localID {1} to start script",
                    Name, ScriptObject.Script.LocalID);

                return;
            }

            //UUID assetID = UUID.Zero;
            TaskInventoryItem taskInventoryItem = new TaskInventoryItem();
            //if (m_host.TaskInventory.TryGetValue(ScriptObject.Script.ItemID, out taskInventoryItem))
            //    assetID = taskInventoryItem.AssetID;

            ScenePresence presence =
                    ScriptObject.Script.RegionInfo.Scene.GetScenePresence(taskInventoryItem.OwnerID);

            CultureInfo USCulture = new CultureInfo("en-US");
            Thread.CurrentThread.CurrentCulture = USCulture;

            try
            {
                //
                // Compile script to an assembly
                //
                //TODO: DEBUG
                BaseClassFactory.MakeBaseClass(ScriptObject.Script);

                m_log.DebugFormat("[{0}] Compiling script {1}", Name, ScriptObject.Script.Name);

                string fileName = "";
                try
                {
                    IScriptCompiler compiler =
                        ScriptObject.Script.RegionInfo.FindCompiler(ScriptObject.Script.ScriptMetaData);
                    //RegionInfoStructure currentRegionInfo = ScriptObject.Script.RegionInfo;
                    fileName = compiler.Compile(ScriptObject.Script.ScriptMetaData, 
                                                ref ScriptObject.Script.Source);
                    ScriptObject.Script.AssemblyFileName = fileName;
                }
                catch (Exception e)
                {
                    m_log.ErrorFormat("[{0}] Internal error while compiling \"{1}\": {2}", Name, ScriptObject.Script.Name, e.ToString());
                }
                m_log.DebugFormat("[{0}] Compiled \"{1}\" to assembly: \"{2}\".", Name, ScriptObject.Script.Name, fileName);

                // Add it to our script memstruct
                MemAddScript(ScriptObject.Script);

                ScriptAssemblies.IScript CompiledScript;
                CompiledScript = CurrentRegion.ScriptLoader.LoadScript(ScriptObject.Script);
                ScriptObject.Script.State = "default";
                ScriptObject.Script.ScriptObject = CompiledScript;
                ScriptObject.Script.Disabled = false;
                ScriptObject.Script.Running = true;
                //id.LineMap = LSLCompiler.LineMap();
                //id.Script = CompiledScript;
                //id.Source = item.Script.Script;
                //item.StartParam = startParam;



                // TODO: Fire the first start-event
                //int eventFlags =
                //        m_scriptEngine.m_ScriptManager.GetStateEventFlags(
                //        localID, itemID);

                //m_host.SetScriptEvents(itemID, eventFlags);
                ScriptObject.Script.RegionInfo.Executors_Execute(ScriptObject.Script,
                    new EventParams(ScriptObject.Script.LocalID, ScriptObject.Script.ItemID, "state_entry", new object[] { }, new Region.ScriptEngine.Shared.DetectParams[0])
                    );
                
                if (ScriptObject.PostOnRez)
                {
                    ScriptObject.Script.RegionInfo.Executors_Execute(ScriptObject.Script,
                        new EventParams(ScriptObject.Script.LocalID, "on_rez", new object[]
                                                      {new Region.ScriptEngine.Shared.LSL_Types.LSLInteger(ScriptObject.StartParam)
                                                        }, new Region.ScriptEngine.Shared.DetectParams[0]));
                }
            }
            catch (Exception e) // LEGIT: User Scripting
            {
                if (presence != null && (!ScriptObject.PostOnRez))
                    presence.ControllingClient.SendAgentAlertMessage(
                            "Script saved with errors, check debug window!",
                            false);
                try
                {
                    // DISPLAY ERROR INWORLD
                    string text = "Error compiling script:\n" +
                            e.Message.ToString();
                    if (text.Length > 1100)
                        text = text.Substring(0, 1099);

                    ScriptObject.Script.RegionInfo.Scene.SimChat(Utils.StringToBytes(text),
                            ChatTypeEnum.DebugChannel, 2147483647,
                            m_host.AbsolutePosition, m_host.Name, m_host.UUID,
                            false);
                }
                catch (Exception e2) // LEGIT: User Scripting
                {
                    m_log.Error("[" +
                            Name +
                            "]: Error displaying error in-world: " +
                            e2.ToString());
                    m_log.Error("[" +
                            Name + "]: " +
                            "Errormessage: Error compiling script:\r\n" +
                            e2.Message.ToString());
                }
            }
        }

  

        public void _StopScript(uint localID, UUID itemID)
        {
            ScriptStructure ss = new ScriptStructure();
            if (!TryGetScript(localID, itemID, ref ss))
                return;

            m_log.DebugFormat("[{0}] Unloading script", Name);

            // Stop long command on script
            //AsyncCommandManager.RemoveScript(ss);

            try
            {
                // Get AppDomain
                // Tell script not to accept new requests
                ss.Running = false;
                ss.Disabled = true;
                //AppDomain ad = ss.AppDomain;

                // Remove from internal structure
                MemRemoveScript(localID, itemID);

                // TODO: Tell AppDomain that we have stopped script
                
            }
            catch (Exception e) // LEGIT: User Scripting
            {
                m_log.Error("[" +
                        Name +
                        "]: Exception stopping script localID: " +
                        localID + " LLUID: " + itemID.ToString() +
                        ": " + e.ToString());
            }
        }


    }
}
