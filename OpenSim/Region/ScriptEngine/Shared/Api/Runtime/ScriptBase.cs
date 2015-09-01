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
using System.Runtime.Remoting;
using System.Runtime.Remoting.Lifetime;
using System.Security.Permissions;
using System.Threading;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics; //for [DebuggerNonUserCode]
using OpenSim.Region.ScriptEngine.Interfaces;
using OpenSim.Region.ScriptEngine.Shared;
using OpenSim.Region.ScriptEngine.Shared.Api.Runtime;

namespace OpenSim.Region.ScriptEngine.Shared.ScriptBase
{
    public partial class ScriptBaseClass : MarshalByRefObject, IScript
    {
        private Dictionary<string, MethodInfo> inits = new Dictionary<string, MethodInfo>();
//        private ScriptSponsor m_sponser;

        public override Object InitializeLifetimeService()
        {
            ILease lease = (ILease)base.InitializeLifetimeService();
            if (lease.CurrentState == LeaseState.Initial)
            {
                // Infinite
                lease.InitialLeaseTime = TimeSpan.FromMinutes(0);
//                lease.RenewOnCallTime = TimeSpan.FromSeconds(10.0);
//                lease.SponsorshipTimeout = TimeSpan.FromMinutes(1.0);
            }
            return lease;
        }
#if DEBUG
        // For tracing GC while debugging
        public static bool GCDummy = false;
        ~ScriptBaseClass()
        {
            GCDummy = true;
        }
#endif

        public ScriptBaseClass()
        {
            m_Executor = new Executor(this);

            MethodInfo[] myArrayMethodInfo = GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance);

            foreach (MethodInfo mi in myArrayMethodInfo)
            {
                if (mi.Name.Length > 7 && mi.Name.Substring(0, 7) == "ApiType")
                {
                    string type = mi.Name.Substring(7);
                    inits[type] = mi;
                }
            }

//            m_sponser = new ScriptSponsor();
        }

        private Executor m_Executor = null;

        public int GetStateEventFlags(string state)
        {
            return (int)m_Executor.GetStateEventFlags(state);
        }

        [DebuggerNonUserCode]
        public void ExecuteEvent(string state, string FunctionName, object[] args)
        {
            m_Executor.ExecuteEvent(state, FunctionName, args);
        }

        public string[] GetApis()
        {
            string[] apis = new string[inits.Count];
            inits.Keys.CopyTo(apis, 0);
            return apis;
        }

        private Dictionary<string, object> m_InitialValues =
                new Dictionary<string, object>();
        private Dictionary<string, FieldInfo> m_Fields =
                new Dictionary<string, FieldInfo>();

        public void InitApi(string api, IScriptApi data)
        {
            if (!inits.ContainsKey(api))
                return;

            //ILease lease = (ILease)RemotingServices.GetLifetimeService(data as MarshalByRefObject);
            //RemotingServices.GetLifetimeService(data as MarshalByRefObject);
//            lease.Register(m_sponser);

            MethodInfo mi = inits[api];

            Object[] args = new Object[1];
            args[0] = data;

            mi.Invoke(this, args);

            m_InitialValues = GetVars();
        }

        public virtual void StateChange(string newState)
        {
        }

        public void Close()
        {
//            m_sponser.Close();
        }

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
                m_Fields[field.Name] = field;

                if (field.FieldType == typeof(LSL_Types.list)) // ref type, copy
                {
                    LSL_Types.list v = (LSL_Types.list)field.GetValue(this);
                    Object[] data = new Object[v.Data.Length];
                    Array.Copy(v.Data, 0, data, 0, v.Data.Length);
                    LSL_Types.list c = new LSL_Types.list();
                    c.Data = data;
                    vars[field.Name] = c;
                }
                else if (field.FieldType == typeof(LSL_Types.LSLInteger) ||
                        field.FieldType == typeof(LSL_Types.LSLString) ||
                        field.FieldType == typeof(LSL_Types.LSLFloat) ||
                        field.FieldType == typeof(Int32) ||
                        field.FieldType == typeof(Double) ||
                        field.FieldType == typeof(Single) ||
                        field.FieldType == typeof(String) ||
                        field.FieldType == typeof(Byte) ||
                        field.FieldType == typeof(short) ||
                        field.FieldType == typeof(LSL_Types.Vector3) ||
                        field.FieldType == typeof(LSL_Types.Quaternion))
                {
                    vars[field.Name] = field.GetValue(this);
                }
            }

            return vars;
        }

        public void SetVars(Dictionary<string, object> vars)
        {
            foreach (KeyValuePair<string, object> var in vars)
            {
                if (m_Fields.ContainsKey(var.Key))
                {
                    if (m_Fields[var.Key].FieldType == typeof(LSL_Types.list))
                    {
                        LSL_Types.list v = (LSL_Types.list)m_Fields[var.Key].GetValue(this);
                        Object[] data = ((LSL_Types.list)var.Value).Data;
                        v.Data = new Object[data.Length];
                        Array.Copy(data, 0, v.Data, 0, data.Length);
                        m_Fields[var.Key].SetValue(this, v);
                    }
                    else if (m_Fields[var.Key].FieldType == typeof(LSL_Types.LSLInteger) ||
                            m_Fields[var.Key].FieldType == typeof(LSL_Types.LSLString) ||
                            m_Fields[var.Key].FieldType == typeof(LSL_Types.LSLFloat) ||
                            m_Fields[var.Key].FieldType == typeof(Int32) ||
                            m_Fields[var.Key].FieldType == typeof(Double) ||
                            m_Fields[var.Key].FieldType == typeof(Single) ||
                            m_Fields[var.Key].FieldType == typeof(String) ||
                            m_Fields[var.Key].FieldType == typeof(Byte) ||
                            m_Fields[var.Key].FieldType == typeof(short) ||
                            m_Fields[var.Key].FieldType == typeof(LSL_Types.Vector3) ||
                            m_Fields[var.Key].FieldType == typeof(LSL_Types.Quaternion)
                        )
                    {
                        m_Fields[var.Key].SetValue(this, var.Value);
                    }
                }
            }
        }

        public void ResetVars()
        {
            SetVars(m_InitialValues);
        }

        public void NoOp()
        {
            // Does what is says on the packet. Nowt, nada, nothing.
            // Required for insertion after a jump label to do what it says on the packet!
            // With a bit of luck the compiler may even optimize it out.
        }
    }
}
