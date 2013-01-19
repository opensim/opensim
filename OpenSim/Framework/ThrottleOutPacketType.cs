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

namespace OpenSim.Framework
{
    public enum ThrottleOutPacketType : int
    {
        /// <summary>Unthrottled packets</summary>
        Unknown = -1,
        /// <summary>Packets that are being resent</summary>
        Resend = 0,
        /// <summary>Terrain data</summary>
        Land = 1,
        /// <summary>Wind data</summary>
        Wind = 2,
        /// <summary>Cloud data</summary>
        Cloud = 3,
        /// <summary>Any packets that do not fit into the other throttles</summary>
        Task = 4,
        /// <summary>Texture assets</summary>
        Texture = 5,
        /// <summary>Non-texture assets</summary>
        Asset = 6,

        HighPriority = 128,
    }

    [Flags]
    public enum ThrottleOutPacketTypeFlags
    {
        Land = 1 << 0,
        Wind = 1 << 1,
        Cloud = 1 << 2,
        Task = 1 << 3,
        Texture = 1 << 4,
        Asset = 1 << 5,
    }
}
