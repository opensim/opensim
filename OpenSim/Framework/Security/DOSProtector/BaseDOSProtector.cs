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
using System.Reflection;
using log4net;

using OpenSim.Framework.Security.DOSProtector.Interfaces;
using OpenSim.Framework.Security.DOSProtector.Options;

namespace OpenSim.Framework.Security.DOSProtector
{
    /// <summary>
    /// Abstract base class for DOS protector implementations providing common logging functionality
    /// </summary>
    public abstract class BaseDOSProtector(IDOSProtectorOptions options) : IDOSProtector
    {
        // ReSharper disable once MemberCanBePrivate.Global
        protected static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod()?.DeclaringType);
        protected readonly IDOSProtectorOptions _options = options;

        /// <summary>
        /// Redacts client identifier for logging if RedactClientIdentifiers is enabled
        /// </summary>
        protected virtual string RedactClient(string clientIdentifier)
        {
            if (!_options.RedactClientIdentifiers || string.IsNullOrEmpty(clientIdentifier))
                return clientIdentifier;

            // Redact IP addresses: "192.168.1.100" → "192.168.***.***"
            var parts = clientIdentifier.Split('.');
            if (parts.Length == 4) // IPv4
            {
                return $"{parts[0]}.{parts[1]}.***.***.***";
            }

            // For other formats, show first 8 chars
            return clientIdentifier.Length > 8
                ? clientIdentifier.Substring(0, 8) + "***"
                : "***";
        }

        /// <summary>
        /// Logs message at appropriate level based on configuration
        /// </summary>
        protected virtual void Log(DOSProtectorLogLevel level, string message)
        {
            if (_options.LogLevel < level)
                return;

            switch (level)
            {
                case DOSProtectorLogLevel.Error:
                    m_log.Error(message);
                    break;
                case DOSProtectorLogLevel.Warn:
                    m_log.Warn(message);
                    break;
                case DOSProtectorLogLevel.Info:
                    m_log.Info(message);
                    break;
                case DOSProtectorLogLevel.Debug:
                    m_log.Debug(message);
                    break;
            }
        }

        public abstract void Dispose();
        public abstract bool IsBlocked(string key);
        public abstract bool Process(string key, string endpoint);
        public abstract void ProcessEnd(string key, string endpoint);
        public abstract IDisposable CreateSession(string key, string endpoint);

        /// <summary>
        /// Helper struct for automatic session cleanup using 'using' pattern.
        /// Example: using (var session = protector.CreateSession(key, endpoint)) { ... }
        /// </summary>
        protected readonly struct SessionScope : IDisposable
        {
            private readonly IDOSProtector _protector;
            private readonly string _key;
            private readonly string _endpoint;

            internal SessionScope(IDOSProtector protector, string key, string endpoint)
            {
                _protector = protector;
                _key = key;
                _endpoint = endpoint;
            }

            public void Dispose()
            {
                _protector?.ProcessEnd(_key, _endpoint);
            }
        }
    }
}
