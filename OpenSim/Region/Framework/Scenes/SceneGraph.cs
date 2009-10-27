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
using System.Reflection;
using OpenMetaverse;
using OpenMetaverse.Packets;
using log4net;
using OpenSim.Framework;
using OpenSim.Region.Framework.Scenes.Types;
using OpenSim.Region.Physics.Manager;

namespace OpenSim.Region.Framework.Scenes
{
    public delegate void PhysicsCrash();

    public delegate void ObjectDuplicateDelegate(EntityBase original, EntityBase clone);

    public delegate void ObjectCreateDelegate(EntityBase obj);

    public delegate void ObjectDeleteDelegate(EntityBase obj);

    /// <summary>
    /// This class used to be called InnerScene and may not yet truly be a SceneGraph.  The non scene graph components
    /// should be migrated out over time.
    /// </summary>
    public class SceneGraph
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        #region Events

        protected internal event PhysicsCrash UnRecoverableError;
        private PhysicsCrash handlerPhysicsCrash = null;

        public event ObjectDuplicateDelegate OnObjectDuplicate;
        public event ObjectCreateDelegate OnObjectCreate;
        public event ObjectDeleteDelegate OnObjectRemove;

        #endregion

        #region Fields

        protected Dictionary<UUID, ScenePresence> m_scenePresences = new Dictionary<UUID, ScenePresence>();
        protected ScenePresence[] m_scenePresenceArray = new ScenePresence[0];

        // SceneObjects is not currently populated or used.
        //public Dictionary<UUID, SceneObjectGroup> SceneObjects;
        protected internal EntityManager Entities = new EntityManager();
//        protected internal Dictionary<UUID, EntityBase> Entities = new Dictionary<UUID, EntityBase>();
        protected internal Dictionary<UUID, ScenePresence> RestorePresences = new Dictionary<UUID, ScenePresence>();

        protected internal BasicQuadTreeNode QuadTree;

        protected RegionInfo m_regInfo;
        protected Scene m_parentScene;
        protected Dictionary<UUID, SceneObjectGroup> m_updateList = new Dictionary<UUID, SceneObjectGroup>();
        protected int m_numRootAgents = 0;
        protected int m_numPrim = 0;
        protected int m_numChildAgents = 0;
        protected int m_physicalPrim = 0;

        protected int m_activeScripts = 0;
        protected int m_scriptLPS = 0;

        protected internal object m_syncRoot = new object();

        protected internal PhysicsScene _PhyScene;

        protected internal Dictionary<uint, SceneObjectGroup> SceneObjectGroupsByLocalID = new Dictionary<uint, SceneObjectGroup>();
        protected internal Dictionary<UUID, SceneObjectGroup> SceneObjectGroupsByFullID = new Dictionary<UUID, SceneObjectGroup>();
        private readonly Object m_dictionary_lock = new Object();

        #endregion

        protected internal SceneGraph(Scene parent, RegionInfo regInfo)
        {
            m_parentScene = parent;
            m_regInfo = regInfo;
            QuadTree = new BasicQuadTreeNode(null, "/0/", 0, 0, (short)Constants.RegionSize, (short)Constants.RegionSize);
            QuadTree.Subdivide();
            QuadTree.Subdivide();
        }

        public PhysicsScene PhysicsScene
        {
            get { return _PhyScene; }
            set
            {
                // If we're not doing the initial set
                // Then we've got to remove the previous
                // event handler

                if (_PhyScene != null)
                    _PhyScene.OnPhysicsCrash -= physicsBasedCrash;

                _PhyScene = value;

                if (_PhyScene != null)
                    _PhyScene.OnPhysicsCrash += physicsBasedCrash;
            }
        }

        protected internal void Close()
        {
            lock (m_scenePresences)
            {
                m_scenePresences.Clear();
                m_scenePresenceArray = new ScenePresence[0];
            }

            lock (m_dictionary_lock)
            {
                SceneObjectGroupsByFullID.Clear();
                SceneObjectGroupsByLocalID.Clear();
            }

            Entities.Clear();
        }

        #region Update Methods

        protected internal void UpdatePreparePhysics()
        {
            // If we are using a threaded physics engine
            // grab the latest scene from the engine before
            // trying to process it.

            // PhysX does this (runs in the background).

            if (_PhyScene.IsThreaded)
            {
                _PhyScene.GetResults();
            }
        }

        protected internal void UpdatePresences()
        {
            ScenePresence[] updateScenePresences = GetScenePresences();
            for (int i = 0; i < updateScenePresences.Length; i++)
                updateScenePresences[i].Update();
        }

        protected internal float UpdatePhysics(double elapsed)
        {
            lock (m_syncRoot)
            {
                // Here is where the Scene calls the PhysicsScene. This is a one-way
                // interaction; the PhysicsScene cannot access the calling Scene directly.
                // But with joints, we want a PhysicsActor to be able to influence a
                // non-physics SceneObjectPart. In particular, a PhysicsActor that is connected
                // with a joint should be able to move the SceneObjectPart which is the visual
                // representation of that joint (for editing and serialization purposes).
                // However the PhysicsActor normally cannot directly influence anything outside
                // of the PhysicsScene, and the non-physical SceneObjectPart which represents
                // the joint in the Scene does not exist in the PhysicsScene.
                //
                // To solve this, we have an event in the PhysicsScene that is fired when a joint
                // has changed position (because one of its associated PhysicsActors has changed 
                // position).
                //
                // Therefore, JointMoved and JointDeactivated events will be fired as a result of the following Simulate().

                return _PhyScene.Simulate((float)elapsed);
            }
        }

        protected internal void UpdateScenePresenceMovement()
        {
            ScenePresence[] moveEntities = GetScenePresences();
            for (int i = 0; i < moveEntities.Length; i++)
                moveEntities[i].UpdateMovement();
        }

        #endregion

        #region Entity Methods

        /// <summary>
        /// Add an object into the scene that has come from storage
        /// </summary>
        /// <param name="sceneObject"></param>
        /// <param name="attachToBackup">
        /// If true, changes to the object will be reflected in its persisted data
        /// If false, the persisted data will not be changed even if the object in the scene is changed
        /// </param>
        /// <param name="alreadyPersisted">
        /// If true, we won't persist this object until it changes
        /// If false, we'll persist this object immediately
        /// </param>
        /// <returns>
        /// true if the object was added, false if an object with the same uuid was already in the scene
        /// </returns>
        protected internal bool AddRestoredSceneObject(
            SceneObjectGroup sceneObject, bool attachToBackup, bool alreadyPersisted)
        {
            if (!alreadyPersisted)
            {
                sceneObject.ForceInventoryPersistence();
                sceneObject.HasGroupChanged = true;
            }

            return AddSceneObject(sceneObject, attachToBackup);
        }

        /// <summary>
        /// Add a newly created object to the scene.  This will both update the scene, and send information about the
        /// new object to all clients interested in the scene.
        /// </summary>
        /// <param name="sceneObject"></param>
        /// <param name="attachToBackup">
        /// If true, the object is made persistent into the scene.
        /// If false, the object will not persist over server restarts
        /// </param>
        /// <returns>
        /// true if the object was added, false if an object with the same uuid was already in the scene
        /// </returns>
        protected internal bool AddNewSceneObject(SceneObjectGroup sceneObject, bool attachToBackup)
        {
            // Ensure that we persist this new scene object
            sceneObject.HasGroupChanged = true;

            return AddSceneObject(sceneObject, attachToBackup);
        }

        /// <summary>
        /// Add an object to the scene.  This will both update the scene, and send information about the
        /// new object to all clients interested in the scene.
        /// </summary>
        /// <param name="sceneObject"></param>
        /// <param name="attachToBackup">
        /// If true, the object is made persistent into the scene.
        /// If false, the object will not persist over server restarts
        /// </param>
        /// <returns>true if the object was added, false if an object with the same uuid was already in the scene
        /// </returns>
        protected bool AddSceneObject(SceneObjectGroup sceneObject, bool attachToBackup)
        {
            if (sceneObject == null || sceneObject.RootPart == null || sceneObject.RootPart.UUID == UUID.Zero)
                return false;
            
            if (m_parentScene.m_clampPrimSize)
            {
                foreach (SceneObjectPart part in sceneObject.Children.Values)
                {
                    Vector3 scale = part.Shape.Scale;

                    if (scale.X > m_parentScene.m_maxNonphys)
                        scale.X = m_parentScene.m_maxNonphys;
                    if (scale.Y > m_parentScene.m_maxNonphys)
                        scale.Y = m_parentScene.m_maxNonphys;
                    if (scale.Z > m_parentScene.m_maxNonphys)
                        scale.Z = m_parentScene.m_maxNonphys;

                    part.Shape.Scale = scale;
                }
            }

            sceneObject.AttachToScene(m_parentScene);

            lock (sceneObject)
            {
                if (!Entities.ContainsKey(sceneObject.UUID))
                {
                    Entities.Add(sceneObject);
                    m_numPrim += sceneObject.Children.Count;

                    if (attachToBackup)
                        sceneObject.AttachToBackup();

                    if (OnObjectCreate != null)
                        OnObjectCreate(sceneObject);
                    
                    lock (m_dictionary_lock)
                    {
                        SceneObjectGroupsByFullID[sceneObject.UUID] = sceneObject;
                        SceneObjectGroupsByLocalID[sceneObject.LocalId] = sceneObject;
                        foreach (SceneObjectPart part in sceneObject.Children.Values)
                        {
                            SceneObjectGroupsByFullID[part.UUID] = sceneObject;
                            SceneObjectGroupsByLocalID[part.LocalId] = sceneObject;
                        }
                    }

                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Delete an object from the scene
        /// </summary>
        /// <returns>true if the object was deleted, false if there was no object to delete</returns>
        public bool DeleteSceneObject(UUID uuid, bool resultOfObjectLinked)
        {
            if (Entities.ContainsKey(uuid))
            {
                if (!resultOfObjectLinked)
                {
                    m_numPrim -= ((SceneObjectGroup) Entities[uuid]).Children.Count;

                    if ((((SceneObjectGroup)Entities[uuid]).RootPart.Flags & PrimFlags.Physics) == PrimFlags.Physics)
                    {
                        RemovePhysicalPrim(((SceneObjectGroup)Entities[uuid]).Children.Count);
                    }
                }

                if (OnObjectRemove != null)
                    OnObjectRemove(Entities[uuid]);

                lock (m_dictionary_lock)
                {
                    SceneObjectGroupsByFullID.Remove(uuid);
                    SceneObjectGroupsByLocalID.Remove(((SceneObjectGroup)Entities[uuid]).LocalId);
                }
                Entities.Remove(uuid);
                //SceneObjectGroup part;
                //((part.RootPart.Flags & PrimFlags.Physics) == PrimFlags.Physics)

                return true;
            }

            return false;
        }

        /// <summary>
        /// Add an object to the list of prims to process on the next update
        /// </summary>
        /// <param name="obj">
        /// A <see cref="SceneObjectGroup"/>
        /// </param>
        protected internal void AddToUpdateList(SceneObjectGroup obj)
        {
            lock (m_updateList)
            {
                m_updateList[obj.UUID] = obj;
            }
        }

        /// <summary>
        /// Process all pending updates
        /// </summary>
        protected internal void UpdateObjectGroups()
        {
            Dictionary<UUID, SceneObjectGroup> updates;
            // Some updates add more updates to the updateList. 
            // Get the current list of updates and clear the list before iterating
            lock (m_updateList)
            {
                updates = new Dictionary<UUID, SceneObjectGroup>(m_updateList);
                m_updateList.Clear();
            }
            // Go through all updates
            foreach (KeyValuePair<UUID, SceneObjectGroup> kvp in updates)
            {
                // Don't abort the whole update if one entity happens to give us an exception.
                try
                {
                    kvp.Value.Update();
                }
                catch (Exception e)
                {
                    m_log.ErrorFormat(
                        "[INNER SCENE]: Failed to update {0}, {1} - {2}", kvp.Value.Name, kvp.Value.UUID, e);
                }
            }
        }

        protected internal void AddPhysicalPrim(int number)
        {
            m_physicalPrim++;
        }

        protected internal void RemovePhysicalPrim(int number)
        {
            m_physicalPrim--;
        }

        protected internal void AddToScriptLPS(int number)
        {
            m_scriptLPS += number;
        }

        protected internal void AddActiveScripts(int number)
        {
            m_activeScripts += number;
        }

        public void DropObject(uint objectLocalID, IClientAPI remoteClient)
        {
            SceneObjectGroup group = GetGroupByPrim(objectLocalID);
            if (group != null)
            {
                m_parentScene.DetachSingleAttachmentToGround(group.UUID, remoteClient);
            }
        }

        protected internal void DetachObject(uint objectLocalID, IClientAPI remoteClient)
        {
            SceneObjectGroup group = GetGroupByPrim(objectLocalID);
            if (group != null)
            {
                //group.DetachToGround();
                m_parentScene.DetachSingleAttachmentToInv(group.GetFromItemID(), remoteClient);
            }
        }

        protected internal void HandleUndo(IClientAPI remoteClient, UUID primId)
        {
            if (primId != UUID.Zero)
            {
                SceneObjectPart part =  m_parentScene.GetSceneObjectPart(primId);
                if (part != null)
                    part.Undo();
            }
        }

        protected internal void HandleObjectGroupUpdate(
            IClientAPI remoteClient, UUID GroupID, uint objectLocalID, UUID Garbage)
        {
            SceneObjectGroup group = GetGroupByPrim(objectLocalID);
            if (group != null)
            {
                if (group.OwnerID == remoteClient.AgentId)
                    group.SetGroup(GroupID, remoteClient);
            }
        }

        /// <summary>
        /// Event Handling routine for Attach Object
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="objectLocalID"></param>
        /// <param name="AttachmentPt"></param>
        /// <param name="rot"></param>
        protected internal void AttachObject(IClientAPI remoteClient, uint objectLocalID, uint AttachmentPt, Quaternion rot, bool silent)
        {
            // If we can't take it, we can't attach it!
            SceneObjectPart part = m_parentScene.GetSceneObjectPart(objectLocalID);
            if (part == null)
                return;

            if (!m_parentScene.Permissions.CanTakeObject(
                    part.UUID, remoteClient.AgentId))
                return;

            // Calls attach with a Zero position
            AttachObject(remoteClient, objectLocalID, AttachmentPt, rot, Vector3.Zero, false);
            m_parentScene.SendAttachEvent(objectLocalID, part.ParentGroup.GetFromItemID(), remoteClient.AgentId);

            // Save avatar attachment information
            ScenePresence presence;
            if (m_parentScene.AvatarFactory != null && m_parentScene.TryGetAvatar(remoteClient.AgentId, out presence))
            {
                m_log.Info("[SCENE]: Saving avatar attachment. AgentID: " + remoteClient.AgentId + ", AttachmentPoint: " + AttachmentPt);
                m_parentScene.AvatarFactory.UpdateDatabase(remoteClient.AgentId, presence.Appearance);
            }
        }

        public SceneObjectGroup RezSingleAttachment(
            IClientAPI remoteClient, UUID itemID, uint AttachmentPt)
        {
            SceneObjectGroup objatt = m_parentScene.RezObject(remoteClient,
                itemID, Vector3.Zero, Vector3.Zero, UUID.Zero, (byte)1, true,
                false, false, remoteClient.AgentId, true);


            if (objatt != null)
            {
                bool tainted = false;
                if (AttachmentPt != 0 && AttachmentPt != objatt.GetAttachmentPoint())
                    tainted = true;

                AttachObject(remoteClient, objatt.LocalId, AttachmentPt, Quaternion.Identity, objatt.AbsolutePosition, false);
                objatt.ScheduleGroupForFullUpdate();
                if (tainted)
                    objatt.HasGroupChanged = true;

                // Fire after attach, so we don't get messy perms dialogs
                // 3 == AttachedRez
                objatt.CreateScriptInstances(0, true, m_parentScene.DefaultScriptEngine, 3);
            }
            return objatt;
        }

        // What makes this method odd and unique is it tries to detach using an UUID....     Yay for standards.
        // To LocalId or UUID, *THAT* is the question. How now Brown UUID??
        public void DetachSingleAttachmentToInv(UUID itemID, IClientAPI remoteClient)
        {
            if (itemID == UUID.Zero) // If this happened, someone made a mistake....
                return;

            // We can NOT use the dictionries here, as we are looking
            // for an entity by the fromAssetID, which is NOT the prim UUID
            //
            List<EntityBase> detachEntities = GetEntities();
            SceneObjectGroup group;

            foreach (EntityBase entity in detachEntities)
            {
                if (entity is SceneObjectGroup)
                {
                    group = (SceneObjectGroup)entity;
                    if (group.GetFromItemID() == itemID)
                    {
                        m_parentScene.SendAttachEvent(group.LocalId, itemID, UUID.Zero);
                        group.DetachToInventoryPrep();
                        m_log.Debug("[DETACH]: Saving attachpoint: " +
                                ((uint)group.GetAttachmentPoint()).ToString());
                        m_parentScene.UpdateKnownItem(remoteClient, group,
                                group.GetFromItemID(), group.OwnerID);
                        m_parentScene.DeleteSceneObject(group, false);
                        return;
                    }
                }
            }
        }

        protected internal void AttachObject(
            IClientAPI remoteClient, uint objectLocalID, uint AttachmentPt, Quaternion rot, Vector3 attachPos, bool silent)
        {
            SceneObjectGroup group = GetGroupByPrim(objectLocalID);
            if (group != null)
            {
                if (m_parentScene.Permissions.CanTakeObject(group.UUID, remoteClient.AgentId))
                {
                    // If the attachment point isn't the same as the one previously used
                    // set it's offset position = 0 so that it appears on the attachment point
                    // and not in a weird location somewhere unknown.
                    if (AttachmentPt != 0 && AttachmentPt != (uint)group.GetAttachmentPoint())
                    {
                        attachPos = Vector3.Zero;
                    }

                    // AttachmentPt 0 means the client chose to 'wear' the attachment.
                    if (AttachmentPt == 0)
                    {
                        // Check object for stored attachment point
                        AttachmentPt = (uint)group.GetAttachmentPoint();
                    }

                    // if we still didn't find a suitable attachment point.......
                    if (AttachmentPt == 0)
                    {
                        // Stick it on left hand with Zero Offset from the attachment point.
                        AttachmentPt = (uint)AttachmentPoint.LeftHand;
                        attachPos = Vector3.Zero;

                    }


                    group.SetAttachmentPoint((byte)AttachmentPt);
                    group.AbsolutePosition = attachPos;

                    // Saves and gets itemID
                    UUID itemId;

                    if (group.GetFromItemID() == UUID.Zero)
                    {
                        m_parentScene.attachObjectAssetStore(remoteClient, group, remoteClient.AgentId, out itemId);
                    }
                    else
                    {
                        itemId = group.GetFromItemID();
                    }

                    m_parentScene.AttachObject(remoteClient, AttachmentPt, itemId, group);

                    group.AttachToAgent(remoteClient.AgentId, AttachmentPt, attachPos, silent);
                    // In case it is later dropped again, don't let
                    // it get cleaned up
                    //
                    group.RootPart.RemFlag(PrimFlags.TemporaryOnRez);
                    group.HasGroupChanged = false;
                }
                else
                {
                    remoteClient.SendAgentAlertMessage("You don't have sufficient permissions to attach this object", false);
                }
            }
            else
                m_log.DebugFormat("[SCENE GRAPH]: AttachObject found no such scene object {0}", objectLocalID);
        }

        protected internal ScenePresence CreateAndAddChildScenePresence(IClientAPI client, AvatarAppearance appearance)
        {
            ScenePresence newAvatar = null;

            newAvatar = new ScenePresence(client, m_parentScene, m_regInfo, appearance);
            newAvatar.IsChildAgent = true;

            AddScenePresence(newAvatar);

            return newAvatar;
        }

        /// <summary>
        /// Add a presence to the scene
        /// </summary>
        /// <param name="presence"></param>
        protected internal void AddScenePresence(ScenePresence presence)
        {
            bool child = presence.IsChildAgent;

            if (child)
            {
                m_numChildAgents++;
            }
            else
            {
                m_numRootAgents++;
                presence.AddToPhysicalScene(false);
            }

            Entities[presence.UUID] = presence;

            lock (m_scenePresences)
            {
                if (!m_scenePresences.ContainsKey(presence.UUID))
                {
                    m_scenePresences.Add(presence.UUID, presence);

                    // Create a new array of ScenePresence references
                    int oldLength = m_scenePresenceArray.Length;
                    ScenePresence[] newArray = new ScenePresence[oldLength + 1];
                    Array.Copy(m_scenePresenceArray, newArray, oldLength);
                    newArray[oldLength] = presence;
                    m_scenePresenceArray = newArray;
                }
                else
                {
                    m_scenePresences[presence.UUID] = presence;
                    
                    // Do a linear search through the array of ScenePresence references
                    // and update the modified entry
                    for (int i = 0; i < m_scenePresenceArray.Length; i++)
                    {
                        if (m_scenePresenceArray[i].UUID == presence.UUID)
                        {
                            m_scenePresenceArray[i] = presence;
                            break;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Remove a presence from the scene
        /// </summary>
        protected internal void RemoveScenePresence(UUID agentID)
        {
            if (!Entities.Remove(agentID))
            {
                m_log.WarnFormat(
                    "[SCENE] Tried to remove non-existent scene presence with agent ID {0} from scene Entities list",
                    agentID);
            }

            lock (m_scenePresences)
            {
                if (m_scenePresences.Remove(agentID))
                {
                    // Copy all of the elements from the previous array
                    // into the new array except the removed element
                    int oldLength = m_scenePresenceArray.Length;
                    ScenePresence[] newArray = new ScenePresence[oldLength - 1];
                    int j = 0;
                    for (int i = 0; i < m_scenePresenceArray.Length; i++)
                    {
                        ScenePresence presence = m_scenePresenceArray[i];
                        if (presence.UUID != agentID)
                        {
                            newArray[j] = presence;
                            ++j;
                        }
                    }
                    m_scenePresenceArray = newArray;
                }
                else
                {
                    m_log.WarnFormat("[SCENE] Tried to remove non-existent scene presence with agent ID {0} from scene ScenePresences list", agentID);
                }
            }
        }

        protected internal void SwapRootChildAgent(bool direction_RC_CR_T_F)
        {
            if (direction_RC_CR_T_F)
            {
                m_numRootAgents--;
                m_numChildAgents++;
            }
            else
            {
                m_numChildAgents--;
                m_numRootAgents++;
            }
        }

        public void removeUserCount(bool TypeRCTF)
        {
            if (TypeRCTF)
            {
                m_numRootAgents--;
            }
            else
            {
                m_numChildAgents--;
            }
        }

        public void RecalculateStats()
        {
            ScenePresence[] presences = GetScenePresences();
            int rootcount = 0;
            int childcount = 0;

            for (int i = 0; i < presences.Length; i++)
            {
                ScenePresence user = presences[i];
                if (user.IsChildAgent)
                    ++childcount;
                else
                    ++rootcount;
            }

            m_numRootAgents = rootcount;
            m_numChildAgents = childcount;
        }

        public int GetChildAgentCount()
        {
            // some network situations come in where child agents get closed twice.
            if (m_numChildAgents < 0)
            {
                m_numChildAgents = 0;
            }

            return m_numChildAgents;
        }

        public int GetRootAgentCount()
        {
            return m_numRootAgents;
        }

        public int GetTotalObjectsCount()
        {
            return m_numPrim;
        }

        public int GetActiveObjectsCount()
        {
            return m_physicalPrim;
        }

        public int GetActiveScriptsCount()
        {
            return m_activeScripts;
        }

        public int GetScriptLPS()
        {
            int returnval = m_scriptLPS;
            m_scriptLPS = 0;
            return returnval;
        }

        #endregion

        #region Get Methods

        /// <summary>
        /// Request a List of all scene presences in this scene.  This is a new list, so no
        /// locking is required to iterate over it.
        /// </summary>
        /// <returns></returns>
        protected internal ScenePresence[] GetScenePresences()
        {
            return m_scenePresenceArray;
        }

        protected internal List<ScenePresence> GetAvatars()
        {
            List<ScenePresence> result =
                GetScenePresences(delegate(ScenePresence scenePresence) { return !scenePresence.IsChildAgent; });

            return result;
        }

        /// <summary>
        /// Get the controlling client for the given avatar, if there is one.
        ///
        /// FIXME: The only user of the method right now is Caps.cs, in order to resolve a client API since it can't
        /// use the ScenePresence.  This could be better solved in a number of ways - we could establish an
        /// OpenSim.Framework.IScenePresence, or move the caps code into a region package (which might be the more
        /// suitable solution).
        /// </summary>
        /// <param name="agentId"></param>
        /// <returns>null if either the avatar wasn't in the scene, or
        /// they do not have a controlling client</returns>
        /// <remarks>this used to be protected internal, but that
        /// prevents CapabilitiesModule from accessing it</remarks>
        public IClientAPI GetControllingClient(UUID agentId)
        {
            ScenePresence presence = GetScenePresence(agentId);

            if (presence != null)
            {
                return presence.ControllingClient;
            }

            return null;
        }

        /// <summary>
        /// Request a filtered list of m_scenePresences in this World
        /// </summary>
        /// <returns></returns>
        protected internal List<ScenePresence> GetScenePresences(FilterAvatarList filter)
        {
            // No locking of scene presences here since we're passing back a list...

            List<ScenePresence> result = new List<ScenePresence>();
            ScenePresence[] scenePresences = GetScenePresences();

            for (int i = 0; i < scenePresences.Length; i++)
            {
                ScenePresence avatar = scenePresences[i];
                if (filter(avatar))
                    result.Add(avatar);
            }

            return result;
        }

        /// <summary>
        /// Request a scene presence by UUID
        /// </summary>
        /// <param name="avatarID"></param>
        /// <returns>null if the agent was not found</returns>
        protected internal ScenePresence GetScenePresence(UUID agentID)
        {
            ScenePresence sp;
            
            lock (m_scenePresences)
            {
                m_scenePresences.TryGetValue(agentID, out sp);
            }

            return sp;
        }

        /// <summary>
        /// Get a scene object group that contains the prim with the given local id
        /// </summary>
        /// <param name="localID"></param>
        /// <returns>null if no scene object group containing that prim is found</returns>
        public SceneObjectGroup GetGroupByPrim(uint localID)
        {
            if (Entities.ContainsKey(localID))
                return Entities[localID] as SceneObjectGroup;

            //m_log.DebugFormat("Entered GetGroupByPrim with localID {0}", localID);
            SceneObjectGroup sog;
            lock (SceneObjectGroupsByLocalID)
            {
                if (SceneObjectGroupsByLocalID.TryGetValue(localID, out sog))
                {
                    return sog;
                }
            }

            List<EntityBase> EntityList = GetEntities();
            foreach (EntityBase ent in EntityList)
            {
                //m_log.DebugFormat("Looking at entity {0}", ent.UUID);
                if (ent is SceneObjectGroup)
                {
                    if (((SceneObjectGroup)ent).HasChildPrim(localID))
                    {
                        sog = (SceneObjectGroup)ent;
                        lock (SceneObjectGroupsByLocalID)
                        {
                            SceneObjectGroupsByLocalID[localID] = sog;
                        }
                        return sog;
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Get a scene object group that contains the prim with the given uuid
        /// </summary>
        /// <param name="fullID"></param>
        /// <returns>null if no scene object group containing that prim is found</returns>
        private SceneObjectGroup GetGroupByPrim(UUID fullID)
        {
            SceneObjectGroup sog;
            lock (SceneObjectGroupsByFullID)
            {
                if (SceneObjectGroupsByFullID.TryGetValue(fullID, out sog))
                {
                    return sog;
                }
            }

            List<EntityBase> EntityList = GetEntities();

            foreach (EntityBase ent in EntityList)
            {
                if (ent is SceneObjectGroup)
                {
                    if (((SceneObjectGroup)ent).HasChildPrim(fullID))
                    {
                        sog = (SceneObjectGroup)ent;
                        lock (SceneObjectGroupsByFullID)
                        {
                            SceneObjectGroupsByFullID[fullID] = sog;
                        }
                        return sog;
                    }
                }
            }
            return null;
        }

        protected internal EntityIntersection GetClosestIntersectingPrim(Ray hray, bool frontFacesOnly, bool faceCenters)
        {
            // Primitive Ray Tracing
            float closestDistance = 280f;
            EntityIntersection result = new EntityIntersection();
            List<EntityBase> EntityList = GetEntities();
            foreach (EntityBase ent in EntityList)
            {
                if (ent is SceneObjectGroup)
                {
                    SceneObjectGroup reportingG = (SceneObjectGroup)ent;
                    EntityIntersection inter = reportingG.TestIntersection(hray, frontFacesOnly, faceCenters);
                    if (inter.HitTF && inter.distance < closestDistance)
                    {
                        closestDistance = inter.distance;
                        result = inter;
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// Get a part contained in this scene.
        /// </summary>
        /// <param name="localID"></param>
        /// <returns>null if the part was not found</returns>
        protected internal SceneObjectPart GetSceneObjectPart(uint localID)
        {
            SceneObjectGroup group = GetGroupByPrim(localID);
            if (group == null)
                return null;
            return group.GetChildPart(localID);
        }
        
        /// <summary>
        /// Get a named prim contained in this scene (will return the first 
        /// found, if there are more than one prim with the same name)
        /// </summary>
        /// <param name="name"></param>
        /// <returns>null if the part was not found</returns>
        protected internal SceneObjectPart GetSceneObjectPart(string name)
        {
            List<EntityBase> EntityList = GetEntities();

            // FIXME: use a dictionary here
            foreach (EntityBase ent in EntityList)
            {
                if (ent is SceneObjectGroup)
                {
                    foreach (SceneObjectPart p in ((SceneObjectGroup) ent).GetParts())
                    {
                        if (p.Name == name)
                        {
                            return p;
                        }
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Get a part contained in this scene.
        /// </summary>
        /// <param name="fullID"></param>
        /// <returns>null if the part was not found</returns>
        protected internal SceneObjectPart GetSceneObjectPart(UUID fullID)
        {
            SceneObjectGroup group = GetGroupByPrim(fullID);
            if (group == null)
                return null;
            return group.GetChildPart(fullID);
        }

        protected internal bool TryGetAvatar(UUID avatarId, out ScenePresence avatar)
        {
            lock (m_scenePresences)
                return m_scenePresences.TryGetValue(avatarId, out avatar);
        }

        protected internal bool TryGetAvatarByName(string avatarName, out ScenePresence avatar)
        {
            ScenePresence[] presences = GetScenePresences();

            for (int i = 0; i < presences.Length; i++)
            {
                ScenePresence presence = presences[i];

                if (!presence.IsChildAgent)
                {
                    if (String.Compare(avatarName, presence.ControllingClient.Name, true) == 0)
                    {
                        avatar = presence;
                        return true;
                    }
                }
            }

            avatar = null;
            return false;
        }

        /// <summary>
        /// Returns a list of the entities in the scene.  This is a new list so no locking is required to iterate over
        /// it
        /// </summary>
        /// <returns></returns>
        protected internal List<EntityBase> GetEntities()
        {
            return Entities.GetEntities();
        }

        public Dictionary<uint, float> GetTopScripts()
        {
            Dictionary<uint, float> topScripts = new Dictionary<uint, float>();

            List<EntityBase> EntityList = GetEntities();
            int limit = 0;
            foreach (EntityBase ent in EntityList)
            {
                if (ent is SceneObjectGroup)
                {
                    SceneObjectGroup grp = (SceneObjectGroup)ent;
                    if ((grp.RootPart.GetEffectiveObjectFlags() & (uint)PrimFlags.Scripted) != 0)
                    {
                        if (grp.scriptScore >= 0.01)
                        {
                            topScripts.Add(grp.LocalId, grp.scriptScore);
                            limit++;
                            if (limit >= 100)
                            {
                                break;
                            }
                        }
                        grp.scriptScore = 0;
                    }
                }
            }

            return topScripts;
        }

        #endregion

        #region Other Methods

        protected internal void physicsBasedCrash()
        {
            handlerPhysicsCrash = UnRecoverableError;
            if (handlerPhysicsCrash != null)
            {
                handlerPhysicsCrash();
            }
        }

        protected internal UUID ConvertLocalIDToFullID(uint localID)
        {
            SceneObjectGroup group = GetGroupByPrim(localID);
            if (group != null)
                return group.GetPartsFullID(localID);
            else
                return UUID.Zero;
        }

        protected internal void ForEachSOG(Action<SceneObjectGroup> action)
        {
            List<SceneObjectGroup> objlist = new List<SceneObjectGroup>(SceneObjectGroupsByFullID.Values);
            foreach (SceneObjectGroup obj in objlist)
            {
                try
                {
                    action(obj);
                }
                catch (Exception e)
                {
                    // Catch it and move on. This includes situations where splist has inconsistent info
                    m_log.WarnFormat("[SCENE]: Problem processing action in ForEachSOG: ", e.Message);
                }
            }
        }
        
        #endregion

        #region Client Event handlers

        /// <summary>
        ///
        /// </summary>
        /// <param name="localID"></param>
        /// <param name="scale"></param>
        /// <param name="remoteClient"></param>
        protected internal void UpdatePrimScale(uint localID, Vector3 scale, IClientAPI remoteClient)
        {
            SceneObjectGroup group = GetGroupByPrim(localID);
            if (group != null)
            {
                if (m_parentScene.Permissions.CanEditObject(group.UUID, remoteClient.AgentId))
                {
                    group.Resize(scale, localID);
                }
            }
        }

        protected internal void UpdatePrimGroupScale(uint localID, Vector3 scale, IClientAPI remoteClient)
        {
            SceneObjectGroup group = GetGroupByPrim(localID);
            if (group != null)
            {
                if (m_parentScene.Permissions.CanEditObject(group.UUID, remoteClient.AgentId))
                {
                    group.GroupResize(scale, localID);
                }
            }
        }

        /// <summary>
        /// This handles the nifty little tool tip that you get when you drag your mouse over an object
        /// Send to the Object Group to process.  We don't know enough to service the request
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="AgentID"></param>
        /// <param name="RequestFlags"></param>
        /// <param name="ObjectID"></param>
        protected internal void RequestObjectPropertiesFamily(
             IClientAPI remoteClient, UUID AgentID, uint RequestFlags, UUID ObjectID)
        {
            SceneObjectGroup group = GetGroupByPrim(ObjectID);
            if (group != null)
            {
                group.ServiceObjectPropertiesFamilyRequest(remoteClient, AgentID, RequestFlags);
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="localID"></param>
        /// <param name="rot"></param>
        /// <param name="remoteClient"></param>
        protected internal void UpdatePrimSingleRotation(uint localID, Quaternion rot, IClientAPI remoteClient)
        {
            SceneObjectGroup group = GetGroupByPrim(localID);
            if (group != null)
            {
                if (m_parentScene.Permissions.CanMoveObject(group.UUID, remoteClient.AgentId))
                {
                    group.UpdateSingleRotation(rot, localID);
                }
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="localID"></param>
        /// <param name="rot"></param>
        /// <param name="remoteClient"></param>
        protected internal void UpdatePrimSingleRotationPosition(uint localID, Quaternion rot, Vector3 pos, IClientAPI remoteClient)
        {
            SceneObjectGroup group = GetGroupByPrim(localID);
            if (group != null)
            {
                if (m_parentScene.Permissions.CanMoveObject(group.UUID, remoteClient.AgentId))
                {
                    group.UpdateSingleRotation(rot,pos, localID);
                }
            }
        }


        /// <summary>
        ///
        /// </summary>
        /// <param name="localID"></param>
        /// <param name="rot"></param>
        /// <param name="remoteClient"></param>
        protected internal void UpdatePrimRotation(uint localID, Quaternion rot, IClientAPI remoteClient)
        {
            SceneObjectGroup group = GetGroupByPrim(localID);
            if (group != null)
            {
                if (m_parentScene.Permissions.CanMoveObject(group.UUID, remoteClient.AgentId))
                {
                    group.UpdateGroupRotationR(rot);
                }
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="localID"></param>
        /// <param name="pos"></param>
        /// <param name="rot"></param>
        /// <param name="remoteClient"></param>
        protected internal void UpdatePrimRotation(uint localID, Vector3 pos, Quaternion rot, IClientAPI remoteClient)
        {
            SceneObjectGroup group = GetGroupByPrim(localID);
            if (group != null)
            {
                if (m_parentScene.Permissions.CanMoveObject(group.UUID, remoteClient.AgentId))
                {
                    group.UpdateGroupRotationPR(pos, rot);
                }
            }
        }

        /// <summary>
        /// Update the position of the given part
        /// </summary>
        /// <param name="localID"></param>
        /// <param name="pos"></param>
        /// <param name="remoteClient"></param>
        protected internal void UpdatePrimSinglePosition(uint localID, Vector3 pos, IClientAPI remoteClient)
        {
            SceneObjectGroup group = GetGroupByPrim(localID);
            if (group != null)
            {
                if (m_parentScene.Permissions.CanMoveObject(group.UUID, remoteClient.AgentId) || group.IsAttachment)
                {
                    group.UpdateSinglePosition(pos, localID);
                }
            }
        }

        /// <summary>
        /// Update the position of the given part
        /// </summary>
        /// <param name="localID"></param>
        /// <param name="pos"></param>
        /// <param name="remoteClient"></param>
        protected internal void UpdatePrimPosition(uint localID, Vector3 pos, IClientAPI remoteClient)
        {
            SceneObjectGroup group = GetGroupByPrim(localID);
            if (group != null)
            {

                // Vector3 oldPos = group.AbsolutePosition;
                if (group.IsAttachment || (group.RootPart.Shape.PCode == 9 && group.RootPart.Shape.State != 0))
                {
 
                    // If this is an attachment, then we need to save the modified
                    // object back into the avatar's inventory. First we save the
                    // attachment point information, then we update the relative 
                    // positioning (which caused this method to get driven in the
                    // first place. Then we have to mark the object as NOT an
                    // attachment. This is necessary in order to correctly save
                    // and retrieve GroupPosition information for the attachment.
                    // Then we save the asset back into the appropriate inventory
                    // entry. Finally, we restore the object's attachment status.

                    byte attachmentPoint = group.GetAttachmentPoint();
                    group.UpdateGroupPosition(pos);
                    group.RootPart.IsAttachment = false;
                    group.AbsolutePosition = group.RootPart.AttachedPos;
                    m_parentScene.UpdateKnownItem(remoteClient, group, group.GetFromItemID(), group.OwnerID);
                    group.SetAttachmentPoint(attachmentPoint);

                }
                else
                {
                    if (m_parentScene.Permissions.CanMoveObject(group.UUID, remoteClient.AgentId) && m_parentScene.Permissions.CanObjectEntry(group.UUID, false, pos))
                    {
                        group.UpdateGroupPosition(pos);
                    }
                }
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="localID"></param>
        /// <param name="texture"></param>
        /// <param name="remoteClient"></param>
        protected internal void UpdatePrimTexture(uint localID, byte[] texture, IClientAPI remoteClient)
        {
            SceneObjectGroup group = GetGroupByPrim(localID);
            if (group != null)
            {
                if (m_parentScene.Permissions.CanEditObject(group.UUID,remoteClient.AgentId))
                {
                    group.UpdateTextureEntry(localID, texture);
                }
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="localID"></param>
        /// <param name="packet"></param>
        /// <param name="remoteClient"></param>
        /// This routine seems to get called when a user changes object settings in the viewer.
        /// If some one can confirm that, please change the comment according.
        protected internal void UpdatePrimFlags(uint localID, bool UsePhysics, bool IsTemporary, bool IsPhantom, IClientAPI remoteClient)
        {
            SceneObjectGroup group = GetGroupByPrim(localID);
            if (group != null)
            {
                if (m_parentScene.Permissions.CanEditObject(group.UUID, remoteClient.AgentId))
                {
                    group.UpdatePrimFlags(localID, UsePhysics, IsTemporary, IsPhantom, false); // VolumeDetect can't be set via UI and will always be off when a change is made there
                }
            }
        }

        /// <summary>
        /// Move the given object
        /// </summary>
        /// <param name="objectID"></param>
        /// <param name="offset"></param>
        /// <param name="pos"></param>
        /// <param name="remoteClient"></param>
        protected internal void MoveObject(UUID objectID, Vector3 offset, Vector3 pos, IClientAPI remoteClient, List<SurfaceTouchEventArgs> surfaceArgs)
        {
            SceneObjectGroup group = GetGroupByPrim(objectID);
            if (group != null)
            {
                if (m_parentScene.Permissions.CanMoveObject(group.UUID, remoteClient.AgentId))// && PermissionsMngr.)
                {
                    group.GrabMovement(offset, pos, remoteClient);
                }
                // This is outside the above permissions condition
                // so that if the object is locked the client moving the object
                // get's it's position on the simulator even if it was the same as before
                // This keeps the moving user's client in sync with the rest of the world.
                group.SendGroupTerseUpdate();
            }
        }

        /// <summary>
        /// Start spinning the given object
        /// </summary>
        /// <param name="objectID"></param>
        /// <param name="rotation"></param>
        /// <param name="remoteClient"></param>
        protected internal void SpinStart(UUID objectID, IClientAPI remoteClient)
        {
            SceneObjectGroup group = GetGroupByPrim(objectID);
            if (group != null)
            {
                if (m_parentScene.Permissions.CanMoveObject(group.UUID, remoteClient.AgentId))// && PermissionsMngr.)
                {
                    group.SpinStart(remoteClient);
                }
            }
        }

        /// <summary>
        /// Spin the given object
        /// </summary>
        /// <param name="objectID"></param>
        /// <param name="rotation"></param>
        /// <param name="remoteClient"></param>
        protected internal void SpinObject(UUID objectID, Quaternion rotation, IClientAPI remoteClient)
        {
            SceneObjectGroup group = GetGroupByPrim(objectID);
            if (group != null)
            {
                if (m_parentScene.Permissions.CanMoveObject(group.UUID, remoteClient.AgentId))// && PermissionsMngr.)
                {
                    group.SpinMovement(rotation, remoteClient);
                }
                // This is outside the above permissions condition
                // so that if the object is locked the client moving the object
                // get's it's position on the simulator even if it was the same as before
                // This keeps the moving user's client in sync with the rest of the world.
                group.SendGroupTerseUpdate();
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="primLocalID"></param>
        /// <param name="description"></param>
        protected internal void PrimName(IClientAPI remoteClient, uint primLocalID, string name)
        {
            SceneObjectGroup group = GetGroupByPrim(primLocalID);
            if (group != null)
            {
                if (m_parentScene.Permissions.CanEditObject(group.UUID, remoteClient.AgentId))
                {
                    group.SetPartName(Util.CleanString(name), primLocalID);
                    group.HasGroupChanged = true;
                }
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="primLocalID"></param>
        /// <param name="description"></param>
        protected internal void PrimDescription(IClientAPI remoteClient, uint primLocalID, string description)
        {
            SceneObjectGroup group = GetGroupByPrim(primLocalID);
            if (group != null)
            {
                if (m_parentScene.Permissions.CanEditObject(group.UUID, remoteClient.AgentId))
                {
                    group.SetPartDescription(Util.CleanString(description), primLocalID);
                    group.HasGroupChanged = true;
                }
            }
        }

        protected internal void PrimClickAction(IClientAPI remoteClient, uint primLocalID, string clickAction)
        {
            SceneObjectGroup group = GetGroupByPrim(primLocalID);
            if (group != null)
            {
                if (m_parentScene.Permissions.CanEditObject(group.UUID, remoteClient.AgentId))
                {
                    SceneObjectPart part = m_parentScene.GetSceneObjectPart(primLocalID);
                    part.ClickAction = Convert.ToByte(clickAction);
                    group.HasGroupChanged = true;
                }
            }
        }

        protected internal void PrimMaterial(IClientAPI remoteClient, uint primLocalID, string material)
        {
            SceneObjectGroup group = GetGroupByPrim(primLocalID);
            if (group != null)
            {
                if (m_parentScene.Permissions.CanEditObject(group.UUID, remoteClient.AgentId))
                {
                    SceneObjectPart part = m_parentScene.GetSceneObjectPart(primLocalID);
                    part.Material = Convert.ToByte(material);
                    group.HasGroupChanged = true;
                }
            }
        }

        protected internal void UpdateExtraParam(UUID agentID, uint primLocalID, ushort type, bool inUse, byte[] data)
        {
            SceneObjectGroup group = GetGroupByPrim(primLocalID);

            if (group != null)
            {
                if (m_parentScene.Permissions.CanEditObject(group.UUID,agentID))
                {
                    group.UpdateExtraParam(primLocalID, type, inUse, data);
                }
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="primLocalID"></param>
        /// <param name="shapeBlock"></param>
        protected internal void UpdatePrimShape(UUID agentID, uint primLocalID, UpdateShapeArgs shapeBlock)
        {
            SceneObjectGroup group = GetGroupByPrim(primLocalID);
            if (group != null)
            {
                if (m_parentScene.Permissions.CanEditObject(group.GetPartsFullID(primLocalID), agentID))
                {
                    ObjectShapePacket.ObjectDataBlock shapeData = new ObjectShapePacket.ObjectDataBlock();
                    shapeData.ObjectLocalID = shapeBlock.ObjectLocalID;
                    shapeData.PathBegin = shapeBlock.PathBegin;
                    shapeData.PathCurve = shapeBlock.PathCurve;
                    shapeData.PathEnd = shapeBlock.PathEnd;
                    shapeData.PathRadiusOffset = shapeBlock.PathRadiusOffset;
                    shapeData.PathRevolutions = shapeBlock.PathRevolutions;
                    shapeData.PathScaleX = shapeBlock.PathScaleX;
                    shapeData.PathScaleY = shapeBlock.PathScaleY;
                    shapeData.PathShearX = shapeBlock.PathShearX;
                    shapeData.PathShearY = shapeBlock.PathShearY;
                    shapeData.PathSkew = shapeBlock.PathSkew;
                    shapeData.PathTaperX = shapeBlock.PathTaperX;
                    shapeData.PathTaperY = shapeBlock.PathTaperY;
                    shapeData.PathTwist = shapeBlock.PathTwist;
                    shapeData.PathTwistBegin = shapeBlock.PathTwistBegin;
                    shapeData.ProfileBegin = shapeBlock.ProfileBegin;
                    shapeData.ProfileCurve = shapeBlock.ProfileCurve;
                    shapeData.ProfileEnd = shapeBlock.ProfileEnd;
                    shapeData.ProfileHollow = shapeBlock.ProfileHollow;

                    group.UpdateShape(shapeData, primLocalID);
                }
            }
        }

        /// <summary>
        /// Initial method invoked when we receive a link objects request from the client.
        /// </summary>
        /// <param name="client"></param>
        /// <param name="parentPrim"></param>
        /// <param name="childPrims"></param>
        protected internal void LinkObjects(IClientAPI client, uint parentPrimId, List<uint> childPrimIds)
        {
            SceneObjectGroup parentGroup = GetGroupByPrim(parentPrimId);

            List<SceneObjectGroup> childGroups = new List<SceneObjectGroup>();
            if (parentGroup != null)
            {
                // We do this in reverse to get the link order of the prims correct
                for (int i = childPrimIds.Count - 1; i >= 0; i--)
                {
                    SceneObjectGroup child = GetGroupByPrim(childPrimIds[i]);
                    if (child != null)
                    {
                        // Make sure no child prim is set for sale
                        // So that, on delink, no prims are unwittingly
                        // left for sale and sold off
                        child.RootPart.ObjectSaleType = 0;
                        child.RootPart.SalePrice = 10;
                        childGroups.Add(child);
                    }
                }
            }
            else
            {
                return; // parent is null so not in this region
            }

            foreach (SceneObjectGroup child in childGroups)
            {
                parentGroup.LinkToGroup(child);

                // this is here so physics gets updated!
                // Don't remove!  Bad juju!  Stay away! or fix physics!
                child.AbsolutePosition = child.AbsolutePosition;
            }

            // We need to explicitly resend the newly link prim's object properties since no other actions
            // occur on link to invoke this elsewhere (such as object selection)
            parentGroup.RootPart.AddFlag(PrimFlags.CreateSelected);
            parentGroup.TriggerScriptChangedEvent(Changed.LINK);
            
            if (client != null)
            {
                parentGroup.GetProperties(client);
            }
            else
            {
                foreach (ScenePresence p in GetScenePresences())
                {
                    parentGroup.GetProperties(p.ControllingClient);
                }
            }
        }

        /// <summary>
        /// Delink a linkset
        /// </summary>
        /// <param name="prims"></param>
        protected internal void DelinkObjects(List<uint> primIds)
        {
            DelinkObjects(primIds, true);
        }

        protected internal void DelinkObjects(List<uint> primIds, bool sendEvents)
        {
            List<SceneObjectPart> childParts = new List<SceneObjectPart>();
            List<SceneObjectPart> rootParts = new List<SceneObjectPart>();
            List<SceneObjectGroup> affectedGroups = new List<SceneObjectGroup>();
            // Look them all up in one go, since that is comparatively expensive
            //
            foreach (uint primID in primIds)
            {
                SceneObjectPart part = m_parentScene.GetSceneObjectPart(primID);
                if (part != null)
                {
                    if (part.LinkNum < 2) // Root or single
                        rootParts.Add(part);
                    else
                        childParts.Add(part);

                    SceneObjectGroup group = part.ParentGroup;
                    if (!affectedGroups.Contains(group))
                        affectedGroups.Add(group);
                }
                else
                {
                    m_log.ErrorFormat("Viewer requested unlink of nonexistent part {0}", primID);
                }
            }

            foreach (SceneObjectPart child in childParts)
            {
                // Unlink all child parts from their groups
                //
                child.ParentGroup.DelinkFromGroup(child, sendEvents);
            }

            foreach (SceneObjectPart root in rootParts)
            {
                // In most cases, this will run only one time, and the prim
                // will be a solo prim
                // However, editing linked parts and unlinking may be different
                //
                SceneObjectGroup group = root.ParentGroup;
                List<SceneObjectPart> newSet = new List<SceneObjectPart>(group.Children.Values);
                int numChildren = group.Children.Count;

                // If there are prims left in a link set, but the root is
                // slated for unlink, we need to do this
                //
                if (numChildren != 1)
                {
                    // Unlink the remaining set
                    //
                    bool sendEventsToRemainder = true;
                    if (numChildren > 1)
                        sendEventsToRemainder = false;

                    foreach (SceneObjectPart p in newSet)
                    {
                        if (p != group.RootPart)
                            group.DelinkFromGroup(p, sendEventsToRemainder);
                    }

                    // If there is more than one prim remaining, we
                    // need to re-link
                    //
                    if (numChildren > 2)
                    {
                        // Remove old root
                        //
                        if (newSet.Contains(root))
                            newSet.Remove(root);

                        // Preserve link ordering
                        //
                        newSet.Sort(delegate (SceneObjectPart a, SceneObjectPart b)
                        {
                            return a.LinkNum.CompareTo(b.LinkNum);
                        });

                        // Determine new root
                        //
                        SceneObjectPart newRoot = newSet[0];
                        newSet.RemoveAt(0);

                        List<uint> linkIDs = new List<uint>();

                        foreach (SceneObjectPart newChild in newSet)
                        {
                            newChild.UpdateFlag = 0;
                            linkIDs.Add(newChild.LocalId);
                        }

                        LinkObjects(null, newRoot.LocalId, linkIDs);
                        if (!affectedGroups.Contains(newRoot.ParentGroup))
                            affectedGroups.Add(newRoot.ParentGroup);
                    }
                }
            }

            // Finally, trigger events in the roots
            //
            foreach (SceneObjectGroup g in affectedGroups)
            {
                g.TriggerScriptChangedEvent(Changed.LINK);
                g.HasGroupChanged = true; // Persist
                g.ScheduleGroupForFullUpdate();
            }
        }

        protected internal void MakeObjectSearchable(IClientAPI remoteClient, bool IncludeInSearch, uint localID)
        {
            UUID user = remoteClient.AgentId;
            UUID objid = UUID.Zero;
            SceneObjectPart obj = null;

            List<EntityBase> EntityList = GetEntities();
            foreach (EntityBase ent in EntityList)
            {
                if (ent is SceneObjectGroup)
                {
                    foreach (KeyValuePair<UUID, SceneObjectPart> subent in ((SceneObjectGroup)ent).Children)
                    {
                        if (subent.Value.LocalId == localID)
                        {
                            objid = subent.Key;
                            obj = subent.Value;
                        }
                    }
                }
            }

            //Protip: In my day, we didn't call them searchable objects, we called them limited point-to-point joints
            //aka ObjectFlags.JointWheel = IncludeInSearch

            //Permissions model: Object can be REMOVED from search IFF:
            // * User owns object
            //use CanEditObject

            //Object can be ADDED to search IFF:
            // * User owns object
            // * Asset/DRM permission bit "modify" is enabled
            //use CanEditObjectPosition

            // libomv will complain about PrimFlags.JointWheel being
            // deprecated, so we
            #pragma warning disable 0612
            if (IncludeInSearch && m_parentScene.Permissions.CanEditObject(objid, user))
            {
                obj.ParentGroup.RootPart.AddFlag(PrimFlags.JointWheel);
                obj.ParentGroup.HasGroupChanged = true;
            }
            else if (!IncludeInSearch && m_parentScene.Permissions.CanMoveObject(objid,user))
            {
                obj.ParentGroup.RootPart.RemFlag(PrimFlags.JointWheel);
                obj.ParentGroup.HasGroupChanged = true;
            }
            #pragma warning restore 0612
        }

        /// <summary>
        /// Duplicate the given object, Fire and Forget, No rotation, no return wrapper
        /// </summary>
        /// <param name="originalPrim"></param>
        /// <param name="offset"></param>
        /// <param name="flags"></param>
        protected internal void DuplicateObject(uint originalPrim, Vector3 offset, uint flags, UUID AgentID, UUID GroupID)
        {
            //m_log.DebugFormat("[SCENE]: Duplication of object {0} at offset {1} requested by agent {2}", originalPrim, offset, AgentID);

            // SceneObjectGroup dupe = DuplicateObject(originalPrim, offset, flags, AgentID, GroupID, Quaternion.Zero);
            DuplicateObject(originalPrim, offset, flags, AgentID, GroupID, Quaternion.Identity);
        }
        
        /// <summary>
        /// Duplicate the given object.
        /// </summary>
        /// <param name="originalPrim"></param>
        /// <param name="offset"></param>
        /// <param name="flags"></param>
        protected internal SceneObjectGroup DuplicateObject(uint originalPrimID, Vector3 offset, uint flags, UUID AgentID, UUID GroupID, Quaternion rot)
        {
            //m_log.DebugFormat("[SCENE]: Duplication of object {0} at offset {1} requested by agent {2}", originalPrim, offset, AgentID);
            SceneObjectGroup original = GetGroupByPrim(originalPrimID);
            if (original != null)
            {
                if (m_parentScene.Permissions.CanDuplicateObject(original.Children.Count, original.UUID, AgentID, original.AbsolutePosition))
                {
                    SceneObjectGroup copy = original.Copy(AgentID, GroupID, true);
                    copy.AbsolutePosition = copy.AbsolutePosition + offset;

                    Entities.Add(copy);

                    // Since we copy from a source group that is in selected
                    // state, but the copy is shown deselected in the viewer,
                    // We need to clear the selection flag here, else that
                    // prim never gets persisted at all. The client doesn't
                    // think it's selected, so it will never send a deselect...
                    copy.IsSelected = false;

                    m_numPrim += copy.Children.Count;

                    if (rot != Quaternion.Identity)
                    {
                        copy.UpdateGroupRotationR(rot);
                    }

                    copy.CreateScriptInstances(0, false, m_parentScene.DefaultScriptEngine, 0);
                    copy.HasGroupChanged = true;
                    copy.ScheduleGroupForFullUpdate();

                    // required for physics to update it's position
                    copy.AbsolutePosition = copy.AbsolutePosition;

                    if (OnObjectDuplicate != null)
                        OnObjectDuplicate(original, copy);

                    return copy;
                }
            }
            else
            {
                m_log.WarnFormat("[SCENE]: Attempted to duplicate nonexistant prim id {0}", GroupID);
            }
            
            return null;
        }
        
        /// <summary>
        /// Calculates the distance between two Vector3s
        /// </summary>
        /// <param name="v1"></param>
        /// <param name="v2"></param>
        /// <returns></returns>
        protected internal float Vector3Distance(Vector3 v1, Vector3 v2)
        {
            // We don't really need the double floating point precision...
            // so casting it to a single

            return
                (float)
                Math.Sqrt((v1.X - v2.X) * (v1.X - v2.X) + (v1.Y - v2.Y) * (v1.Y - v2.Y) + (v1.Z - v2.Z) * (v1.Z - v2.Z));
        }

        #endregion

       
    }
}
