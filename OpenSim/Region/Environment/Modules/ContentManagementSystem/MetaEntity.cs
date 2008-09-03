// MetaEntity.cs
// User: bongiojp
//
// TODO:
//    Create a physics manager to the meta object if there isn't one or the object knows of no scene but the user wants physics enabled.


using System;
using System.Collections.Generic;
using System.Drawing;
using libsecondlife;
using Nini.Config;
using OpenSim.Framework;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Scenes;
using log4net;
using OpenSim.Region.Physics.Manager;
using Axiom.Math;

namespace OpenSim.Region.Environment.Modules.ContentManagement
{	
	public class MetaEntity
	{
		protected static readonly ILog m_log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
		 
		protected SceneObjectGroup m_Entity = null; // The scene object group that represents this meta entity.
		protected uint m_metaLocalid;
		
		// Settings for transparency of metaentity
		public const float NONE = 0f;
		public const float TRANSLUCENT = .5f;
		public const float INVISIBLE = .95f;
		
		public Scene Scene
		{
			get { return m_Entity.Scene; }
		}
		
		public SceneObjectPart RootPart
		{
			get { return m_Entity.RootPart; }
			set { m_Entity.RootPart = value; }
		}
		
		public LLUUID UUID 
		{
			get { return m_Entity.UUID; }
			set { m_Entity.UUID = value; }
		}
		
		public uint LocalId 
		{
			get { return m_Entity.LocalId; }
			set { m_Entity.LocalId = value; }
		}
		
		public SceneObjectGroup ObjectGroup 
		{
			get { return m_Entity; }
		}
		
		public Dictionary<LLUUID, SceneObjectPart> Children
		{
			get { return m_Entity.Children; }
			set { m_Entity.Children = value; }
		}
		
		public int PrimCount
		{
			get { return m_Entity.PrimCount; }
		}
		
		public MetaEntity()
		{
		}
		
		/// <summary>
		/// Makes a new meta entity by copying the given scene object group. 
		/// The physics boolean is just a stub right now.
		/// </summary>
		public MetaEntity(SceneObjectGroup orig, bool physics)
		{
			m_Entity = orig.Copy(orig.RootPart.OwnerID, orig.RootPart.GroupID, false);
			Initialize(physics);
		}
		
		/// <summary>
		/// Takes an XML description of a scene object group and converts it to a meta entity.
		/// </summary>
		public MetaEntity(string objectXML, Scene scene, bool physics)
		{
			m_Entity = new SceneObjectGroup(objectXML);
			m_Entity.SetScene(scene);
			Initialize(physics);
		}

		// The metaentity objectgroup must have unique localids as well as unique uuids.
		// localids are used by the client to refer to parts.
		// uuids are sent to the client and back to the server to identify parts on the server side.
		/// <summary>
		/// Changes localids and uuids of m_Entity.
		/// </summary>
		protected void Initialize(bool physics)
		{
			//make new uuids
			Dictionary<LLUUID, SceneObjectPart> parts = new Dictionary<LLUUID, SceneObjectPart>();
			foreach(SceneObjectPart part in m_Entity.Children.Values)
			{
				part.ResetIDs(part.LinkNum);
				parts.Add(part.UUID, part);
			}
			
			// make new localids
			foreach (SceneObjectPart part in m_Entity.Children.Values)
				part.LocalId = m_Entity.Scene.PrimIDAllocate();
			
			//finalize
			m_Entity.UpdateParentIDs();
			m_Entity.RootPart.PhysActor = null;	
			m_Entity.Children = parts;
			
		}
		
		public void SendFullUpdate(IClientAPI client)
		{
			// Not sure what clientFlags should be but 0 seems to work
			SendFullUpdate(client, 0);		
		}
		public void SendFullUpdateToAll()
		{		
			uint clientFlags = 0;
			m_Entity.Scene.ClientManager.ForEachClient(delegate(IClientAPI controller)
			                                           { m_Entity.SendFullUpdateToClient(controller, clientFlags); }
			);
		}
		
		public void SendFullUpdate(IClientAPI client, uint clientFlags)
		{
			m_Entity.SendFullUpdateToClient(client, clientFlags);			
		}
		
		/// <summary>
		/// Hides the metaentity from a single client.
		/// </summary>
		public virtual void Hide(IClientAPI client)
		{
			//This deletes the group without removing from any databases.
			//This is important because we are not IN any database.
			//m_Entity.FakeDeleteGroup();
			foreach( SceneObjectPart part in m_Entity.Children.Values)
				client.SendKillObject(m_Entity.RegionHandle, part.LocalId);
		}
		
		/// <summary>
		/// Sends a kill object message to all clients, effectively "hiding" the metaentity even though it's still on the server.
		/// </summary>
		public virtual void HideFromAll()
		{
			foreach( SceneObjectPart part in m_Entity.Children.Values)
				m_Entity.Scene.ClientManager.ForEachClient(delegate(IClientAPI controller)
				                                           { controller.SendKillObject(m_Entity.RegionHandle, part.LocalId); }
				);			
		}
		
		/// <summary>
		/// Makes a single SceneObjectPart see through.
		/// </summary>
		/// <param name="part">
		/// A <see cref="SceneObjectPart"/>
		/// The part to make see through
		/// </param>
		/// <param name="transparencyAmount">
		/// A <see cref="System.Single"/>
		/// The degree of transparency to imbue the part with, 0f being solid and .95f being invisible.
		/// </param>
		public static void SetPartTransparency(SceneObjectPart part, float transparencyAmount)
		{
			LLObject.TextureEntry tex = null;
			LLColor texcolor;
			try
			{
				tex = part.Shape.Textures;
				texcolor = new LLColor();
			}
			catch(Exception)
			{
				//m_log.ErrorFormat("[Content Management]: Exception thrown while accessing textures of scene object: " + e);
				return;
			}
			
			for (uint i = 0; i < tex.FaceTextures.Length; i++)
			{
				try {
					if (tex.FaceTextures[i] != null)
					{
						texcolor = tex.FaceTextures[i].RGBA;
						texcolor.A = transparencyAmount;
						tex.FaceTextures[i].RGBA = texcolor;
					}
				}
				catch (Exception)
				{
					//m_log.ErrorFormat("[Content Management]: Exception thrown while accessing different face textures of object: " + e);
					continue;
				}
			}
			try {
				texcolor = tex.DefaultTexture.RGBA;
				texcolor.A = transparencyAmount;
				tex.DefaultTexture.RGBA = texcolor;
				part.Shape.TextureEntry = tex.ToBytes();
			}
			catch (Exception)
			{
				//m_log.Info("[Content Management]: Exception thrown while accessing default face texture of object: " + e);
			}
		}
	}
}