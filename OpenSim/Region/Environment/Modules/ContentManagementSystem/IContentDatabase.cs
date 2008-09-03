#region Header

// IContentDatabase.cs 
// User: bongiojp
//
// 
//

#endregion Header

using System;

using libsecondlife;

using OpenSim.Region.Environment.Scenes;

using Nini.Config;

namespace OpenSim.Region.Environment.Modules.ContentManagement
{
    public interface IContentDatabase
    {
        #region Methods

        /// <summary>
        /// Returns the most recent revision number of a region.
        /// </summary>
        int GetMostRecentRevision(LLUUID regionid);

        string GetRegionObjectHeightMap(LLUUID regionid);

        string GetRegionObjectHeightMap(LLUUID regionid, int revision);

        /// <summary>
        /// Retrieves the xml that describes each individual object from the last revision or specific revision of the given region.
        /// </summary>
        System.Collections.ArrayList GetRegionObjectXMLList(LLUUID regionid);

        System.Collections.ArrayList GetRegionObjectXMLList(LLUUID regionid, int revision);

        /// <summary>
        /// Similar to the IRegionModule function. This is the function to be called before attempting to interface with the database.
        /// Initialise should be called one for each region to be contained in the database. The directory should be the full path 
        /// to the repository and will only be defined once, regardless of how many times the method is called.
        /// </summary>
        void Initialise(Scene scene, String dir);

        /// <summary>
        /// Returns a list of the revision numbers and corresponding log messages for a given region.
        /// </summary>
        System.Collections.Generic.SortedDictionary<string, string> ListOfRegionRevisions(LLUUID id);

        /// <summary>
        /// Returns the total number of revisions saved for a specific region. 
        /// </summary>
        int NumOfRegionRev(LLUUID regionid);

        /// <summary>
        /// Should be called once after Initialise has been called.
        /// </summary>
        void PostInitialise();

        /// <summary>
        /// Saves the Region terrain map and objects within the region as xml to the database.
        /// </summary>
        void SaveRegion(LLUUID regionid, string regionName, string logMessage);

        #endregion Methods
    }
}