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
using OpenMetaverse;
using OpenSim.Region.ScriptEngine.Shared.Api;

namespace OpenSim.Region.ScriptEngine.Shared.Api.Plugins
{
    public class Timer
    {
        public class TimerInfo
        {
            public uint localID;
            public UUID itemID;
            //public double interval;
            public long interval;
            //public DateTime next;
            public long next;

            public TimerInfo Clone()
            {
                return (TimerInfo)this.MemberwiseClone();
            }
        }

        public AsyncCommandManager m_CmdManager;

        public int TimersCount
        {
            get
            {
                lock (TimerListLock)
                    return Timers.Count;
            }
        }

        public Timer(AsyncCommandManager CmdManager)
        {
            m_CmdManager = CmdManager;
        }

        //
        // TIMER
        //
        static private string MakeTimerKey(uint localID, UUID itemID)
        {
            return localID.ToString() + itemID.ToString();
        }

        private Dictionary<string,TimerInfo> Timers = new Dictionary<string,TimerInfo>();
        private object TimerListLock = new object();

        public void SetTimerEvent(uint m_localID, UUID m_itemID, double sec)
        {
            if (sec == 0) // Disabling timer
            {
                UnSetTimerEvents(m_localID, m_itemID);
                return;
            }

            // Add to timer
            TimerInfo ts = new TimerInfo();
            ts.localID = m_localID;
            ts.itemID = m_itemID;
            ts.interval = Convert.ToInt64(sec * 10000000); // How many 100 nanoseconds (ticks) should we wait
            //       2193386136332921 ticks
            //       219338613 seconds

            //ts.next = DateTime.Now.ToUniversalTime().AddSeconds(ts.interval);
            ts.next = DateTime.Now.Ticks + ts.interval;

            string key = MakeTimerKey(m_localID, m_itemID);
            lock (TimerListLock)
            {
                // Adds if timer doesn't exist, otherwise replaces with new timer
                Timers[key] = ts;
            }
        }

        public void UnSetTimerEvents(uint m_localID, UUID m_itemID)
        {
            // Remove from timer
            string key = MakeTimerKey(m_localID, m_itemID);
            lock (TimerListLock)
            {
                if (Timers.TryGetValue(key, out TimerInfo ts))
                {
                    m_CmdManager.m_ScriptEngine.CancelScriptEvent(ts.itemID, "timer");
                    Timers.Remove(key);
                }
            }
        }

        public void CheckTimerEvents()
        {
            // Nothing to do here?
            if (Timers.Count == 0)
                return;

            Dictionary<string, TimerInfo> tvals;
            lock (TimerListLock)
            {
                // Go through all timers
                tvals = new Dictionary<string, TimerInfo>(Timers);
            }

            foreach (TimerInfo ts in tvals.Values)
            {
                // Time has passed?
                if (ts.next < DateTime.Now.Ticks)
                {
                    //m_log.Debug("Time has passed: Now: " + DateTime.Now.Ticks + ", Passed: " + ts.next);
                    // Add it to queue
                    m_CmdManager.m_ScriptEngine.PostScriptEvent(ts.itemID,
                            new EventParams("timer", new Object[0],
                            new DetectParams[0]));
                    // set next interval

                    //ts.next = DateTime.Now.ToUniversalTime().AddSeconds(ts.interval);
                    ts.next = DateTime.Now.Ticks + ts.interval;
                }
            }
        }

        public Object[] GetSerializationData(UUID itemID)
        {
            List<Object> data = new List<Object>();

            lock (TimerListLock)
            {
                Dictionary<string, TimerInfo>.ValueCollection tvals = Timers.Values;
                foreach (TimerInfo ts in tvals)
                {
                    if (ts.itemID == itemID)
                    {
                        data.Add(ts.interval);
                        data.Add(ts.next-DateTime.Now.Ticks);
                    }
                }
            }
            return data.ToArray();
        }

        public void CreateFromData(uint localID, UUID itemID, UUID objectID,
                                   Object[] data)
        {
            int idx = 0;

            while (idx < data.Length)
            {
                TimerInfo ts = new TimerInfo();

                ts.localID = localID;
                ts.itemID = itemID;
                ts.interval = (long)data[idx];
                ts.next = DateTime.Now.Ticks + (long)data[idx+1];
                idx += 2;

                lock (TimerListLock)
                {
                    Timers.Add(MakeTimerKey(localID, itemID), ts);
                }
            }
        }

        public List<TimerInfo> GetTimersInfo()
        {
            List<TimerInfo> retList = new List<TimerInfo>();

            lock (TimerListLock)
            {
                foreach (TimerInfo i in Timers.Values)
                    retList.Add(i.Clone());
            }

            return retList;
        }
    }
}
