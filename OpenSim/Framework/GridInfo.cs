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
using System.Reflection;
using log4net;
using Nini.Config;

namespace OpenSim.Framework
{
    public enum OSHTTPURIFlags : byte
    {
        None        = 0,
        ValidHost   = 1,
        Resolved    = 1 << 1,
        Empty       = 1 << 2,

        ValidResolved = ValidHost | Resolved
    }

    // full http or https schema, server host, port and possible server path any query is ignored.
    public struct OSHTTPURI:IComparable<OSHTTPURI>, IEquatable<OSHTTPURI>
    {
        public OSHTTPURIFlags Flags;
        public int Port;
        public IPAddress IP;
        public readonly string Host;
        public readonly string URL;
        public readonly string Path;
        public readonly string URI;

        public OSHTTPURI(string uri, bool withDNSResolve = false)
        {
            Flags = OSHTTPURIFlags.Empty;
            Port = -1;
            IP = null;
            Host = string.Empty;
            URI = string.Empty;
            URL = string.Empty;
            Path = string.Empty;

            if (string.IsNullOrEmpty(uri))
                return;

            Flags = OSHTTPURIFlags.None;
            try
            {
                Uri m_checkuri = new Uri(uri);

                if(m_checkuri.Scheme != Uri.UriSchemeHttp && m_checkuri.Scheme != Uri.UriSchemeHttps)
                    return;

                Flags = OSHTTPURIFlags.ValidHost;
                Host = m_checkuri.DnsSafeHost.ToLowerInvariant();

                Port = m_checkuri.Port;
                Path = m_checkuri.AbsolutePath;
                URL = m_checkuri.Scheme + "://" + Host + ":" + Port;
                URI = m_checkuri.AbsoluteUri;

                if (withDNSResolve)
                {
                    IPAddress ip = Util.GetHostFromDNS(Host);
                    if (ip != null)
                    {
                        IP = ip;
                        Flags = OSHTTPURIFlags.ValidResolved;
                    }
                }
            }
            catch
            {
                Flags = OSHTTPURIFlags.None;
                IP = null;
                URI = string.Empty;
            }
        }

        public bool ResolveDNS()
        {
            IPAddress ip = Util.GetHostFromDNS(Host);
            if (ip == null)
            {
                Flags &= ~OSHTTPURIFlags.Resolved;
                return false;
            }
            Flags |= OSHTTPURIFlags.Resolved;
            return true;
        }

        public bool IsValidHost
        {
            get { return (Flags & OSHTTPURIFlags.ValidHost) != 0; }
        }

        public bool ValidAndResolved(out string error)
        {
            if ((Flags & OSHTTPURIFlags.ValidHost) == 0)
            {
                error = "failed to parse uri";
                return false;
            }
            if ((Flags & OSHTTPURIFlags.Resolved) == 0)
            {
                error = "failed DNS resolve of uri host";
                return false;
            }
            error = string.Empty;
            return true;
        }

        public bool IsResolvedHost
        {
            get {return (Flags & OSHTTPURIFlags.Resolved) != 0; }
        }

        public string URIwEndSlash
        {
            get { return Flags == OSHTTPURIFlags.None ? "" : URI + "/";}
        }

        public string HostAndPort
        {
            get { return (Flags == OSHTTPURIFlags.None) ? "" : Host + ":" + Port.ToString(); }
        }

        public int CompareTo(OSHTTPURI other)
        {
            if (Port == other.Port && ((Flags & other.Flags) & OSHTTPURIFlags.ValidHost) != 0)
            {
                if (Path.Equals(other.Path))
                    return Host.CompareTo(other.Host);
            }
            return -1;
        }

        public bool Equals(OSHTTPURI other)
        {
            if (Port == other.Port && ((Flags & other.Flags) & OSHTTPURIFlags.ValidHost) != 0)
            {
                if (Path.Equals(other.Path))
                    return Host.Equals(other.Host);
            }
            return false;
        }

        public override int GetHashCode()
        {
            return URI.GetHashCode();
        }
    }

    //host and port. Can not have server internal path or query. http scheme is assumed if not present
    public struct OSHHTPHost : IComparable<OSHHTPHost>, IEquatable<OSHHTPHost>
    {
        public OSHTTPURIFlags Flags;
        public int Port;
        public IPAddress IP;
        public readonly string Host;
        public readonly string URI;

        public OSHHTPHost(string url, bool withDNSResolve = false)
        {
            Flags = OSHTTPURIFlags.Empty;
            Port = 80;
            IP = null;
            Host = string.Empty;
            URI = string.Empty;

            bool secureHTTP = false;

            if (string.IsNullOrEmpty(url))
                return;

            Flags = OSHTTPURIFlags.None;

            url = url.ToLowerInvariant();
            try
            {
                int urllen = url.Length;
                if (url[urllen - 1] == '/')
                    --urllen;
                int start;
                if (url.StartsWith("http"))
                {
                    if (url[4] == 's')
                    {
                        start = 8;
                        secureHTTP = true;
                    }
                    else
                        start = 7;
                }
                else
                    start = 0;

                string host;
                UriHostNameType type;
                int indx = url.IndexOf(':', start, urllen - start);
                if (indx > 0)
                {
                    host = url.Substring(start, indx - start);
                    type = Uri.CheckHostName(host);
                    if (type == UriHostNameType.Unknown || type == UriHostNameType.Basic)
                        return;
                    ++indx;
                    string sport = url.Substring(indx, urllen - indx);
                    int tmp;
                    if (!int.TryParse(sport, out tmp) || tmp < 0 || tmp > 65535)
                        return;

                    Flags = OSHTTPURIFlags.ValidHost;
                    Host = host;
                    Port = tmp;
                    URI = (secureHTTP ? "https://" : "http://") + Host + ":" + Port.ToString();
                }
                else
                {
                    host = url.Substring(start, urllen - start);
                    type = Uri.CheckHostName(host);
                    if (type == UriHostNameType.Unknown || type == UriHostNameType.Basic)
                        return;

                    Flags = OSHTTPURIFlags.ValidHost;
                    Host = host;
                    if (secureHTTP)
                    {
                        Port = 443;
                        URI = "https://" + Host + ":443";
                    }
                    else
                    {
                        Port = 80;
                        URI = "http://" + Host + ":80";
                    }
                }

                if (withDNSResolve)
                {
                    IPAddress ip = Util.GetHostFromDNS(host);
                    if (ip != null)
                    {
                        Flags = OSHTTPURIFlags.ValidResolved;
                        IP = ip;
                    }
                }
            }
            catch
            {
                Flags = OSHTTPURIFlags.None;
                IP = null;
                URI = string.Empty;
            }
        }

        public bool ResolveDNS()
        {
            IPAddress ip = Util.GetHostFromDNS(Host);
            if (ip == null)
            {
                Flags &= ~OSHTTPURIFlags.Resolved;
                return false;
            }
            Flags |= OSHTTPURIFlags.Resolved;
            return true;
        }

        public bool IsValidHost
        {
            get { return (Flags & OSHTTPURIFlags.ValidHost) != 0; }
        }

        public bool IsResolvedHost
        {
            get { return (Flags & OSHTTPURIFlags.Resolved) != 0; }
        }

        public bool ValidAndResolved(out string error)
        {
            if ((Flags & OSHTTPURIFlags.ValidHost) == 0)
            {
                error = "failed to parse uri";
                return false;
            }
            if ((Flags & OSHTTPURIFlags.Resolved) == 0)
            {
                error = "failed DNS resolve of uri host";
                return false;
            }
            error = string.Empty;
            return true;
        }

        public string URIwEndSlash
        {
            get { return (Flags == OSHTTPURIFlags.None) ? "" : URI + "/"; }
        }

        public string HostAndPort
        {
            get { return (Flags == OSHTTPURIFlags.None) ? "" : Host + ":" + Port.ToString(); }
        }

        public int CompareTo(OSHHTPHost other)
        {
            if (Port == other.Port && ((Flags & other.Flags) & OSHTTPURIFlags.ValidHost) != 0)
            {
                return Host.CompareTo(other.Host);
            }
            return -1;
        }

        public bool Equals(OSHHTPHost other)
        {
            if (Port == other.Port && ((Flags & other.Flags) & OSHTTPURIFlags.ValidHost) != 0)
            {
                return Host.Equals(other.Host);
            }
            return false;
        }

        public override int GetHashCode()
        {
            return Host.GetHashCode() + Port;
        }
    }

    public class GridInfo
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private bool m_hasHGconfig;
        private OSHHTPHost m_gateKeeperURL;
        private HashSet<OSHHTPHost> m_gateKeeperAlias;

        private OSHHTPHost m_homeURL;
        private HashSet<OSHHTPHost> m_homeURLAlias;
        private string m_gridUrl = string.Empty;
        private string[] m_gridUrlAlias = null;
        private string m_GridName = string.Empty;
        private string m_GridNick = string.Empty;
        private string m_SearchURL = string.Empty;
        private string m_DestinationGuideURL = string.Empty;
        private string m_economyURL = string.Empty;

        public GridInfo (IConfigSource config, string defaultURI = "")
        {
            string[] sections = new string[] {"Const", "Startup", "Hypergrid"};

            string gatekeeper = Util.GetConfigVarFromSections<string>(config, "GatekeeperURI", sections, string.Empty);
            if (string.IsNullOrEmpty(gatekeeper))
            {
                IConfig serverConfig = config.Configs["GatekeeperService"];
                if (serverConfig != null)
                    gatekeeper = serverConfig.GetString("ExternalName", string.Empty);
            }
            if (string.IsNullOrEmpty(gatekeeper))
            {
                IConfig gridConfig = config.Configs["GridService"];
                if (gridConfig != null)
                    gatekeeper = gridConfig.GetString("Gatekeeper", string.Empty);
            }
            if (string.IsNullOrEmpty(gatekeeper))
            {
                m_hasHGconfig = false;
                if (!string.IsNullOrEmpty(defaultURI))
                    m_gateKeeperURL = new OSHHTPHost(defaultURI, true);
            }
            else
            {
                m_gateKeeperURL = new OSHHTPHost(gatekeeper, true);
                m_hasHGconfig = true;
            }

            if (!m_gateKeeperURL.IsResolvedHost)
            {
                m_log.Error(m_gateKeeperURL.IsValidHost ?  "Could not resolve GatekeeperURI" : "GatekeeperURI is a invalid host");
                throw new Exception("GatekeeperURI configuration error");
            }

            m_gridUrl = m_gateKeeperURL.URI;

            string gatekeeperURIAlias = Util.GetConfigVarFromSections<string>(config, "GatekeeperURIAlias", sections, string.Empty);

            if (!string.IsNullOrWhiteSpace(gatekeeperURIAlias))
            {
                string[] alias = gatekeeperURIAlias.Split(',');
                for (int i = 0; i < alias.Length; ++i)
                {
                    OSHHTPHost tmp = new OSHHTPHost(alias[i].Trim(), false);
                    if (tmp.IsValidHost)
                    {
                        if (m_gateKeeperAlias == null)
                            m_gateKeeperAlias = new HashSet<OSHHTPHost>();
                        m_gateKeeperAlias.Add(tmp);
                    }
                }
            }

            if (m_gateKeeperAlias != null && m_gateKeeperAlias.Count > 0)
            {
                m_gridUrlAlias = new string[m_gateKeeperAlias.Count];
                int i = 0;
                foreach (OSHHTPHost a in m_gateKeeperAlias)
                    m_gridUrlAlias[i++] = a.URI;
            }

            string home = Util.GetConfigVarFromSections<string>(config, "HomeURI", sections, string.Empty);

            if (string.IsNullOrEmpty(home))
            {
                if (!string.IsNullOrEmpty(gatekeeper))
                    m_homeURL = m_gateKeeperURL;
                else if (!string.IsNullOrEmpty(defaultURI))
                    m_homeURL = new OSHHTPHost(defaultURI, true);
            }
            else
                m_homeURL = new OSHHTPHost(home, true);

            if (!m_homeURL.IsResolvedHost)
            {
                m_log.Error(m_homeURL.IsValidHost ?  "Could not resolve HomeURI" : "HomeURI is a invalid host");
                throw new Exception("HomeURI configuration error");
            }

            string homeAlias = Util.GetConfigVarFromSections<string>(config, "HomeURIAlias", sections, string.Empty);
            if (!string.IsNullOrWhiteSpace(homeAlias))
            {
                string[] alias = homeAlias.Split(',');
                for (int i = 0; i < alias.Length; ++i)
                {
                    OSHHTPHost tmp = new OSHHTPHost(alias[i].Trim(), false);
                    if (tmp.IsValidHost)
                    {
                        if (m_homeURLAlias == null)
                            m_homeURLAlias = new HashSet<OSHHTPHost>();
                        m_homeURLAlias.Add(tmp);
                    }
                }
            }

            string[] namessections = new string[] { "Const", "GridInfo", "SimulatorFeatures" };
            m_GridName = Util.GetConfigVarFromSections<string>(config, "GridName", namessections, string.Empty);
            if (string.IsNullOrEmpty(m_GridName))
                m_GridName = Util.GetConfigVarFromSections<string>(config, "gridname", namessections, string.Empty);

            m_GridNick = Util.GetConfigVarFromSections<string>(config, "GridNick", namessections, string.Empty);

            if (string.IsNullOrEmpty(m_GridName))
                m_GridName = "Another bad configured grid";

            OSHTTPURI tmpuri;
            m_SearchURL = Util.GetConfigVarFromSections<string>(config,"SearchServerURI", namessections, string.Empty);
            if (!string.IsNullOrEmpty(m_SearchURL))
            {
                tmpuri = new OSHTTPURI(m_SearchURL.Trim(), true);
                if (!tmpuri.IsResolvedHost)
                {
                    m_log.Error(tmpuri.IsValidHost ? "Could not resolve SearchServerURI" : "SearchServerURI is a invalid host");
                    throw new Exception("SearchServerURI configuration error");
                }
                m_SearchURL = tmpuri.URI;
            }

            m_DestinationGuideURL = Util.GetConfigVarFromSections<string>(config, "DestinationGuideURI",namessections, string.Empty);

            if (string.IsNullOrEmpty(m_DestinationGuideURL)) // Make this consistent with the variable in the LoginService config
                m_DestinationGuideURL = Util.GetConfigVarFromSections<string>(config, "DestinationGuide", namessections, string.Empty);

            if (!string.IsNullOrEmpty(m_DestinationGuideURL))
            {
                tmpuri = new OSHTTPURI(m_DestinationGuideURL.Trim(), true);
                if (!tmpuri.IsResolvedHost)
                {
                    m_log.Error(tmpuri.IsValidHost ? "Could not resolve DestinationGuideURL" : "DestinationGuideURL is a invalid host");
                    throw new Exception("DestinationGuideURL configuration error");
                }
                m_DestinationGuideURL = tmpuri.URI;
            }

            m_economyURL = Util.GetConfigVarFromSections<string>(config, "economy", new string[] { "Economy", "GridInfo" });
            if (!string.IsNullOrEmpty(m_economyURL))
            {
                tmpuri = new OSHTTPURI(m_economyURL.Trim(), true);
                if (!tmpuri.IsResolvedHost)
                {
                    m_log.Error(tmpuri.IsValidHost ? "Could not resolve economyURL" : "economyURL is a invalid host");
                    throw new Exception("economyURL configuration error");
                }
                m_economyURL = tmpuri.URI;
            }
        }

        public bool HasHGConfig
        {
            get { return m_hasHGconfig; }
        }

        public string GateKeeperURL
        {
            get { return m_gateKeeperURL.URIwEndSlash; }
        }

        public string GateKeeperURLNoEndSlash
        {
            get { return m_gateKeeperURL.URI; }
        }

        public string HGGateKeeperURL
        {
            get
            {
                if (m_hasHGconfig)
                    return m_gateKeeperURL.URIwEndSlash;
                return string.Empty;
            }
        }

        public string HGGateKeeperURLNoEndSlash
        {
            get
            {
                if (m_hasHGconfig)
                    return m_gateKeeperURL.URI;
                return string.Empty;
            }
        }

        public string HomeURL
        {
            get { return m_homeURL.URIwEndSlash; }
        }

        public string HomeURLNoEndSlash
        {
            get { return m_homeURL.URI; }
        }

        public string HGHomeURL
        {
            get
            {
                if (m_hasHGconfig)
                    return m_homeURL.URIwEndSlash;
                return string.Empty;
            }
        }

        public string HGHomeURLNoEndSlash
        {
            get
            {
                if (m_hasHGconfig)
                    return m_homeURL.URI;
                return string.Empty;
            }
        }

        // -2 dns failed
        // -1 if bad url
        // 0 if not local
        // 1 if local
        public int IsLocalGrid(string othergatekeeper)
        {
            OSHHTPHost tmp = new OSHHTPHost(othergatekeeper, false);
            if (!tmp.IsValidHost)
                return ((tmp.Flags & OSHTTPURIFlags.Empty) == 0) ? -1 : 1;
            if (tmp.Equals(m_gateKeeperURL))
                return 1;
            if (m_gateKeeperAlias != null && m_gateKeeperAlias.Contains(tmp))
                return 1;
            return 0;
        }

        public int IsLocalGrid(string othergatekeeper, bool withResolveCheck)
        {
            OSHHTPHost tmp = new OSHHTPHost(othergatekeeper, false);
            if (!tmp.IsValidHost)
                return ((tmp.Flags & OSHTTPURIFlags.Empty) == 0) ? -1 : 1;
            if (tmp.Equals(m_gateKeeperURL))
                return 1;
            if (m_gateKeeperAlias != null && m_gateKeeperAlias.Contains(tmp))
                return 1;
            if (withResolveCheck)
            {
                if (tmp.IsResolvedHost)
                    return 0;
                return tmp.ResolveDNS() ? 0 : -2;
            }
            return 0;
        }

        public int IsLocalGrid(OSHHTPHost othergatekeeper)
        {
            if (!othergatekeeper.IsValidHost)
                return ((othergatekeeper.Flags & OSHTTPURIFlags.Empty) == 0) ? -1 : 1;

            if (othergatekeeper.Equals(m_gateKeeperURL))
                return 1;
            if (m_gateKeeperAlias != null && m_gateKeeperAlias.Contains(othergatekeeper))
                return 1;
            return 0;
        }

        public int IsLocalHome(string otherhome)
        {
            OSHHTPHost tmp = new OSHHTPHost(otherhome, false);
            if (!tmp.IsValidHost)
                return ((tmp.Flags & OSHTTPURIFlags.Empty) == 0) ? -1 : 1;
            if (tmp.Equals(m_homeURL))
                return 1;
            if (m_homeURLAlias != null && m_homeURLAlias.Contains(tmp))
                return 1;
            return 0;
        }

        public int IsLocalHome(string otherhome, bool withResolveCheck)
        {
            OSHHTPHost tmp = new OSHHTPHost(otherhome, false);
            if (!tmp.IsValidHost)
                return ((tmp.Flags & OSHTTPURIFlags.Empty) == 0) ? -1 : 1;
            if (tmp.Equals(m_homeURL))
                return 1;
            if (m_homeURLAlias != null && m_homeURLAlias.Contains(tmp))
                return 1;

            if (withResolveCheck)
            {
                return tmp.ResolveDNS() ? 0 : -2;
            }
            return 0;
        }

        public string[] GridUrlAlias
        {
            get { return m_gridUrlAlias; }
            set
            {
                if(value.Length > 0)
                {
                    for (int i = 0; i < value.Length; ++i)
                    {
                        OSHHTPHost tmp = new OSHHTPHost(value[i].Trim(), false);
                        if (tmp.IsValidHost)
                        {
                            if (m_gateKeeperAlias == null)
                                m_gateKeeperAlias = new HashSet<OSHHTPHost>();
                            m_gateKeeperAlias.Add(tmp);
                        }
                    }
                    if (m_gateKeeperAlias != null && m_gateKeeperAlias.Count > 0)
                    {
                        m_gridUrlAlias = new string[m_gateKeeperAlias.Count];
                        int i = 0;
                        foreach (OSHHTPHost a in m_gateKeeperAlias)
                            m_gridUrlAlias[i++] = a.URI;
                    }
                }
            }
        }

        public string GridName
        {
            get { return m_GridName; }
            set
            {
                if (!string.IsNullOrEmpty(value))
                    m_GridName = value;
            }
        }

        public string GridNick
        {
            get { return m_GridNick; }
            set
            {
                if (!string.IsNullOrEmpty(value))
                    m_GridNick = value;
            }
        }

        public string GridUrl
        {
            get { return m_gridUrl; }
            set
            {
                OSHHTPHost tmp = new OSHHTPHost(value, true);
                if (tmp.IsResolvedHost)
                    m_gridUrl = tmp.URI;
                else
                    m_log.Error((tmp.IsValidHost ? "Could not resolve GridUrl" : "GridUrl is a invalid host ") + value ?? "");
            }
        }

        public string SearchURL
        {
            get { return m_SearchURL; }
            set
            {
                OSHTTPURI tmp = new OSHTTPURI(value, true);
                if (tmp.IsResolvedHost)
                    m_SearchURL = tmp.URI;
                else
                    m_log.Error((tmp.IsValidHost ? "Could not resolve SearchURL" : "SearchURL is a invalid host ") + value??"");
            }
        }

        public string DestinationGuideURL
        {
            get { return m_DestinationGuideURL; }
            set
            {
                OSHTTPURI tmp = new OSHTTPURI(value, true);
                if (tmp.IsResolvedHost)
                    m_DestinationGuideURL = tmp.URI;
                else
                    m_log.Error((tmp.IsValidHost ? "Could not resolve DestinationGuideURL" : "DestinationGuideURL is a invalid host ") + value ?? "");
            }
        }

        public string EconomyURL
        {
            get { return m_economyURL; }
            set
            {
                OSHTTPURI tmp = new OSHTTPURI(value, true);
                if (tmp.IsResolvedHost)
                    m_economyURL = tmp.URI;
                else
                    m_log.Error((tmp.IsValidHost ? "Could not resolve EconomyURL" : "EconomyURL is a invalid host ") + value ?? "");
            }
        }
    }
}
