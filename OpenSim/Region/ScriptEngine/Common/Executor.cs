using System;
using System.Collections.Generic;
using System.Text;
using System.Reflection;

namespace OpenSim.Region.ScriptEngine.Common
{
    public class Executor : MarshalByRefObject
    {
        /* TODO:
         * 
         * Needs to be common for all AppDomains - share memory too?
         * Needs to have an instance in each AppDomain, and some way of referring it.
         * Need to know what AppDomain a script is in so we know where to find our instance.
         * 
         */

        private IScript m_Script;
        private Dictionary<string, MethodInfo> Events = new Dictionary<string, MethodInfo>();
        private bool m_Running = true;


        public Executor(IScript Script)
        {
            m_Script = Script;

        }

        public void StopScript()
        {
            m_Running = false;
        }
        public AppDomain GetAppDomain()
        {
            return AppDomain.CurrentDomain;
        }

        public void ExecuteEvent(string FunctionName, object[] args)
        {
            // IMPORTANT: Types and MemberInfo-derived objects require a LOT of memory.
            // Instead use RuntimeTypeHandle, RuntimeFieldHandle and RunTimeHandle (IntPtr) instead!

            //foreach (MemberInfo mi in this.GetType().GetMembers())
            //{
            //if (mi.ToString().ToLower().Contains("default"))
            //{
            //    Console.WriteLine("Member found: " + mi.ToString());
            //}
            //}

            if (m_Running == false)
            {
                // Script is inactive, do not execute!
                return;
            }

            string EventName = m_Script.State() + "_event_" + FunctionName;

            //type.InvokeMember(EventName, BindingFlags.InvokeMethod, null, m_Script, args);

            Console.WriteLine("ScriptEngine Executor.ExecuteEvent: \"" + EventName + "\"");

            if (Events.ContainsKey(EventName) == false)
            {
                // Not found, create
                Type type = m_Script.GetType();
                try
                {
                    MethodInfo mi = type.GetMethod(EventName);
                    Events.Add(EventName, mi);
                }
                catch (Exception e)
                {
                    // Event name not found, cache it as not found
                    Events.Add(EventName, null);
                }
            }

            // Get event
            MethodInfo ev = null;
            Events.TryGetValue(EventName, out ev);

            if (ev == null) // No event by that name!
                return;

            // Found
            try
            {
                // Invoke it
                ev.Invoke(m_Script, args);

            }
            catch (Exception e)
            {
                // TODO: Send to correct place
                Console.WriteLine("ScriptEngine Exception attempting to executing script function: " + e.ToString());
            }
        }

    }

}
