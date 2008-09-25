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
using OpenSim;
using OpenSim.Framework;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Region.Environment;
using OpenSim.Region.Interfaces;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Modules.Avatar.Currency.SampleMoney;
using OpenSim.Region.Environment.Modules.World.Land;
using OpenSim.Region.Environment.Scenes;
using OpenSim.Region.Physics.Manager;
using OpenSim.Region.ScriptEngine.Shared;
using OpenSim.Region.ScriptEngine.Shared.Api.Plugins;
using OpenSim.Region.ScriptEngine.Shared.ScriptBase;
using OpenSim.Region.ScriptEngine.Interfaces;
using OpenSim.Region.ScriptEngine.Shared.Api.Interfaces;

using LSL_Float = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLFloat;
using LSL_Integer = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLInteger;
using LSL_Key = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLString;
using LSL_List = OpenSim.Region.ScriptEngine.Shared.LSL_Types.list;
using LSL_Rotation = OpenSim.Region.ScriptEngine.Shared.LSL_Types.Quaternion;
using LSL_String = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLString;
using LSL_Vector = OpenSim.Region.ScriptEngine.Shared.LSL_Types.Vector3;

namespace OpenSim.Region.ScriptEngine.Shared.Api
{
    /// <summary>
    /// Contains all LSL ll-functions. This class will be in Default AppDomain.
    /// </summary>
    public class LSL_Api : LSL_Api_Base, ILSL_Api, IScriptApi
    {
        private IScriptEngine m_ScriptEngineDirect;

        //private static readonly log4net.ILog m_log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public void Initialize(IScriptEngine ScriptEngine, SceneObjectPart host, uint localID, UUID itemID)
        {
            m_ScriptEngine = ScriptEngine;
            m_ScriptEngineDirect = ScriptEngine;
            m_host = host;
            m_localID = localID;
            m_itemID = itemID;

            IConfigSource config = new IniConfigSource(Application.iniFilePath);
            if (config.Configs["XEngine"] == null)
                config.AddConfig("XEngine");

            m_ScriptDelayFactor = config.Configs["XEngine"].
                    GetFloat("ScriptDelayFactor", 1.0f);
            m_ScriptDistanceFactor = config.Configs["XEngine"].
                    GetFloat("ScriptDistanceLimitFactor", 1.0f);

            AsyncCommands = new AsyncCommandManager(ScriptEngine);
        }

        private DateTime m_timer = DateTime.Now;
        private bool m_waitingForScriptAnswer=false;
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

        public void state(string newState)
        {
            m_ScriptEngineDirect.SetState(m_itemID, newState);
            throw new EventAbortException();
        }

        // Extension commands use this:
        public ICommander GetCommander(string name)
        {
            return World.GetCommander(name);
        }

        public LSL_Integer llGetScriptState(string name)
        {
            UUID item;

            m_host.AddScriptLPS(1);

            if ((item = ScriptByName(name)) != UUID.Zero)
            {
                return m_ScriptEngineDirect.GetScriptState(item) ?1:0;
            }

            ShoutError("llGetScriptState: script "+name+" not found");

            // If we didn't find it, then it's safe to
            // assume it is not running.

            return 0;
        }

        public void llResetOtherScript(string name)
        {
            UUID item;

            m_host.AddScriptLPS(1);

            if ((item = ScriptByName(name)) != UUID.Zero)
                m_ScriptEngineDirect.ResetScript(item);
            else
                ShoutError("llResetOtherScript: script "+name+" not found");
        }

        public void llResetScript()
        {
            m_host.AddScriptLPS(1);
            m_ScriptEngineDirect.ApiResetScript(m_itemID);
            throw new EventAbortException();
        }

        public void llSetScriptState(string name, int run)
        {
            UUID item;

            m_host.AddScriptLPS(1);

            // These functions are supposed to be robust,
            // so get the state one step at a time.

            if ((item = ScriptByName(name)) != UUID.Zero)
            {
                m_ScriptEngineDirect.SetScriptState(item, run == 0 ? false : true);
            }
            else
            {
                ShoutError("llSetScriptState: script "+name+" not found");
            }
        }
    }
}
