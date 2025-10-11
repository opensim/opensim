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
    /// Configuration options for HoneypotDOSProtector.
    /// Detects bot behavior through honeypot traps and timing analysis.
    /// </summary>
    public class HoneypotDosProtectorOptions : BasicDosProtectorOptions
    {
        /// <summary>
        /// Minimum time between requests in milliseconds.
        /// Requests faster than this are considered suspicious (inhuman speed).
        /// Default: 100ms (10 requests/second)
        /// </summary>
        public int MinRequestIntervalMs { get; set; } = 100;

        /// <summary>
        /// Number of fast requests before marking client as suspicious bot.
        /// Default: 3
        /// </summary>
        public int FastRequestThreshold { get; set; } = 3;

        /// <summary>
        /// List of honeypot trap endpoints.
        /// Any request to these endpoints marks the client as a bot.
        /// Example: ["/admin", "/wp-admin", "/.env", "/phpmyadmin"]
        /// </summary>
        public List<string> TrapEndpoints { get; set; } = new();

        /// <summary>
        /// Enable detection of inhuman request speed.
        /// Default: true
        /// </summary>
        public bool DetectInhumanSpeed { get; set; } = true;

        /// <summary>
        /// Constructor with sensible defaults
        /// </summary>
        public HoneypotDosProtectorOptions()
        {
            ReportingName = "Honeypot";
        }
    }
}
