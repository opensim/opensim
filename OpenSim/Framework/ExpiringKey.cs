/*
 * Copyright (c) Contributors, http://opensimulator.org/
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
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
using System.Threading;
using System.Collections.Generic;
using Timer = System.Threading.Timer ;

namespace OpenSim.Framework
{
    public sealed class ExpiringKey<Tkey1> : IDisposable
    {
        private const int MINEXPIRECHECK = 500;

        private Timer m_purgeTimer;
        private ReaderWriterLockSlim m_rwLock;
        private readonly Dictionary<Tkey1, int> m_dictionary;
        private readonly double m_startTS;
        private readonly int m_expire;

        public ExpiringKey()
        {
            m_dictionary = new Dictionary<Tkey1, int>();
            m_rwLock = new ReaderWriterLockSlim();
            m_expire = MINEXPIRECHECK;
            m_startTS = Util.GetTimeStampMS();
        }

        public ExpiringKey(int expireCheckTimeinMS)
        {
            m_dictionary = new Dictionary<Tkey1, int>();
            m_rwLock = new ReaderWriterLockSlim();
            m_startTS = Util.GetTimeStampMS();
            m_expire = (expireCheckTimeinMS > MINEXPIRECHECK) ? m_expire = expireCheckTimeinMS : MINEXPIRECHECK;
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private void CheckTimer()
        {
            m_purgeTimer ??= new Timer(Purge, null, m_expire, Timeout.Infinite);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private void DisposeTimer()
        {
            if (m_purgeTimer != null)
            {
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

        private void Dispose(bool disposing)
        {
            if (m_rwLock != null)
            {
                DisposeTimer();
                m_rwLock.Dispose();
                m_rwLock = null;
            }
        }

        private void Purge(object ignored)
        {
            m_rwLock.EnterUpgradeableReadLock();
            try
            {
                if (m_dictionary.Count == 0)
                {
                    DisposeTimer();
                    return;
                }

                int now = (int)(Util.GetTimeStampMS() - m_startTS);
                List<Tkey1> expired = new List<Tkey1>(m_dictionary.Count);
                foreach(KeyValuePair<Tkey1,int> kvp in m_dictionary)
                {
                    int expire = kvp.Value;
                    if (expire > 0 && expire < now)
                        expired.Add(kvp.Key);
                }

                if (expired.Count > 0)
                {
                    m_rwLock.EnterWriteLock();
                    try
                    {
                        foreach (Tkey1 key in expired)
                            m_dictionary.Remove(key);
                    }
                    finally { m_rwLock.ExitWriteLock(); }

                    if (m_dictionary.Count == 0)
                        DisposeTimer();
                    else
                        m_purgeTimer.Change(m_expire, Timeout.Infinite);
                }
                else
                    m_purgeTimer.Change(m_expire, Timeout.Infinite);
            }
            finally { m_rwLock.ExitUpgradeableReadLock(); }
        }

        public void Add(Tkey1 key)
        {
            int now = (int)(Util.GetTimeStampMS() - m_startTS) + m_expire;

            m_rwLock.EnterWriteLock();
            try
            {
                m_dictionary[key] = now;
                CheckTimer();
            }
            finally { m_rwLock.ExitWriteLock(); }
        }

        public void Add(Tkey1 key, int expireMS)
        {
            int now;
            if (expireMS > 0)
            {
                expireMS = (expireMS > m_expire) ? expireMS : m_expire;
                now = (int)(Util.GetTimeStampMS() - m_startTS) + expireMS;
            }
            else
                now = int.MinValue;

            m_rwLock.EnterWriteLock();
            try
            {
                m_dictionary[key] = now;
                CheckTimer();
            }
            finally { m_rwLock.ExitWriteLock(); }
        }

        public bool Remove(Tkey1 key)
        {
            m_rwLock.EnterWriteLock();
            try
            {
                bool success = m_dictionary.Remove(key);
                if(m_dictionary.Count == 0)
                    DisposeTimer();
                return success;
            }
            finally { m_rwLock.ExitWriteLock(); }

        }

        public void Clear()
        {
            m_rwLock.EnterWriteLock();
            try
            {
                m_dictionary.Clear();
                DisposeTimer();
            }
            finally { m_rwLock.ExitWriteLock(); }
        }

        public int Count
        {
            get { return m_dictionary.Count; }
        }

        public bool ContainsKey(Tkey1 key)
        {
            m_rwLock.EnterReadLock();
            try
            {
                return m_dictionary.ContainsKey(key);
            }
            finally { m_rwLock.ExitReadLock(); }
        }

        public bool ContainsKey(Tkey1 key, int expireMS)
        {
            m_rwLock.EnterUpgradeableReadLock();
            try
            {
                if (m_dictionary.ContainsKey(key))
                {
                    m_rwLock.EnterWriteLock();
                    try
                    {
                        int now;
                        if (expireMS > 0)
                        {
                            expireMS = (expireMS > m_expire) ? expireMS : m_expire;
                            now = (int)(Util.GetTimeStampMS() - m_startTS) + expireMS;
                        }
                        else
                            now = int.MinValue;

                        m_dictionary[key] = now;
                        return true;
                    }
                    finally { m_rwLock.ExitWriteLock(); }
                }
                return false;
            }
            finally { m_rwLock.EnterUpgradeableReadLock(); }
        }

        public bool TryGetValue(Tkey1 key, out int value)
        {
            m_rwLock.EnterReadLock();
            try
            {
                return m_dictionary.TryGetValue(key, out value);
            }
            finally { m_rwLock.ExitReadLock(); }
        }
    }
}