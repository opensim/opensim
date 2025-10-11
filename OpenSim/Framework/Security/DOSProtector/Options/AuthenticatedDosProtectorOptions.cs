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

namespace OpenSim.Framework.Security.DOSProtector.Options
{
    /// <summary>
    /// Configuration options for AuthenticatedDOSProtector.
    /// Provides different rate limits for authenticated vs anonymous users.
    /// </summary>
    public class AuthenticatedDosProtectorOptions : BasicDosProtectorOptions
    {
        /// <summary>
        /// Maximum requests per timeframe for anonymous users.
        /// Should be more restrictive than authenticated limits.
        /// Default: 10 requests
        /// </summary>
        public int AnonymousMaxRequests { get; set; } = 10;

        /// <summary>
        /// Maximum requests per timeframe for authenticated users.
        /// Can be more relaxed for trusted logged-in users.
        /// Default: 50 requests
        /// </summary>
        public int AuthenticatedMaxRequests { get; set; } = 50;

        /// <summary>
        /// Maximum concurrent sessions for anonymous users.
        /// Default: 2 sessions
        /// </summary>
        public int AnonymousMaxConcurrentSessions { get; set; } = 2;

        /// <summary>
        /// Maximum concurrent sessions for authenticated users.
        /// Default: 10 sessions
        /// </summary>
        public int AuthenticatedMaxConcurrentSessions { get; set; } = 10;

        /// <summary>
        /// Constructor with sensible defaults
        /// </summary>
        public AuthenticatedDosProtectorOptions()
        {
            ReportingName = "AuthenticatedDOS";
            RequestTimeSpan = TimeSpan.FromMinutes(1);
            ForgetTimeSpan = TimeSpan.FromMinutes(5);
        }
    }
}
