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
* 
*/

using System;
using System.Collections.Generic;
using libsecondlife;

namespace OpenSim.Framework.Data.DB4o
{
    /// <summary>
    /// A grid server storage mechanism employing the DB4o database system
    /// </summary>
    internal class DB4oGridData : IGridData
    {
        /// <summary>
        /// The database manager object
        /// </summary>
        private DB4oGridManager manager;

        /// <summary>
        /// Called when the plugin is first loaded (as constructors are not called)
        /// </summary>
        public void Initialise()
        {
            manager = new DB4oGridManager("gridserver.yap");
        }

        /// <summary>
        /// Returns a list of regions within the specified ranges
        /// </summary>
        /// <param name="a">minimum X coordinate</param>
        /// <param name="b">minimum Y coordinate</param>
        /// <param name="c">maximum X coordinate</param>
        /// <param name="d">maximum Y coordinate</param>
        /// <returns>An array of region profiles</returns>
        public RegionProfileData[] GetProfilesInRange(uint a, uint b, uint c, uint d)
        {
            return null;
        }

        /// <summary>
        /// Returns a region located at the specified regionHandle (warning multiple regions may occupy the one spot, first found is returned)
        /// </summary>
        /// <param name="handle">The handle to search for</param>
        /// <returns>A region profile</returns>
        public RegionProfileData GetProfileByHandle(ulong handle)
        {
            lock (manager.simProfiles)
            {
                foreach (LLUUID UUID in manager.simProfiles.Keys)
                {
                    if (manager.simProfiles[UUID].regionHandle == handle)
                    {
                        return manager.simProfiles[UUID];
                    }
                }
            }
            throw new Exception("Unable to find profile with handle (" + handle.ToString() + ")");
        }

        /// <summary>
        /// Returns a specific region
        /// </summary>
        /// <param name="uuid">The region ID code</param>
        /// <returns>A region profile</returns>
        public RegionProfileData GetProfileByLLUUID(LLUUID uuid)
        {
            lock (manager.simProfiles)
            {
                if (manager.simProfiles.ContainsKey(uuid))
                    return manager.simProfiles[uuid];
            }
            throw new Exception("Unable to find profile with UUID (" + uuid.ToString() +
                                "). Total Registered Regions: " + manager.simProfiles.Count);
        }

        /// <summary>
        /// Adds a new specified region to the database
        /// </summary>
        /// <param name="profile">The profile to add</param>
        /// <returns>A dataresponse enum indicating success</returns>
        public DataResponse AddProfile(RegionProfileData profile)
        {
            lock (manager.simProfiles)
            {
                if (manager.AddRow(profile))
                {
                    return DataResponse.RESPONSE_OK;
                }
                else
                {
                    return DataResponse.RESPONSE_ERROR;
                }
            }
        }

        /// <summary>
        /// Authenticates a new region using the shared secrets. NOT SECURE.
        /// </summary>
        /// <param name="uuid">The UUID the region is authenticating with</param>
        /// <param name="handle">The location the region is logging into (unused in Db4o)</param>
        /// <param name="key">The shared secret</param>
        /// <returns>Authenticated?</returns>
        public bool AuthenticateSim(LLUUID uuid, ulong handle, string key)
        {
            if (manager.simProfiles[uuid].regionRecvKey == key)
                return true;
            return false;
        }

        /// <summary>
        /// Shuts down the database
        /// </summary>
        public void Close()
        {
            manager = null;
        }
        /// <summary>
        /// // Returns a list of avatar and UUIDs that match the query
        /// </summary>

        public List<AvatarPickerAvatar> GeneratePickerResults(LLUUID queryID, string query)
        {
            //Do nothing yet
            List<AvatarPickerAvatar> returnlist = new List<AvatarPickerAvatar>();
            return returnlist;
        }
        /// <summary>
        /// Returns the providers name
        /// </summary>
        /// <returns>The name of the storage system</returns>
        public string getName()
        {
            return "DB4o Grid Provider";
        }

        /// <summary>
        /// Returns the providers version
        /// </summary>
        /// <returns>The version of the storage system</returns>
        public string getVersion()
        {
            return "0.1";
        }

        public ReservationData GetReservationAtPoint(uint x, uint y)
        {
            return null;
        }
    }
}
