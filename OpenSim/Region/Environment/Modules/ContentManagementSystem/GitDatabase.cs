// GitDatabase.cs 
//
//
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml;
using libsecondlife;
using Nini.Config;
using OpenSim.Framework;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Modules.World.Serialiser;
using OpenSim.Region.Environment.Modules.World.Terrain;
using OpenSim.Region.Environment.Scenes;
using log4net;
using OpenSim.Region.Physics.Manager;
using Axiom.Math;
using Slash=System.IO.Path;

namespace OpenSim.Region.Environment.Modules.ContentManagement
{
	
	/// <summary>
	/// Just a stub :-(
	/// </summary>
	public class GitDatabase : IContentDatabase
	{
		
		public GitDatabase()
		{
		}

		public void Initialise(Scene scene, String dir)
		{
			
		}
		
		public void PostInitialise()
		{
			
		}

		public int NumOfObjectRev(LLUUID id)
		{
			return 0;
		}

		public int NumOfRegionRev(LLUUID regionid)
		{
			return 0;
		}

		public bool InRepository(LLUUID id)
		{
			return false;
		}

		public void SaveRegion(LLUUID regionid, string regionName, string logMessage)
		{
			
		}

		public System.Collections.ArrayList GetRegionObjectXMLList(LLUUID regionid)
		{
			return null;
		}

		public System.Collections.ArrayList GetRegionObjectXMLList(LLUUID regionid, int revision)
		{
			return null;
		}
		
		public string GetRegionObjectXML(LLUUID regionid)
		{
			return null;
		}
				
		public string GetRegionObjectXML(LLUUID regionid, int revision)
		{
			return null;
		}
		
		public string GetRegionObjectHeightMap(LLUUID regionid)
		{
			return null;
		}
		
		public string GetRegionObjectHeightMap(LLUUID regionid, int revision)
		{
			return null;
		}
		
		public System.Collections.ArrayList GetObjectsFromRegion(LLUUID regionid, int revision)
		{
			return null;
		}
				
		public System.Collections.Generic.SortedDictionary<string, string> ListOfRegionRevisions(LLUUID id)
		{
			return null;
		}

		public 	void SaveObject(SceneObjectGroup entity)
		{
		}
		
		public SceneObjectGroup GetMostRecentObjectRevision(LLUUID id)
		{
			return null;
		}
		
		public SceneObjectGroup GetObjectRevision(LLUUID id, int revision)
		{
			return null;
		}
		
		public System.Collections.Generic.SortedDictionary<string, string> ListOfObjectRevisions(LLUUID id)
		{
			return null;
		}
		
		public int GetMostRecentRevision(LLUUID regionid) 
		{
			return 0;
		}
	}
}
