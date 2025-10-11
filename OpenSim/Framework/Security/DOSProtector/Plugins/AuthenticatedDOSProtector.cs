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
using OpenSim.Framework.Security.DOSProtector.Attributes;
using OpenSim.Framework.Security.DOSProtector.Interfaces;
using OpenSim.Framework.Security.DOSProtector.Options;

namespace OpenSim.Framework.Security.DOSProtector.Plugins
{
    /// <summary>
    /// DOS Protector with different rate limits for authenticated vs anonymous users.
    /// Authenticated users get higher limits, anonymous users are more restricted.
    /// Key format: "IP|UserID" for authenticated, "IP|anonymous" for anonymous
    /// </summary>
    [DOSProtectorOptions(typeof(AuthenticatedDosProtectorOptions))]
    public class AuthenticatedDOSProtector : BaseDOSProtector
    {
        private readonly AuthenticatedDosProtectorOptions _authOptions;
        private readonly DOSProtector.BasicDOSProtector _anonymousProtector;
        private readonly DOSProtector.BasicDOSProtector _authenticatedProtector;

        public AuthenticatedDOSProtector(AuthenticatedDosProtectorOptions options)
            : base(options)
        {
            ArgumentNullException.ThrowIfNull(options);

            _authOptions = options;

            // Create protector for anonymous users (strict limits)
            var anonymousOptions = new Options.BasicDosProtectorOptions
            {
                MaxRequestsInTimeframe = options.AnonymousMaxRequests,
                RequestTimeSpan = options.RequestTimeSpan,
                ForgetTimeSpan = options.ForgetTimeSpan,
                MaxConcurrentSessions = options.AnonymousMaxConcurrentSessions,
                AllowXForwardedFor = options.AllowXForwardedFor,
                ThrottledAction = options.ThrottledAction,
                InspectionTTL = options.InspectionTTL,
                LogLevel = options.LogLevel,
                RedactClientIdentifiers = options.RedactClientIdentifiers,
                ReportingName = $"{options.ReportingName}-Anonymous"
            };
            _anonymousProtector = new DOSProtector.BasicDOSProtector(anonymousOptions);

            // Create protector for authenticated users (relaxed limits)
            var authenticatedOptions = new Options.BasicDosProtectorOptions
            {
                MaxRequestsInTimeframe = options.AuthenticatedMaxRequests,
                RequestTimeSpan = options.RequestTimeSpan,
                ForgetTimeSpan = options.ForgetTimeSpan,
                MaxConcurrentSessions = options.AuthenticatedMaxConcurrentSessions,
                AllowXForwardedFor = options.AllowXForwardedFor,
                ThrottledAction = options.ThrottledAction,
                InspectionTTL = options.InspectionTTL,
                LogLevel = options.LogLevel,
                RedactClientIdentifiers = options.RedactClientIdentifiers,
                ReportingName = $"{options.ReportingName}-Authenticated"
            };
            _authenticatedProtector = new DOSProtector.BasicDOSProtector(authenticatedOptions);

            Log(DOSProtectorLogLevel.Info,
                $"[AuthenticatedDOSProtector]: Initialized - " +
                $"Anonymous: {options.AnonymousMaxRequests} req/min, " +
                $"Authenticated: {options.AuthenticatedMaxRequests} req/min");
        }

        public override bool IsBlocked(string key, IDOSProtectorContext context = null)
        {
            var protector = GetProtectorForKey(key);
            return protector.IsBlocked(key, context);
        }

        public override bool Process(string key, string endpoint, IDOSProtectorContext context = null)
        {
            var protector = GetProtectorForKey(key);
            bool isAuthenticated = IsAuthenticatedKey(key);

            Log(DOSProtectorLogLevel.Debug,
                $"[AuthenticatedDOSProtector]: Processing {RedactClient(key)} as " +
                $"{(isAuthenticated ? "authenticated" : "anonymous")} user");

            return protector.Process(key, endpoint, context);
        }

        public override void ProcessEnd(string key, string endpoint, IDOSProtectorContext context = null)
        {
            var protector = GetProtectorForKey(key);
            protector.ProcessEnd(key, endpoint, context);
        }

        public override IDisposable CreateSession(string key, string endpoint, IDOSProtectorContext context = null)
        {
            var protector = GetProtectorForKey(key);
            return protector.CreateSession(key, endpoint, context);
        }

        public override void Dispose()
        {
            _anonymousProtector?.Dispose();
            _authenticatedProtector?.Dispose();
        }

        /// <summary>
        /// Determines if the key represents an authenticated user.
        /// Key format: "IP|UserID" for authenticated, "IP|anonymous" or "IP" for anonymous
        /// </summary>
        private bool IsAuthenticatedKey(string key)
        {
            if (string.IsNullOrEmpty(key))
                return false;

            var parts = key.Split('|');
            if (parts.Length < 2)
                return false;

            string userPart = parts[1].ToLowerInvariant();
            return userPart != "anonymous" &&
                   userPart != "null" &&
                   userPart != "00000000-0000-0000-0000-000000000000" &&
                   !string.IsNullOrEmpty(userPart);
        }

        /// <summary>
        /// Selects the appropriate protector based on authentication status
        /// </summary>
        private DOSProtector.BasicDOSProtector GetProtectorForKey(string key)
        {
            return IsAuthenticatedKey(key)
                ? _authenticatedProtector
                : _anonymousProtector;
        }
    }
}
