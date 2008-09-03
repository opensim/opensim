// IContentDatabase.cs 
// User: bongiojp
//
// 
//

using System;
using libsecondlife;
using OpenSim.Region.Environment.Scenes;
using Nini.Config;

namespace OpenSim.Region.Environment.Modules.ContentManagement
{	
	public interface IContentDatabase
	{	
		/// <summary>
		/// Similar to the IRegionModule function. This is the function to be called before attempting to interface with the database.
		/// Initialise should be called one for each region to be contained in the database. The directory should be the full path 
		/// to the repository and will only be defined once, regardless of how many times the method is called.
		/// </summary>
		void Initialise(Scene scene, String dir);
		
		/// <summary>
		/// Should be called once after Initialise has been called.
		/// </summary>
		void PostInitialise();
		
		/// <summary>
		/// Returns the total number of revisions saved for a specific region. 
		/// </summary>
		int NumOfRegionRev(LLUUID regionid);
		
		/// <summary>
		/// Saves the Region terrain map and objects within the region as xml to the database.
		/// </summary>
		void SaveRegion(LLUUID regionid, string regionName, string logMessage);

		/// <summary>
		/// Retrieves the xml that describes each individual object from the last revision or specific revision of the given region.
		/// </summary>
		System.Collections.ArrayList GetRegionObjectXMLList(LLUUID regionid);
		System.Collections.ArrayList GetRegionObjectXMLList(LLUUID regionid, int revision);
		
		string GetRegionObjectHeightMap(LLUUID regionid);
		string GetRegionObjectHeightMap(LLUUID regionid, int revision);
				
		/// <summary>
		/// Returns a list of the revision numbers and corresponding log messages for a given region.
		/// </summary>
		System.Collections.Generic.SortedDictionary<string, string> ListOfRegionRevisions(LLUUID id);

		/// <summary>
		/// Returns the most recent revision number of a region.
		/// </summary>
		int GetMostRecentRevision(LLUUID regionid);
	}
}
