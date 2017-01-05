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
using System.Reflection;
using System.Collections.Generic;
using Nini.Config;
using log4net;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using Mono.Addins;
using OpenMetaverse;
using System.Linq;
using System.Linq.Expressions;

namespace OpenSim.Region.CoreModules.Scripting.ScriptModuleComms
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "ScriptModuleCommsModule")]
    public class ScriptModuleCommsModule : INonSharedRegionModule, IScriptModuleComms
    {
        private static readonly ILog m_log =
            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private static string LogHeader = "[MODULE COMMS]";

        private Dictionary<string,object> m_constants = new Dictionary<string,object>();

#region ScriptInvocation
        protected class ScriptInvocationData
        {
            public Delegate ScriptInvocationDelegate { get; private set; }
            public string FunctionName { get; private set; }
            public Type[] TypeSignature { get; private set; }
            public Type ReturnType { get; private set; }

            public ScriptInvocationData(string fname, Delegate fn, Type[] callsig, Type returnsig)
            {
                FunctionName = fname;
                ScriptInvocationDelegate = fn;
                TypeSignature = callsig;
                ReturnType = returnsig;
            }
        }

        private Dictionary<string,ScriptInvocationData> m_scriptInvocation = new Dictionary<string,ScriptInvocationData>();
#endregion

        private IScriptModule m_scriptModule = null;
        public event ScriptCommand OnScriptCommand;

#region RegionModuleInterface
        public void Initialise(IConfigSource config)
        {
        }

        public void AddRegion(Scene scene)
        {
            scene.RegisterModuleInterface<IScriptModuleComms>(this);
        }

        public void RemoveRegion(Scene scene)
        {
        }

        public void RegionLoaded(Scene scene)
        {
            m_scriptModule = scene.RequestModuleInterface<IScriptModule>();

            if (m_scriptModule != null)
                m_log.Info("[MODULE COMMANDS]: Script engine found, module active");
        }

        public string Name
        {
            get { return "ScriptModuleCommsModule"; }
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public void Close()
        {
        }
#endregion

#region ScriptModuleComms

        public void RaiseEvent(UUID script, string id, string module, string command, string k)
        {
            ScriptCommand c = OnScriptCommand;

            if (c == null)
                return;

            c(script, id, module, command, k);
        }

        public void DispatchReply(UUID script, int code, string text, string k)
        {
            if (m_scriptModule == null)
                return;

            Object[] args = new Object[] {-1, code, text, k};

            m_scriptModule.PostScriptEvent(script, "link_message", args);
        }

        private static MethodInfo GetMethodInfoFromType(Type target, string meth, bool searchInstanceMethods)
        {
            BindingFlags getMethodFlags =
                    BindingFlags.NonPublic | BindingFlags.Public;

            if (searchInstanceMethods)
                getMethodFlags |= BindingFlags.Instance;
            else
                getMethodFlags |= BindingFlags.Static;

            return target.GetMethod(meth, getMethodFlags);
        }

        public void RegisterScriptInvocation(object target, string meth)
        {
            MethodInfo mi = GetMethodInfoFromType(target.GetType(), meth, true);
            if (mi == null)
            {
                m_log.WarnFormat("{0} Failed to register method {1}", LogHeader, meth);
                return;
            }

            RegisterScriptInvocation(target, mi);
        }

        public void RegisterScriptInvocation(object target, string[] meth)
        {
            foreach (string m in meth)
                RegisterScriptInvocation(target, m);
        }

        public void RegisterScriptInvocation(object target, MethodInfo mi)
        {
//            m_log.DebugFormat("[MODULE COMMANDS] Register method {0} from type {1}", mi.Name, (target is Type) ? ((Type)target).Name : target.GetType().Name);

            Type delegateType = typeof(void);
            List<Type> typeArgs = mi.GetParameters()
                    .Select(p => p.ParameterType)
                    .ToList();

            if (mi.ReturnType == typeof(void))
            {
                delegateType = Expression.GetActionType(typeArgs.ToArray());
            }
            else
            {
                try
                {
                    typeArgs.Add(mi.ReturnType);
                    delegateType = Expression.GetFuncType(typeArgs.ToArray());
                }
                catch (Exception e)
                {
                    m_log.ErrorFormat("{0} Failed to create function signature. Most likely more than 5 parameters. Method={1}. Error={2}",
                        LogHeader, mi.Name, e);
                }
            }

            Delegate fcall;
            if (!(target is Type))
                fcall = Delegate.CreateDelegate(delegateType, target, mi);
            else
                fcall = Delegate.CreateDelegate(delegateType, (Type)target, mi.Name);

            lock (m_scriptInvocation)
            {
                ParameterInfo[] parameters = fcall.Method.GetParameters();
                if (parameters.Length < 2) // Must have two UUID params
                    return;

                // Hide the first two parameters
                Type[] parmTypes = new Type[parameters.Length - 2];
                for (int i = 2; i < parameters.Length; i++)
                    parmTypes[i - 2] = parameters[i].ParameterType;
                m_scriptInvocation[fcall.Method.Name] = new ScriptInvocationData(fcall.Method.Name, fcall, parmTypes, fcall.Method.ReturnType);
            }
        }

        public void RegisterScriptInvocation(Type target, string[] methods)
        {
            foreach (string method in methods)
            {
                MethodInfo mi = GetMethodInfoFromType(target, method, false);
                if (mi == null)
                    m_log.WarnFormat("[MODULE COMMANDS] Failed to register method {0}", method);
                else
                    RegisterScriptInvocation(target, mi);
            }
        }

        public void RegisterScriptInvocations(IRegionModuleBase target)
        {
            foreach(MethodInfo method in target.GetType().GetMethods(
                    BindingFlags.Public | BindingFlags.Instance |
                    BindingFlags.Static))
            {
                if(method.GetCustomAttributes(
                        typeof(ScriptInvocationAttribute), true).Any())
                {
                    if(method.IsStatic)
                        RegisterScriptInvocation(target.GetType(), method);
                    else
                        RegisterScriptInvocation(target, method);
                }
            }
        }

        public Delegate[] GetScriptInvocationList()
        {
            List<Delegate> ret = new List<Delegate>();

            lock (m_scriptInvocation)
            {
                foreach (ScriptInvocationData d in m_scriptInvocation.Values)
                    ret.Add(d.ScriptInvocationDelegate);
            }
            return ret.ToArray();
        }

        public string LookupModInvocation(string fname)
        {
            lock (m_scriptInvocation)
            {
                ScriptInvocationData sid;
                if (m_scriptInvocation.TryGetValue(fname,out sid))
                {
                    if (sid.ReturnType == typeof(string))
                        return "modInvokeS";
                    else if (sid.ReturnType == typeof(int))
                        return "modInvokeI";
                    else if (sid.ReturnType == typeof(float))
                        return "modInvokeF";
                    else if (sid.ReturnType == typeof(UUID))
                        return "modInvokeK";
                    else if (sid.ReturnType == typeof(OpenMetaverse.Vector3))
                        return "modInvokeV";
                    else if (sid.ReturnType == typeof(OpenMetaverse.Quaternion))
                        return "modInvokeR";
                    else if (sid.ReturnType == typeof(object[]))
                        return "modInvokeL";
                    else if (sid.ReturnType == typeof(void))
                        return "modInvokeN";

                    m_log.WarnFormat("[MODULE COMMANDS] failed to find match for {0} with return type {1}",fname,sid.ReturnType.Name);
                }
            }

            return null;
        }

        public Delegate LookupScriptInvocation(string fname)
        {
            lock (m_scriptInvocation)
            {
                ScriptInvocationData sid;
                if (m_scriptInvocation.TryGetValue(fname,out sid))
                    return sid.ScriptInvocationDelegate;
            }

            return null;
        }

        public Type[] LookupTypeSignature(string fname)
        {
            lock (m_scriptInvocation)
            {
                ScriptInvocationData sid;
                if (m_scriptInvocation.TryGetValue(fname,out sid))
                    return sid.TypeSignature;
            }

            return null;
        }

        public Type LookupReturnType(string fname)
        {
            lock (m_scriptInvocation)
            {
                ScriptInvocationData sid;
                if (m_scriptInvocation.TryGetValue(fname,out sid))
                    return sid.ReturnType;
            }

            return null;
        }

        public object InvokeOperation(UUID hostid, UUID scriptid, string fname, params object[] parms)
        {
            List<object> olist = new List<object>();
            olist.Add(hostid);
            olist.Add(scriptid);
            foreach (object o in parms)
                olist.Add(o);
            Delegate fn = LookupScriptInvocation(fname);
            return fn.DynamicInvoke(olist.ToArray());
        }

        /// <summary>
        /// Operation to for a region module to register a constant to be used
        /// by the script engine
        /// </summary>
        public void RegisterConstant(string cname, object value)
        {
//            m_log.DebugFormat("[MODULE COMMANDS] register constant <{0}> with value {1}",cname,value.ToString());
            lock (m_constants)
            {
                m_constants.Add(cname,value);
            }
        }

        public void RegisterConstants(IRegionModuleBase target)
        {
            foreach (FieldInfo field in target.GetType().GetFields(
                    BindingFlags.Public | BindingFlags.Static |
                    BindingFlags.Instance))
            {
                if (field.GetCustomAttributes(
                        typeof(ScriptConstantAttribute), true).Any())
                {
                    RegisterConstant(field.Name, field.GetValue(target));
                }
            }
        }

        /// <summary>
        /// Operation to check for a registered constant
        /// </summary>
        public object LookupModConstant(string cname)
        {
            // m_log.DebugFormat("[MODULE COMMANDS] lookup constant <{0}>",cname);

            lock (m_constants)
            {
                object value = null;
                if (m_constants.TryGetValue(cname,out value))
                    return value;
            }

            return null;
        }

        /// <summary>
        /// Get all registered constants
        /// </summary>
        public Dictionary<string, object> GetConstants()
        {
            Dictionary<string, object> ret = new Dictionary<string, object>();

            lock (m_constants)
            {
                foreach (KeyValuePair<string, object> kvp in m_constants)
                    ret[kvp.Key] = kvp.Value;
            }

            return ret;
        }

#endregion

    }
}
