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
using System.Text;
using OpenMetaverse;

namespace OpenSim.Data
{
    public interface IRegionProfileService
    {
        /// <summary>
        /// Returns a region by argument
        /// </summary>
        /// <param name="uuid">A UUID key of the region to return</param>
        /// <returns>A SimProfileData for the region</returns>
        RegionProfileData GetRegion(UUID uuid);

        /// <summary>
        /// Returns a region by argument
        /// </summary>
        /// <param name="uuid">A regionHandle of the region to return</param>
        /// <returns>A SimProfileData for the region</returns>
        RegionProfileData GetRegion(ulong handle);

        /// <summary>
        /// Returns a region by argument
        /// </summary>
        /// <param name="regionName">A partial regionName of the region to return</param>
        /// <returns>A SimProfileData for the region</returns>
        RegionProfileData GetRegion(string regionName);

        List<RegionProfileData> GetRegions(uint xmin, uint ymin, uint xmax, uint ymax);
        List<RegionProfileData> GetRegions(string name, int maxNum);
        DataResponse AddUpdateRegion(RegionProfileData sim, RegionProfileData existingSim);
        DataResponse DeleteRegion(string uuid);
    }

    public interface IRegionProfileRouter
    {
        /// <summary>
        /// Request sim profile information from a grid server, by Region UUID
        /// </summary>
        /// <param name="regionId">The region UUID to look for</param>
        /// <param name="gridserverUrl"></param>
        /// <param name="gridserverSendkey"></param>
        /// <param name="gridserverRecvkey"></param>
        /// <returns>The sim profile.  Null if there was a request failure</returns>
        /// <remarks>This method should be statics</remarks>
        RegionProfileData RequestSimProfileData(UUID regionId, Uri gridserverUrl,
                                                       string gridserverSendkey, string gridserverRecvkey);

        /// <summary>
        /// Request sim profile information from a grid server, by Region Handle
        /// </summary>
        /// <param name="regionHandle">the region handle to look for</param>
        /// <param name="gridserverUrl"></param>
        /// <param name="gridserverSendkey"></param>
        /// <param name="gridserverRecvkey"></param>
        /// <returns>The sim profile.  Null if there was a request failure</returns>
        RegionProfileData RequestSimProfileData(ulong regionHandle, Uri gridserverUrl,
                                                       string gridserverSendkey, string gridserverRecvkey);

        /// <summary>
        /// Request sim profile information from a grid server, by Region Name
        /// </summary>
        /// <param name="regionName">the region name to look for</param>
        /// <param name="gridserverUrl"></param>
        /// <param name="gridserverSendkey"></param>
        /// <param name="gridserverRecvkey"></param>
        /// <returns>The sim profile.  Null if there was a request failure</returns>
        RegionProfileData RequestSimProfileData(string regionName, Uri gridserverUrl,
                                                       string gridserverSendkey, string gridserverRecvkey);
    }
}
