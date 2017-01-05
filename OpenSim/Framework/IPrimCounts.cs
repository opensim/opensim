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

using OpenMetaverse;

namespace OpenSim.Framework
{
    public interface IPrimCounts
    {
        /// <summary>
        /// Parcel owner owned prims
        /// </summary>
        int Owner { get; }

        /// <summary>
        /// Parcel group owned prims
        /// </summary>
        int Group { get; }

        /// <summary>
        /// Prims owned by others (not parcel owner or parcel group).
        /// </summary>
        int Others { get; }

        /// <summary>
        /// Selected prims
        /// </summary>
        int Selected { get; }

        /// <summary>
        /// Total prims on the parcel.
        /// </summary>
        int Total { get; }

        /// <summary>
        /// Prims on the simulator that are owned by the parcel owner, even if they are in other parcels.
        /// </summary>
        int Simulator { get; }

        /// <summary>
        /// Prims per individual users.
        /// </summary>
        IUserPrimCounts Users { get; }
    }

    public interface IUserPrimCounts
    {
        int this[UUID agentID] { get; }
    }
}