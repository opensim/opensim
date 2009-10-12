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
using System.Collections.Generic;
using System.Net;
using OpenSim.Framework;
using OpenMetaverse;

using ReaderWriterLockImpl = OpenMetaverse.ReaderWriterLockSlim;

namespace OpenSim.Region.ClientStack.LindenUDP
{
    public sealed class UDPClientCollection
    {
        Dictionary<UUID, LLUDPClient> Dictionary1;
        Dictionary<IPEndPoint, LLUDPClient> Dictionary2;
        LLUDPClient[] Array;
        ReaderWriterLockImpl rwLock = new ReaderWriterLockImpl();
        object m_sync = new object();

        public UDPClientCollection()
        {
            Dictionary1 = new Dictionary<UUID, LLUDPClient>();
            Dictionary2 = new Dictionary<IPEndPoint, LLUDPClient>();
            Array = new LLUDPClient[0];
        }

        public UDPClientCollection(int capacity)
        {
            Dictionary1 = new Dictionary<UUID, LLUDPClient>(capacity);
            Dictionary2 = new Dictionary<IPEndPoint, LLUDPClient>(capacity);
            Array = new LLUDPClient[0];
        }

        public void Add(UUID key1, IPEndPoint key2, LLUDPClient value)
        {
            //rwLock.EnterWriteLock();

            //try
            //{
            //    if (Dictionary1.ContainsKey(key1))
            //    {
            //        if (!Dictionary2.ContainsKey(key2))
            //            throw new ArgumentException("key1 exists in the dictionary but not key2");
            //    }
            //    else if (Dictionary2.ContainsKey(key2))
            //    {
            //        if (!Dictionary1.ContainsKey(key1))
            //            throw new ArgumentException("key2 exists in the dictionary but not key1");
            //    }

            //    Dictionary1[key1] = value;
            //    Dictionary2[key2] = value;

            //    LLUDPClient[] oldArray = Array;
            //    int oldLength = oldArray.Length;

            //    LLUDPClient[] newArray = new LLUDPClient[oldLength + 1];
            //    for (int i = 0; i < oldLength; i++)
            //        newArray[i] = oldArray[i];
            //    newArray[oldLength] = value;

            //    Array = newArray;
            //}
            //finally { rwLock.ExitWriteLock(); }

            lock (m_sync)
            {
                if (Dictionary1.ContainsKey(key1))
                {
                    if (!Dictionary2.ContainsKey(key2))
                        throw new ArgumentException("key1 exists in the dictionary but not key2");
                }
                else if (Dictionary2.ContainsKey(key2))
                {
                    if (!Dictionary1.ContainsKey(key1))
                        throw new ArgumentException("key2 exists in the dictionary but not key1");
                }

                Dictionary1[key1] = value;
                Dictionary2[key2] = value;

                LLUDPClient[] oldArray = Array;
                int oldLength = oldArray.Length;

                LLUDPClient[] newArray = new LLUDPClient[oldLength + 1];
                for (int i = 0; i < oldLength; i++)
                    newArray[i] = oldArray[i];
                newArray[oldLength] = value;

                Array = newArray;
            }

        }

        public bool Remove(UUID key1, IPEndPoint key2)
        {
            //rwLock.EnterWriteLock();

            //try
            //{
            //    LLUDPClient value;
            //    if (Dictionary1.TryGetValue(key1, out value))
            //    {
            //        Dictionary1.Remove(key1);
            //        Dictionary2.Remove(key2);

            //        LLUDPClient[] oldArray = Array;
            //        int oldLength = oldArray.Length;

            //        LLUDPClient[] newArray = new LLUDPClient[oldLength - 1];
            //        int j = 0;
            //        for (int i = 0; i < oldLength; i++)
            //        {
            //            if (oldArray[i] != value)
            //                newArray[j++] = oldArray[i];
            //        }

            //        Array = newArray;
            //        return true;
            //    }
            //}
            //finally { rwLock.ExitWriteLock(); }

            //return false;

            lock (m_sync)
            {
                LLUDPClient value;
                if (Dictionary1.TryGetValue(key1, out value))
                {
                    Dictionary1.Remove(key1);
                    Dictionary2.Remove(key2);

                    LLUDPClient[] oldArray = Array;
                    int oldLength = oldArray.Length;

                    LLUDPClient[] newArray = new LLUDPClient[oldLength - 1];
                    int j = 0;
                    for (int i = 0; i < oldLength; i++)
                    {
                        if (oldArray[i] != value)
                            newArray[j++] = oldArray[i];
                    }

                    Array = newArray;
                    return true;
                }
            }

            return false;

        }

        public void Clear()
        {
            //rwLock.EnterWriteLock();

            //try
            //{
            //    Dictionary1.Clear();
            //    Dictionary2.Clear();
            //    Array = new LLUDPClient[0];
            //}
            //finally { rwLock.ExitWriteLock(); }

            lock (m_sync)
            {
                Dictionary1.Clear();
                Dictionary2.Clear();
                Array = new LLUDPClient[0];
            }

        }

        public int Count
        {
            get { return Array.Length; }
        }

        public bool ContainsKey(UUID key)
        {
            return Dictionary1.ContainsKey(key);
        }

        public bool ContainsKey(IPEndPoint key)
        {
            return Dictionary2.ContainsKey(key);
        }

        public bool TryGetValue(UUID key, out LLUDPClient value)
        {
            ////bool success;
            ////bool doLock = !rwLock.IsUpgradeableReadLockHeld;
            ////if (doLock) rwLock.EnterReadLock();

            ////try { success = Dictionary1.TryGetValue(key, out value); }
            ////finally { if (doLock) rwLock.ExitReadLock(); }

            ////return success;

            lock (m_sync)
                return Dictionary1.TryGetValue(key, out value); 

            //try
            //{
            //    return Dictionary1.TryGetValue(key, out value);
            //}
            //catch { }
            //value = null;
            //return false;
        }

        public bool TryGetValue(IPEndPoint key, out LLUDPClient value)
        {
            ////bool success;
            ////bool doLock = !rwLock.IsUpgradeableReadLockHeld;
            ////if (doLock) rwLock.EnterReadLock();

            ////try { success = Dictionary2.TryGetValue(key, out value); }
            ////finally { if (doLock) rwLock.ExitReadLock(); }

            ////return success;

            lock (m_sync)
                return Dictionary2.TryGetValue(key, out value);

            //try
            //{
            //    return Dictionary2.TryGetValue(key, out value);
            //}
            //catch { }
            //value = null;
            //return false;

        }

        public void ForEach(Action<LLUDPClient> action)
        {
            //bool doLock = !rwLock.IsUpgradeableReadLockHeld;
            //if (doLock) rwLock.EnterUpgradeableReadLock();

            //try { Parallel.ForEach<LLUDPClient>(Array, action); }
            //finally { if (doLock) rwLock.ExitUpgradeableReadLock(); }

            LLUDPClient[] localArray = null;
            lock (m_sync)
            {
                localArray = new LLUDPClient[Array.Length];
                Array.CopyTo(localArray, 0);
            }

            Parallel.ForEach<LLUDPClient>(localArray, action); 

        }
    }
}
