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
using OpenMetaverse;
using OpenSim.Framework;

namespace OpenSim.Data
{
    public interface IEstateDataStore
    {
        /// <summary>
        /// Initialise the data store.
        /// </summary>
        /// <param name="connectstring"></param>
        void Initialise(string connectstring);

        /// <summary>
        /// Load estate settings for a region.
        /// </summary>
        /// <param name="regionID"></param>
        /// <param name="create">If true, then an estate is created if one is not found.</param>
        /// <returns></returns>
        EstateSettings LoadEstateSettings(UUID regionID, bool create);
        
        /// <summary>
        /// Load estate settings for an estate ID.
        /// </summary>
        /// <param name="estateID"></param>
        /// <returns></returns>
        EstateSettings LoadEstateSettings(int estateID);
        
        /// <summary>
        /// Create a new estate.
        /// </summary>
        /// <returns>
        /// A <see cref="EstateSettings"/>
        /// </returns>
        EstateSettings CreateNewEstate();

        /// <summary>
        /// Load/Get all estate settings.
        /// </summary>
        /// <returns>An empty list if no estates were found.</returns>
        List<EstateSettings> LoadEstateSettingsAll();
        
        /// <summary>
        /// Store estate settings.
        /// </summary>
        /// <remarks>
        /// This is also called by EstateSettings.Save()</remarks>
        /// <param name="es"></param>
        void StoreEstateSettings(EstateSettings es);
        
        /// <summary>
        /// Get estate IDs.
        /// </summary>
        /// <param name="search">Name of estate to search for.  This is the exact name, no parttern matching is done.</param>
        /// <returns></returns>
        List<int> GetEstates(string search);

        /// <summary>
        /// Get the IDs of all estates owned by the given user.
        /// </summary>
        /// <returns>An empty list if no estates were found.</returns>
        List<int> GetEstatesByOwner(UUID ownerID);
        
        /// <summary>
        /// Get the IDs of all estates.
        /// </summary>
        /// <returns>An empty list if no estates were found.</returns>
        List<int> GetEstatesAll();
        
        /// <summary>
        /// Link a region to an estate.
        /// </summary>
        /// <param name="regionID"></param>
        /// <param name="estateID"></param>
        /// <returns>true if the link succeeded, false otherwise</returns>
        bool LinkRegion(UUID regionID, int estateID);
        
        /// <summary>
        /// Get the UUIDs of all the regions in an estate.
        /// </summary>
        /// <param name="estateID"></param>
        /// <returns></returns>
        List<UUID> GetRegions(int estateID);
        
        /// <summary>
        /// Delete an estate
        /// </summary>
        /// <param name="estateID"></param>
        /// <returns>true if the delete succeeded, false otherwise</returns>
        bool DeleteEstate(int estateID);
    }
}