using System;
using System.Net;

namespace OpenSim.Framework.Types
{
    /// <summary>
    /// A class for manipulating RegionHandle coordinates
    /// </summary>
    class RegionHandle
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

            long baseHandle = addr.Address;

            // Split the IP address in half
            short a = (short)((baseHandle << 16) & 0xFFFF);
            short b = (short)((baseHandle <<  0) & 0xFFFF);

            // Raise the bounds a little
            uint nx = (uint)x;
            uint ny = (uint)y;

            // Multiply grid coords to get region coords
            nx *= 256;
            ny *= 256;

            // Stuff the IP address in too
            nx = (uint)a << 16;
            ny = (uint)b << 16;

            handle = ((UInt64)nx << 32) | (uint)ny;
        }

        /// <summary>
        /// Initialises a new RegionHandle that is not inter-grid aware
        /// </summary>
        /// <param name="x">Grid X Coordinate</param>
        /// <param name="y">Grid Y Coordinate</param>
        public RegionHandle(uint x, uint y)
        {
            handle = ((x * 256) << 32) | (y * 256);
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
            uint a = (uint)((handle >> 16) & 0xFFFF);
            uint b = (uint)((handle >> 48) & 0xFFFF);

            return new IPAddress((long)(a << 16) | (long)b);
        }

        /// <summary>
        /// Returns the X Coordinate from a Grid-Encoded RegionHandle
        /// </summary>
        /// <returns>X Coordinate</returns>
        public uint getGridX()
        {
            uint x = (uint)((handle >> 32) & 0xFFFF);

            return x;
        }

        /// <summary>
        /// Returns the Y Coordinate from a Grid-Encoded RegionHandle
        /// </summary>
        /// <returns>Y Coordinate</returns>
        public uint getGridY()
        {
            uint y = (uint)((handle >> 0) & 0xFFFF);

            return y;
        }
    }
}
