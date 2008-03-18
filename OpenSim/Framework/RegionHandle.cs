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
 *     * Neither the name of the OpenSim Project nor the
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
using System.Net;
using System.Net.Sockets;

namespace OpenSim.Framework
{
    /// <summary>
    /// A class for manipulating RegionHandle coordinates
    /// </summary>
    internal class RegionHandle
    {
        private UInt64 handle;

        /// <summary>
        /// Initialises a new grid-aware RegionHandle
        /// </summary>
        /// <param name="ip">IP Address of the Grid Server for this region</param>
        /// <param name="x">Grid X Coordinate</param>
        /// <param name="y">Grid Y Coordinate</param>
        public RegionHandle(string ip, short x, short y)
        {
            IPAddress addr = IPAddress.Parse(ip);

            if (addr.AddressFamily != AddressFamily.InterNetwork)
                throw new Exception("Bad RegionHandle Parameter - must be an IPv4 address");

            uint baseHandle = BitConverter.ToUInt32(addr.GetAddressBytes(), 0);

            // Split the IP address in half
            short a = (short) ((baseHandle << 16) & 0xFFFF);
            short b = (short) ((baseHandle << 0) & 0xFFFF);

            // Raise the bounds a little
            uint nx = (uint) x;
            uint ny = (uint) y;

            // Multiply grid coords to get region coords
            nx *= Constants.RegionSize;
            ny *= Constants.RegionSize;

            // Stuff the IP address in too
            nx = (uint) a << 16;
            ny = (uint) b << 16;

            handle = ((UInt64) nx << 32) | (uint) ny;
        }

        /// <summary>
        /// Initialises a new RegionHandle that is not inter-grid aware
        /// </summary>
        /// <param name="x">Grid X Coordinate</param>
        /// <param name="y">Grid Y Coordinate</param>
        public RegionHandle(uint x, uint y)
        {
            handle = ((x * Constants.RegionSize) << 32) | (y * Constants.RegionSize);
        }

        /// <summary>
        /// Initialises a new RegionHandle from an existing value
        /// </summary>
        /// <param name="Region">A U64 RegionHandle</param>
        public RegionHandle(UInt64 Region)
        {
            handle = Region;
        }

        /// <summary>
        /// Returns the Grid Masked RegionHandle - For use in Teleport packets and other packets where sending the grid IP address may be handy.
        /// </summary>
        /// <remarks>Do not use for SimulatorEnable packets. The client will choke.</remarks>
        /// <returns>Region Handle including IP Address encoding</returns>
        public UInt64 getTeleportHandle()
        {
            return handle;
        }

        /// <summary>
        /// Returns a RegionHandle which may be used for SimulatorEnable packets. Removes the IP address encoding and returns the lower bounds.
        /// </summary>
        /// <returns>A U64 RegionHandle for use in SimulatorEnable packets.</returns>
        public UInt64 getNeighbourHandle()
        {
            UInt64 mask = 0x0000FFFF0000FFFF;

            return handle | mask;
        }

        /// <summary>
        /// Returns the IP Address of the GridServer from a Grid-Encoded RegionHandle
        /// </summary>
        /// <returns>Grid Server IP Address</returns>
        public IPAddress getGridIP()
        {
            uint a = (uint) ((handle >> 16) & 0xFFFF);
            uint b = (uint) ((handle >> 48) & 0xFFFF);

            return new IPAddress((long) (a << 16) | (long) b);
        }

        /// <summary>
        /// Returns the X Coordinate from a Grid-Encoded RegionHandle
        /// </summary>
        /// <returns>X Coordinate</returns>
        public uint getGridX()
        {
            uint x = (uint) ((handle >> 32) & 0xFFFF);

            return x;
        }

        /// <summary>
        /// Returns the Y Coordinate from a Grid-Encoded RegionHandle
        /// </summary>
        /// <returns>Y Coordinate</returns>
        public uint getGridY()
        {
            uint y = (uint) ((handle >> 0) & 0xFFFF);

            return y;
        }
    }
}
