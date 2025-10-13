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
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net;
using OpenSim.Framework.Security.DOSProtector.SDK;
using OpenSim.Framework.Security.DOSProtector.Core.Plugin.Basic;

namespace OpenSim.Framework.Security.DOSProtector.Core.Plugin.IPFilter
{
    
    /// <summary>
    /// Configuration options for IPFilterDOSProtector.
    /// Provides IP whitelist/blacklist filtering with CIDR support.
    /// </summary>
    public class IPFilterDosProtectorOptions : Basic.BasicDosProtectorOptions
    {
        /// <summary>
        /// List of whitelisted individual IP addresses.
        /// These IPs bypass rate limiting (if WhitelistBypassesRateLimit is true).
        /// Example: ["127.0.0.1", "192.168.1.100"]
        /// </summary>
        public List<string> WhitelistedIPs { get; set; } = new();

        /// <summary>
        /// List of whitelisted IP ranges in CIDR notation.
        /// Example: ["192.168.1.0/24", "10.0.0.0/8"]
        /// </summary>
        public List<string> WhitelistedCIDRs { get; set; } = new();

        /// <summary>
        /// List of blacklisted individual IP addresses.
        /// These IPs are always blocked, regardless of rate limits.
        /// Example: ["1.2.3.4", "5.6.7.8"]
        /// </summary>
        public List<string> BlacklistedIPs { get; set; } = new();

        /// <summary>
        /// List of blacklisted IP ranges in CIDR notation.
        /// Example: ["123.45.0.0/16"]
        /// </summary>
        public List<string> BlacklistedCIDRs { get; set; } = new();

        /// <summary>
        /// If true, whitelisted IPs completely bypass rate limiting.
        /// If false, whitelisted IPs are still subject to rate limits (but not blacklist).
        /// Default: true
        /// </summary>
        public bool WhitelistBypassesRateLimit { get; set; } = true;

        /// <summary>
        /// Constructor with sensible defaults
        /// </summary>
        public IPFilterDosProtectorOptions()
        {
            ReportingName = "IPFilter";
        }
    }
    
    /// <summary>
    /// DOS Protector with IP whitelist/blacklist filtering.
    /// Supports individual IPs and CIDR ranges.
    /// </summary>
    [DOSProtectorOptions(typeof(IPFilterDosProtectorOptions))]
    public class IPFilterDOSProtector : BaseDOSProtector
    {
        private readonly IPFilterDosProtectorOptions _filterOptions;
        private readonly Basic.BasicDOSProtector _baseProtector;
        private readonly HashSet<string> _whitelist;
        private readonly HashSet<string> _blacklist;
        private readonly List<IPNetwork> _whitelistNetworks;
        private readonly List<IPNetwork> _blacklistNetworks;

        public IPFilterDOSProtector(IPFilterDosProtectorOptions options)
            : base(options)
        {
            ArgumentNullException.ThrowIfNull(options);

            _filterOptions = options;
            _whitelist = new HashSet<string>(options.WhitelistedIPs ?? new List<string>());
            _blacklist = new HashSet<string>(options.BlacklistedIPs ?? new List<string>());
            _whitelistNetworks = ParseNetworks(options.WhitelistedCIDRs ?? new List<string>());
            _blacklistNetworks = ParseNetworks(options.BlacklistedCIDRs ?? new List<string>());

            // Create base protector for rate limiting
            _baseProtector = new Basic.BasicDOSProtector(options);

            Log(DOSProtectorLogLevel.Info,
                $"[IPFilterDOSProtector]: Initialized - " +
                $"Whitelist: {_whitelist.Count + _whitelistNetworks.Count}, " +
                $"Blacklist: {_blacklist.Count + _blacklistNetworks.Count}");
        }

        public override bool IsBlocked(string key, IDOSProtectorContext context = null)
        {
            string ip = ExtractIP(key);

            // Blacklist check (highest priority)
            if (IsBlacklisted(ip))
            {
                Log(DOSProtectorLogLevel.Warn,
                    $"[IPFilterDOSProtector]: {RedactClient(ip)} is blacklisted");
                return true;
            }

            // Whitelist bypass
            if (IsWhitelisted(ip))
            {
                Log(DOSProtectorLogLevel.Debug,
                    $"[IPFilterDOSProtector]: {RedactClient(ip)} is whitelisted, bypassing checks");
                return false;
            }

            // Regular DOS protection for non-whitelisted IPs
            return _baseProtector.IsBlocked(key, context);
        }

        public override bool Process(string key, string endpoint, IDOSProtectorContext context = null)
        {
            string ip = ExtractIP(key);

            // Blacklist check
            if (IsBlacklisted(ip))
            {
                Log(DOSProtectorLogLevel.Warn,
                    $"[IPFilterDOSProtector]: Blocked blacklisted IP: {RedactClient(ip)}");
                return false;
            }

            // Whitelist bypass
            if (IsWhitelisted(ip))
            {
                if (_filterOptions.WhitelistBypassesRateLimit)
                {
                    Log(DOSProtectorLogLevel.Debug,
                        $"[IPFilterDOSProtector]: Whitelisted IP {RedactClient(ip)} bypassing rate limit");
                    return true;
                }
            }

            // Regular DOS protection
            return _baseProtector.Process(key, endpoint, context);
        }

        public override void ProcessEnd(string key, string endpoint, IDOSProtectorContext context = null)
        {
            string ip = ExtractIP(key);

            // Skip ProcessEnd for whitelisted IPs that bypass rate limiting
            if (IsWhitelisted(ip) && _filterOptions.WhitelistBypassesRateLimit)
                return;

            _baseProtector.ProcessEnd(key, endpoint, context);
        }

        public override IDisposable CreateSession(string key, string endpoint, IDOSProtectorContext context = null)
        {
            if (!Process(key, endpoint, context))
                return new NullSession();

            return new SessionScope(this, key, endpoint, context);
        }

        public override void Dispose()
        {
            _baseProtector?.Dispose();
        }

        /// <summary>
        /// Checks if IP is in whitelist
        /// </summary>
        private bool IsWhitelisted(string ip)
        {
            if (string.IsNullOrEmpty(ip))
                return false;

            // Check exact match
            if (_whitelist.Contains(ip))
                return true;

            // Check CIDR ranges
            if (IPAddress.TryParse(ip, out var ipAddress))
            {
                return _whitelistNetworks.Any(network => network.Contains(ipAddress));
            }

            return false;
        }

        /// <summary>
        /// Checks if IP is in blacklist
        /// </summary>
        private bool IsBlacklisted(string ip)
        {
            if (string.IsNullOrEmpty(ip))
                return false;

            // Check exact match
            if (_blacklist.Contains(ip))
                return true;

            // Check CIDR ranges
            if (IPAddress.TryParse(ip, out var ipAddress))
            {
                return _blacklistNetworks.Any(network => network.Contains(ipAddress));
            }

            return false;
        }

        /// <summary>
        /// Extracts IP from key (handles "IP|UserID" format)
        /// </summary>
        private string ExtractIP(string key)
        {
            if (string.IsNullOrEmpty(key))
                return key;

            var parts = key.Split('|');
            return parts[0];
        }

        /// <summary>
        /// Parses CIDR notation into IPNetwork objects
        /// </summary>
        private List<IPNetwork> ParseNetworks(List<string> cidrs)
        {
            var networks = new List<IPNetwork>();

            foreach (var cidr in cidrs)
            {
                try
                {
                    networks.Add(IPNetwork.Parse(cidr));
                }
                catch (Exception ex)
                {
                    Log(DOSProtectorLogLevel.Error,
                        $"[IPFilterDOSProtector]: Invalid CIDR notation '{cidr}': {ex.Message}");
                }
            }

            return networks;
        }

        /// <summary>
        /// Represents an IP network (CIDR)
        /// </summary>
        private class IPNetwork
        {
            private readonly IPAddress _network;
            private readonly IPAddress _mask;

            private IPNetwork(IPAddress network, IPAddress mask)
            {
                _network = network;
                _mask = mask;
            }

            public static IPNetwork Parse(string cidr)
            {
                var parts = cidr.Split('/');
                if (parts.Length != 2)
                    throw new ArgumentException("Invalid CIDR notation");

                var network = IPAddress.Parse(parts[0]);
                int prefixLength = int.Parse(parts[1]);

                // Create subnet mask
                uint maskBits = 0xFFFFFFFF << (32 - prefixLength);
                var mask = new IPAddress(new[]
                {
                    (byte)(maskBits >> 24),
                    (byte)(maskBits >> 16),
                    (byte)(maskBits >> 8),
                    (byte)maskBits
                });

                return new IPNetwork(network, mask);
            }

            public bool Contains(IPAddress address)
            {
                if (address.AddressFamily != _network.AddressFamily)
                    return false;

                var networkBytes = _network.GetAddressBytes();
                var maskBytes = _mask.GetAddressBytes();
                var addressBytes = address.GetAddressBytes();

                for (int i = 0; i < networkBytes.Length; i++)
                {
                    if ((networkBytes[i] & maskBytes[i]) != (addressBytes[i] & maskBytes[i]))
                        return false;
                }

                return true;
            }
        }

        private class NullSession : IDisposable
        {
            public void Dispose() { }
        }
    }
}
