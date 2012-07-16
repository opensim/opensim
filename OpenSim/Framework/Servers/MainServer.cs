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
using System.Reflection;
using System.Net;
using log4net;
using OpenSim.Framework;
using OpenSim.Framework.Console;
using OpenSim.Framework.Servers.HttpServer;

namespace OpenSim.Framework.Servers
{
    public class MainServer
    {
//        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private static BaseHttpServer instance = null;
        private static Dictionary<uint, BaseHttpServer> m_Servers = new Dictionary<uint, BaseHttpServer>();

        /// <summary>
        /// Control the printing of certain debug messages.
        /// </summary>
        /// <remarks>
        /// If DebugLevel >= 1, then short warnings are logged when receiving bad input data.
        /// If DebugLevel >= 2, then long warnings are logged when receiving bad input data.
        /// If DebugLevel >= 3, then short notices about all incoming non-poll HTTP requests are logged.
        /// </remarks>
        public static int DebugLevel
        {
            get { return s_debugLevel; }
            set
            {
                s_debugLevel = value;

                lock (m_Servers)
                    foreach (BaseHttpServer server in m_Servers.Values)
                        server.DebugLevel = s_debugLevel;
            }
        }

        private static int s_debugLevel;

        /// <summary>
        /// Set the main HTTP server instance.
        /// </summary>
        /// <remarks>
        /// This will be used to register all handlers that listen to the default port.
        /// </remarks>
        /// <exception cref='Exception'>
        /// Thrown if the HTTP server has not already been registered via AddHttpServer()
        /// </exception>
        public static BaseHttpServer Instance
        {
            get { return instance; }

            set
            {
                lock (m_Servers)
                    if (!m_Servers.ContainsValue(value))
                        throw new Exception("HTTP server must already have been registered to be set as the main instance");

                instance = value;
            }
        }

        /// <summary>
        /// Get all the registered servers.
        /// </summary>
        /// <remarks>
        /// Returns a copy of the dictionary so this can be iterated through without locking.
        /// </remarks>
        /// <value></value>
        public static Dictionary<uint, BaseHttpServer> Servers
        {
            get { return new Dictionary<uint, BaseHttpServer>(m_Servers); }
        }


        public static void RegisterHttpConsoleCommands(ICommandConsole console)
        {
            console.Commands.AddCommand(
                "Debug", false, "debug http", "debug http [<level>]",
                "Turn on inbound non-poll http request debugging.",
                  "If level <= 0, then no extra logging is done.\n"
                + "If level >= 1, then short warnings are logged when receiving bad input data.\n"
                + "If level >= 2, then long warnings are logged when receiving bad input data.\n"
                + "If level >= 3, then short notices about all incoming non-poll HTTP requests are logged.\n"
                + "If no level is specified then the current level is returned.",
                HandleDebugHttpCommand);
        }

        /// <summary>
        /// Turn on some debugging values for OpenSim.
        /// </summary>
        /// <param name="args"></param>
        private static void HandleDebugHttpCommand(string module, string[] args)
        {
            if (args.Length == 3)
            {
                int newDebug;
                if (int.TryParse(args[2], out newDebug))
                {
                    MainServer.DebugLevel = newDebug;
                    MainConsole.Instance.OutputFormat("Debug http level set to {0}", newDebug);
                }
            }
            else if (args.Length == 2)
            {
                MainConsole.Instance.OutputFormat("Current debug http level is {0}", MainServer.DebugLevel);
            }
            else
            {
                MainConsole.Instance.Output("Usage: debug http 0..3");
            }
        }

        /// <summary>
        /// Register an already started HTTP server to the collection of known servers.
        /// </summary>
        /// <param name='server'></param>
        public static void AddHttpServer(BaseHttpServer server)
        {
            lock (m_Servers)
            {
                if (m_Servers.ContainsKey(server.Port))
                    throw new Exception(string.Format("HTTP server for port {0} already exists.", server.Port));

                m_Servers.Add(server.Port, server);
            }
        }

        /// <summary>
        /// Removes the http server listening on the given port.
        /// </summary>
        /// <remarks>
        /// It is the responsibility of the caller to do clean up.
        /// </remarks>
        /// <param name='port'></param>
        /// <returns></returns>
        public static bool RemoveHttpServer(uint port)
        {
            lock (m_Servers)
                return m_Servers.Remove(port);
        }

        /// <summary>
        /// Does this collection of servers contain one with the given port?
        /// </summary>
        /// <remarks>
        /// Unlike GetHttpServer, this will not instantiate a server if one does not exist on that port.
        /// </remarks>
        /// <param name='port'></param>
        /// <returns>true if a server with the given port is registered, false otherwise.</returns>
        public static bool ContainsHttpServer(uint port)
        {
            lock (m_Servers)
                return m_Servers.ContainsKey(port);
        }

        /// <summary>
        /// Get the default http server or an http server for a specific port.
        /// </summary>
        /// <remarks>
        /// If the requested HTTP server doesn't already exist then a new one is instantiated and started.
        /// </remarks>
        /// <returns></returns>
        /// <param name='port'>If 0 then the default HTTP server is returned.</param>
        public static IHttpServer GetHttpServer(uint port)
        {
            return GetHttpServer(port, null);
        }

        /// <summary>
        /// Get the default http server, an http server for a specific port
        /// and/or an http server bound to a specific address
        /// </summary>
        /// <remarks>
        /// If the requested HTTP server doesn't already exist then a new one is instantiated and started.
        /// </remarks>
        /// <returns></returns>
        /// <param name='port'>If 0 then the default HTTP server is returned.</param>
        /// <param name='ipaddr'>A specific IP address to bind to.  If null then the default IP address is used.</param>
        public static IHttpServer GetHttpServer(uint port, IPAddress ipaddr)
        {
            if (port == 0)
                return Instance;
            
            if (instance != null && port == Instance.Port)
                return Instance;

            lock (m_Servers)
            {
                if (m_Servers.ContainsKey(port))
                    return m_Servers[port];

                m_Servers[port] = new BaseHttpServer(port);

                if (ipaddr != null)
                    m_Servers[port].ListenIPAddress = ipaddr;

                m_Servers[port].Start();

                return m_Servers[port];
            }
        }
    }
}