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
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS AS IS AND ANY
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

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Remoting.Lifetime;

namespace OpenSim.Region.ScriptEngine.Common
{
    public class Executor : MarshalByRefObject
    {
        // Private instance for each script

        private IScript m_Script;
        private Dictionary<string, MethodInfo> Events = new Dictionary<string, MethodInfo>();
        private bool m_Running = true;
        //private List<IScript> Scripts = new List<IScript>();

        public Executor(IScript Script)
        {
            m_Script = Script;
        }

        // Object never expires
        public override Object InitializeLifetimeService()
        {
            //Console.WriteLine("Executor: InitializeLifetimeService()");
            //            return null;
            ILease lease = (ILease) base.InitializeLifetimeService();

            if (lease.CurrentState == LeaseState.Initial)
            {
                lease.InitialLeaseTime = TimeSpan.Zero; // TimeSpan.FromMinutes(1);
//                lease.SponsorshipTimeout = TimeSpan.FromMinutes(2);
//                lease.RenewOnCallTime = TimeSpan.FromSeconds(2);
            }
            return lease;
        }

        public AppDomain GetAppDomain()
        {
            return AppDomain.CurrentDomain;
        }

        public void ExecuteEvent(string FunctionName, object[] args)
        {
            // IMPORTANT: Types and MemberInfo-derived objects require a LOT of memory.
            // Instead use RuntimeTypeHandle, RuntimeFieldHandle and RunTimeHandle (IntPtr) instead!
            //try
            //{
            if (m_Running == false)
            {
                // Script is inactive, do not execute!
                return;
            }

            string EventName = m_Script.State() + "_event_" + FunctionName;

//cfk 2-7-08 dont need this right now and the default Linux build has DEBUG defined
///#if DEBUG
///            Console.WriteLine("ScriptEngine: Script event function name: " + EventName);
///#endif

            //type.InvokeMember(EventName, BindingFlags.InvokeMethod, null, m_Script, args);

            //Console.WriteLine("ScriptEngine Executor.ExecuteEvent: \String.Empty + EventName + "\String.Empty);

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
///#if DEBUG
///            Console.WriteLine("ScriptEngine: Executing function name: " + EventName);
///#endif
            // Found
            //try
            //{
            // Invoke it
            ev.Invoke(m_Script, args);

            //}
            //catch (Exception e)
            //{
            //    // TODO: Send to correct place
            //    Console.WriteLine("ScriptEngine Exception attempting to executing script function: " + e.ToString());
            //}


            //}
            //catch { }
        }


        public void StopScript()
        {
            m_Running = false;
        }
    }
}