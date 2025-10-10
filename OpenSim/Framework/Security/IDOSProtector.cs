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

namespace OpenSim.Framework.Security
{
    /// <summary>
    /// Interface for DOS protection implementations
    /// </summary>
    public interface IDOSProtector : IDisposable
    {
        /// <summary>
        /// Check if a given key is currently blocked
        /// </summary>
        /// <param name="key">A Key identifying the context</param>
        /// <returns>True if blocked, false otherwise</returns>
        bool IsBlocked(string key);

        /// <summary>
        /// Process the velocity of this context
        /// </summary>
        /// <param name="key">A Key identifying the context</param>
        /// <param name="endpoint">The endpoint for logging purposes</param>
        /// <returns>True if allowed, false if throttled</returns>
        bool Process(string key, string endpoint);

        /// <summary>
        /// Mark the end of processing for this context (decrements session counter)
        /// </summary>
        /// <param name="key">A Key identifying the context</param>
        /// <param name="endpoint">The endpoint for logging purposes</param>
        void ProcessEnd(string key, string endpoint);

        /// <summary>
        /// Creates a disposable session scope that automatically calls ProcessEnd when disposed.
        /// Use with 'using' statement to ensure ProcessEnd is always called.
        /// </summary>
        /// <param name="key">A Key identifying the context</param>
        /// <param name="endpoint">The endpoint for logging purposes</param>
        /// <returns>A SessionScope that calls ProcessEnd on dispose</returns>
        IDisposable CreateSession(string key, string endpoint);
    }
}
