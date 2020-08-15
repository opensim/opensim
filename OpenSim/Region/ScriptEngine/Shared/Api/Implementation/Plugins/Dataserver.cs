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
using Amib.Threading;
using OpenSim.Region.ScriptEngine.Shared;
using OpenSim.Region.ScriptEngine.Shared.Api;
using Action = System.Action;

namespace OpenSim.Region.ScriptEngine.Shared.Api.Plugins
{
    public class Dataserver
    {
        private SmartThreadPool m_ThreadPool;

        public AsyncCommandManager m_CmdManager;

        public int DataserverRequestsCount
        {
            get
            {
                lock (DataserverRequests)
                    return DataserverRequests.Count;
            }
        }

        private Dictionary<string, DataserverRequest> DataserverRequests =  new Dictionary<string, DataserverRequest>();

        public Dataserver(AsyncCommandManager CmdManager)
        {
            m_CmdManager = CmdManager;

            STPStartInfo startInfo = new STPStartInfo();
            startInfo.ThreadPoolName = "ScriptV";
            startInfo.IdleTimeout = 1000;
            startInfo.MaxWorkerThreads = 4;
            startInfo.MinWorkerThreads = 0;
            startInfo.ThreadPriority = ThreadPriority.Normal;
            startInfo.StartSuspended = true;

            m_ThreadPool = new SmartThreadPool(startInfo);
            m_ThreadPool.Start();
        }

        private class DataserverRequest
        {
            public uint localID;
            public UUID itemID;

            public UUID ID;
            public string handle;

            public DateTime startTime;
            public Action<string> action;
        }

        public string RequestWithImediatePost(uint localID, UUID itemID, string reply)
        {
            string ID = UUID.Random().ToString();
            m_CmdManager.m_ScriptEngine.PostObjectEvent(localID,
                    new EventParams("dataserver", new Object[]
                            { new LSL_Types.LSLString(ID),
                            new LSL_Types.LSLString(reply)},
                    new DetectParams[0]));
            return ID;
        }

        //legacy
        public UUID RegisterRequest(uint localID, UUID itemID, string identifier)
        {
            lock (DataserverRequests)
            {
                if (DataserverRequests.ContainsKey(identifier))
                    return UUID.Zero;

                DataserverRequest ds = new DataserverRequest()
                {
                    localID = localID,
                    itemID = itemID,

                    ID = UUID.Random(),
                    handle = identifier,

                    startTime = DateTime.UtcNow,
                    action = null
                };

                DataserverRequests[identifier] = ds;
                return ds.ID;
            }
        }

        // action, if provided, is executed async
        // its code pattern should be:
        //Action<string> act = eventID =>
        //{
        //     need operations to get reply string
        //  m_AsyncCommands.DataserverPlugin.DataserverReply(eventID, reply);
        //}
        // eventID is the event id, provided by this on Invoque
        // see ProcessActions below

        // temporary don't use
        public UUID RegisterRequest(uint localID, UUID itemID, string identifier, Action<string> action)
        {
            lock (DataserverRequests)
            {
                if (DataserverRequests.ContainsKey(identifier))
                    return UUID.Zero;

                DataserverRequest ds = new DataserverRequest()
                {
                    localID = localID,
                    itemID = itemID,

                    ID = UUID.Random(),
                    handle = identifier,

                    startTime = DateTime.UtcNow,
                    action = action
                };

                DataserverRequests[identifier] = ds;
                if (action != null)
                    m_ThreadPool.QueueWorkItem((WorkItemCallback)ProcessActions, identifier);

                return ds.ID;
            }
        }

        public UUID RegisterRequest(uint localID, UUID itemID, Action<string> action)
        {
            lock (DataserverRequests)
            {
                string identifier = UUID.Random().ToString();

                DataserverRequest ds = new DataserverRequest()
                {
                    localID = localID,
                    itemID = itemID,

                    ID = UUID.Random(),
                    handle = identifier,

                    startTime = DateTime.MaxValue,
                    action = action
                };

                DataserverRequests[identifier] = ds;
                if (action != null)
                    m_ThreadPool.QueueWorkItem((WorkItemCallback)ProcessActions, identifier);

                return ds.ID;
            }
        }


        public object ProcessActions(object st)
        {
            string id = st as string;
            if(string.IsNullOrEmpty(id))
                return null;

            DataserverRequest ds = null;
            lock (DataserverRequests)
            {
                if (!DataserverRequests.TryGetValue(id, out ds))
                    return null;
            }

            if (ds == null || ds.action == null)
                return null;
            try
            {
                ds.action.Invoke(ds.handle);
            }
            catch { }

            ds.action = null;
            lock (DataserverRequests)
            {
                if (DataserverRequests.TryGetValue(id, out ds))
                    DataserverRequests.Remove(id);
            }

            return null;
        }

        //legacy ?
        public void DataserverReply(string identifier, string reply)
        {
            DataserverRequest ds;

            lock (DataserverRequests)
            {
                if (!DataserverRequests.ContainsKey(identifier))
                    return;

                ds = DataserverRequests[identifier];
                DataserverRequests.Remove(identifier);
            }

            m_CmdManager.m_ScriptEngine.PostObjectEvent(ds.localID,
                    new EventParams("dataserver", new Object[]
                            { new LSL_Types.LSLString(ds.ID.ToString()),
                            new LSL_Types.LSLString(reply)},
                    new DetectParams[0]));
        }

        public void RemoveEvents(uint localID, UUID itemID)
        {
            lock (DataserverRequests)
            {
                List<string> toremove = new List<string>(DataserverRequests.Count);
                foreach (DataserverRequest ds in DataserverRequests.Values)
                {
                    if (ds.itemID == itemID)
                        toremove.Add(ds.handle);
                }
                foreach (string s in toremove)
                {
                    DataserverRequests.Remove(s);
                }
            }
        }

        public void ExpireRequests()
        {
            lock (DataserverRequests)
            {
                List<string> toremove = new List<string>(DataserverRequests.Count);
                DateTime expirebase = DateTime.UtcNow.AddSeconds(-30);
                foreach (DataserverRequest ds in DataserverRequests.Values)
                {
                    if (ds.action == null && ds.startTime < expirebase)
                        toremove.Add(ds.handle);
                }
                foreach (string s in toremove)
                {
                    DataserverRequests.Remove(s);
                }
            }
        }
    }
}
