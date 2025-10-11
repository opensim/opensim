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

using System.Collections.Generic;

namespace OpenSim.Framework.Security.DOSProtector.Options
{
    /// <summary>
    /// Configuration options for IPFilterDOSProtector.
    /// Provides IP whitelist/blacklist filtering with CIDR support.
    /// </summary>
    public class IPFilterDosProtectorOptions : BasicDosProtectorOptions
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
}
