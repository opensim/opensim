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

#region Header

// IContentDatabase.cs 
// User: bongiojp
//
// 
//

#endregion Header

using System;
using OpenMetaverse;
using OpenSim.Region.Framework.Scenes;
using Nini.Config;

namespace OpenSim.Region.OptionalModules.ContentManagement
{
    public interface IContentDatabase
    {
        #region Methods

        /// <summary>
        /// Returns the most recent revision number of a region.
        /// </summary>
        int GetMostRecentRevision(UUID regionid);

        string GetRegionObjectHeightMap(UUID regionid);

        string GetRegionObjectHeightMap(UUID regionid, int revision);

        /// <summary>
        /// Retrieves the xml that describes each individual object from the last revision or specific revision of the given region.
        /// </summary>
        System.Collections.ArrayList GetRegionObjectXMLList(UUID regionid);

        System.Collections.ArrayList GetRegionObjectXMLList(UUID regionid, int revision);

        /// <summary>
        /// Similar to the IRegionModule function. This is the function to be called before attempting to interface with the database.
        /// Initialise should be called one for each region to be contained in the database. The directory should be the full path 
        /// to the repository and will only be defined once, regardless of how many times the method is called.
        /// </summary>
        void Initialise(Scene scene, String dir);

        /// <summary>
        /// Returns a list of the revision numbers and corresponding log messages for a given region.
        /// </summary>
        System.Collections.Generic.SortedDictionary<string, string> ListOfRegionRevisions(UUID id);

        /// <summary>
        /// Returns the total number of revisions saved for a specific region. 
        /// </summary>
        int NumOfRegionRev(UUID regionid);

        /// <summary>
        /// Should be called once after Initialise has been called.
        /// </summary>
        void PostInitialise();

        /// <summary>
        /// Saves the Region terrain map and objects within the region as xml to the database.
        /// </summary>
        void SaveRegion(UUID regionid, string regionName, string logMessage);

        #endregion Methods
    }
}
