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
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using log4net;
using OpenSim.Region.ScriptEngine.Interfaces;

namespace OpenSim.Region.ScriptEngine.Shared.Api
{
    public class ApiManager
    {
//        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private Dictionary<string,Type> m_Apis = new Dictionary<string,Type>();

        public string[] GetApis()
        {
            if (m_Apis.Count <= 0)
            {
                Assembly a = Assembly.GetExecutingAssembly();

                Type[] types = a.GetExportedTypes();

                foreach (Type t in types)
                {
                    string name = t.ToString();
                    int idx = name.LastIndexOf('.');
                    if (idx != -1)
                        name = name.Substring(idx+1);

                    if (name.EndsWith("_Api"))
                    {
                        name = name.Substring(0, name.Length - 4);
                        m_Apis[name] = t;
                    }
                }
            }

//            m_log.DebugFormat("[API MANAGER]: Found {0} apis", m_Apis.Keys.Count);

            return new List<string>(m_Apis.Keys).ToArray();
        }

        public IScriptApi CreateApi(string api)
        {
            if (!m_Apis.ContainsKey(api))
                return null;

            IScriptApi ret = (IScriptApi)(Activator.CreateInstance(m_Apis[api]));
            return ret;
        }
    }
}