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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;


namespace OpenSim.Framework.Security.DOSProtector.SDK
{
    /// <summary>
    /// Thread-safe implementation of IDOSProtectorContext using ConcurrentDictionary.
    /// Supports namespaced keys to prevent collisions between plugins.
    /// </summary>
    public class DOSProtectorContext : IDOSProtectorContext
    {
        private readonly ConcurrentDictionary<string, object> _contextData = new();

        /// <summary>
        /// Read-only access to all context data.
        /// </summary>
        public IReadOnlyDictionary<string, object> ContextData => _contextData;

        /// <summary>
        /// Builds a namespaced key from key and optional namespace.
        /// Format: "namespace:key" or just "key" if namespace is null/empty.
        /// </summary>
        private static string BuildKey(string key, string @namespace)
        {
            if (string.IsNullOrEmpty(@namespace))
                return key;
            return $"{@namespace}:{key}";
        }

        /// <summary>
        /// Validates that a key is not null or empty.
        /// </summary>
        private static void ValidateKey(string key)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentNullException(nameof(key), "Context key cannot be null or empty");
        }

        /// <inheritdoc/>
        public T GetContextData<T>(string key, string @namespace = null, T defaultValue = default)
        {
            ValidateKey(key);
            string fullKey = BuildKey(key, @namespace);

            if (_contextData.TryGetValue(fullKey, out var value) && value is T typedValue)
                return typedValue;

            return defaultValue;
        }

        /// <inheritdoc/>
        public bool TryGetContextData<T>(string key, out T value, string @namespace = null)
        {
            ValidateKey(key);
            string fullKey = BuildKey(key, @namespace);

            if (_contextData.TryGetValue(fullKey, out var objValue) && objValue is T typedValue)
            {
                value = typedValue;
                return true;
            }

            value = default;
            return false;
        }

        /// <inheritdoc/>
        public void SetContextData(string key, object value, string @namespace = null, bool allowOverwrite = true)
        {
            ValidateKey(key);
            string fullKey = BuildKey(key, @namespace);

            if (!allowOverwrite && _contextData.ContainsKey(fullKey))
            {
                throw new InvalidOperationException(
                    $"Context key '{fullKey}' already exists and allowOverwrite is false");
            }

            _contextData[fullKey] = value;
        }

        /// <inheritdoc/>
        public bool HasContextData(string key, string @namespace = null)
        {
            ValidateKey(key);
            string fullKey = BuildKey(key, @namespace);
            return _contextData.ContainsKey(fullKey);
        }

        /// <inheritdoc/>
        public bool RemoveContextData(string key, string @namespace = null)
        {
            ValidateKey(key);
            string fullKey = BuildKey(key, @namespace);
            return _contextData.TryRemove(fullKey, out _);
        }

        /// <inheritdoc/>
        public IEnumerable<string> GetKeys(string @namespace = null)
        {
            if (string.IsNullOrEmpty(@namespace))
            {
                // Return all keys as-is
                return _contextData.Keys.ToList();
            }

            // Filter by namespace and strip namespace prefix
            string prefix = $"{@namespace}:";
            return _contextData.Keys
                .Where(k => k.StartsWith(prefix))
                .Select(k => k.Substring(prefix.Length))
                .ToList();
        }
    }
}
