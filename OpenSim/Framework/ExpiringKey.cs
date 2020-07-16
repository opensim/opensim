/*
 * Copyright (c) 2008, openmetaverse.org, http://opensimulator.org/
 * All rights reserved.
 *
 * - Redistribution and use in source and binary forms, with or without
 *   modification, are permitted provided that the following conditions are met:
 *
 * - Redistributions of source code must retain the above copyright notice, this
 *   list of conditions and the following disclaimer.
 * - Neither the name of the openmetaverse.org nor the names
 *   of its contributors may be used to endorse or promote products derived from
 *   this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
 * AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
 * IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
 * ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE
 * LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
 * CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
 * SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
 * INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
 * CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
 * ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
 * POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Timers;
using System.Threading;
using System.Collections.Generic;
using Timer = System.Timers.Timer;

namespace OpenSim.Framework
{
    public class ExpiringKey<Tkey1> : IDisposable
    {
        private Dictionary<Tkey1, int> m_dictionary;

        private ReaderWriterLockSlim m_rwLock = new ReaderWriterLockSlim();
        private readonly double m_startTS;
        private readonly int expire;
        private Timer m_purgeTimer;

        public ExpiringKey(int expireTimeinMS)
        {
            m_dictionary = new Dictionary<Tkey1, int>();
            m_startTS = Util.GetTimeStampMS();
            expire = expireTimeinMS;
            if(expire < 500)
                expire = 500;
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private void SetTimer()
        {
            if (m_purgeTimer == null)
            {
                m_purgeTimer = new Timer()
                {
                    Interval = expire,
                    AutoReset = false // time drift is not a issue.

                };
                m_purgeTimer.Elapsed += Purge;
                m_purgeTimer.Start();
            }
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private void DisposeTimer()
        {
            if (m_purgeTimer != null)
            {
                m_purgeTimer.Stop();
                m_purgeTimer.Dispose();
                m_purgeTimer = null;
            }
        }

        ~ExpiringKey()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public void Dispose(bool disposing)
        {
            if (m_rwLock != null)
            {
                m_rwLock.Dispose();
                m_rwLock = null;
                DisposeTimer();
            }
        }

        private void Purge(object source, ElapsedEventArgs e)
        {
            if (m_dictionary.Count == 0)
            {
                DisposeTimer();
                return;
            }

            bool gotLock = false;
            int now = (int)(Util.GetTimeStampMS() - m_startTS);

            try
            {
                try { }
                finally
                {
                    m_rwLock.EnterWriteLock();
                    gotLock = true;
                }
                List<Tkey1> expired = new List<Tkey1>(m_dictionary.Count);
                foreach(KeyValuePair<Tkey1,int> kvp in m_dictionary)
                {
                    if(kvp.Value < now)
                        expired.Add(kvp.Key);
                }
                foreach(Tkey1 key in expired)
                    m_dictionary.Remove(key);
                if(m_dictionary.Count == 0)
                    DisposeTimer();
                else
                    m_purgeTimer.Start();
            }
            finally
            {
                if (gotLock)
                    m_rwLock.ExitWriteLock();
            }
        }

        public void Add(Tkey1 key)
        {
            bool gotLock = false;
            int now = (int)(Util.GetTimeStampMS() - m_startTS) + expire;

            try
            {
                // Avoid an asynchronous Thread.Abort() from possibly never existing an acquired lock by placing
                // the acquision inside the main try.  The inner finally block is needed because thread aborts cannot
                // interrupt code in these blocks (hence gotLock is guaranteed to be set correctly).
                try { }
                finally
                {
                    m_rwLock.EnterWriteLock();
                    gotLock = true;
                }

                m_dictionary[key] = now;
                SetTimer();
            }
            finally
            {
                if (gotLock)
                    m_rwLock.ExitWriteLock();
            }
        }

        public void Add(Tkey1 key, int expireMS)
        {
            bool gotLock = false;
            int now;
            if (expireMS > 500)
                now = (int)(Util.GetTimeStampMS() - m_startTS) + expire;
            else
                now = (int)(Util.GetTimeStampMS() - m_startTS) + 500;

            try
            {
                // Avoid an asynchronous Thread.Abort() from possibly never existing an acquired lock by placing
                // the acquision inside the main try.  The inner finally block is needed because thread aborts cannot
                // interrupt code in these blocks (hence gotLock is guaranteed to be set correctly).
                try { }
                finally
                {
                    m_rwLock.EnterWriteLock();
                    gotLock = true;
                }

                m_dictionary[key] = now;
                SetTimer();
            }
            finally
            {
                if (gotLock)
                    m_rwLock.ExitWriteLock();
            }
        }

        public bool Remove(Tkey1 key)
        {
            bool success;
            bool gotLock = false;

            try
            {
                // Avoid an asynchronous Thread.Abort() from possibly never existing an acquired lock by placing
                // the acquision inside the main try.  The inner finally block is needed because thread aborts cannot
                // interrupt code in these blocks (hence gotLock is guaranteed to be set correctly).
                try {}
                finally
                {
                    m_rwLock.EnterWriteLock();
                    gotLock = true;
                }
                success = m_dictionary.Remove(key);
                if(m_dictionary.Count == 0)
                    DisposeTimer();
            }
            finally
            {
                if (gotLock)
                    m_rwLock.ExitWriteLock();
            }

            return success;
        }


        public void Clear()
        {
            bool gotLock = false;

            try
            {
                // Avoid an asynchronous Thread.Abort() from possibly never existing an acquired lock by placing
                // the acquision inside the main try.  The inner finally block is needed because thread aborts cannot
                // interrupt code in these blocks (hence gotLock is guaranteed to be set correctly).
                try {}
                finally
                {
                    m_rwLock.EnterWriteLock();
                    gotLock = true;
                    m_dictionary.Clear();
                    DisposeTimer();
                }
            }
            finally
            {
                if (gotLock)
                    m_rwLock.ExitWriteLock();
            }
        }

        public int Count
        {
            get { return m_dictionary.Count; }
        }

        public bool ContainsKey(Tkey1 key)
        {
            return m_dictionary.ContainsKey(key);
        }

        public bool TryGetValue(Tkey1 key, out int value)
        {
            bool success;
            bool gotLock = false;

            try
            {
                // Avoid an asynchronous Thread.Abort() from possibly never existing an acquired lock by placing
                // the acquision inside the main try.  The inner finally block is needed because thread aborts cannot
                // interrupt code in these blocks (hence gotLock is guaranteed to be set correctly).
                try {}
                finally
                {
                    m_rwLock.EnterReadLock();
                    gotLock = true;
                }

                success = m_dictionary.TryGetValue(key, out value);
            }
            finally
            {
                if (gotLock)
                    m_rwLock.ExitReadLock();
            }

            return success;
        }
    }
}