using System;
using System.Collections.Generic;
using System.Text;
using System.Reflection;

namespace OpenSim.Region.ScriptEngine.Common
{
    public class Executor: MarshalByRefObject
    {
        /* TODO:
         * 
         * Needs to be common for all AppDomains - share memory too?
         * Needs to have an instance in each AppDomain, and some way of referring it.
         * Need to know what AppDomain a script is in so we know where to find our instance.
         * 
         */

        private IScript m_Script;

        public Executor(IScript Script)
        {
            m_Script = Script;
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

            Type type = m_Script.GetType();

            Console.WriteLine("ScriptEngine Executor.ExecuteEvent: \"" + m_Script.State() + "_event_" + FunctionName + "\"");

            try
            {
                type.InvokeMember(m_Script.State() + "_event_" + FunctionName, BindingFlags.InvokeMethod, null, m_Script, args);
            }
            catch (Exception e)
            {
                // TODO: Send to correct place
                Console.WriteLine("ScriptEngine Exception attempting to executing script function: " + e.ToString());
            }


        }


    }
}
