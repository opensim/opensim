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
 */

using System;
using System.Collections.Generic;
using System.Reflection;
using Axiom.Math;
using libsecondlife;
using libsecondlife.Packets;
using log4net;
using OpenSim.Framework;
using OpenSim.Region.Environment.Types;
using OpenSim.Region.Physics.Manager;

namespace OpenSim.Region.Environment.Scenes
{
    public delegate void PhysicsCrash();

    public class InnerScene
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        #region Events

        protected internal event PhysicsCrash UnRecoverableError;
        private PhysicsCrash handlerPhysicsCrash = null;

        #endregion

        #region Fields

        protected internal Dictionary<LLUUID, ScenePresence> ScenePresences = new Dictionary<LLUUID, ScenePresence>();
        // SceneObjects is not currently populated or used.
        //public Dictionary<LLUUID, SceneObjectGroup> SceneObjects;
        protected internal Dictionary<LLUUID, EntityBase> Entities = new Dictionary<LLUUID, EntityBase>();
        protected internal Dictionary<LLUUID, ScenePresence> RestorePresences = new Dictionary<LLUUID, ScenePresence>();

        protected internal BasicQuadTreeNode QuadTree;

        protected RegionInfo m_regInfo;
        protected Scene m_parentScene;
        protected List<EntityBase> m_updateList = new List<EntityBase>();
        protected int m_numRootAgents = 0;
        protected int m_numPrim = 0;
        protected int m_numChildAgents = 0;
        protected int m_physicalPrim = 0;

        protected int m_activeScripts = 0;
        protected int m_scriptLPS = 0;

        protected internal object m_syncRoot = new object();

        protected internal PhysicsScene _PhyScene;

        #endregion

        protected internal InnerScene(Scene parent, RegionInfo regInfo)
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
                try
                {
                    _PhyScene.OnPhysicsCrash -= physicsBasedCrash;
                }
                catch (NullReferenceException)
                {
                    // This occurs when storing to _PhyScene the first time.
                    // Is there a better way to check the event handler before
                    // getting here
                    // This can be safely ignored.  We're setting the first inital
                    // there are no event handler's registered.
                }

                _PhyScene = value;

                _PhyScene.OnPhysicsCrash += physicsBasedCrash;
            }
        }

        protected internal void Close()
        {
            lock (ScenePresences)
            {
                ScenePresences.Clear();
            }

            //SceneObjects.Clear();
            lock (Entities)
            {
                Entities.Clear();
            }
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

        protected internal void UpdateEntities()
        {
            List<EntityBase> updateEntities = GetEntities();

            foreach (EntityBase entity in updateEntities)
            {
                entity.Update();
            }
        }

        protected internal void UpdatePresences()
        {
            List<ScenePresence> updateScenePresences = GetScenePresences();
            foreach (ScenePresence pres in updateScenePresences)
            {
                pres.Update();
            }
        }

        protected internal float UpdatePhysics(double elapsed)
        {
            lock (m_syncRoot)
            {
                return _PhyScene.Simulate((float)elapsed);
            }
        }

        protected internal void UpdateEntityMovement()
        {
            List<EntityBase> moveEntities = GetEntities();

            foreach (EntityBase entity in moveEntities)
            {
                //cfk. This throws occaisional exceptions on a heavily used region
                //and I added this null check to try to preclude the exception.
                if (entity != null)
                    entity.UpdateMovement();
            }
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
        /// <returns>
        /// true if the object was added, false if an object with the same uuid was already in the scene
        /// </returns>         
        protected internal bool AddRestoredSceneObject(SceneObjectGroup sceneObject, bool attachToBackup)
        {
            sceneObject.RegionHandle = m_regInfo.RegionHandle;
            sceneObject.SetScene(m_parentScene);

            foreach (SceneObjectPart part in sceneObject.Children.Values)
            {
                part.LocalId = m_parentScene.PrimIDAllocate();
            }
            
            sceneObject.UpdateParentIDs();

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
            sceneObject.ApplyPhysics(m_parentScene.m_physicalPrim);
            sceneObject.ScheduleGroupForFullUpdate();
            
            lock (Entities)
            {
                if (!Entities.ContainsKey(sceneObject.UUID))
                {
                    //  QuadTree.AddSceneObject(sceneObject);
                    Entities.Add(sceneObject.UUID, sceneObject);
                    m_numPrim += sceneObject.Children.Count;

                    if (attachToBackup)
                        sceneObject.AttachToBackup();

                    return true;
                }

                return false;
            }
        }

        /// <summary>
        /// Delete an object from the scene
        /// </summary>
        /// <param name="sceneObject"></param>
        /// <returns>true if the object was deleted, false if there was no object to delete</returns>
        protected internal bool DeleteSceneObject(LLUUID uuid, bool resultOfObjectLinked)
        {
            lock (Entities)
            {
                if (Entities.ContainsKey(uuid))
                {
                    if (!resultOfObjectLinked)
                    {
                        m_numPrim -= ((SceneObjectGroup)Entities[uuid]).Children.Count;
                    }
                    Entities.Remove(uuid);

                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Add an entity to the list of prims to process on the next update
        /// </summary>
        /// <param name="obj">
        /// A <see cref="EntityBase"/>
        /// </param>
        protected internal void AddToUpdateList(EntityBase obj)
        {
            lock (m_updateList)
            {
                if (!m_updateList.Contains(obj))
                {
                    m_updateList.Add(obj);
                }
            }
        }

        /// <summary>
        /// Process all pending updates
        /// </summary>
        protected internal void ProcessUpdates()
        {
            lock (m_updateList)
            {
                for (int i = 0; i < m_updateList.Count; i++)
                {
                    EntityBase entity = m_updateList[i];

                    // Don't abort the whole update if one entity happens to give us an exception.
                    try
                    {
                        // Check that the group was not deleted before the scheduled update
                        // FIXME: This is merely a temporary measure to reduce the incidence of failure, when
                        // an object has been deleted from a scene before update was processed.
                        // A more fundamental overhaul of the update mechanism is required to eliminate all
                        // the race conditions.
                        if (!entity.IsDeleted)
                        {
                            m_updateList[i].Update();
                        }
                    }
                    catch (Exception e)
                    {
                        m_log.ErrorFormat(
                            "[INNER SCENE]: Failed to update {0}, {1} - {2}", entity.Name, entity.UUID, e);
                    }
                }
                
                m_updateList.Clear();
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

        protected internal void DetachObject(uint objectLocalID, IClientAPI remoteClient)
        {
            List<EntityBase> EntityList = GetEntities();

            foreach (EntityBase obj in EntityList)
            {
                if (obj is SceneObjectGroup)
                {
                    if (((SceneObjectGroup)obj).LocalId == objectLocalID)
                    {
                        SceneObjectGroup group = (SceneObjectGroup)obj;

                        //group.DetachToGround();
                        DetachSingleAttachmentToInv(group.GetFromAssetID(),remoteClient);
                    }
                }
            }
        }

        protected internal void HandleUndo(IClientAPI remoteClient, LLUUID primId)
        {
            if (primId != LLUUID.Zero)
            {
                SceneObjectPart part =  m_parentScene.GetSceneObjectPart(primId);
                if (part != null)
                    part.Undo();
            }
        }

        protected internal void HandleObjectGroupUpdate(
            IClientAPI remoteClient, LLUUID GroupID, uint objectLocalID, LLUUID Garbage)
        {
            List<EntityBase> EntityList = GetEntities();

            foreach (EntityBase obj in EntityList)
            {
                if (obj is SceneObjectGroup)
                {
                    if (((SceneObjectGroup)obj).LocalId == objectLocalID)
                    {
                        SceneObjectGroup group = (SceneObjectGroup)obj;

                        if (m_parentScene.ExternalChecks.ExternalChecksCanEditObject(group.UUID, remoteClient.AgentId))
                            group.SetGroup(GroupID, remoteClient);
                        else
                            remoteClient.SendAgentAlertMessage("You don't have permission to set the group", false);
                    }
                }
            }
        }

        /// <summary>
        /// Event Handling routine for Attach Object
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="objectLocalID"></param>
        /// <param name="AttachmentPt"></param>
        /// <param name="rot"></param>
        protected internal void AttachObject(IClientAPI remoteClient, uint objectLocalID, uint AttachmentPt, LLQuaternion rot)
        {
            // Calls attach with a Zero position

            AttachObject(remoteClient, objectLocalID, AttachmentPt, rot, LLVector3.Zero);
        }

        protected internal void RezSingleAttachment(
            IClientAPI remoteClient, LLUUID itemID, uint AttachmentPt,uint ItemFlags, uint NextOwnerMask)
        {
            SceneObjectGroup objatt = m_parentScene.RezObject(remoteClient, itemID, LLVector3.Zero, LLVector3.Zero, LLUUID.Zero, (byte)1, true,
                (uint)(PermissionMask.Copy | PermissionMask.Move | PermissionMask.Modify | PermissionMask.Transfer),
                (uint)(PermissionMask.Copy | PermissionMask.Move | PermissionMask.Modify | PermissionMask.Transfer),
                (uint)(PermissionMask.Copy | PermissionMask.Move | PermissionMask.Modify | PermissionMask.Transfer),
                ItemFlags, false, false, remoteClient.AgentId, true);

            if (objatt != null)
            {
                AttachObject(remoteClient,objatt.LocalId,AttachmentPt,new LLQuaternion(0,0,0,1),objatt.AbsolutePosition);
                objatt.ScheduleGroupForFullUpdate();
            }
        }

        // What makes this method odd and unique is it tries to detach using an LLUUID....     Yay for standards.
        // To LocalId or LLUUID, *THAT* is the question. How now Brown LLUUID??
        protected internal void DetachSingleAttachmentToInv(LLUUID itemID, IClientAPI remoteClient)
        {
            if (itemID == LLUUID.Zero) // If this happened, someone made a mistake....
                return;

            List<EntityBase> EntityList = GetEntities();

            foreach (EntityBase obj in EntityList)
            {
                if (obj is SceneObjectGroup)
                {
                    if (((SceneObjectGroup)obj).GetFromAssetID() == itemID)
                    {
                        SceneObjectGroup group = (SceneObjectGroup)obj;
                        group.DetachToInventoryPrep();
                        m_log.Debug("[DETACH]: Saving attachpoint: " + ((uint)group.GetAttachmentPoint()).ToString());
                        m_parentScene.updateKnownAsset(remoteClient, group, group.GetFromAssetID(), group.OwnerID);
                        m_parentScene.DeleteSceneObject(group);
                    }
                }
            }
        }

        protected internal void AttachObject(
            IClientAPI remoteClient, uint objectLocalID, uint AttachmentPt, LLQuaternion rot, LLVector3 attachPos)
        {
            List<EntityBase> EntityList = GetEntities();
            foreach (EntityBase obj in EntityList)
            {
                if (obj is SceneObjectGroup)
                {
                    if (((SceneObjectGroup)obj).LocalId == objectLocalID)
                    {
                        SceneObjectGroup group = (SceneObjectGroup)obj;
                        if (m_parentScene.ExternalChecks.ExternalChecksCanTakeObject(obj.UUID, remoteClient.AgentId))
                        {
                            // If the attachment point isn't the same as the one previously used
                            // set it's offset position = 0 so that it appears on the attachment point
                            // and not in a weird location somewhere unknown.
                            if (AttachmentPt != 0 && AttachmentPt != (uint)group.GetAttachmentPoint())
                            {

                                attachPos = LLVector3.Zero;
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
                                attachPos = LLVector3.Zero;
                            }
                            m_log.Debug("[ATTACH]: Using attachpoint: " + AttachmentPt.ToString());



                            // Saves and gets assetID
                            if (group.GetFromAssetID() == LLUUID.Zero)
                            {
                                LLUUID newAssetID = m_parentScene.attachObjectAssetStore(remoteClient, group, remoteClient.AgentId);

                                // sets assetID so client can show asset as 'attached' in inventory
                                group.SetFromAssetID(newAssetID);
                            }
                            group.AttachToAgent(remoteClient.AgentId, AttachmentPt, attachPos);
                        }
                        else
                        {
                            remoteClient.SendAgentAlertMessage("You don't have sufficient permissions to attach this object", false);
                        }

                    }
                }
            }
        }

        protected internal ScenePresence CreateAndAddScenePresence(IClientAPI client, bool child, AvatarAppearance appearance)
        {
            ScenePresence newAvatar = null;

            newAvatar = new ScenePresence(client, m_parentScene, m_regInfo, appearance);
            newAvatar.IsChildAgent = child;

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
                m_log.Info("[SCENE]" + m_regInfo.RegionName + ": Creating new child agent.");
            }
            else
            {
                m_numRootAgents++;
                m_log.Info("[SCENE] " + m_regInfo.RegionName + ": Creating new root agent.");
                m_log.Info("[SCENE] " + m_regInfo.RegionName + ": Adding Physical agent.");

                presence.AddToPhysicalScene();
            }

            lock (Entities)
            {
                Entities[presence.UUID] = presence;
            }

            lock (ScenePresences)
            {
                ScenePresences[presence.UUID] = presence;
            }
        }

        /// <summary>
        /// Remove a presence from the scene
        /// </summary>
        protected internal void RemoveScenePresence(LLUUID agentID)
        {
            lock (Entities)
            {
                if (Entities.Remove(agentID))
                {
                    //m_log.InfoFormat("[SCENE] Removed scene presence {0} from entities list", agentID);
                }
                else
                {
                    m_log.WarnFormat("[SCENE] Tried to remove non-existent scene presence with agent ID {0} from scene Entities list", agentID);
                }
            }

            lock (ScenePresences)
            {
                if (ScenePresences.Remove(agentID))
                {
                    //m_log.InfoFormat("[SCENE] Removed scene presence {0}", agentID);
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

        protected internal void removeUserCount(bool TypeRCTF)
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
        protected internal List<ScenePresence> GetScenePresences()
        {
            return new List<ScenePresence>(ScenePresences.Values);
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
        /// <returns>null if either the avatar wasn't in the scene, or they do not have a controlling client</returns>
        protected internal IClientAPI GetControllingClient(LLUUID agentId)
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
            List<ScenePresence> ScenePresencesList = GetScenePresences();

            foreach (ScenePresence avatar in ScenePresencesList)
            {
                if (filter(avatar))
                {
                    result.Add(avatar);
                }
            }

            return result;
        }

        /// <summary>
        /// Request a scene presence by UUID
        /// </summary>
        /// <param name="avatarID"></param>
        /// <returns>null if the agent was not found</returns>
        protected internal ScenePresence GetScenePresence(LLUUID agentID)
        {
            ScenePresence sp;
            ScenePresences.TryGetValue(agentID, out sp);

            return sp;
        }

        /// <summary>
        /// Get a scene object group that contains the prim with the given local id
        /// </summary>
        /// <param name="localID"></param>
        /// <returns>null if no scene object group containing that prim is found</returns>
        private SceneObjectGroup GetGroupByPrim(uint localID)
        {
            List<EntityBase> EntityList = GetEntities();

            foreach (EntityBase ent in EntityList)
            {
                if (ent is SceneObjectGroup)
                {
                    if (((SceneObjectGroup)ent).HasChildPrim(localID))
                        return (SceneObjectGroup)ent;
                }
            }
            return null;
        }

        /// <summary>
        /// Get a scene object group that contains the prim with the given uuid
        /// </summary>
        /// <param name="fullID"></param>
        /// <returns>null if no scene object group containing that prim is found</returns>        
        private SceneObjectGroup GetGroupByPrim(LLUUID fullID)
        {
            List<EntityBase> EntityList = GetEntities();

            foreach (EntityBase ent in EntityList)
            {
                if (ent is SceneObjectGroup)
                {
                    if (((SceneObjectGroup)ent).HasChildPrim(fullID))
                        return (SceneObjectGroup)ent;
                }
            }
            return null;
        }

        protected internal EntityIntersection GetClosestIntersectingPrim(Ray hray, bool frontFacesOnly, bool faceCenters)
        {
            // Primitive Ray Tracing
            float closestDistance = 280f;
            EntityIntersection returnResult = new EntityIntersection();
            List<EntityBase> EntityList = GetEntities();
            foreach (EntityBase ent in EntityList)
            {
                if (ent is SceneObjectGroup)
                {
                    SceneObjectGroup reportingG = (SceneObjectGroup)ent;
                    EntityIntersection result = reportingG.TestIntersection(hray, frontFacesOnly, faceCenters);
                    if (result.HitTF)
                    {
                        if (result.distance < closestDistance)
                        {
                            closestDistance = result.distance;
                            returnResult = result;
                        }
                    }
                }
            }
            return returnResult;
        }

        /// <summary>
        /// Get a part contained in this scene.
        /// </summary>
        /// <param name="localID"></param>
        /// <returns>null if the part was not found</returns>
        protected internal SceneObjectPart GetSceneObjectPart(uint localID)
        {
            SceneObjectGroup group = GetGroupByPrim(localID);
            
            if (group != null)
                return group.GetChildPart(localID);
            else
                return null;
        }

        /// <summary>
        /// Get a part contained in this scene.
        /// </summary>
        /// <param name="fullID"></param>
        /// <returns>null if the part was not found</returns>        
        protected internal SceneObjectPart GetSceneObjectPart(LLUUID fullID)
        {
            SceneObjectGroup group = GetGroupByPrim(fullID);
            if (group != null)
                return group.GetChildPart(fullID);
            else
                return null;
        }

        protected internal bool TryGetAvatar(LLUUID avatarId, out ScenePresence avatar)
        {
            ScenePresence presence;
            if (ScenePresences.TryGetValue(avatarId, out presence))
            {
                if (!presence.IsChildAgent)
                {
                    avatar = presence;
                    return true;
                }
                else
                {
                    m_log.WarnFormat(
                        "[INNER SCENE]: Requested avatar {0} could not be found in scene {1} since it is only registered as a child agent!",
                        avatarId, m_parentScene.RegionInfo.RegionName);
                }
            }

            avatar = null;
            return false;
        }

        protected internal bool TryGetAvatarByName(string avatarName, out ScenePresence avatar)
        {
            lock (ScenePresences)
            {
                foreach (ScenePresence presence in ScenePresences.Values)
                {
                    if (!presence.IsChildAgent)
                    {
                        string name = presence.ControllingClient.Name;

                        if (String.Compare(avatarName, name, true) == 0)
                        {
                            avatar = presence;
                            return true;
                        }
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
            lock (Entities)
            {
                return new List<EntityBase>(Entities.Values);
            }
        }

        protected internal Dictionary<uint, float> GetTopScripts()
        {
            Dictionary<uint, float> topScripts = new Dictionary<uint, float>();

            List<EntityBase> EntityList = GetEntities();
            int limit = 0;
            foreach (EntityBase ent in EntityList)
            {
                if (ent is SceneObjectGroup)
                {
                    SceneObjectGroup grp = (SceneObjectGroup)ent;
                    if ((grp.RootPart.GetEffectiveObjectFlags() & (uint)LLObject.ObjectFlags.Scripted) != 0)
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

        protected internal LLUUID ConvertLocalIDToFullID(uint localID)
        {
            SceneObjectGroup group = GetGroupByPrim(localID);
            if (group != null)
                return group.GetPartsFullID(localID);
            else
                return LLUUID.Zero;
        }

        protected internal void SendAllSceneObjectsToClient(ScenePresence presence)
        {
            List<EntityBase> EntityList = GetEntities();

            foreach (EntityBase ent in EntityList)
            {
                if (ent is SceneObjectGroup)
                {
                    // Only send child agents stuff in their draw distance.
                    // This will need to be done for every agent once we figure out
                    // what we're going to use to store prim that agents already got
                    // the initial update for and what we'll use to limit the
                    // space we check for new objects on movement.

                    if (presence.IsChildAgent && m_parentScene.m_seeIntoRegionFromNeighbor)
                    {
                        LLVector3 oLoc = ((SceneObjectGroup)ent).AbsolutePosition;
                        float distResult = (float)Util.GetDistanceTo(presence.AbsolutePosition, oLoc);

                        //m_log.Info("[DISTANCE]: " + distResult.ToString());

                        if (distResult < presence.DrawDistance)
                        {
                            // Send Only if we don't already know about it.
                            // KnownPrim also makes the prim known when called.
                            if (!presence.KnownPrim(((SceneObjectGroup)ent).UUID))
                                ((SceneObjectGroup)ent).ScheduleFullUpdateToAvatar(presence);
                        }
                    }
                    else
                    {
                        ((SceneObjectGroup)ent).ScheduleFullUpdateToAvatar(presence);
                    }
                }
            }
        }

        protected internal void ForEachClient(Action<IClientAPI> action)
        {
            lock (ScenePresences)
            {
                foreach (ScenePresence presence in ScenePresences.Values)
                {
                    action(presence.ControllingClient);
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
        protected internal void UpdatePrimScale(uint localID, LLVector3 scale, IClientAPI remoteClient)
        {
            SceneObjectGroup group = GetGroupByPrim(localID);
            if (group != null)
            {
                if (m_parentScene.ExternalChecks.ExternalChecksCanEditObject(group.UUID, remoteClient.AgentId))
                {
                    group.Resize(scale, localID);
                }
            }
        }

        protected internal void UpdatePrimGroupScale(uint localID, LLVector3 scale, IClientAPI remoteClient)
        {
            SceneObjectGroup group = GetGroupByPrim(localID);
            if (group != null)
            {
                if (m_parentScene.ExternalChecks.ExternalChecksCanEditObject(group.UUID, remoteClient.AgentId))
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
             IClientAPI remoteClient, LLUUID AgentID, uint RequestFlags, LLUUID ObjectID)
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
        protected internal void UpdatePrimSingleRotation(uint localID, LLQuaternion rot, IClientAPI remoteClient)
        {
            SceneObjectGroup group = GetGroupByPrim(localID);
            if (group != null)
            {
                if (m_parentScene.ExternalChecks.ExternalChecksCanMoveObject(group.UUID, remoteClient.AgentId))
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
        protected internal void UpdatePrimRotation(uint localID, LLQuaternion rot, IClientAPI remoteClient)
        {
            SceneObjectGroup group = GetGroupByPrim(localID);
            if (group != null)
            {
                if (m_parentScene.ExternalChecks.ExternalChecksCanMoveObject(group.UUID, remoteClient.AgentId))
                {
                    group.UpdateGroupRotation(rot);
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
        protected internal void UpdatePrimRotation(uint localID, LLVector3 pos, LLQuaternion rot, IClientAPI remoteClient)
        {
            SceneObjectGroup group = GetGroupByPrim(localID);
            if (group != null)
            {
                if (m_parentScene.ExternalChecks.ExternalChecksCanMoveObject(group.UUID, remoteClient.AgentId))
                {
                    group.UpdateGroupRotation(pos, rot);
                }
            }
        }

        /// <summary>
        /// Update the position of the given part
        /// </summary>
        /// <param name="localID"></param>
        /// <param name="pos"></param>
        /// <param name="remoteClient"></param>
        protected internal void UpdatePrimSinglePosition(uint localID, LLVector3 pos, IClientAPI remoteClient)
        {
            SceneObjectGroup group = GetGroupByPrim(localID);
            if (group != null)
            {
                // LLVector3 oldPos = group.AbsolutePosition;
                if (!m_parentScene.ExternalChecks.ExternalChecksCanObjectEntry(group.UUID,pos) && !group.RootPart.m_IsAttachment)
                {
                    group.SendGroupTerseUpdate();
                    return;
                }
                
                if (m_parentScene.ExternalChecks.ExternalChecksCanMoveObject(group.UUID, remoteClient.AgentId) || group.RootPart.m_IsAttachment)
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
        protected internal void UpdatePrimPosition(uint localID, LLVector3 pos, IClientAPI remoteClient)
        {
            SceneObjectGroup group = GetGroupByPrim(localID);
            if (group != null)
            {

                // LLVector3 oldPos = group.AbsolutePosition;
                if (group.RootPart.m_IsAttachment)
                {
                    group.UpdateGroupPosition(pos);
                }
                else
                {
                    if (!m_parentScene.ExternalChecks.ExternalChecksCanObjectEntry(group.UUID,pos) && !group.RootPart.m_IsAttachment)
                    {
                        group.SendGroupTerseUpdate();
                        
                        return;
                    }
                        if (m_parentScene.ExternalChecks.ExternalChecksCanMoveObject(group.UUID, remoteClient.AgentId) || group.RootPart.m_IsAttachment)
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
                if (m_parentScene.ExternalChecks.ExternalChecksCanEditObject(group.UUID,remoteClient.AgentId))
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
        protected internal void UpdatePrimFlags(uint localID, Packet packet, IClientAPI remoteClient)
        {
            SceneObjectGroup group = GetGroupByPrim(localID);
            if (group != null)
            {
                if (m_parentScene.ExternalChecks.ExternalChecksCanEditObject(group.UUID, remoteClient.AgentId))
                {
                    group.UpdatePrimFlags(localID, (ushort)packet.Type, true, packet.ToBytes());
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
        protected internal void MoveObject(LLUUID objectID, LLVector3 offset, LLVector3 pos, IClientAPI remoteClient)
        {
            SceneObjectGroup group = GetGroupByPrim(objectID);
            if (group != null)
            {
                if (m_parentScene.ExternalChecks.ExternalChecksCanMoveObject(group.UUID, remoteClient.AgentId))// && PermissionsMngr.)
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
        ///
        /// </summary>
        /// <param name="primLocalID"></param>
        /// <param name="description"></param>
        protected internal void PrimName(IClientAPI remoteClient, uint primLocalID, string name)
        {
            SceneObjectGroup group = GetGroupByPrim(primLocalID);
            if (group != null)
            {
                if (m_parentScene.ExternalChecks.ExternalChecksCanEditObject(group.UUID, remoteClient.AgentId))
                {
                    group.SetPartName(Util.CleanString(name), primLocalID);
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
                if (m_parentScene.ExternalChecks.ExternalChecksCanEditObject(group.UUID, remoteClient.AgentId))
                {
                    group.SetPartDescription(Util.CleanString(description), primLocalID);
                }
            }
        }

        protected internal void UpdateExtraParam(LLUUID agentID, uint primLocalID, ushort type, bool inUse, byte[] data)
        {
            SceneObjectGroup group = GetGroupByPrim(primLocalID);

            if (group != null)
            {
                if (m_parentScene.ExternalChecks.ExternalChecksCanEditObject(group.UUID,agentID))
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
        protected internal void UpdatePrimShape(LLUUID agentID, uint primLocalID, UpdateShapeArgs shapeBlock)
        {
            SceneObjectGroup group = GetGroupByPrim(primLocalID);
            if (group != null)
            {
                if (m_parentScene.ExternalChecks.ExternalChecksCanEditObject(group.GetPartsFullID(primLocalID), agentID))
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
        protected internal void LinkObjects(IClientAPI client, uint parentPrim, List<uint> childPrims)
        {
            List<EntityBase> EntityList = GetEntities();

            SceneObjectGroup parenPrim = null;
            foreach (EntityBase ent in EntityList)
            {
                if (ent is SceneObjectGroup)
                {
                    if (((SceneObjectGroup)ent).LocalId == parentPrim)
                    {
                        parenPrim = (SceneObjectGroup)ent;
                        break;
                    }
                }
            }

            List<SceneObjectGroup> children = new List<SceneObjectGroup>();
            if (parenPrim != null)
            {
                for (int i = 0; i < childPrims.Count; i++)
                {
                    foreach (EntityBase ent in EntityList)
                    {
                        if (ent is SceneObjectGroup)
                        {
                            if (((SceneObjectGroup)ent).LocalId == childPrims[i])
                            {
                                children.Add((SceneObjectGroup)ent);
                            }
                        }
                    }
                }
            }

            foreach (SceneObjectGroup sceneObj in children)
            {
                parenPrim.LinkToGroup(sceneObj);
            }

            // We need to explicitly resend the newly link prim's object properties since no other actions
            // occur on link to invoke this elsewhere (such as object selection)
            parenPrim.GetProperties(client);
        }

        /// <summary>
        /// Delink a linkset
        /// </summary>
        /// <param name="prims"></param>
        protected internal void DelinkObjects(List<uint> primIds)
        {
            SceneObjectGroup parenPrim = null;

            // Need a list of the SceneObjectGroup local ids
            // XXX I'm anticipating that building this dictionary once is more efficient than
            // repeated scanning of the Entity.Values for a large number of primIds.  However, it might
            // be more efficient yet to keep this dictionary permanently on hand.

            Dictionary<uint, SceneObjectGroup> sceneObjects = new Dictionary<uint, SceneObjectGroup>();

            List<EntityBase> EntityList = GetEntities();
            foreach (EntityBase ent in EntityList)
            {
                if (ent is SceneObjectGroup)
                {
                    SceneObjectGroup obj = (SceneObjectGroup)ent;
                    sceneObjects.Add(obj.LocalId, obj);

                }
            }

            // Find the root prim among the prim ids we've been given
            for (int i = 0; i < primIds.Count; i++)
            {

                if (sceneObjects.ContainsKey(primIds[i]))
                {

                    parenPrim = sceneObjects[primIds[i]];
                    primIds.RemoveAt(i);
                    break;
                }
            }

            if (parenPrim != null)
            {
                foreach (uint childPrimId in primIds)
                {
                    parenPrim.DelinkFromGroup(childPrimId);
                }
            }
            else
            {
                // If the first scan failed, we need to do a /deep/ scan of the linkages.  This is /really/ slow
                // We know that this is not the root prim now essentially, so we don't have to worry about remapping
                // which one is the root prim
                bool delinkedSomething = false;
                for (int i = 0; i < primIds.Count; i++)
                {
                    foreach (SceneObjectGroup grp in sceneObjects.Values)
                    {
                        SceneObjectPart gPart = grp.GetChildPart(primIds[i]);
                        if (gPart != null)
                        {
                            grp.DelinkFromGroup(primIds[i]);
                            delinkedSomething = true;
                        }

                    }
                }
                if (!delinkedSomething)
                {
                    m_log.InfoFormat("[SCENE]: " +
                                    "DelinkObjects(): Could not find a root prim out of {0} as given to a delink request!",
                                    primIds);
                }
            }
        }

        protected internal void MakeObjectSearchable(IClientAPI remoteClient, bool IncludeInSearch, uint localID)
        {
            LLUUID user = remoteClient.AgentId;
            LLUUID objid = null;
            SceneObjectPart obj = null;

            List<EntityBase> EntityList = GetEntities();
            foreach (EntityBase ent in EntityList)
            {
                if (ent is SceneObjectGroup)
                {
                    foreach (KeyValuePair<LLUUID, SceneObjectPart> subent in ((SceneObjectGroup)ent).Children)
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

            if (IncludeInSearch && m_parentScene.ExternalChecks.ExternalChecksCanEditObject(objid, user))
            {
                obj.ParentGroup.RootPart.AddFlag(LLObject.ObjectFlags.JointWheel);
            }
            else if (!IncludeInSearch && m_parentScene.ExternalChecks.ExternalChecksCanMoveObject(objid,user))
            {
                obj.ParentGroup.RootPart.RemFlag(LLObject.ObjectFlags.JointWheel);
            }
        }

        /// <summary>
        /// Duplicate the given object, Fire and Forget, No rotation, no return wrapper
        /// </summary>
        /// <param name="originalPrim"></param>
        /// <param name="offset"></param>
        /// <param name="flags"></param>
        protected internal void DuplicateObject(uint originalPrim, LLVector3 offset, uint flags, LLUUID AgentID, LLUUID GroupID)
        {
            //m_log.DebugFormat("[SCENE]: Duplication of object {0} at offset {1} requested by agent {2}", originalPrim, offset, AgentID);

            // SceneObjectGroup dupe = DuplicateObject(originalPrim, offset, flags, AgentID, GroupID, Quaternion.Zero);
            DuplicateObject(originalPrim, offset, flags, AgentID, GroupID, Quaternion.Zero);
        }
        /// <summary>
        /// Duplicate the given object.
        /// </summary>
        /// <param name="originalPrim"></param>
        /// <param name="offset"></param>
        /// <param name="flags"></param>
        protected internal SceneObjectGroup DuplicateObject(uint originalPrim, LLVector3 offset, uint flags, LLUUID AgentID, LLUUID GroupID, Quaternion rot)
        {
            //m_log.DebugFormat("[SCENE]: Duplication of object {0} at offset {1} requested by agent {2}", originalPrim, offset, AgentID);

            List<EntityBase> EntityList = GetEntities();

            SceneObjectGroup originPrim = null;
            foreach (EntityBase ent in EntityList)
            {
                if (ent is SceneObjectGroup)
                {
                    if (((SceneObjectGroup)ent).LocalId == originalPrim)
                    {
                        originPrim = (SceneObjectGroup)ent;
                        break;
                    }
                }
            }

            if (originPrim != null)
            {
                if (m_parentScene.ExternalChecks.ExternalChecksCanDuplicateObject(originPrim.Children.Count, originPrim.UUID, AgentID, originPrim.AbsolutePosition))
                {
                    SceneObjectGroup copy = originPrim.Copy(AgentID, GroupID, true);
                    copy.AbsolutePosition = copy.AbsolutePosition + offset;
                    copy.ResetIDs();

                    lock (Entities)
                    {
                        Entities.Add(copy.UUID, copy);
                    }

                    // Since we copy from a source group that is in selected
                    // state, but the copy is shown deselected in the viewer,
                    // We need to clear the selection flag here, else that
                    // prim never gets persisted at all. The client doesn't
                    // think it's selected, so it will never send a deselect...
                    copy.IsSelected = false;

                    m_numPrim += copy.Children.Count;

                    if (rot != Quaternion.Zero)
                    {
                        copy.UpdateGroupRotation(new LLQuaternion(rot.x, rot.y, rot.z, rot.w));
                    }

                    copy.CreateScriptInstances(0, false);
                    copy.HasGroupChanged = true;
                    copy.ScheduleGroupForFullUpdate();
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
                Math.Sqrt((v1.x - v2.x) * (v1.x - v2.x) + (v1.y - v2.y) * (v1.y - v2.y) + (v1.z - v2.z) * (v1.z - v2.z));
        }

        #endregion
    }
}
