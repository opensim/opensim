using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Net;
using System.Net.NetworkInformation;
using System.Reflection;
using log4net;

namespace OpenSim.Framework
{
    /// <summary>
    /// Handles NAT translation in a 'manner of speaking'
    /// Allows you to return multiple different external
    /// hostnames depending on the requestors network
    /// 
    /// This enables standard port forwarding techniques
    /// to work correctly with OpenSim.
    /// </summary>
    public static class NetworkUtil
    {
        // Logger
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        // IPv4Address, Subnet
        static readonly Dictionary<IPAddress,IPAddress> m_subnets = new Dictionary<IPAddress, IPAddress>();

        public static IPAddress GetIPFor(IPAddress user, IPAddress simulator)
        {
            // Check if we're accessing localhost.
            foreach (IPAddress host in Dns.GetHostAddresses(Dns.GetHostName()))
            {
                if (host.Equals(user) && host.AddressFamily == AddressFamily.InterNetwork)
                {
                    m_log.Info("[NATROUTING] Localhost user detected, sending them '" + host + "' instead of '" + simulator + "'");
                    return host;
                }
            }

            // Check for same LAN segment
            foreach (KeyValuePair<IPAddress, IPAddress> subnet in m_subnets)
            {
                byte[] subnetBytes = subnet.Value.GetAddressBytes();
                byte[] localBytes = subnet.Key.GetAddressBytes();
                byte[] destBytes = user.GetAddressBytes();

                if (subnetBytes.Length != destBytes.Length || subnetBytes.Length != localBytes.Length)
                    return null;

                bool valid = true;

                for (int i = 0; i < subnetBytes.Length; i++)
                {
                    if ((localBytes[i] & subnetBytes[i]) != (destBytes[i] & subnetBytes[i]))
                    {
                        valid = false;
                        break;
                    }
                }

                if (subnet.Key.AddressFamily != AddressFamily.InterNetwork)
                    valid = false;

                if (valid)
                {
                    m_log.Info("[NATROUTING] Local LAN user detected, sending them '" + subnet.Key + "' instead of '" + simulator + "'");
                    return subnet.Key;
                }
            }

            // Otherwise, return outside address
            return simulator;
        }

        private static IPAddress GetExternalIPFor(IPAddress destination, string defaultHostname)
        {
            // Adds IPv6 Support (Not that any of the major protocols supports it...)
            if (destination.AddressFamily == AddressFamily.InterNetworkV6)
            {
                foreach (IPAddress host in Dns.GetHostAddresses(defaultHostname))
                {
                    if (host.AddressFamily == AddressFamily.InterNetworkV6)
                    {
                        m_log.Info("[NATROUTING] Localhost user detected, sending them '" + host + "' instead of '" + defaultHostname + "'");
                        return host;
                    }
                }
            }

            if(destination.AddressFamily != AddressFamily.InterNetwork)
                return null;

            // Check if we're accessing localhost.
            foreach (KeyValuePair<IPAddress, IPAddress> pair in m_subnets)
            {
                IPAddress host = pair.Value;
                if (host.Equals(destination) && host.AddressFamily == AddressFamily.InterNetwork)
                {
                    m_log.Info("[NATROUTING] Localhost user detected, sending them '" + host + "' instead of '" + defaultHostname + "'");
                    return destination;
                }
            }

            // Check for same LAN segment
            foreach (KeyValuePair<IPAddress, IPAddress> subnet in m_subnets)
            {
                byte[] subnetBytes = subnet.Value.GetAddressBytes();
                byte[] localBytes = subnet.Key.GetAddressBytes();
                byte[] destBytes = destination.GetAddressBytes();
                
                if(subnetBytes.Length != destBytes.Length || subnetBytes.Length != localBytes.Length)
                    return null;

                bool valid = true;

                for(int i=0;i<subnetBytes.Length;i++)
                {
                    if ((localBytes[i] & subnetBytes[i]) != (destBytes[i] & subnetBytes[i]))
                    {
                        valid = false;
                        break;
                    }
                }

                if (subnet.Key.AddressFamily != AddressFamily.InterNetwork)
                    valid = false;

                if (valid)
                {
                    m_log.Info("[NATROUTING] Local LAN user detected, sending them '" + subnet.Key + "' instead of '" + defaultHostname + "'");
                    return subnet.Key;
                }
            }

            // Check to see if we can find a IPv4 address.
            foreach (IPAddress host in Dns.GetHostAddresses(defaultHostname))
            {
                if (host.AddressFamily == AddressFamily.InterNetwork)
                    return host;
            }

            // Unable to find anything.
            throw new ArgumentException("[NetworkUtil] Unable to resolve defaultHostname to an IPv4 address for an IPv4 client");
        }

        static NetworkUtil()
        {
            foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                foreach (UnicastIPAddressInformation address in ni.GetIPProperties().UnicastAddresses)
                {
                    if (address.Address.AddressFamily == AddressFamily.InterNetwork)
                    {
                        if (address.IPv4Mask != null)
                        {
                            m_subnets.Add(address.Address, address.IPv4Mask);
                        }
                        else
                        {
                            m_log.Warn("[NetworkUtil] Found IPv4 Address without Subnet Mask!?");
                        }
                    }
                }
            }
        }

        public static IPAddress GetIPFor(IPEndPoint user, string defaultHostname)
        {
            // Try subnet matching
            IPAddress rtn = GetExternalIPFor(user.Address, defaultHostname);
            if (rtn != null)
                return rtn;

            // Otherwise use the old algorithm
            IPAddress ia;

            if (IPAddress.TryParse(defaultHostname, out ia))
                return ia;

            ia = null;

            foreach (IPAddress Adr in Dns.GetHostAddresses(defaultHostname))
            {
                if (Adr.AddressFamily == AddressFamily.InterNetwork)
                {
                    ia = Adr;
                    break;
                }
            }

            return ia;
        }

        public static string GetHostFor(IPAddress user, string defaultHostname)
        {
            IPAddress rtn = GetExternalIPFor(user, defaultHostname);
            if(rtn != null)
                return rtn.ToString();

            return defaultHostname;
        }
    }
}
