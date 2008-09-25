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
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Remoting.Lifetime;
using System.Text;
using System.Threading;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.Packets;
using OpenSim.Framework;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Region.Environment;
using OpenSim.Region.Interfaces;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Modules.Avatar.Currency.SampleMoney;
using OpenSim.Region.Environment.Modules.World.Land;
using OpenSim.Region.Environment.Scenes;
using OpenSim.Region.Physics.Manager;
using OpenSim.Region.ScriptEngine.Common.ScriptEngineBase;
using OpenSim.Region.ScriptEngine.Shared;
using OpenSim.Region.ScriptEngine.Shared.Api;

using LSL_Float = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLFloat;
using LSL_Integer = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLInteger;
using LSL_Key = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLString;
using LSL_List = OpenSim.Region.ScriptEngine.Shared.LSL_Types.list;
using LSL_Rotation = OpenSim.Region.ScriptEngine.Shared.LSL_Types.Quaternion;
using LSL_String = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLString;
using LSL_Vector = OpenSim.Region.ScriptEngine.Shared.LSL_Types.Vector3;

namespace OpenSim.Region.ScriptEngine.Common
{
    /// <summary>
    /// Contains all LSL ll-functions. This class will be in Default AppDomain.
    /// </summary>
    public class LSL_BuiltIn_Commands : LSL_Api_Base, LSL_BuiltIn_Commands_Interface
    {
//        private static readonly log4net.ILog m_log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        internal ScriptEngineBase.ScriptEngine m_ScriptEngineDirect;

        public LSL_BuiltIn_Commands(ScriptEngineBase.ScriptEngine ScriptEngine, SceneObjectPart host, uint localID, UUID itemID)
        {
            m_ScriptEngineDirect = ScriptEngine;
            m_ScriptEngine = ScriptEngine;
            m_host = host;
            m_localID = localID;
            m_itemID = itemID;

            AsyncCommands = new AsyncCommandManager(m_ScriptEngine);

            //m_log.Info(ScriptEngineName, "LSL_BaseClass.Start() called. Hosted by [" + m_host.Name + ":" + m_host.UUID + "@" + m_host.AbsolutePosition + "]");


            IConfigSource config = new IniConfigSource(Application.iniFilePath);
            if (config.Configs["LL-Functions"] == null)
                config.AddConfig("LL-Functions");

            m_ScriptDelayFactor = config.Configs["LL-Functions"].GetFloat("ScriptDelayFactor", 1.0f);
            m_ScriptDistanceFactor = config.Configs["LL-Functions"].GetFloat("ScriptDistanceLimitFactor", 1.0f);

        }

        private string m_state = "default";

        protected void ScriptSleep(int delay)
        {
            delay = (int)((float)delay * m_ScriptDelayFactor);
            if (delay == 0)
                return;
            System.Threading.Thread.Sleep(delay);
        }

        // Object never expires
        public override Object InitializeLifetimeService()
        {
            ILease lease = (ILease)base.InitializeLifetimeService();

            if (lease.CurrentState == LeaseState.Initial)
            {
                lease.InitialLeaseTime = TimeSpan.Zero;
            }
            return lease;
        }

        public string State
        {
            get { return m_state; }
            set {
                // Set it if it changed
                if (m_state != value)
                {
                    try
                    {
                        m_ScriptEngineDirect.m_EventManager.state_exit(m_localID);

                    }
                    catch (AppDomainUnloadedException)
                    {
                        Console.WriteLine("[SCRIPT]: state change called when script was unloaded.  Nothing to worry about, but noting the occurance");
                    }
                    m_state = value;
                    try
                    {
                        int eventFlags = m_ScriptEngineDirect.m_ScriptManager.GetStateEventFlags(m_localID, m_itemID);
                        m_host.SetScriptEvents(m_itemID, eventFlags);
                        m_ScriptEngineDirect.m_EventManager.state_entry(m_localID);
                    }
                    catch (AppDomainUnloadedException)
                    {
                        Console.WriteLine("[SCRIPT]: state change called when script was unloaded.  Nothing to worry about, but noting the occurance");
                    }
                }
            }
        }

        // Extension commands use this:
        public ICommander GetCommander(string name)
        {
            return World.GetCommander(name);
        }

        public LSL_Integer llGetScriptState(string name)
        {
            UUID item;
            ScriptManager sm;
            IScript script = null;

            m_host.AddScriptLPS(1);

            // These functions are supposed to be robust,
            // so get the state one step at a time.

            if ((item = ScriptByName(name)) != UUID.Zero)
            {
                if ((sm = m_ScriptEngineDirect.m_ScriptManager) != null)
                {
                    if ((script = sm.GetScript(m_localID, item)) != null)
                    {
                        return script.Exec.Running?1:0;
                    }
                }
            }

            // Required by SL

            if (script == null)
                ShoutError("llGetScriptState: script "+name+" not found");

            // If we didn't find it, then it's safe to
            // assume it is not running.

            return 0;
        }

        public void llResetOtherScript(string name)
        {
            UUID item;
            ScriptManager sm;
            IScript script = null;

            m_host.AddScriptLPS(8000);

            // These functions are supposed to be robust,
            // so get the state one step at a time.

            if ((item = ScriptByName(name)) != UUID.Zero)
                if ((sm = m_ScriptEngineDirect.m_ScriptManager) != null)
                    sm.ResetScript(m_localID, item);

            // Required by SL

            if (script == null)
                ShoutError("llResetOtherScript: script "+name+" not found");

            // If we didn't find it, then it's safe to
            // assume it is not running.
        }

        public void llResetScript()
        {
            m_host.AddScriptLPS(800);
            m_ScriptEngineDirect.m_ScriptManager.ResetScript(m_localID, m_itemID);
        }

        public void llSetScriptState(string name, int run)
        {
            UUID item;
            ScriptManager sm;
            IScript script = null;

            m_host.AddScriptLPS(1);

            // These functions are supposed to be robust,
            // so get the state one step at a time.

            if ((item = ScriptByName(name)) != UUID.Zero)
            {
                if ((sm = m_ScriptEngineDirect.m_ScriptManager) != null)
                {
                    if (sm.Scripts.ContainsKey(m_localID))
                    {
                        if ((script = sm.GetScript(m_localID, item)) != null)
                        {
                            script.Exec.Running = (run==0) ? false : true;
                        }
                    }
                }
            }

            // Required by SL

            if (script == null)
                ShoutError("llSetScriptState: script "+name+" not found");

            // If we didn't find it, then it's safe to
            // assume it is not running.
        }

        internal UUID ScriptByName(string name)
        {
            foreach (TaskInventoryItem item in m_host.TaskInventory.Values)
            {
                if (item.Type == 10 && item.Name == name)
                    return item.ItemID;
            }
            return UUID.Zero;
        }

        internal void ShoutError(string msg)
        {
            llShout(ScriptBaseClass.DEBUG_CHANNEL, msg);
        }
    }
}
