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
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Environment.Interfaces;
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
        private static List<IEventReceiver> m_ScriptEngines =
                new List<IEventReceiver>();

        public IEventReceiver m_ScriptEngine;
        private IScene m_Scene;

        private static Dictionary<IScene, Dataserver> m_Dataserver =
                new Dictionary<IScene, Dataserver>();
        private static Dictionary<IScene, Timer> m_Timer =
                new Dictionary<IScene, Timer>();
        private static Dictionary<IScene, Listener> m_Listener =
                new Dictionary<IScene, Listener>();
        private static Dictionary<IScene, HttpRequest> m_HttpRequest =
                new Dictionary<IScene, HttpRequest>();
        private static Dictionary<IScene, SensorRepeat> m_SensorRepeat =
                new Dictionary<IScene, SensorRepeat>();
        private static Dictionary<IScene, XmlRequest> m_XmlRequest =
                new Dictionary<IScene, XmlRequest>();

        public Dataserver DataserverPlugin
        {
            get { return m_Dataserver[m_Scene]; }
        }

        public Timer TimerPlugin
        {
            get { return m_Timer[m_Scene]; }
        }

        public HttpRequest HttpRequestPlugin
        {
            get { return m_HttpRequest[m_Scene]; }
        }

        public Listener ListenerPlugin
        {
            get { return m_Listener[m_Scene]; }
        }

        public SensorRepeat SensorRepeatPlugin
        {
            get { return m_SensorRepeat[m_Scene]; }
        }

        public XmlRequest XmlRequestPlugin
        {
            get { return m_XmlRequest[m_Scene]; }
        }

        public IEventReceiver[] ScriptEngines
        {
            get { return m_ScriptEngines.ToArray(); }
        }

        public AsyncCommandManager(IEventReceiver _ScriptEngine)
        {
            m_ScriptEngine = _ScriptEngine;
            m_Scene = m_ScriptEngine.World;

            if (!m_Scenes.Contains(m_Scene))
                m_Scenes.Add(m_Scene);
            if (!m_ScriptEngines.Contains(m_ScriptEngine))
                m_ScriptEngines.Add(m_ScriptEngine);

            ReadConfig();

            // Create instances of all plugins
            if (!m_Dataserver.ContainsKey(m_Scene))
                m_Dataserver[m_Scene] = new Dataserver(this);
            if (!m_Timer.ContainsKey(m_Scene))
                m_Timer[m_Scene] = new Timer(this);
            if (!m_HttpRequest.ContainsKey(m_Scene))
                m_HttpRequest[m_Scene] = new HttpRequest(this);
            if (!m_Listener.ContainsKey(m_Scene))
                m_Listener[m_Scene] = new Listener(this);
            if (!m_SensorRepeat.ContainsKey(m_Scene))
                m_SensorRepeat[m_Scene] = new SensorRepeat(this);
            if (!m_XmlRequest.ContainsKey(m_Scene))
                m_XmlRequest[m_Scene] = new XmlRequest(this);

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
                    }
                }
                catch
                {
                }
            }
        }

        private static void DoOneCmdHandlerPass()
        {
            foreach (IScene s in m_Scenes)
            {
                // Check timers
                m_Timer[s].CheckTimerEvents();
                // Check HttpRequests
                m_HttpRequest[s].CheckHttpRequests();
                // Check XMLRPCRequests
                m_XmlRequest[s].CheckXMLRPCRequests();
                // Check Listeners
                m_Listener[s].CheckListeners();
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
        public static void RemoveScript(IScene scene, uint localID, UUID itemID)
        {
            // Remove a specific script

            // Remove dataserver events
            m_Dataserver[scene].RemoveEvents(localID, itemID);

            // Remove from: Timers
            m_Timer[scene].UnSetTimerEvents(localID, itemID);

            // Remove from: HttpRequest
            IHttpRequests iHttpReq =
                scene.RequestModuleInterface<IHttpRequests>();
            iHttpReq.StopHttpRequest(localID, itemID);

            IWorldComm comms = scene.RequestModuleInterface<IWorldComm>();
            comms.DeleteListener(itemID);

            IXMLRPC xmlrpc = scene.RequestModuleInterface<IXMLRPC>();
            xmlrpc.DeleteChannels(itemID);
            xmlrpc.CancelSRDRequests(itemID);

            // Remove Sensors
            m_SensorRepeat[scene].UnSetSenseRepeaterEvents(localID, itemID);

        }

        public static Object[] GetSerializationData(IScene scene, UUID itemID)
        {
            List<Object> data = new List<Object>();

            Object[] listeners=m_Listener[scene].GetSerializationData(itemID);
            if (listeners.Length > 0)
            {
                data.Add("listener");
                data.Add(listeners.Length);
                data.AddRange(listeners);
            }

            Object[] timers=m_Timer[scene].GetSerializationData(itemID);
            if (timers.Length > 0)
            {
                data.Add("timer");
                data.Add(timers.Length);
                data.AddRange(timers);
            }

            Object[] sensors=m_SensorRepeat[scene].GetSerializationData(itemID);
            if (sensors.Length > 0)
            {
                data.Add("sensor");
                data.Add(sensors.Length);
                data.AddRange(sensors);
            }

            return data.ToArray();
        }

        public static void CreateFromData(IScene scene, uint localID,
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
                        m_Listener[scene].CreateFromData(localID, itemID,
                                                    hostID, item);
                        break;
                    case "timer":
                        m_Timer[scene].CreateFromData(localID, itemID,
                                                    hostID, item);
                        break;
                    case "sensor":
                        m_SensorRepeat[scene].CreateFromData(localID,
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
