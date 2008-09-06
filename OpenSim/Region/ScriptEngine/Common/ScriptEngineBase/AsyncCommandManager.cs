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

using System.Collections;
using System.Threading;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.ScriptEngine.Common.ScriptEngineBase.AsyncCommandPlugins;
using Timer=OpenSim.Region.ScriptEngine.Common.ScriptEngineBase.AsyncCommandPlugins.Timer;

namespace OpenSim.Region.ScriptEngine.Common.ScriptEngineBase
{
    /// <summary>
    /// Handles LSL commands that takes long time and returns an event, for example timers, HTTP requests, etc.
    /// </summary>
    public class AsyncCommandManager : iScriptEngineFunctionModule
    {
        private static Thread cmdHandlerThread;
        private static int cmdHandlerThreadCycleSleepms;

        public ScriptEngine m_ScriptEngine;

        public Timer m_Timer;
        public HttpRequest m_HttpRequest;
        public Listener m_Listener;
        public SensorRepeat m_SensorRepeat;
        public XmlRequest m_XmlRequest;
        public Dataserver m_Dataserver;

        public AsyncCommandManager(ScriptEngine _ScriptEngine)
        {
            m_ScriptEngine = _ScriptEngine;
            ReadConfig();

            // Create instances of all plugins
            m_Timer = new Timer(this);
            m_HttpRequest = new HttpRequest(this);
            m_Listener = new Listener(this);
            m_SensorRepeat = new SensorRepeat(this);
            m_XmlRequest = new XmlRequest(this);
            m_Dataserver = new Dataserver(this);

            StartThread();
        }

        private static void StartThread()
        {
            if (cmdHandlerThread == null)
            {
                // Start the thread that will be doing the work
                cmdHandlerThread = new Thread(CmdHandlerThreadLoop);
                cmdHandlerThread.Name = "AsyncLSLCmdHandlerThread";
                cmdHandlerThread.Priority = ThreadPriority.BelowNormal;
                cmdHandlerThread.IsBackground = true;
                cmdHandlerThread.Start();
                ThreadTracker.Add(cmdHandlerThread);
            }
        }

        public void ReadConfig()
        {
            cmdHandlerThreadCycleSleepms = m_ScriptEngine.ScriptConfigSource.GetInt("AsyncLLCommandLoopms", 100);
        }

        ~AsyncCommandManager()
        {
            // Shut down thread
            try
            {
                if (cmdHandlerThread != null)
                {
                    if (cmdHandlerThread.IsAlive == true)
                    {
                        cmdHandlerThread.Abort();
                        //cmdHandlerThread.Join();
                    }
                }
            }
            catch
            {
            }
        }

        private static void CmdHandlerThreadLoop()
        {
            while (true)
            {
                try
                {
                    while (true)
                    {
                        Thread.Sleep(cmdHandlerThreadCycleSleepms);
                        //lock (ScriptEngine.ScriptEngines)
                        //{
                            foreach (ScriptEngine se in new ArrayList(ScriptEngine.ScriptEngines))
                            {
                                se.m_ASYNCLSLCommandManager.DoOneCmdHandlerPass();
                            }
                        //}
                        // Sleep before next cycle
                        //Thread.Sleep(cmdHandlerThreadCycleSleepms);
                    }
                }
                catch
                {
                }
            }
        }

        internal void DoOneCmdHandlerPass()
        {
            // Check timers
            m_Timer.CheckTimerEvents();
            // Check HttpRequests
            m_HttpRequest.CheckHttpRequests();
            // Check XMLRPCRequests
            m_XmlRequest.CheckXMLRPCRequests();
            // Check Listeners
            m_Listener.CheckListeners();
            // Check Sensors
            m_SensorRepeat.CheckSenseRepeaterEvents();
            // Check dataserver
            m_Dataserver.ExpireRequests();
        }

        /// <summary>
        /// Remove a specific script (and all its pending commands)
        /// </summary>
        /// <param name="localID"></param>
        /// <param name="itemID"></param>
        public void RemoveScript(uint localID, UUID itemID)
        {
            // Remove a specific script

            // Remove from: Timers
            m_Timer.UnSetTimerEvents(localID, itemID);

            // Remove from: HttpRequest
            IHttpRequests iHttpReq =
                m_ScriptEngine.World.RequestModuleInterface<IHttpRequests>();
            iHttpReq.StopHttpRequest(localID, itemID);

            IWorldComm comms = m_ScriptEngine.World.RequestModuleInterface<IWorldComm>();
            comms.DeleteListener(itemID);

            IXMLRPC xmlrpc = m_ScriptEngine.World.RequestModuleInterface<IXMLRPC>();
            xmlrpc.DeleteChannels(itemID);
            xmlrpc.CancelSRDRequests(itemID);

            // Remove Sensors
            m_SensorRepeat.UnSetSenseRepeaterEvents(localID, itemID);

            // Remove queries
            m_Dataserver.RemoveEvents(localID, itemID);

        }


        #region Check llRemoteData channels


        #endregion

        #region Check llListeners


        #endregion

        /// <summary>
        /// If set to true then threads and stuff should try to make a graceful exit
        /// </summary>
        public bool PleaseShutdown
        {
            get { return _PleaseShutdown; }
            set { _PleaseShutdown = value; }
        }
        private bool _PleaseShutdown = false;
    }
}
