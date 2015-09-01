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

namespace OpenSim.Framework.Communications.Limit
{
    /// <summary>
    /// Limit requests by discarding repeat attempts that occur within a given time period
    ///
    /// XXX Don't use this for limiting texture downloading, at least not until we better handle multiple requests
    /// for the same texture at different resolutions.
    /// </summary>
    public class TimeLimitStrategy<TId> : IRequestLimitStrategy<TId>
    {
        /// <summary>
        /// Record the time at which an asset request occurs.
        /// </summary>
        private readonly Dictionary<TId, Request> requests = new Dictionary<TId, Request>();

        /// <summary>
        /// The minimum time period between which requests for the same data will be serviced.
        /// </summary>
        private readonly TimeSpan m_repeatPeriod;
        public TimeSpan RepeatPeriod
        {
            get { return m_repeatPeriod; }
        }

        /// <summary></summary>
        /// <param name="repeatPeriod"></param>
        public TimeLimitStrategy(TimeSpan repeatPeriod)
        {
            m_repeatPeriod = repeatPeriod;
        }

        /// <summary>
        /// <see cref="IRequestLimitStrategy"/>
        /// </summary>
        public bool AllowRequest(TId id)
        {
            if (IsMonitoringRequests(id))
            {
                DateTime now = DateTime.Now;
                TimeSpan elapsed = now - requests[id].Time;

                if (elapsed < RepeatPeriod)
                {
                    requests[id].Refusals += 1;
                    return false;
                }

                requests[id].Time = now;
            }

            return true;
        }

        /// <summary>
        /// <see cref="IRequestLimitStrategy"/>
        /// </summary>
        public bool IsFirstRefusal(TId id)
        {
            if (IsMonitoringRequests(id))
            {
                if (1 == requests[id].Refusals)
                {
                    return true;
                }
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
                requests.Add(id, new Request(DateTime.Now));
            }
        }

        /// <summary>
        /// <see cref="IRequestLimitStrategy"/>
        /// </summary>
        public bool IsMonitoringRequests(TId id)
        {
            return requests.ContainsKey(id);
        }
    }

    /// <summary>
    /// Private request details.
    /// </summary>
    class Request
    {
        /// <summary>
        /// Time of last request
        /// </summary>
        public DateTime Time;

        /// <summary>
        /// Number of refusals associated with this request
        /// </summary>
        public int Refusals;

        public Request(DateTime time)
        {
            Time = time;
        }
    }
}
