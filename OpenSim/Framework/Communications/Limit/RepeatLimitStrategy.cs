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

namespace OpenSim.Framework.Communications.Limit
{
    /// <summary>
    /// Limit requests by discarding them after they've been repeated a certain number of times.
    /// </summary>
    public class RepeatLimitStrategy<TId> : IRequestLimitStrategy<TId>
    {
        /// <summary>
        /// Record each asset request that we're notified about.
        /// </summary>
        private readonly Dictionary<TId, int> requestCounts = new Dictionary<TId, int>();

        /// <summary>
        /// The maximum number of requests that can be made before we drop subsequent requests.
        /// </summary>
        private readonly int m_maxRequests;
        public int MaxRequests
        {
            get { return m_maxRequests; }
        }

        /// <summary></summary>
        /// <param name="maxRequests">The maximum number of requests that may be served before all further
        /// requests are dropped.</param>
        public RepeatLimitStrategy(int maxRequests)
        {
            m_maxRequests = maxRequests;
        }

        /// <summary>
        /// <see cref="IRequestLimitStrategy"/>
        /// </summary>
        public bool AllowRequest(TId id)
        {
            if (requestCounts.ContainsKey(id))
            {
                requestCounts[id] += 1;

                if (requestCounts[id] > m_maxRequests)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// <see cref="IRequestLimitStrategy"/>
        /// </summary>
        public bool IsFirstRefusal(TId id)
        {
            if (requestCounts.ContainsKey(id) && m_maxRequests + 1 == requestCounts[id])
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// <see cref="IRequestLimitStrategy"/>
        /// </summary>
        public void MonitorRequests(TId id)
        {
            if (!IsMonitoringRequests(id))
            {
                requestCounts.Add(id, 1);
            }
        }

        /// <summary>
        /// <see cref="IRequestLimitStrategy"/>
        /// </summary>
        public bool IsMonitoringRequests(TId id)
        {
            return requestCounts.ContainsKey(id);
        }
    }
}
