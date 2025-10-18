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

// this is a lighter alternative to libomv, no sliding option

using System;
using System.Threading;
using System.Collections.Generic;
using Timer = System.Threading.Timer;
using System.Runtime.InteropServices;

namespace OpenSim.Framework
{
    public sealed class ExpiringCacheOS<TKey1, TValue1> : IDisposable
    {
        private const int MINEXPIRECHECK = 500;

        private Timer m_purgeTimer;
        private ReaderWriterLockSlim m_rwLock;
        private readonly Dictionary<TKey1, int> m_expireControl;
        private readonly Dictionary<TKey1, TValue1> m_values;
        TValue1[] valuesArrayCache = null;
        private readonly double m_startTS;
        private readonly int m_expire;

        public ExpiringCacheOS()
        {
            m_expireControl = new Dictionary<TKey1, int>();
            m_values = new Dictionary<TKey1, TValue1>();
            m_rwLock = new ReaderWriterLockSlim();
            m_expire = MINEXPIRECHECK;
            m_startTS = Util.GetTimeStampMS();
        }

        public ExpiringCacheOS(int expireCheckTimeinMS)
        {
            m_expireControl = new Dictionary<TKey1, int>();
            m_values = new Dictionary<TKey1, TValue1>();
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

        ~ExpiringCacheOS()
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
                if (m_expireControl.Count == 0)
                {
                    DisposeTimer();
                    return;
                }

                int now = (int)(Util.GetTimeStampMS() - m_startTS);
                List<TKey1> expired = new List<TKey1>(m_expireControl.Count);
                foreach(KeyValuePair<TKey1, int> kvp in m_expireControl)
                {
                    int expire = kvp.Value;
                    if(expire > 0 && expire < now)
                        expired.Add(kvp.Key);
                }

                if(expired.Count > 0)
                {
                    m_rwLock.EnterWriteLock();
                    try
                    {
                        valuesArrayCache = null;
                        foreach (TKey1 key in expired)
                        {
                            m_expireControl.Remove(key);
                            m_values.Remove(key);
                        }
                    }
                    finally { m_rwLock.ExitWriteLock(); }

                    if (m_expireControl.Count == 0)
                        DisposeTimer();
                    else
                        m_purgeTimer.Change(m_expire, Timeout.Infinite);
                }
                else
                    m_purgeTimer.Change(m_expire, Timeout.Infinite);
            }
            finally { m_rwLock.ExitUpgradeableReadLock(); }
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void AddOrUpdate(TKey1 key, TValue1 val)
        {
            Add(key, val);
        }

        public void Add(TKey1 key, TValue1 val)
        {
            int now = (int)(Util.GetTimeStampMS() - m_startTS) + m_expire;

            m_rwLock.EnterWriteLock();
            try
            {
                m_expireControl[key] = now;
                m_values[key] = val;
                valuesArrayCache = null;
                CheckTimer();
            }
            finally { m_rwLock.ExitWriteLock(); }
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void AddOrUpdate(TKey1 key, TValue1 val, int expireSeconds)
        {
            Add(key, val, expireSeconds * 1000);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void AddOrUpdate(TKey1 key, TValue1 val, double expireSeconds)
        {
            Add(key, val, (int)(expireSeconds * 1000));
        }

        public void Add(TKey1 key, TValue1 val, int expireMS)
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
                m_expireControl[key] = now;
                m_values[key] = val;
                valuesArrayCache = null;
                CheckTimer();
            }
            finally { m_rwLock.ExitWriteLock(); }
        }

        public bool Remove(TKey1 key)
        {
            m_rwLock.EnterWriteLock();
            try
            {
                bool success = m_expireControl.Remove(key);
                success |= m_values.Remove(key);
                if(success)
                    valuesArrayCache = null;
                if (m_expireControl.Count == 0)
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
                DisposeTimer();
                m_expireControl.Clear();
                m_values.Clear();
                valuesArrayCache = null;
            }
            finally { m_rwLock.ExitWriteLock(); }
        }

        public int Count
        {
            get { return m_expireControl.Count; }
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public bool Contains(TKey1 key)
        {
            return ContainsKey(key);
        }

        public bool ContainsKey(TKey1 key)
        {
            m_rwLock.EnterReadLock();
            try
            {
                return m_expireControl.ContainsKey(key);
            }
            finally { m_rwLock.ExitReadLock(); }
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public bool Contains(TKey1 key, int expireMS)
        {
            return ContainsKey(key, expireMS);
        }

        public bool ContainsKey(TKey1 key, int expireMS)
        {
            m_rwLock.EnterUpgradeableReadLock();
            try
            {
                if(m_expireControl.ContainsKey(key))
                {
                    m_rwLock.EnterWriteLock();
                    try
                    {
                        int now;
                        if(expireMS > 0)
                        {
                            expireMS = (expireMS > m_expire) ? expireMS : m_expire;
                            now = (int)(Util.GetTimeStampMS() - m_startTS) + expireMS;
                        }
                        else
                            now = int.MinValue;

                        m_expireControl[key] = now;
                        return true;
                    }
                    finally { m_rwLock.ExitWriteLock(); }
                }
                return false;
            }
            finally { m_rwLock.ExitUpgradeableReadLock(); }
        }

        public bool TryGetValue(TKey1 key, out TValue1 value)
        {
            m_rwLock.EnterReadLock();
            try
            {
                return m_values.TryGetValue(key, out value);
            }
            finally { m_rwLock.ExitReadLock(); }
        }

        public bool TryGetValue(TKey1 key, int expireMS, out TValue1 value)
        {
            m_rwLock.EnterUpgradeableReadLock();
            try
            {
                if(m_values.TryGetValue(key, out value))
                {
                    m_rwLock.EnterWriteLock();
                    try
                    {
                        int now;
                        if(expireMS > 0)
                        {
                            expireMS = (expireMS > m_expire) ? expireMS : m_expire;
                            now = (int)(Util.GetTimeStampMS() - m_startTS) + expireMS;
                        }
                        else
                            now = int.MinValue;

                        m_expireControl[key] = now;
                        return true;
                    }
                    finally { m_rwLock.ExitWriteLock(); }
                }
                return false;
            }
            finally { m_rwLock.ExitUpgradeableReadLock(); }
        }

        public ref TValue1 TryGetOrDefaultValue(TKey1 key, out bool existed)
        {
            m_rwLock.ExitUpgradeableReadLock();
            try
            {
                return ref CollectionsMarshal.GetValueRefOrAddDefault(m_values, key, out existed);
            }
            finally { m_rwLock.ExitUpgradeableReadLock(); }
        }

        public ref TValue1 TryGetOrDefaultValue(TKey1 key, int expireMS, out bool existed)
        {
            m_rwLock.EnterWriteLock();
            try
            {
                ref TValue1 ret = ref CollectionsMarshal.GetValueRefOrAddDefault(m_values, key, out existed);
                int now;
                if (expireMS > 0)
                {
                    expireMS = (expireMS > m_expire) ? expireMS : m_expire;
                    now = (int)(Util.GetTimeStampMS() - m_startTS) + expireMS;
                }
                else
                    now = int.MinValue;

                m_expireControl[key] = now;
                return ref ret;
            }
            finally { m_rwLock.EnterWriteLock(); }
        }

        public TValue1[] Values
        {
            get
            {
                m_rwLock.EnterUpgradeableReadLock();
                try
                {
                    if(valuesArrayCache == null)
                    {
                        valuesArrayCache = new TValue1[m_values.Count];
                        m_values.Values.CopyTo(valuesArrayCache, 0);
                    }
                    return valuesArrayCache;
                }
                finally { m_rwLock.ExitUpgradeableReadLock(); }
            }
        }

        /*
        public ICollection<TKey1> Keys
        {
            get
            {
                m_rwLock.EnterUpgradeableReadLock();
                try
                {
                    return m_values.Keys;
                }
                finally { m_rwLock.ExitUpgradeableReadLock(); }
            }
        }
        */
    }
}