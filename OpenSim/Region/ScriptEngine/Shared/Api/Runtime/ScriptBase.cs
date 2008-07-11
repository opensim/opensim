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
using System.Runtime.Remoting.Lifetime;
using System.Threading;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using OpenSim.Region.ScriptEngine.Interfaces;
using OpenSim.Region.ScriptEngine.Shared;

namespace OpenSim.Region.ScriptEngine.Shared.ScriptBase
{
    public partial class ScriptBaseClass : MarshalByRefObject, IScript
    {
        private Dictionary<string,MethodInfo> inits = new Dictionary<string,MethodInfo>();

        //
        // Never expire this object
        //
        public override Object InitializeLifetimeService()
        {
            ILease lease = (ILease)base.InitializeLifetimeService();

            if (lease.CurrentState == LeaseState.Initial)
            {
                lease.InitialLeaseTime = TimeSpan.Zero;
            }
            return lease;
        }

        public ScriptBaseClass()
        {
            MethodInfo[] myArrayMethodInfo = GetType().GetMethods(BindingFlags.Public|BindingFlags.Instance);

            foreach (MethodInfo mi in myArrayMethodInfo)
            {
                if (mi.Name.Length > 7 && mi.Name.Substring(0, 7) == "ApiType")
                {
                    string type=mi.Name.Substring(7);
                    inits[type]=mi;
                }
            }
        }

        public string[] GetApis()
        {
            string[] apis = new string[inits.Count];
            inits.Keys.CopyTo(apis, 0);
            return apis;
        }

        public void InitApi(string api, IScriptApi data)
        {
            if (!inits.ContainsKey(api))
                return;

            MethodInfo mi = inits[api];
            
            Object[] args = new Object[1];
            args[0] = data;

            mi.Invoke(this, args);
        }

        private Dictionary<string, object> m_InitialValues =
                new Dictionary<string, object>();
        private Dictionary<string, FieldInfo> m_Fields =
                new Dictionary<string, FieldInfo>();

        public Dictionary<string, object> GetVars()
        {
            Dictionary<string, object> vars = new Dictionary<string, object>();

            if (m_Fields == null)
                return vars;

            m_Fields.Clear();

            Type t = GetType();

            FieldInfo[] fields = t.GetFields(BindingFlags.NonPublic |
                                             BindingFlags.Public |
                                             BindingFlags.Instance |
                                             BindingFlags.DeclaredOnly);

            foreach (FieldInfo field in fields)
            {
                m_Fields[field.Name]=field;

                vars[field.Name]=field.GetValue(this);
            }

            return vars;
        }

        public void SetVars(Dictionary<string, object> vars)
        {
            foreach (KeyValuePair<string, object> var in vars)
            {
                if (m_Fields.ContainsKey(var.Key))
                {
                    m_Fields[var.Key].SetValue(this, var.Value);
                }
            }
        }

        public void ResetVars()
        {
            SetVars(m_InitialValues);
        }
    }
}
