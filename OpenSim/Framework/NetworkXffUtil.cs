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

using log4net;
using Nini.Config;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace OpenSim.Framework
{
    public static class NetworkXffUtil
    {
        // Logger
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private static List<IPNetwork> m_TrustedProxies = null;
        private static object mainlock = new();
        public static void Initialize(IConfig netconfig)
        {
            if(netconfig == null)
                return;

            lock (mainlock)
            {
                if (m_TrustedProxies == null ) //only read once
                {
                    m_TrustedProxies = [];
                    string proxiesconfig = netconfig.GetString("XFFTrustedProxies", string.Empty);
                    if (proxiesconfig.Length > 0 )
                    {
                        string[] entries = proxiesconfig.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                        foreach ( string entry in entries )
                            AddImpl(entry);
                    }
                }
                m_log.Info($"[NetworkXffUtil] added {m_TrustedProxies.Count} allow proxy entries");
            }
        }

        public static IPAddress GetIP(string address)
        {
            UriHostNameType type = Uri.CheckHostName(address);
            if (type == UriHostNameType.Unknown || type == UriHostNameType.Basic)
            {
                m_log.Error($"[NetworkXffUtil] failed to parse host entry {address}");
                return null;
            }
            IPAddress ip = Util.GetHostFromDNS(address);
            if (ip == null)
            {
                m_log.Error($"[NetworkXffUtil] failed to parse host entry {address}");
                return null;
            }
            if (ip.IsIPv4MappedToIPv6)
                ip = ip.MapToIPv4();
            return ip;
        }

        public static void AddImpl(string entry)
        {
            if (entry.Contains('/'))
            {
                if (IPNetwork.TryParse(entry, out IPNetwork entrynet))
                    m_TrustedProxies.Add(entrynet);
                else
                    m_log.Error($"[NetworkXffUtil] failed to parse CDIR entry {entry}");
            }
            else
            {
                IPAddress ip = GetIP(entry);
                if(ip != null)
                {
                    string ipstr = ip.ToString() + (ip.AddressFamily == AddressFamily.InterNetworkV6 ? "/128" : "/32");
                    if (IPNetwork.TryParse(ipstr, out IPNetwork entrynet))
                        m_TrustedProxies.Add(entrynet);
                    else
                        m_log.Error($"[NetworkXffUtil] failed to parse host entry {entry}");
                }
            }
        }

        public static void Add(string entry)
        {
            lock (mainlock)
            {
                if (m_TrustedProxies == null)
                    return;
                AddImpl(entry);
            }
        }

        public static bool Contains(string entry)
        {
            if(string.IsNullOrEmpty(entry))
                return false;

            lock (mainlock)
            {
                if (m_TrustedProxies == null || m_TrustedProxies.Count == 0)
                    return false;
            }

            IPAddress ip = GetIP(entry);
            if (ip == null)
                return false;

            lock (mainlock)
            {
                foreach(IPNetwork net in m_TrustedProxies)
                {
                    if (net.Contains(ip))
                        return true;
                }
            }
           return false;
        }

        public static bool Contains(IPAddress ip)
        {
            if (ip == null)
                return false;

            lock (mainlock)
            {
                if (m_TrustedProxies == null || m_TrustedProxies.Count == 0)
                    return false;
                foreach(IPNetwork net in m_TrustedProxies)
                {
                    if (net.Contains(ip))
                        return true;
                }
            }
           return false;
        }

        public static bool CheckXFF(IPEndPoint LocalIPEndPoint, string header, out IPEndPoint xffremote, out string shortheader)
        {
            if(!string.IsNullOrEmpty(header) && Contains(LocalIPEndPoint.Address))
            {
                IPAddress lastproxy = LocalIPEndPoint.Address;
                string[] addrs = header.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if(addrs.Length > 0)
                {
                    IPAddress addrip = null;
                    int i = addrs.Length - 1;
                    while (i >= 0)
                    {
                        addrip = GetIP(addrs[i]);
                        if(!Contains(addrip))
                            break;
                        lastproxy = addrip;
                        i--;
                    }
                    // TODO private ips
                    if(addrip != null && (!Util.IsPrivateNetwork(addrip) || Util.IsPrivateNetwork(lastproxy)))
                    {
                        xffremote = new IPEndPoint(addrip, LocalIPEndPoint.Port);
                        shortheader = addrs[i];
                        return true;
                    }
                }
            }

            xffremote = null;
            shortheader = string.Empty;
            return false;
        }
    }
}
