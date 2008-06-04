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

namespace OpenSim.Region.ScriptEngine.XEngine.AsyncCommandPlugins
{
    public class Timer
    {
        public AsyncCommandManager m_CmdManager;

        public Timer(AsyncCommandManager CmdManager)
        {
            m_CmdManager = CmdManager;
        }

        //
        // TIMER
        //
        private class TimerClass
        {
            public uint localID;
            public LLUUID itemID;
            //public double interval;
            public long interval;
            //public DateTime next;
            public long next;
        }

        private List<TimerClass> Timers = new List<TimerClass>();
        private object TimerListLock = new object();

        public void SetTimerEvent(uint m_localID, LLUUID m_itemID, double sec)
        {
            Console.WriteLine("SetTimerEvent");

            // Always remove first, in case this is a re-set
            UnSetTimerEvents(m_localID, m_itemID);
            if (sec == 0) // Disabling timer
                return;

            // Add to timer
            TimerClass ts = new TimerClass();
            ts.localID = m_localID;
            ts.itemID = m_itemID;
            ts.interval = Convert.ToInt64(sec * 10000000); // How many 100 nanoseconds (ticks) should we wait
            //       2193386136332921 ticks
            //       219338613 seconds

            //ts.next = DateTime.Now.ToUniversalTime().AddSeconds(ts.interval);
            ts.next = DateTime.Now.Ticks + ts.interval;
            lock (TimerListLock)
            {
                Timers.Add(ts);
            }
        }

        public void UnSetTimerEvents(uint m_localID, LLUUID m_itemID)
        {
            // Remove from timer
            lock (TimerListLock)
            {
                foreach (TimerClass ts in new ArrayList(Timers))
                {
                    if (ts.localID == m_localID && ts.itemID == m_itemID)
                        Timers.Remove(ts);
                }
            }
        }

        public void CheckTimerEvents()
        {
            // Nothing to do here?
            if (Timers.Count == 0)
                return;

            lock (TimerListLock)
            {
                // Go through all timers
                foreach (TimerClass ts in Timers)
                {
                    // Time has passed?
                    if (ts.next < DateTime.Now.Ticks)
                    {
                        //                        Console.WriteLine("Time has passed: Now: " + DateTime.Now.Ticks + ", Passed: " + ts.next);
                        // Add it to queue
                        m_CmdManager.m_ScriptEngine.PostScriptEvent(ts.itemID,
                                new XEventParams("timer", new Object[0],
                                new XDetectParams[0]));
                        // set next interval

                        //ts.next = DateTime.Now.ToUniversalTime().AddSeconds(ts.interval);
                        ts.next = DateTime.Now.Ticks + ts.interval;
                    }
                }
            }
        }

        public Object[] GetSerializationData(LLUUID itemID)
        {
            List<Object> data = new List<Object>();

            lock (TimerListLock)
            {
                foreach (TimerClass ts in Timers)
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

        public void CreateFromData(uint localID, LLUUID itemID, LLUUID objectID,
                                   Object[] data)
        {
            int idx = 0;

            while (idx < data.Length)
            {
                TimerClass ts = new TimerClass();

                ts.localID = localID;
                ts.itemID = itemID;
                ts.interval = (long)data[idx];
                ts.next = DateTime.Now.Ticks + (long)data[idx+1];
                idx += 2;

                Timers.Add(ts);
            }
        }
    }
}
