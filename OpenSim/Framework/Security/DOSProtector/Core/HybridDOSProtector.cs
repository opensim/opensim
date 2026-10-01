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
using System.Linq;
using OpenSim.Framework.Security.DOSProtector.SDK;

namespace OpenSim.Framework.Security.DOSProtector.Core
{
    /// <summary>
    /// Hybrid DOS protector that chains multiple protector implementations.
    /// Processes requests through all protectors in order - all must pass for request to succeed.
    /// </summary>
    public class HybridDOSProtector : IDOSProtector
    {
        private readonly List<IDOSProtector> _protectors;
        private bool _disposed;

        /// <summary>
        /// Creates a hybrid protector from a list of protector instances
        /// </summary>
        /// <param name="protectors">List of protector instances to chain (order matters)</param>
        public HybridDOSProtector(IEnumerable<IDOSProtector> protectors)
        {
            if (protectors == null)
                throw new ArgumentNullException(nameof(protectors));

            _protectors = protectors.Where(p => p != null).ToList();

            if (_protectors.Count == 0)
                throw new ArgumentException("At least one protector must be provided", nameof(protectors));
        }

        /// <summary>
        /// Checks if the client is blocked by any of the protectors
        /// </summary>
        public bool IsBlocked(string key, IDOSProtectorContext context = null)
        {
            foreach (var protector in _protectors)
            {
                if (protector.IsBlocked(key, context))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Processes a request through all protectors in order.
        /// All protectors must pass for the request to succeed.
        /// </summary>
        public bool Process(string key, string endpoint, IDOSProtectorContext context = null)
        {
            foreach (var protector in _protectors)
            {
                if (!protector.Process(key, endpoint, context))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Signals end of request processing to all protectors
        /// </summary>
        public void ProcessEnd(string key, string endpoint, IDOSProtectorContext context = null)
        {
            // ProcessEnd in reverse order (stack discipline)
            for (int i = _protectors.Count - 1; i >= 0; --i)
            {
                try { _protectors[i].ProcessEnd(key, endpoint, context); }
                catch (Exception)
                {
                    // swallow per protector; 
                    // @todo: log?
                }
            }
        }

        /// <summary>
        /// Creates a session scope that automatically calls ProcessEnd on disposal.
        /// The session succeeds only if all protectors allow it.
        /// </summary>
        public IDisposable CreateSession(string key, string endpoint, IDOSProtectorContext context = null)
        {
            
            // Create nested guards in order; dispose in reverse
            var guards = new Stack<IDisposable>(_protectors.Count);
            
            foreach (var p in _protectors)
            {
                var g = p.CreateSession(key, endpoint, context);
                if (g != null) guards.Push(g);
            }
            
            return new CompositeGuard(this, key, endpoint, context, guards);
            
            // Check all protectors before creating session
            /*
            if (!Process(key, endpoint, context))
                return null;

            return new HybridSessionScope(this, key, endpoint, context);
            */
        }

        /// <summary>
        /// Disposes all contained protectors
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            foreach (var protector in _protectors)
            {
                protector?.Dispose();
            }

            _disposed = true;
        }

        /// <summary>
        /// Session scope for automatic cleanup
        /// </summary>
        /*
        private readonly struct HybridSessionScope : IDisposable
        {
            private readonly HybridDOSProtector _protector;
            private readonly string _key;
            private readonly string _endpoint;
            private readonly IDOSProtectorContext _context;

            public HybridSessionScope(HybridDOSProtector protector, string key, string endpoint, IDOSProtectorContext context = null)
            {
                _protector = protector;
                _key = key;
                _endpoint = endpoint;
                _context = context;
            }

            public void Dispose()
            {
                _protector?.ProcessEnd(_key, _endpoint, _context);
            }
        }
        */
        
        private sealed class CompositeGuard : IDisposable
        {
            private readonly HybridDOSProtector _owner;
            private readonly string _key;
            private readonly string _endpoint;
            private readonly IDOSProtectorContext _context;
            private readonly Stack<IDisposable> _guards; // LIFO
            private bool _disposed;


            public CompositeGuard(HybridDOSProtector owner, string key, string endpoint, IDOSProtectorContext ctx, Stack<IDisposable> guards)
            {
                _owner = owner;
                _key = key;
                _endpoint = endpoint;
                _context = ctx;
                _guards = guards;
            }
            
            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                
                while (_guards.Count > 0)
                {
                    try { _guards.Pop().Dispose(); } 
                    catch { /* ignore */ }
                }
                
                // Ensure ProcessEnd semantics
                try { _owner.ProcessEnd(_key, _endpoint, _context); } 
                catch { /* ignore */ }
            }
        }
    }
}
