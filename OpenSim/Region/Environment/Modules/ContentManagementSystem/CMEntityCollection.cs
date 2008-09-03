// CMEntityCollection.cs created with MonoDevelop
// User: bongiojp at 10:09 AM 7/7/2008
//
// Creates, Deletes, Stores ContentManagementEntities
//


using System;
using System.Collections.Generic;
using System.Collections;
using libsecondlife;
using Nini.Config;
using OpenSim;
using OpenSim.Framework;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Scenes;
using log4net;
using OpenSim.Region.Physics.Manager;
using Axiom.Math;
using System.Threading;

namespace OpenSim.Region.Environment.Modules.ContentManagement
{
	
	public class CMEntityCollection
	{
	//	private static readonly log4net.ILog m_log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
		
		// Any ContentManagementEntities that represent old versions of current SceneObjectGroups or 
		// old versions of deleted SceneObjectGroups will be stored in this hash table.
		// The LLUUID keys are from the SceneObjectGroup RootPart UUIDs
		protected Hashtable m_CMEntityHash = Hashtable.Synchronized(new Hashtable()); //LLUUID to ContentManagementEntity
		
		// SceneObjectParts that have not been revisioned will be given green auras stored in this hashtable
		// The LLUUID keys are from the SceneObjectPart that they are supposed to be on.
		protected Hashtable m_NewlyCreatedEntityAura = Hashtable.Synchronized(new Hashtable()); //LLUUID to AuraMetaEntity
		
		public Hashtable Entities
		{ 
			get { return m_CMEntityHash; }
		}
		
		public Hashtable Auras
		{ 
			get {return m_NewlyCreatedEntityAura; }
		}
		
		public CMEntityCollection()
		{}
		
		public bool AddAura(ContentManagementEntity aura)
		{
			if (m_NewlyCreatedEntityAura.ContainsKey(aura.UUID))
				return false;
			m_NewlyCreatedEntityAura.Add(aura.UUID, aura);
			return true;
		}
		
		public bool AddEntity(ContentManagementEntity ent)
		{
			if (m_CMEntityHash.ContainsKey(ent.UUID))
				return false;
			m_CMEntityHash.Add(ent.UUID, ent);
			return true;
		}
		
		public bool RemoveNewlyCreatedEntityAura(LLUUID uuid)
		{
			if (!m_NewlyCreatedEntityAura.ContainsKey(uuid))
				return false;
			m_NewlyCreatedEntityAura.Remove(uuid);
			return true;
		}
		
		public bool RemoveEntity(LLUUID uuid)
		{
			if (!m_CMEntityHash.ContainsKey(uuid))
				return false;
			m_CMEntityHash.Remove(uuid);
			return true;
		}		
		
		public void ClearAll()
		{
			m_CMEntityHash.Clear();
			m_NewlyCreatedEntityAura.Clear();
		}
		

		
		// Old uuid and new sceneobjectgroup
		public AuraMetaEntity CreateAuraForNewlyCreatedEntity(SceneObjectPart part)
		{
			AuraMetaEntity ent = new AuraMetaEntity(part.ParentGroup.Scene,
			                                        part.ParentGroup.Scene.PrimIDAllocate(), 
			                                        part.GetWorldPosition(), 
			                                        MetaEntity.TRANSLUCENT, 
			                                        new LLVector3(0,254,0),
			                                        part.Scale
			                                        );
			m_NewlyCreatedEntityAura.Add(part.UUID, ent);
			return ent;
		}

		// Old uuid and new sceneobjectgroup
		public ContentManagementEntity CreateNewEntity(SceneObjectGroup group)
		{
			ContentManagementEntity ent = new ContentManagementEntity(group, false);
			m_CMEntityHash.Add(group.UUID, ent);
			return ent;
		}		
		
		public ContentManagementEntity CreateNewEntity(String xml, Scene scene)
		{
			ContentManagementEntity ent = new ContentManagementEntity(xml, scene, false);
			if (ent == null)
				return null;
			m_CMEntityHash.Add(ent.UnchangedEntity.UUID, ent);
			return ent;
		}
		
		// Check if there are SceneObjectGroups in the list that do not have corresponding ContentManagementGroups in the CMEntityHash
		public System.Collections.ArrayList CheckForMissingEntities(System.Collections.Generic.List<EntityBase> currList)
		{
			System.Collections.ArrayList missingList = new System.Collections.ArrayList();
			SceneObjectGroup temp = null;
			foreach( EntityBase currObj in currList )
			{
				if (! (currObj is SceneObjectGroup))
					continue;
				temp = (SceneObjectGroup) currObj;
				
				if (m_CMEntityHash.ContainsKey(temp.UUID))
				{
					foreach(SceneObjectPart part in temp.Children.Values)
						if (!((ContentManagementEntity)m_CMEntityHash[temp.UUID]).HasChildPrim(part.UUID))
							missingList.Add(part);
				}
				else //Entire group is missing from revision. (and is a new part in region)
				{
					foreach(SceneObjectPart part in temp.Children.Values)
						missingList.Add(part);
				}
			}
			return missingList; 
		}
	}
}
