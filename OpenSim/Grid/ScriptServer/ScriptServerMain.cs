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
using System.Reflection;
using libsecondlife;
using log4net;
using OpenSim.Framework.Console;
using OpenSim.Framework.Servers;
using OpenSim.Grid.ScriptServer.ScriptServer;
using OpenSim.Region.ScriptEngine.Common;
using OpenSim.Region.ScriptEngine.Common.TRPC;

namespace OpenSim.Grid.ScriptServer
{
    public class ScriptServerMain : BaseOpenSimServer, conscmd_callback
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        //
        // Root object. Creates objects used.
        //
        private int listenPort = 8010;

        // TEMP
        public static ScriptServerInterfaces.ScriptEngine Engine;
        //public static FakeScene m_Scene = new FakeScene(null,null,null,null,null,null,null,null,null,false, false, false);

        // Objects we use
        internal RegionCommManager RegionScriptDaemon; // Listen for incoming from region
        internal ScriptEngineManager ScriptEngines; // Loads scriptengines
        //internal RemotingServer m_RemotingServer;
        internal TCPServer m_TCPServer;
        internal TRPC_Remote RPC;

        public ScriptServerMain()
        {
            m_console = CreateConsole();

            // Set up script engine mananger
            ScriptEngines = new ScriptEngineManager(this);

            // Load DotNetEngine
            Engine = ScriptEngines.LoadEngine("DotNetEngine");

            Engine.InitializeEngine(null, null, false, Engine.GetScriptManager());

            // Set up server
            //m_RemotingServer = new RemotingServer(listenPort, "DotNetEngine");
            m_TCPServer = new TCPServer(listenPort);
            RPC = new TRPC_Remote(m_TCPServer);
                    RPC.ReceiveCommand += new TRPC_Remote.ReceiveCommandDelegate(RPC_ReceiveCommand);
            m_TCPServer.StartListen();

            Console.ReadLine();
        }

        private static void RPC_ReceiveCommand(int ID, string Command, object[] p)
        {
            m_log.Info("[SERVER]: Received command: '" + Command + "'");
            if (p != null)
            {
                for (int i = 0; i < p.Length; i++)
                {
                    m_log.Info("[SERVER]: Param " + i + ": " + p[i]);
                }
            }

            if (Command == "OnRezScript")
            {
                Engine.EventManager().OnRezScript((uint)p[0], new LLUUID((string)p[1]), (string)p[2], 0, false);
            }
        }

        protected ConsoleBase CreateConsole()
        {
            return new ConsoleBase("Script", this);
        }
    }
}
