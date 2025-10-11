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
using OpenSim.Framework.Security.DOSProtector.Interfaces;

namespace OpenSim.Framework.Security.DOSProtector.Options
{
    
    public class BasicDosProtectorOptions : IDOSProtectorOptions
    {
        public int MaxRequestsInTimeframe { get; set; }
        public TimeSpan RequestTimeSpan { get; set; }
        public TimeSpan ForgetTimeSpan { get; set; }
        public bool AllowXForwardedFor { get; set; }
        public string ReportingName { get; set; } = "BASICDOSPROTECTOR";
        public ThrottleAction ThrottledAction { get; set; } = ThrottleAction.DoThrottledMethod;
        public int MaxConcurrentSessions { get; set; }

        /// <summary>
        /// Time-To-Live for inspection entries. Inactive clients are removed after this duration.
        /// Defaults to 10 minutes to allow for temporary traffic bursts.
        /// </summary>
        public TimeSpan InspectionTTL { get; set; } = TimeSpan.FromMinutes(10);

        /// <summary>
        /// Log level for DOS protection events.
        /// Controls verbosity of logging to prevent log spam during attacks.
        /// Default: Warn (logs blocks and warnings)
        /// </summary>
        public DOSProtectorLogLevel LogLevel { get; set; } = DOSProtectorLogLevel.Warn;

        /// <summary>
        /// Redact client identifiers (IPs) in log messages for privacy/GDPR compliance.
        /// When enabled, logs show partial identifiers (e.g., "192.168.***.***").
        /// Default: false (full identifiers logged)
        /// </summary>
        public bool RedactClientIdentifiers { get; set; } = false;
    }
}
