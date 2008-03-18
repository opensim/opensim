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
using System.Runtime.Remoting.Lifetime;
using System.Text;

namespace OpenSim.Region.ScriptEngine.Common
{
    public abstract class ExecutorBase : MarshalByRefObject
    {
        /// <summary>
        /// Contains the script to execute functions in.
        /// </summary>
        protected IScript m_Script;
        /// <summary>
        /// If set to False events will not be executed.
        /// </summary>
        protected bool m_Running = true;
        /// <summary>
        /// True indicates that the ScriptManager has stopped 
        /// this script. This prevents a script that has been
        /// stopped as part of deactivation from being
        /// resumed by a pending llSetScriptState request.
        /// </summary>
        protected bool m_Disable = false;

        /// <summary>
        /// Indicate the scripts current running status.
        /// </summary>
        public bool Running
        {
            get { return m_Running; }
            set {
                  if(!m_Disable)
                      m_Running = value;
                }
        }

        /// <summary>
        /// Create a new instance of ExecutorBase
        /// </summary>
        /// <param name="Script"></param>
        public ExecutorBase(IScript Script)
        {
            m_Script = Script;
        }

        /// <summary>
        /// Make sure our object does not timeout when in AppDomain. (Called by ILease base class)
        /// </summary>
        /// <returns></returns>
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

        /// <summary>
        /// Get current AppDomain
        /// </summary>
        /// <returns>Current AppDomain</returns>
        public AppDomain GetAppDomain()
        {
            return AppDomain.CurrentDomain;
        }

        /// <summary>
        /// Execute a specific function/event in script.
        /// </summary>
        /// <param name="FunctionName">Name of function to execute</param>
        /// <param name="args">Arguments to pass to function</param>
        public void ExecuteEvent(string FunctionName, object[] args)
        {
            if (m_Running == false)
            {
                // Script is inactive, do not execute!
                return;
            }
            DoExecuteEvent(FunctionName, args);
        }
        protected abstract void DoExecuteEvent(string FunctionName, object[] args);

        /// <summary>
        /// Stop script from running. Event execution will be ignored.
        /// </summary>
        public void StopScript()
        {
            m_Running = false;
            m_Disable = true;
        }
    }
}
