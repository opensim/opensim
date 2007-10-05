using System;
using System.Collections.Generic;
using System.Text;
using System.Reflection;
using System.Runtime.Remoting.Lifetime;

namespace OpenSim.Grid.ScriptEngine.Common
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
            ILease lease = (ILease)base.InitializeLifetimeService();

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

                //type.InvokeMember(EventName, BindingFlags.InvokeMethod, null, m_Script, args);

                //Console.WriteLine("ScriptEngine Executor.ExecuteEvent: \"" + EventName + "\"");

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
                    //Console.WriteLine("ScriptEngine Can not find any event named: \"" + EventName + "\"");
                    return;
                }

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
