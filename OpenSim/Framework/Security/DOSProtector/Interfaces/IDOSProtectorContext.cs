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

namespace OpenSim.Framework.Security.DOSProtector.Interfaces
{
    /// <summary>
    /// Context object for passing additional request data to DOS protector implementations.
    /// Thread-safe and supports namespaced keys to prevent collisions between plugins.
    /// </summary>
    public interface IDOSProtectorContext
    {
        /// <summary>
        /// Read-only access to all context data.
        /// Key format: "key" for default namespace, "namespace:key" for namespaced data.
        /// </summary>
        IReadOnlyDictionary<string, object> ContextData { get; }

        /// <summary>
        /// Gets a strongly-typed value from the context.
        /// </summary>
        /// <typeparam name="T">The type to cast the value to</typeparam>
        /// <param name="key">The context key</param>
        /// <param name="namespace">Optional namespace to avoid key collisions (default: null)</param>
        /// <param name="defaultValue">Value to return if key not found or type mismatch</param>
        /// <returns>The value cast to T, or defaultValue if not found</returns>
        T GetContextData<T>(string key, string @namespace = null, T defaultValue = default);

        /// <summary>
        /// Tries to get a strongly-typed value from the context.
        /// </summary>
        /// <typeparam name="T">The type to cast the value to</typeparam>
        /// <param name="key">The context key</param>
        /// <param name="value">The output value if found and type matches</param>
        /// <param name="namespace">Optional namespace to avoid key collisions (default: null)</param>
        /// <returns>True if value was found and type matches, false otherwise</returns>
        bool TryGetContextData<T>(string key, out T value, string @namespace = null);

        /// <summary>
        /// Sets a value in the context.
        /// </summary>
        /// <param name="key">The context key (cannot be null or empty)</param>
        /// <param name="value">The value to store (can be null)</param>
        /// <param name="namespace">Optional namespace to avoid key collisions (default: null)</param>
        /// <param name="allowOverwrite">If false, throws exception when key already exists (default: true)</param>
        /// <exception cref="System.ArgumentNullException">Thrown when key is null or empty</exception>
        /// <exception cref="System.InvalidOperationException">Thrown when key exists and allowOverwrite is false</exception>
        void SetContextData(string key, object value, string @namespace = null, bool allowOverwrite = true);

        /// <summary>
        /// Checks if a key exists in the context.
        /// </summary>
        /// <param name="key">The context key</param>
        /// <param name="namespace">Optional namespace (default: null)</param>
        /// <returns>True if key exists, false otherwise</returns>
        bool HasContextData(string key, string @namespace = null);

        /// <summary>
        /// Removes a key from the context.
        /// </summary>
        /// <param name="key">The context key</param>
        /// <param name="namespace">Optional namespace (default: null)</param>
        /// <returns>True if key was found and removed, false otherwise</returns>
        bool RemoveContextData(string key, string @namespace = null);

        /// <summary>
        /// Gets all keys in the context, optionally filtered by namespace.
        /// </summary>
        /// <param name="namespace">Optional namespace filter (default: null = all keys)</param>
        /// <returns>Enumerable of context keys</returns>
        IEnumerable<string> GetKeys(string @namespace = null);
    }
}
