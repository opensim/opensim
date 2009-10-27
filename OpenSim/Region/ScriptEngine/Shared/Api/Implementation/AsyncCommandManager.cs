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
using System.Threading;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.ScriptEngine.Interfaces;
using OpenSim.Region.ScriptEngine.Shared;
using OpenSim.Region.ScriptEngine.Shared.Api.Plugins;
using Timer=OpenSim.Region.ScriptEngine.Shared.Api.Plugins.Timer;

namespace OpenSim.Region.ScriptEngine.Shared.Api
{
    /// <summary>
    /// Handles LSL commands that takes long time and returns an event, for example timers, HTTP requests, etc.
    /// </summary>
    public class AsyncCommandManager
    {
        private static Thread cmdHandlerThread;
        private static int cmdHandlerThreadCycleSleepms;

        private static List<IScene> m_Scenes = new List<IScene>();
        private static List<IScriptEngine> m_ScriptEngines =
                new List<IScriptEngine>();

        public IScriptEngine m_ScriptEngine;
        private IScene m_Scene;

        private static Dictionary<IScriptEngine, Dataserver> m_Dataserver =
                new Dictionary<IScriptEngine, Dataserver>();
        private static Dictionary<IScriptEngine, Timer> m_Timer =
                new Dictionary<IScriptEngine, Timer>();
        private static Dictionary<IScriptEngine, Listener> m_Listener =
                new Dictionary<IScriptEngine, Listener>();
        private static Dictionary<IScriptEngine, HttpRequest> m_HttpRequest =
                new Dictionary<IScriptEngine, HttpRequest>();
        private static Dictionary<IScriptEngine, SensorRepeat> m_SensorRepeat =
                new Dictionary<IScriptEngine, SensorRepeat>();
        private static Dictionary<IScriptEngine, XmlRequest> m_XmlRequest =
                new Dictionary<IScriptEngine, XmlRequest>();

        public Dataserver DataserverPlugin
        {
            get { return m_Dataserver[m_ScriptEngine]; }
        }

        public Timer TimerPlugin
        {
            get { return m_Timer[m_ScriptEngine]; }
        }

        public HttpRequest HttpRequestPlugin
        {
            get { return m_HttpRequest[m_ScriptEngine]; }
        }

        public Listener ListenerPlugin
        {
            get { return m_Listener[m_ScriptEngine]; }
        }

        public SensorRepeat SensorRepeatPlugin
        {
            get { return m_SensorRepeat[m_ScriptEngine]; }
        }

        public XmlRequest XmlRequestPlugin
        {
            get { return m_XmlRequest[m_ScriptEngine]; }
        }

        public IScriptEngine[] ScriptEngines
        {
            get { return m_ScriptEngines.ToArray(); }
        }

        public AsyncCommandManager(IScriptEngine _ScriptEngine)
        {
            m_ScriptEngine = _ScriptEngine;
            m_Scene = m_ScriptEngine.World;

            if (m_Scenes.Count == 0)
                ReadConfig();

            if (!m_Scenes.Contains(m_Scene))
                m_Scenes.Add(m_Scene);
            if (!m_ScriptEngines.Contains(m_ScriptEngine))
                m_ScriptEngines.Add(m_ScriptEngine);

            // Create instances of all plugins
            if (!m_Dataserver.ContainsKey(m_ScriptEngine))
                m_Dataserver[m_ScriptEngine] = new Dataserver(this);
            if (!m_Timer.ContainsKey(m_ScriptEngine))
                m_Timer[m_ScriptEngine] = new Timer(this);
            if (!m_HttpRequest.ContainsKey(m_ScriptEngine))
                m_HttpRequest[m_ScriptEngine] = new HttpRequest(this);
            if (!m_Listener.ContainsKey(m_ScriptEngine))
                m_Listener[m_ScriptEngine] = new Listener(this);
            if (!m_SensorRepeat.ContainsKey(m_ScriptEngine))
                m_SensorRepeat[m_ScriptEngine] = new SensorRepeat(this);
            if (!m_XmlRequest.ContainsKey(m_ScriptEngine))
                m_XmlRequest[m_ScriptEngine] = new XmlRequest(this);

            StartThread();
        }

        private static void StartThread()
        {
            if (cmdHandlerThread == null)
            {
                // Start the thread that will be doing the work
                cmdHandlerThread = Watchdog.StartThread(CmdHandlerThreadLoop, "AsyncLSLCmdHandlerThread", ThreadPriority.Normal, true);
            }
        }

        private void ReadConfig()
        {
//            cmdHandlerThreadCycleSleepms = m_ScriptEngine.Config.GetInt("AsyncLLCommandLoopms", 100);
            // TODO: Make this sane again
            cmdHandlerThreadCycleSleepms = 100;
        }

        ~AsyncCommandManager()
        {
            // Shut down thread
//            try
//            {
//                if (cmdHandlerThread != null)
//                {
//                    if (cmdHandlerThread.IsAlive == true)
//                    {
//                        cmdHandlerThread.Abort();
//                        //cmdHandlerThread.Join();
//                    }
//                }
//            }
//            catch
//            {
//            }
        }

        /// <summary>
        /// Main loop for the manager thread
        /// </summary>
        private static void CmdHandlerThreadLoop()
        {
            while (true)
            {
                try
                {
                    while (true)
                    {
                        Thread.Sleep(cmdHandlerThreadCycleSleepms);

                        DoOneCmdHandlerPass();

                        Watchdog.UpdateThread();
                    }
                }
                catch
                {
                }
            }
        }

        private static void DoOneCmdHandlerPass()
        {
            // Check HttpRequests
            m_HttpRequest[m_ScriptEngines[0]].CheckHttpRequests();

            // Check XMLRPCRequests
            m_XmlRequest[m_ScriptEngines[0]].CheckXMLRPCRequests();

            foreach (IScriptEngine s in m_ScriptEngines)
            {
                // Check Listeners
                m_Listener[s].CheckListeners();

                // Check timers
                m_Timer[s].CheckTimerEvents();

                // Check Sensors
                m_SensorRepeat[s].CheckSenseRepeaterEvents();

                // Check dataserver
                m_Dataserver[s].ExpireRequests();
            }
        }

        /// <summary>
        /// Remove a specific script (and all its pending commands)
        /// </summary>
        /// <param name="localID"></param>
        /// <param name="itemID"></param>
        public static void RemoveScript(IScriptEngine engine, uint localID, UUID itemID)
        {
            // Remove a specific script

            // Remove dataserver events
            m_Dataserver[engine].RemoveEvents(localID, itemID);

            // Remove from: Timers
            m_Timer[engine].UnSetTimerEvents(localID, itemID);

            // Remove from: HttpRequest
            IHttpRequestModule iHttpReq =
                engine.World.RequestModuleInterface<IHttpRequestModule>();
            iHttpReq.StopHttpRequest(localID, itemID);

            IWorldComm comms = engine.World.RequestModuleInterface<IWorldComm>();
            comms.DeleteListener(itemID);

            IXMLRPC xmlrpc = engine.World.RequestModuleInterface<IXMLRPC>();
            xmlrpc.DeleteChannels(itemID);
            xmlrpc.CancelSRDRequests(itemID);

            // Remove Sensors
            m_SensorRepeat[engine].UnSetSenseRepeaterEvents(localID, itemID);

        }

        public static Object[] GetSerializationData(IScriptEngine engine, UUID itemID)
        {
            List<Object> data = new List<Object>();

            Object[] listeners=m_Listener[engine].GetSerializationData(itemID);
            if (listeners.Length > 0)
            {
                data.Add("listener");
                data.Add(listeners.Length);
                data.AddRange(listeners);
            }

            Object[] timers=m_Timer[engine].GetSerializationData(itemID);
            if (timers.Length > 0)
            {
                data.Add("timer");
                data.Add(timers.Length);
                data.AddRange(timers);
            }

            Object[] sensors=m_SensorRepeat[engine].GetSerializationData(itemID);
            if (sensors.Length > 0)
            {
                data.Add("sensor");
                data.Add(sensors.Length);
                data.AddRange(sensors);
            }

            return data.ToArray();
        }

        public static void CreateFromData(IScriptEngine engine, uint localID,
                UUID itemID, UUID hostID, Object[] data)
        {
            int idx = 0;
            int len;

            while (idx < data.Length)
            {
                string type = data[idx].ToString();
                len = (int)data[idx+1];
                idx+=2;

                if (len > 0)
                {
                    Object[] item = new Object[len];
                    Array.Copy(data, idx, item, 0, len);

                    idx+=len;

                    switch (type)
                    {
                    case "listener":
                        m_Listener[engine].CreateFromData(localID, itemID,
                                                    hostID, item);
                        break;
                    case "timer":
                        m_Timer[engine].CreateFromData(localID, itemID,
                                                    hostID, item);
                        break;
                    case "sensor":
                        m_SensorRepeat[engine].CreateFromData(localID,
                                                    itemID, hostID, item);
                        break;
                    }
                }
            }
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
