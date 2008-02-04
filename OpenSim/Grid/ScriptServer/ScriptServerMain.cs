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
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS AS IS AND ANY
* EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
* WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
* DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
* DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
* (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
* LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
* ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
* (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
* SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
* 
*/

using System.IO;
using libsecondlife;
using Nini.Config;
using OpenSim.Framework;
using OpenSim.Framework.Console;
using OpenSim.Framework.Servers;
using OpenSim.Grid.ScriptServer.ScriptServer;
using OpenSim.Region.ScriptEngine.Common;
using OpenSim.Region.ScriptEngine.Common.TRPC;

namespace OpenSim.Grid.ScriptServer
{
    public class ScriptServerMain : BaseOpenSimServer, conscmd_callback
    {
        //
        // Root object. Creates objects used.
        //
        private int listenPort = 8010;
        private readonly string m_logFilename = ("scriptserver.log");

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
            m_log = CreateLog();


            // Set up script engine mananger
            ScriptEngines = new ScriptEngineManager(this, m_log);

            // Load DotNetEngine
            Engine = ScriptEngines.LoadEngine("DotNetEngine");
                    IConfigSource config = null;
            Engine.InitializeEngine(null, null, m_log, false, Engine.GetScriptManager());
                    

            // Set up server
            //m_RemotingServer = new RemotingServer(listenPort, "DotNetEngine");
            m_TCPServer = new TCPServer(listenPort);
            RPC = new TRPC_Remote(m_TCPServer);
                    RPC.ReceiveCommand += new TRPC_Remote.ReceiveCommandDelegate(RPC_ReceiveCommand);
            m_TCPServer.StartListen();

            System.Console.ReadLine();
        }

        private void RPC_ReceiveCommand(int ID, string Command, object[] p)
        {
            m_log.Notice("SERVER", "Received command: '" + Command + "'");
            if (p != null)
            {
                for (int i = 0; i < p.Length; i++)
                {
                    m_log.Notice("SERVER", "Param " + i + ": " + p[i].ToString());
                }
            }

            if (Command == "OnRezScript")
            {
                Engine.EventManager().OnRezScript((uint)p[0], new LLUUID((string)p[1]), (string)p[2]);
            }
        }

        ~ScriptServerMain()
        {
        }

        protected LogBase CreateLog()
        {
            if (!Directory.Exists(Util.logDir()))
            {
                Directory.CreateDirectory(Util.logDir());
            }

            return new LogBase((Path.Combine(Util.logDir(), m_logFilename)), "ScriptServer", this, true);
        }
    }
}
