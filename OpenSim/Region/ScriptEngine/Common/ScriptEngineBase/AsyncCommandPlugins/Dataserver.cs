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
using libsecondlife;

namespace OpenSim.Region.ScriptEngine.Common.ScriptEngineBase.AsyncCommandPlugins

{
    public class Dataserver
    {
        public AsyncCommandManager m_CmdManager;

        private Dictionary<string, DataserverRequest> DataserverRequests =
                new Dictionary<string, DataserverRequest>();

        public Dataserver(AsyncCommandManager CmdManager)
        {
            m_CmdManager = CmdManager;
        }

        private class DataserverRequest
        {
            public uint localID;
            public LLUUID itemID;

            public LLUUID ID;
            public string handle;

            public DateTime startTime;
        }

        public LLUUID RegisterRequest(uint localID, LLUUID itemID,
                string identifier)
        {
            lock (DataserverRequests)
            {
                if (DataserverRequests.ContainsKey(identifier))
                    return LLUUID.Zero;

                DataserverRequest ds = new DataserverRequest();

                ds.localID = localID;
                ds.itemID = itemID;

                ds.ID = LLUUID.Random();
                ds.handle = identifier;

                ds.startTime = DateTime.Now;

                DataserverRequests[identifier]=ds;

                return ds.ID;
            }
        }

        public void DataserverReply(string identifier, string reply)
        {
            DataserverRequest ds;

            lock (DataserverRequests)
            {
                if (!DataserverRequests.ContainsKey(identifier))
                    return;

                ds=DataserverRequests[identifier];
                DataserverRequests.Remove(identifier);
            }

            m_CmdManager.m_ScriptEngine.m_EventQueueManager.AddToObjectQueue(
                    ds.localID, "dataserver", EventQueueManager.llDetectNull,
                    new Object[] { new LSL_Types.LSLString(ds.ID.ToString()),
                    new LSL_Types.LSLString(reply)});
        }

        public void RemoveEvents(uint localID, LLUUID itemID)
        {
            lock (DataserverRequests)
            {
                foreach (DataserverRequest ds in new List<DataserverRequest>(DataserverRequests.Values))
                {
                    if (ds.itemID == itemID)
                        DataserverRequests.Remove(ds.handle);
                }
            }
        }

        public void ExpireRequests()
        {
            lock (DataserverRequests)
            {
                foreach (DataserverRequest ds in new List<DataserverRequest>(DataserverRequests.Values))
                {
                    if (ds.startTime > DateTime.Now.AddSeconds(30))
                        DataserverRequests.Remove(ds.handle);
                }
            }
        }
    }
}
