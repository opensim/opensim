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
using System.Reflection;
using OpenSim.Region.ScriptEngine.XEngine.Script;

namespace OpenSim.Region.ScriptEngine.XEngine
{
    public class Executor : ExecutorBase
    {
        // Cache functions by keeping a reference to them in a dictionary
        private Dictionary<string, MethodInfo> Events = new Dictionary<string, MethodInfo>();
        private Dictionary<string, scriptEvents> m_stateEvents = new Dictionary<string, scriptEvents>();

        public Executor(IScript script) : base(script)
        {
            initEventFlags();
        }


        protected override scriptEvents DoGetStateEventFlags()
        {
            //Console.WriteLine("Get event flags for " + m_Script.State);

            // Check to see if we've already computed the flags for this state
            scriptEvents eventFlags = scriptEvents.None;
            if (m_stateEvents.ContainsKey(m_Script.State))
            {
                m_stateEvents.TryGetValue(m_Script.State, out eventFlags);
                return eventFlags;
            }

            Type type=m_Script.GetType();

            // Fill in the events for this state, cache the results in the map
            foreach (KeyValuePair<string, scriptEvents> kvp in m_eventFlagsMap)
            {
                string evname = m_Script.State + "_event_" + kvp.Key;
                //Console.WriteLine("Trying event "+evname);
                try
                {
                    MethodInfo mi = type.GetMethod(evname);
                    if (mi != null)
                    {
                        //Console.WriteLine("Found handler for " + kvp.Key);
                        eventFlags |= kvp.Value;
                    }
                }
                catch(Exception e)
                {
                    //Console.WriteLine("Exeption in GetMethod:\n"+e.ToString());
                }
            }

            // Save the flags we just computed and return the result
            if (eventFlags != 0)
                m_stateEvents.Add(m_Script.State, eventFlags);

            //Console.WriteLine("Returning {0:x}", eventFlags);
            return (eventFlags);
        }

        protected override void DoExecuteEvent(string FunctionName, object[] args)
        {
            // IMPORTANT: Types and MemberInfo-derived objects require a LOT of memory.
            // Instead use RuntimeTypeHandle, RuntimeFieldHandle and RunTimeHandle (IntPtr) instead!

            string EventName = m_Script.State + "_event_" + FunctionName;

//#if DEBUG
//            Console.WriteLine("ScriptEngine: Script event function name: " + EventName);
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
                    Console.WriteLine("Event {0}not found", EventName);
                    // Event name not found, cache it as not found
                    Events.Add(EventName, null);
                }
            }

            // Get event
            MethodInfo ev = null;
            Events.TryGetValue(EventName, out ev);

            if (ev == null) // No event by that name!
            {
                //Console.WriteLine("ScriptEngine Can not find any event named: \String.Empty + EventName + "\String.Empty);
                return;
            }

//cfk 2-7-08 dont need this right now and the default Linux build has DEBUG defined
#if DEBUG
            //Console.WriteLine("ScriptEngine: Executing function name: " + EventName);
#endif
            // Found
            ev.Invoke(m_Script, args);
        }
    }
}
