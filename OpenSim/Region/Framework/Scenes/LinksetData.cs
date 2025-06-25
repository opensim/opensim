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
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;

//using log4net;

namespace OpenSim.Region.Framework.Scenes
{
    public class LinksetData
    {
        public int m_MemoryLimit;
        public int m_MemoryUsed;
        private readonly object linksetDataLock = new();
        public Dictionary<string, LinksetDataEntry> Data;

        public LinksetData(int limit)
        {
            Data = new Dictionary<string, LinksetDataEntry>();

            m_MemoryLimit = limit;
            m_MemoryUsed = 0;
        }

        public LinksetData(int limit, int used)
        {
            Data = new Dictionary<string, LinksetDataEntry>();

            m_MemoryLimit = limit;
            m_MemoryUsed = used;
        }

        public LinksetData Copy()
        {
            lock (linksetDataLock)
            {
                var copy = new LinksetData(m_MemoryLimit, m_MemoryUsed);
                foreach (var entry in Data)
                {
                    LinksetDataEntry val = entry.Value.Copy();
                    copy.Data[entry.Key] = val;
                }
                return copy;
            }
        }

        /// <summary>
        /// Adds or updates a entry to linkset data with optional password
        /// </summary>
        /// <returns>
        /// return values must match values expected by LSL
        /// </returns>
        public int AddOrUpdate(string key, string value, string pass)
        {
            int deltaMem;
            lock (linksetDataLock)
            {
                if (Data.TryGetValue(key, out LinksetDataEntry entry))
                {
                    if (!entry.CheckPassword(pass))
                        return 3;

                    if (entry.Value == value)
                        return 5;

                    deltaMem = value.Length - entry.Value.Length;
                    if ((m_MemoryUsed + deltaMem) > m_MemoryLimit)
                        return 1;

                    m_MemoryUsed += deltaMem;
                    if(m_MemoryUsed < 0)
                        m_MemoryUsed = 0;

                    entry.Value = value;
                    return 0;
                }

                deltaMem = value.Length + key.Length;
                if (!string.IsNullOrEmpty(pass))
                    deltaMem += pass.Length;

                if ((m_MemoryUsed + deltaMem) > m_MemoryLimit)
                    return 1;

                m_MemoryUsed += deltaMem;
                Data[key] = new LinksetDataEntry()
                {
                    Value = value,
                    Password = pass
                };
                return 0;
            }
        }

        public int AddOrUpdate(string key, string value)
        {
            int deltaMem;
            lock (linksetDataLock)
            {
                if (Data.TryGetValue(key, out LinksetDataEntry entry))
                {
                    if (entry.IsProtected)
                        return 3;

                    if (entry.Value == value)
                        return 5;

                    deltaMem = value.Length - entry.Value.Length;
                    if ((m_MemoryUsed + deltaMem) > m_MemoryLimit)
                        return 1;
                    entry.Value = value;
                    m_MemoryUsed += deltaMem;
                    return 0;
                }

                deltaMem = value.Length + key.Length;
                if ((m_MemoryUsed + deltaMem) > m_MemoryLimit)
                    return 1;

                m_MemoryUsed += deltaMem;
                Data[key] = new LinksetDataEntry()
                {
                    Value = value,
                    Password = null
                };
                return 0;
            }
        }

        /// <summary>
        /// Deletes a named key from the key value store
        /// </summary>
        /// <param name="key">The key value we're removing</param>
        /// <param name="pass">The password for a protected field (or string.Empty if not protected)</param>
        /// <returns>
        /// return values must match values expected by LSL
        /// </returns>
        public int Remove(string key, string pass)
        {
            lock (linksetDataLock)
            {
                if (Data.Count <= 0)
                    return 4;

                if (!Data.TryGetValue(key, out LinksetDataEntry entry))
                    return 4;

                if (!entry.CheckPassword(pass))
                    return 3;

                Data.Remove(key);

                m_MemoryUsed -= key.Length + entry.Value.Length;
                if (!string.IsNullOrEmpty(entry.Password))
                    m_MemoryUsed -= entry.Password.Length;
                if (m_MemoryUsed < 0)
                    m_MemoryUsed = 0;

                return 0;
            }
        }
        public int Remove(string key)
        {
            lock (linksetDataLock)
            {
                if (Data.Count <= 0)
                    return 4;

                if(string.IsNullOrEmpty(key))
                    return 4;

                if (!Data.TryGetValue(key, out LinksetDataEntry entry))
                    return 4;

                if (entry.IsProtected)
                    return 3;

                Data.Remove(key);

                m_MemoryUsed -= key.Length + entry.Value.Length;
                if (m_MemoryUsed < 0)
                    m_MemoryUsed = 0;

                return 0;
            }
        }

        public string Get(string key, string pass)
        {
            lock (linksetDataLock)
            {
                return (Data.TryGetValue(key, out LinksetDataEntry entry) && entry.CheckPassword(pass)) ? entry.Value : string.Empty;
            }
        }

        public string Get(string key)
        {
            lock (linksetDataLock)
            {
                return (Data.TryGetValue(key, out LinksetDataEntry entry) && entry.IsNotProtected) ? entry.Value : string.Empty;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HasData()
        {
            return Data.Count > 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Count()
        {
            return Data.Count;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Free()
        {
            int free = m_MemoryLimit - m_MemoryUsed;
            return free > 0 ? free : 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Used()
        {
            return m_MemoryUsed;
        }

        public string[] RemoveByPattern(string pattern, string pass, out int notDeleted)
        {
            notDeleted = 0;
            List<string> ret;

            lock (linksetDataLock)
            {
                if (Data.Count <= 0)
                    return Array.Empty<string>();
                try
                {
                    ret = new List<string>();
                    Regex reg = new(pattern, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(1));

                    foreach (var kvp in Data)
                    {
                        if (reg.IsMatch(kvp.Key))
                        {
                            if (kvp.Value.CheckPassword(pass))
                            {
                                int mem = kvp.Value.Value.Length + kvp.Key.Length;
                                if(kvp.Value.IsProtected)
                                    mem += kvp.Value.Password.Length;
                                m_MemoryUsed -= mem;
                                ret.Add(kvp.Key);
                            }
                            else
                                notDeleted++;
                        }
                    }
                }
                catch
                {
                    notDeleted = 0;
                    return Array.Empty<string>();
                }

                foreach (string k in ret)
                    Data.Remove(k);

                if (m_MemoryUsed < 0)
                    m_MemoryUsed = 0;
                return ret.ToArray();
            }
        }
        public int CountByPattern(string pattern)
        {
            lock (linksetDataLock)
            {
                if (Data.Count <= 0)
                    return 0;
                try
                {
                    Regex reg = new(pattern, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(1));

                    int ret = 0;
                    foreach (string k in Data.Keys)
                    {
                        if (reg.IsMatch(k))
                            ret++;
                    }
                    return ret;
                }
                catch
                {
                    return 0;
                }
            }
        }

        public string[] ListKeysByPatttern(string pattern, int start, int count)
        {
            List<string> lkeys;
            lock (linksetDataLock)
            {
                if (Data.Count <= 0 || start >= Data.Count)
                    return Array.Empty<string>();
                try
                {
                    Regex reg = new(pattern, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(1));
                    lkeys = new(Data.Count);
                    foreach (string k in Data.Keys)
                    {
                        if (reg.IsMatch(k))
                            lkeys.Add(k);
                    }
                }
                catch
                {
                    return Array.Empty<string>();
                }
            }
            if (lkeys.Count == 0)
                return Array.Empty<string>();

            lkeys.Sort();

            if (start < 0)
                start = 0;
            if (count < 1 || start + count > lkeys.Count)
                count = lkeys.Count - start;
            List<string> result = lkeys.GetRange(start, count);
            return result.ToArray();
        }

        public string[] ListKeys(int start, int count)
        {
            string[] keys;
            lock (linksetDataLock)
            {
                if (Data.Count <= 0 || start >= Data.Count)
                    return Array.Empty<string>();

                keys = new string[Data.Count];
                Data.Keys.CopyTo(keys, 0);
            }
            Array.Sort(keys);
            if (start < 0)
                start = 0;
            if (count < 1)
                return keys[start..];
            int end = start + count;
            if (end >= keys.Length)
                return keys[start..];
            return keys[start..end];
        }

        /// <summary>
        /// Merge the linksetData present in another Linkset into this one.
        /// If a key is present in our linksetData it wins, dont overide it.
        /// </summary>
        /// <param name="otherLinkset"></param>
        public void MergeOther(LinksetData otherLinksetData)
        {
            if (otherLinksetData is null || otherLinksetData.Data is null || otherLinksetData.Count() == 0)
                return;

            lock (linksetDataLock)
            {
                if(m_MemoryUsed + otherLinksetData.Used() < m_MemoryLimit)
                {
                    foreach (var kvp in otherLinksetData.Data)
                    {
                        if (Data.TryAdd(kvp.Key, kvp.Value))
                        {
                            m_MemoryUsed += kvp.Key.Length + kvp.Value.Value.Length;
                            if (!string.IsNullOrEmpty(kvp.Value.Password))
                                m_MemoryUsed += kvp.Value.Password.Length;
                        }
                    }
                    return;
                }

                SortedList<string,LinksetDataEntry> otherOrdered = new(otherLinksetData.Data);
                foreach (var kvp in otherOrdered)
                {
                    int mem = kvp.Key.Length + kvp.Value.Value.Length;
                    if (!string.IsNullOrEmpty(kvp.Value.Password))
                        mem += kvp.Value.Password.Length;
                    if (m_MemoryUsed + mem >= m_MemoryLimit)
                        return;
                    if (Data.TryAdd(kvp.Key, kvp.Value))
                    {
                        m_MemoryUsed += mem;
                        if (!string.IsNullOrEmpty(kvp.Value.Password))
                            m_MemoryUsed += kvp.Value.Password.Length;
                    }
                }

                otherLinksetData.Data = null;
                otherLinksetData.m_MemoryUsed = 0;
            }
        }


        /// <summary>
        /// ResetLinksetData - clear the list and update the accounting.
        /// </summary>
        public void ResetLinksetData()
        {
            lock (linksetDataLock)
            {
                if (Data.Count <= 0)
                    return;

                Data.Clear();
                m_MemoryUsed = 0;
            }
        }

        public string ToJson()
        {
            lock (linksetDataLock)
            {
                return JsonSerializer.Serialize<Dictionary<string, LinksetDataEntry>>(Data);
            }
        }

        public void ToXML(XmlTextWriter writer)
        {
            if (Data.Count < 1)
                return;
            using MemoryStream ms = new(m_MemoryUsed);
            ToBin(ms);
            if (ms.Length < 1)
                return;

            writer.WriteStartElement("lnkstdt");
            writer.WriteBase64(ms.GetBuffer(), 0, (int)ms.Length);
            writer.WriteEndElement();
        }

        public static LinksetData FromXML(ReadOnlySpan<char> data)
        {
            if (data.Length < 8)
                return null;
            int minLength = ((data.Length * 3) + 3) / 4;
            byte[] bindata = ArrayPool<byte>.Shared.Rent(minLength);
            try
            {
                if (Convert.TryFromBase64Chars(data, bindata, out int bytesWritten))
                    return FromBin(bindata);
            }
            catch
            {
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(bindata);
            }
            return  null;
        }

        public byte[] ToBin()
        {
            if(Data.Count < 1)
                return null;

            using MemoryStream ms = new(m_MemoryUsed);
            ToBin(ms);
            return ms.Length > 0 ? ms.ToArray() : null;
        }

        public void ToBin(MemoryStream ms)
        {
            try
            {
                using BinaryWriter bw = new BinaryWriter(ms, System.Text.Encoding.UTF8, true);
                bw.Write((byte)1); // storage version
                bw.Write7BitEncodedInt(m_MemoryLimit);
                lock (linksetDataLock)
                {
                    bw.Write7BitEncodedInt(Data.Count);
                    foreach (var kvp in Data)
                    {
                        bw.Write(kvp.Key);
                        bw.Write(kvp.Value.Value);
                        if(kvp.Value.IsProtected)
                            bw.Write(kvp.Value.Password);
                        else
                            bw.Write((byte)0);
                    }
                }
                return;
            }
            catch { }
            ms.SetLength(0);
        }

        public static LinksetData FromBin(byte[] data)
        {
            if (data.Length < 8)
                return null;

            try
            {
                using BinaryReader br = new BinaryReader(new MemoryStream(data));
                int version = br.Read7BitEncodedInt();
                int memoryLimit = br.Read7BitEncodedInt();
                if(memoryLimit < 0 || memoryLimit > 256 * 1024)
                    memoryLimit = 256 * 1024;

                int count = br.Read7BitEncodedInt();
                if(count == 0)
                    return null;
                LinksetData ld = new LinksetData(memoryLimit);
                for(int i = 0; i < count; i++)
                {
                    string key = br.ReadString();
                    if (key.Length == 0)
                        continue;
                    ld.m_MemoryUsed += key.Length;

                    string value = br.ReadString();
                    if(value.Length == 0)
                        continue;
                    ld.m_MemoryUsed += value.Length;

                    string pass = br.ReadString();
                    ld.m_MemoryUsed += pass.Length;
                    if(ld.m_MemoryUsed > memoryLimit)
                        break;

                    if(pass.Length > 0)
                    {
                        ld.Data[key] = new LinksetDataEntry()
                        {
                            Value = value,
                            Password = pass
                        };
                    }
                    else
                    {
                        ld.Data[key] = new LinksetDataEntry()
                        {
                            Value = value,
                            Password = null
                        };
                    }
                }
                return ld;
            }
            catch
            {
            }
            return null;
        }
    }

    public class LinksetDataEntry
    {
        public string Password;
        public string Value;

        public LinksetDataEntry() { }
        public LinksetDataEntry(string value, string password)
        {
            Value = value;
            Password = password;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool CheckPassword(string pass)
        {
            // A undocumented caveat for LinksetData appears to be that even for unprotected values,
            // if a pass is provided, it is still treated as protected
            return string.IsNullOrEmpty(Password) || (Password == pass);
        }

        public LinksetDataEntry Copy()
        {
            return new LinksetDataEntry
            {
                Password = Password,
                Value = Value
            };
        }

        public bool IsProtected
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return !string.IsNullOrEmpty(Password); }
        }
        public bool IsNotProtected
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return string.IsNullOrEmpty(Password); }
        }
    }
}
