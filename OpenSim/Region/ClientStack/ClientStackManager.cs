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
using System.Collections.Generic;
using System.Net;
using System.Reflection;
using log4net;
using Nini.Config;
using OpenSim.Framework;

namespace OpenSim.Region.ClientStack
{
    public class ClientStackManager
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private List<Type> plugin = new List<Type>();
        private List<Assembly> pluginAssembly = new List<Assembly>();

        public ClientStackManager(string pDllName) 
        {
            List<string> clientstacks = new List<string>();
            if (pDllName.Contains(","))
            {
                clientstacks = new List<string>(pDllName.Split(','));
            }
            else
            {
                clientstacks.Add(pDllName);
            }
            foreach (string dllName in clientstacks)
            {
                m_log.Info("[CLIENTSTACK]: Attempting to load " + dllName);

                try
                {
                    //plugin = null;
                    Assembly itemAssembly = Assembly.LoadFrom(dllName);
                    pluginAssembly.Add(itemAssembly);

                    foreach (Type pluginType in itemAssembly.GetTypes())
                    {
                        if (pluginType.IsPublic)
                        {
                            Type typeInterface = pluginType.GetInterface("IClientNetworkServer", true);

                            if (typeInterface != null)
                            {
                                m_log.Info("[CLIENTSTACK]: Added IClientNetworkServer Interface");
                                plugin.Add(pluginType);
                                break;
                            }
                        }
                    }
                }
                catch (ReflectionTypeLoadException e)
                {
                    foreach (Exception e2 in e.LoaderExceptions)
                    {
                        m_log.Error(e2.ToString());
                    }
                    throw e;
                }
            }
        }
        
        /// <summary>
        /// Create a server that can set up sessions for virtual world client <-> server communications
        /// </summary>
        /// <param name="_listenIP"></param>
        /// <param name="port"></param>
        /// <param name="proxyPortOffset"></param>
        /// <param name="allow_alternate_port"></param>
        /// <param name="assetCache"></param>
        /// <param name="authenticateClass"></param>
        /// <returns></returns>
        public List<IClientNetworkServer> CreateServers(
            IPAddress _listenIP, ref uint port, int proxyPortOffset, bool allow_alternate_port,
            AgentCircuitManager authenticateClass)
        {
            return CreateServers(
                _listenIP, ref port, proxyPortOffset, allow_alternate_port, null, authenticateClass);
        }

        /// <summary>
        /// Create a server that can set up sessions for virtual world client <-> server communications
        /// </summary>
        /// <param name="_listenIP"></param>
        /// <param name="port"></param>
        /// <param name="proxyPortOffset"></param>
        /// <param name="allow_alternate_port"></param>
        /// <param name="configSource">
        /// Can be null, in which case default values are used
        /// </param>
        /// <param name="assetCache"></param>
        /// <param name="authenticateClass"></param>
        /// <returns></returns>
        public List<IClientNetworkServer> CreateServers(
            IPAddress _listenIP, ref uint port, int proxyPortOffset, bool allow_alternate_port, IConfigSource configSource,
            AgentCircuitManager authenticateClass)
        {
            List<IClientNetworkServer> servers = new List<IClientNetworkServer>();
            if (plugin != null)
            {
                for (int i = 0; i < plugin.Count; i++)
                {
                    IClientNetworkServer server =
                        (IClientNetworkServer) Activator.CreateInstance(pluginAssembly[i].GetType(plugin[i].ToString()));

                    server.Initialise(
                        _listenIP, ref port, proxyPortOffset, allow_alternate_port,
                        configSource, authenticateClass);
                    servers.Add(server);
                }
                return servers;
            }
            
            m_log.Error("[CLIENTSTACK]: Couldn't initialize a new server");
            return null;
        }
    }
}
