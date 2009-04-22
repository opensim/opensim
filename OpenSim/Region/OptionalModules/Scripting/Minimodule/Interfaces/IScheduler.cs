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

namespace OpenSim.Region.OptionalModules.Scripting.Minimodule
{
    interface IScheduler
    {
        /// <summary>
        /// Schedule an event callback to occur
        /// when 'time' is elapsed.
        /// </summary>
        /// <param name="time">The period to wait before executing</param>
        void RunIn(TimeSpan time);

        /// <summary>
        /// Schedule an event callback to fire
        /// every "time". Equivilent to a repeating
        /// timer.
        /// </summary>
        /// <param name="time">The period to wait between executions</param>
        void RunAndRepeat(TimeSpan time);

        /// <summary>
        /// Fire this scheduler only when the region has
        /// a user in it.
        /// </summary>
        bool IfOccupied { get; set; }

        /// <summary>
        /// Fire this only when simulator performance
        /// is reasonable. (eg sysload <= 1.0)
        /// </summary>
        bool IfHealthy { get; set; }

        /// <summary>
        /// Fire this event only when the region is visible
        /// to a child agent, or there is a full agent
        /// in this region.
        /// </summary>
        bool IfVisible { get; set; }

        /// <summary>
        /// Determines whether this runs in the master scheduler thread, or a new thread
        /// is spawned to handle your request. Running in scheduler may mean that your
        /// code does not execute perfectly on time, however will result in better
        /// region performance.
        /// </summary>
        /// <remarks>
        /// Default: true
        /// </remarks>
        bool Schedule { get; set; }
    }
}
