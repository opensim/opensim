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
using System.Diagnostics; //for [DebuggerNonUserCode]
using System.Reflection;
using System.Runtime.Remoting.Lifetime;
using OpenSim.Region.ScriptEngine.Shared;
using OpenSim.Region.ScriptEngine.Shared.ScriptBase;
using log4net;

namespace OpenSim.Region.ScriptEngine.Shared.ScriptBase
{
    public class Executor
    {
        // private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// Contains the script to execute functions in.
        /// </summary>
        protected IScript m_Script;

        protected static readonly Dictionary<string, scriptEvents> m_eventFlagsMap = new Dictionary<string, scriptEvents>()
        {
            {"attach", scriptEvents.attach},
            {"at_rot_target", scriptEvents.at_rot_target},
            {"at_target", scriptEvents.at_target},
            // {"changed",(long)scriptEvents.changed},
            {"collision", scriptEvents.collision},
            {"collision_end", scriptEvents.collision_end},
            {"collision_start", scriptEvents.collision_start},
            {"control", scriptEvents.control},
            {"dataserver", scriptEvents.dataserver},
            {"email", scriptEvents.email},
            {"http_response", scriptEvents.http_response},
            {"land_collision", scriptEvents.land_collision},
            {"land_collision_end", scriptEvents.land_collision_end},
            {"land_collision_start", scriptEvents.land_collision_start},
            //{"link_message",scriptEvents.link_message},
            {"listen", scriptEvents.listen},
            {"money", scriptEvents.money},
            {"moving_end", scriptEvents.moving_end},
            {"moving_start", scriptEvents.moving_start},
            {"not_at_rot_target", scriptEvents.not_at_rot_target},
            {"not_at_target", scriptEvents.not_at_target},
            //{"no_sensor",(long)scriptEvents.no_sensor},
            //{"on_rez",(long)scriptEvents.on_rez},
            {"remote_data", scriptEvents.remote_data},
            {"run_time_permissions", scriptEvents.run_time_permissions},
            //{"sensor",(long)scriptEvents.sensor},
            {"state_entry", scriptEvents.state_entry},
            {"state_exit", scriptEvents.state_exit},
            {"timer", scriptEvents.timer},
            {"touch", scriptEvents.touch},
            {"touch_end", scriptEvents.touch_end},
            {"touch_start", scriptEvents.touch_start},
            {"transaction_result", scriptEvents.transaction_result},
            {"object_rez", scriptEvents.object_rez}
        };

        [Flags]
        public enum scriptEvents : int
        {
            None                    = 0,
            attach                  = 1,
            state_exit              = 1 << 1,
            timer                   = 1 << 2,
            touch                   = 1 << 3,
            collision               = 1 << 4,
            collision_end           = 1 << 5,
            collision_start         = 1 << 6,
            control                 = 1 << 7,
            dataserver              = 1 << 8,
            email                   = 1 << 9,
            http_response           = 1 << 10,
            land_collision          = 1 << 11,
            land_collision_end      = 1 << 12,
            land_collision_start    = 1 << 13,
            at_target               = 1 << 14,
            listen                  = 1 << 15,
            money                   = 1 << 16,
            moving_end              = 1 << 17,
            moving_start            = 1 << 18,
            not_at_rot_target       = 1 << 19,
            not_at_target           = 1 << 20,
            touch_start             = 1 << 21,
            object_rez              = 1 << 22,
            remote_data             = 1 << 23,
            at_rot_target           = 1 << 24,
            transaction_result      = 1 << 25,
            //
            //
            run_time_permissions    = 1 << 28,
            touch_end               = 1 << 29,
            state_entry             = 1 << 30,
        }

        // Cache functions by keeping a reference to them in a dictionary
        private Dictionary<string, MethodInfo> Events = new Dictionary<string, MethodInfo>();
        private Dictionary<string, scriptEvents> m_stateEvents = new Dictionary<string, scriptEvents>();

        public Executor(IScript script)
        {
            m_Script = script;
        }

        public scriptEvents GetStateEventFlags(string state)
        {
            //m_log.Debug("Get event flags for " + state);

            // Check to see if we've already computed the flags for this state
            scriptEvents eventFlags = scriptEvents.None;
            if (m_stateEvents.ContainsKey(state))
            {
                m_stateEvents.TryGetValue(state, out eventFlags);
                return eventFlags;
            }

            Type type=m_Script.GetType();

            // Fill in the events for this state, cache the results in the map
            foreach (KeyValuePair<string, scriptEvents> kvp in m_eventFlagsMap)
            {
                string evname = state + "_event_" + kvp.Key;
                //m_log.Debug("Trying event "+evname);
                try
                {
                    MethodInfo mi = type.GetMethod(evname);
                    if (mi != null)
                    {
                        //m_log.Debug("Found handler for " + kvp.Key);
                        eventFlags |= kvp.Value;
                    }
                }
                catch(Exception)
                {
                    //m_log.Debug("Exeption in GetMethod:\n"+e.ToString());
                }
            }

            // Save the flags we just computed and return the result
            if (eventFlags != 0)
                m_stateEvents.Add(state, eventFlags);

            //m_log.Debug("Returning {0:x}", eventFlags);
            return (eventFlags);
        }

        [DebuggerNonUserCode]
        public void ExecuteEvent(string state, string FunctionName, object[] args)
        {
            // IMPORTANT: Types and MemberInfo-derived objects require a LOT of memory.
            // Instead use RuntimeTypeHandle, RuntimeFieldHandle and RunTimeHandle (IntPtr) instead!

            string EventName = state + "_event_" + FunctionName;

//#if DEBUG
            //m_log.Debug("ScriptEngine: Script event function name: " + EventName);
//#endif

            if (Events.ContainsKey(EventName) == false)
            {
                // Not found, create
                Type type = m_Script.GetType();
                try
                {
                    MethodInfo mi = type.GetMethod(EventName);
                    Events.Add(EventName, mi);
                }
                catch
                {
                    // m_log.Error("Event "+EventName+" not found.");
                    // Event name not found, cache it as not found
                    Events.Add(EventName, null);
                }
            }

            // Get event
            MethodInfo ev = null;
            Events.TryGetValue(EventName, out ev);

            if (ev == null) // No event by that name!
            {
                //m_log.Debug("ScriptEngine Can not find any event named: \String.Empty + EventName + "\String.Empty);
                return;
            }

//cfk 2-7-08 dont need this right now and the default Linux build has DEBUG defined
#if DEBUG
            //m_log.Debug("ScriptEngine: Executing function name: " + EventName);
#endif
            // Found
            try
            {
                ev.Invoke(m_Script, args);
            }
            catch (TargetInvocationException tie)
            {
                // Grab the inner exception and rethrow it, unless the inner
                // exception is an EventAbortException as this indicates event
                // invocation termination due to a state change.
                // DO NOT THROW JUST THE INNER EXCEPTION!
                // FriendlyErrors depends on getting the whole exception!
                //
                if (!(tie.InnerException is EventAbortException))
                {
                    throw;
                }
            }
        }
    }
}
