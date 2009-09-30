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

// CMController.cs
// User: bongiojp
//

#endregion Header

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

using OpenMetaverse;

using OpenSim;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Physics.Manager;

using log4net;

namespace OpenSim.Region.OptionalModules.ContentManagement
{
    /// <summary>
    /// The controller in a Model-View-Controller framework. This controller catches actions by the avatars, creates work packets, loops through these work packets in a separate thread,
    /// then dictates to the model how the data should change and dictates to the view which data should be displayed. The main mechanism for interaction is through the simchat system.
    /// </summary>
    public class CMController
    {
        #region Static Fields

        private static readonly log4net.ILog m_log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        /// <value>
        /// The queue that keeps track of which actions have happened. The MainLoop thread eats through this queue.
        /// </value>
        private static OpenSim.Framework.BlockingQueue<Work> m_WorkQueue = new OpenSim.Framework.BlockingQueue<Work>();

        #endregion Static Fields

        #region Fields

        //bool init = false;
        int m_channel = -1;

        /// <value>
        /// The estate module is used to identify which clients are estateManagers. Presently, the controller only pays attention to estate managers.
        /// </value>
        IEstateModule m_estateModule = null;

        //These have to be global variables, threading doesn't allow for passing parameters. (Used in MainLoop)
        CMModel m_model = null;

        /// <value>
        /// A list of all the scenes that should be revisioned. Controller is the only class that keeps track of all scenes in the region.
        /// </value>
        Hashtable m_sceneList = Hashtable.Synchronized(new Hashtable());
        State m_state = State.NONE;
        Thread m_thread = null;
        CMView m_view = null;

        #endregion Fields

        #region Constructors

        /// <summary>
        /// Initializes a work thread with an initial scene. Additional scenes should be added through the RegisterNewRegion method.
        /// </summary>
        /// <param name="model">
        ///  <see cref="CMModel"/>
        /// </param>
        /// <param name="view">
        /// <see cref="CMView"/>
        /// </param>
        /// <param name="scene">
        /// The first scene to keep track of. <see cref="Scene"/>
        /// </param>
        /// <param name="channel">
        /// The simchat channel number to listen to for instructions <see cref="System.Int32"/>
        /// </param>
        public CMController(CMModel model, CMView view, Scene scene, int channel)
        {
            m_model = model; m_view = view; m_channel = channel;
            RegisterNewRegion(scene);
            Initialize(model, view, scene, channel);
        }

        #endregion Constructors

        #region Private Methods

        //------------------------------------------------ EVENTS ----------------------------------------------------//
//        private void AvatarEnteringParcel(ScenePresence avatar, int localLandID, LLUUID regionID)
//        {
//        }

        /// <summary>
        /// Searches in all scenes for a SceneObjectGroup that contains a part with a specific localID. If found, the object is returned. Else null is returned.
        /// </summary>
        private SceneObjectGroup GetGroupByPrim(uint localID)
        {
            foreach (Object currScene in m_sceneList.Values)
            {
                foreach (EntityBase ent in ((Scene)currScene).GetEntities())
                {
                    if (ent is SceneObjectGroup)
                    {
                        if (((SceneObjectGroup)ent).HasChildPrim(localID))
                            return (SceneObjectGroup)ent;
                    }
                }
            }
            return null;
        }

        private void Initialize(CMModel model, CMView view, Scene scene, int channel)
        {
            lock (this)
            {
                m_estateModule = scene.RequestModuleInterface<IEstateModule>();
                m_thread = new Thread(MainLoop);
                m_thread.Name = "Content Management";
                m_thread.IsBackground = true;
                m_thread.Start();
                ThreadTracker.Add(m_thread);
                m_state = State.NONE;
            }
        }

        /// <summary>
        /// Run in a thread of its own. A endless loop that consumes (or blocks on) and work queue. Thw work queue is filled through client actions.
        /// </summary>
        private void MainLoop()
        {
            try
            {
                CMModel model = m_model; CMView view = m_view; int channel = m_channel;
                Work currentJob = new Work();
                while (true)
                {
                    currentJob = m_WorkQueue.Dequeue();
                    m_log.Debug("[CONTENT MANAGEMENT] MAIN LOOP -- DeQueued a request");
                    m_log.Debug("[CONTENT MANAGEMENT] MAIN LOOP -- Work type: " + currentJob.Type);
                    switch (currentJob.Type)
                    {
                    case WorkType.NONE:
                        break;
                    case WorkType.OBJECTATTRIBUTECHANGE:
                        ObjectAttributeChanged(model, view, currentJob.LocalId);
                        break;
                    case WorkType.PRIMITIVEADDED:
                        PrimitiveAdded(model, view, currentJob);
                        break;
                    case WorkType.OBJECTDUPLICATED:
                        ObjectDuplicated(model, view, currentJob.LocalId);
                        break;
                    case WorkType.OBJECTKILLED:
                        ObjectKilled(model, view, (SceneObjectGroup) currentJob.Data1);
                        break;
                    case WorkType.UNDODID:
                        UndoDid(model, view, currentJob.UUID);
                        break;
                    case WorkType.NEWCLIENT:
                        NewClient(view, (IClientAPI) currentJob.Data1);
                        break;
                    case WorkType.SIMCHAT:
                        m_log.Debug("[CONTENT MANAGEMENT] MAIN LOOP -- Message received:  " + ((OSChatMessage) currentJob.Data1).Message);
                        SimChat(model, view, (OSChatMessage) currentJob.Data1, channel);
                        break;
                    default:
                        m_log.Debug("[CONTENT MANAGEMENT] MAIN LOOP -- uuuuuuuuuh, what?");
                        break;
                    }
                }
            }
            catch (Exception e)
            {
                // TODO: Let users in the sim and those entering it and possibly an external watchdog know what has happened
                m_log.ErrorFormat(
                    "[CONTENT MANAGEMENT]: Content management thread terminating with exception.  PLEASE REBOOT YOUR SIM - CONTENT MANAGEMENT WILL NOT BE AVAILABLE UNTIL YOU DO.  Exception is {0}", 
                    e);
            }
        }

        /// <summary>
        /// Only called by the MainLoop. Updates the view of a new client with metaentities if diff-mode is currently enabled.
        /// </summary>
        private void NewClient(CMView view, IClientAPI client)
        {
            if ((m_state & State.SHOWING_CHANGES) > 0)
                view.SendMetaEntitiesToNewClient(client);
        }

        /// <summary>
        /// Only called by the MainLoop.
        /// </summary>
        private void ObjectAttributeChanged(CMModel model, CMView view, uint LocalId)
        {
            SceneObjectGroup group = null;
            if ((m_state & State.SHOWING_CHANGES) > 0)
            {
                group = GetGroupByPrim(LocalId);
                if (group != null)
                {
                    view.DisplayAuras(model.UpdateNormalEntityEffects(group)); //Might be a normal entity (green aura)
                    m_view.DisplayMetaEntity(group.UUID); //Might be a meta entity (blue aura)
                }
            }
        }

        /// <summary>
        /// Only called by the MainLoop. Displays new green auras over the newly created part when a part is shift copied.
        /// </summary>
        private void ObjectDuplicated(CMModel model, CMView view, uint localId)
        {
            if ((m_state & State.SHOWING_CHANGES) > 0)
                view.DisplayAuras(model.CheckForNewEntitiesMissingAuras(GetGroupByPrim(localId).Scene));
        }

        /// <summary>
        /// Only called by the MainLoop.
        /// </summary>
        private void ObjectKilled(CMModel model, CMView view, SceneObjectGroup group)
        {
            if ((m_state & State.SHOWING_CHANGES) > 0)
            {
                view.RemoveOrUpdateDeletedEntity(group);
                model.RemoveOrUpdateDeletedEntity(group);
            }
        }

        /// <summary>
        /// Only called by the MainLoop.
        /// </summary>
        private void PrimitiveAdded(CMModel model, CMView view, Work currentJob)
        {
            if ((m_state & State.SHOWING_CHANGES) > 0)
            {
                foreach (Object scene in m_sceneList.Values)
                    m_view.DisplayAuras(model.CheckForNewEntitiesMissingAuras((Scene) scene));
            }
        }

        /// <summary>
        /// Only called by the MainLoop.
        /// </summary>
        private void UndoDid(CMModel model, CMView view, UUID uuid)
        {
            if ((m_state & State.SHOWING_CHANGES) > 0)
            {
                ContentManagementEntity ent = model.FindMetaEntityAffectedByUndo(uuid);
                if (ent != null)
                    view.DisplayEntity(ent);
            }
        }

        #endregion Private Methods

        #region Protected Methods

        protected void GroupBeingDeleted(SceneObjectGroup group)
        {
            m_log.Debug("[CONTENT MANAGEMENT] Something was deleted!!!");
            Work moreWork = new Work();
            moreWork.Type = WorkType.OBJECTKILLED;
            moreWork.Data1 = group.Copy();
            m_WorkQueue.Enqueue(moreWork);
        }

        protected void ObjectDuplicated(uint localID, Vector3 offset, uint dupeFlags, UUID AgentID, UUID GroupID)
        {
            Work moreWork = new Work();
            moreWork.Type = WorkType.OBJECTDUPLICATED;
            moreWork.LocalId = localID;
            m_WorkQueue.Enqueue(moreWork);
            m_log.Debug("[CONTENT MANAGEMENT] dup queue");
        }

        protected void ObjectDuplicatedOnRay(uint localID, uint dupeFlags, UUID AgentID, UUID GroupID,
            UUID RayTargetObj, Vector3 RayEnd, Vector3 RayStart,
            bool BypassRaycast, bool RayEndIsIntersection, bool CopyCenters, bool CopyRotates)
        {
            Work moreWork = new Work();
            moreWork.Type = WorkType.OBJECTDUPLICATED;
            moreWork.LocalId = localID;
            m_WorkQueue.Enqueue(moreWork);
            m_log.Debug("[CONTENT MANAGEMENT] dup queue");
        }

        protected void OnNewClient(IClientAPI client)
        {
            Work moreWork = new Work();
            moreWork.Type = WorkType.NEWCLIENT;
            moreWork.Data1 = client;
            m_WorkQueue.Enqueue(moreWork);
            m_log.Debug("[CONTENT MANAGEMENT] new client");
        }

        protected void OnUnDid(IClientAPI remoteClient, UUID primId)
        {
            Work moreWork = new Work();
            moreWork.Type = WorkType.UNDODID;
            moreWork.UUID = primId;
            m_WorkQueue.Enqueue(moreWork);
            m_log.Debug("[CONTENT MANAGEMENT] undid");
        }

        /// <summary>
        /// Takes a list of scenes and forms a new orderd list according to the proximity of scenes to the second argument.
        /// </summary>
        protected static System.Collections.Generic.List<Scene> ScenesInOrderOfProximity(Hashtable sceneList, Scene scene)
        {
            int somethingAddedToList = 1;
            System.Collections.Generic.List<Scene> newList = new List<Scene>();
            newList.Add(scene);

            if (!sceneList.ContainsValue(scene))
            {
                foreach (Object sceneObj in sceneList)
                    newList.Add((Scene) sceneObj);
                return newList;
            }

            while (somethingAddedToList > 0)
            {
                somethingAddedToList = 0;
                for (int i = 0; i < newList.Count; i++)
                {
                    foreach (Object sceneObj in sceneList.Values)
                    {
                        if (newList[i].CheckNeighborRegion(((Scene)sceneObj).RegionInfo) && (!newList.Contains((Scene)sceneObj)))
                        {
                            newList.Add((Scene)sceneObj);
                            somethingAddedToList++;
                        }
                    }
                }
            }

            foreach (Object sceneObj in sceneList.Values)
                if (!newList.Contains((Scene)sceneObj))
                    newList.Add((Scene)sceneObj);

            return newList;
        }

        //This is stupid, the same information is contained in the first and second argument
        protected void SimChatSent(Object x, OSChatMessage e)
        {
            m_log.Debug("[CONTENT MANAGEMENT] SIMCHAT SENT !!!!!!!");
            m_log.Debug("[CONTENT MANAGEMENT] message was: " + e.Message);
            Work moreWork = new Work();
            moreWork.Type = WorkType.SIMCHAT;
            moreWork.Data1 = e;
            m_WorkQueue.Enqueue(moreWork);
        }

        /// <summary>
        /// Adds extra handlers to a number of events so that the controller can produce work based on the client's actions.
        /// </summary>
        protected void StartManaging(IClientAPI client)
        {
            m_log.Debug("[CONTENT MANAGEMENT] Registering channel with chat services.");
            // client.OnChatFromClient += SimChatSent;
            //init = true;

            OnNewClient(client);

            m_log.Debug("[CONTENT MANAGEMENT] Adding handlers to client.");
            client.OnUpdatePrimScale += UpdateSingleScale;
            client.OnUpdatePrimGroupScale += UpdateMultipleScale;
            client.OnUpdatePrimGroupPosition += UpdateMultiplePosition;
            client.OnUpdatePrimSinglePosition += UpdateSinglePosition;
            client.OnUpdatePrimGroupRotation += UpdateMultipleRotation;
            client.OnUpdatePrimSingleRotation += UpdateSingleRotation;
            client.OnAddPrim += UpdateNewParts;
            client.OnObjectDuplicate += ObjectDuplicated;
            client.OnObjectDuplicateOnRay += ObjectDuplicatedOnRay;
            client.OnUndo += OnUnDid;
            //client.OnUpdatePrimGroupMouseRotation += m_innerScene.UpdatePrimRotation;
        }

        /// <summary>
        ///
        /// </summary>
        protected void StopManaging(UUID clientUUID)
        {
            foreach (Object sceneobj in m_sceneList.Values)
            {
                ScenePresence presence = ((Scene)sceneobj).GetScenePresence(clientUUID);
                if (presence != null)
                {
                    IClientAPI client = presence.ControllingClient;
                    m_log.Debug("[CONTENT MANAGEMENT] Unregistering channel with chat services.");
                    // client.OnChatFromViewer -= SimChatSent;

                    m_log.Debug("[CONTENT MANAGEMENT] Removing handlers to client");
                    client.OnUpdatePrimScale -= UpdateSingleScale;
                    client.OnUpdatePrimGroupScale -= UpdateMultipleScale;
                    client.OnUpdatePrimGroupPosition -= UpdateMultiplePosition;
                    client.OnUpdatePrimSinglePosition -= UpdateSinglePosition;
                    client.OnUpdatePrimGroupRotation -= UpdateMultipleRotation;
                    client.OnUpdatePrimSingleRotation -= UpdateSingleRotation;
                    client.OnAddPrim -= UpdateNewParts;
                    client.OnObjectDuplicate -= ObjectDuplicated;
                    client.OnObjectDuplicateOnRay -= ObjectDuplicatedOnRay;
                    client.OnUndo -= OnUnDid;
                    //client.OnUpdatePrimGroupMouseRotation += m_innerScene.UpdatePrimRotation;
                    return;
                }
            }
        }

        protected void UpdateMultiplePosition(uint localID, Vector3 pos, IClientAPI remoteClient)
        {
            Work moreWork = new Work();
            moreWork.Type = WorkType.OBJECTATTRIBUTECHANGE;
            moreWork.LocalId = localID;
            m_WorkQueue.Enqueue(moreWork);
            m_log.Debug("[CONTENT MANAGEMENT] pos");
        }

        protected void UpdateMultipleRotation(uint localID, Quaternion rot, IClientAPI remoteClient)
        {
            Work moreWork = new Work();
            moreWork.Type = WorkType.OBJECTATTRIBUTECHANGE;
            moreWork.LocalId = localID;
            m_WorkQueue.Enqueue(moreWork);
            m_log.Debug("[CONTENT MANAGEMENT] rot");
        }

        protected void UpdateMultipleScale(uint localID, Vector3 scale, IClientAPI remoteClient)
        {
            Work moreWork = new Work();
            moreWork.Type = WorkType.OBJECTATTRIBUTECHANGE;
            moreWork.LocalId = localID;
            m_WorkQueue.Enqueue(moreWork);
            m_log.Debug("[CONTENT MANAGEMENT]scale");
        }

        protected void UpdateNewParts(UUID ownerID, UUID groupID, Vector3 RayEnd, Quaternion rot, PrimitiveBaseShape shape,
            byte bypassRaycast, Vector3 RayStart, UUID RayTargetID,
            byte RayEndIsIntersection)
        {
            Work moreWork = new Work();
            moreWork.Type = WorkType.PRIMITIVEADDED;
            moreWork.UUID = ownerID;
            m_WorkQueue.Enqueue(moreWork);
            m_log.Debug("[CONTENT MANAGEMENT] new parts");
        }

        protected void UpdateSinglePosition(uint localID, Vector3 pos, IClientAPI remoteClient)
        {
            Work moreWork = new Work();
            moreWork.Type = WorkType.OBJECTATTRIBUTECHANGE;
            moreWork.LocalId = localID;
            m_WorkQueue.Enqueue(moreWork);
            m_log.Debug("[CONTENT MANAGEMENT] move");
        }

        /// <summary>
        ///
        /// </summary>
        protected void UpdateSingleRotation(uint localID, Quaternion rot, IClientAPI remoteClient)
        {
            Work moreWork = new Work();
            moreWork.Type = WorkType.OBJECTATTRIBUTECHANGE;
            moreWork.LocalId = localID;
            m_WorkQueue.Enqueue(moreWork);
            m_log.Debug("[CONTENT MANAGEMENT] rot");
        }

        protected void UpdateSingleScale(uint localID, Vector3 scale, IClientAPI remoteClient)
        {
            Work moreWork = new Work();
            moreWork.Type = WorkType.OBJECTATTRIBUTECHANGE;
            moreWork.LocalId = localID;
            m_WorkQueue.Enqueue(moreWork);
            m_log.Debug("[CONTENT MANAGEMENT] scale");
        }

        /// <summary>
        /// Only called from within the SimChat method.
        /// </summary>
        protected void commit(string message, Scene scene, CMModel model, CMView view)
        {
            System.Collections.Generic.List<Scene> proximitySceneList = ScenesInOrderOfProximity(m_sceneList, scene);

            string[] args = message.Split(new  char[] {' '});

            char[] logMessage = {' '};
            if (args.Length > 1)
            {
                logMessage = new char[message.Length - (args[0].Length)];
                message.CopyTo(args[0].Length, logMessage, 0, message.Length - (args[0].Length));
            }

            m_log.Debug("[CONTENT MANAGEMENT] Saving terrain and objects of region.");
            foreach (Scene currScene in proximitySceneList)
            {
                model.CommitRegion(currScene, new String(logMessage));
                view.SendSimChatMessage(scene, "Region Saved Successfully: " + currScene.RegionInfo.RegionName);
            }

            view.SendSimChatMessage(scene, "Successfully saved all regions.");
            m_state |= State.DIRTY;

            if ((m_state & State.SHOWING_CHANGES) > 0) //DISPLAY NEW CHANGES INSTEAD OF OLD CHANGES
            {
                view.SendSimChatMessage(scene, "Updating differences between new revision and current environment.");
                //Hide objects from users and Forget about them
                view.HideAllMetaEntities();
                view.HideAllAuras();
                model.DeleteAllMetaObjects();

                //Recreate them from backend files
                foreach (Scene currScene in proximitySceneList)
                {
                    model.UpdateCMEntities(currScene);
                    view.SendSimChatMessage(scene, "Finished updating differences between current scene and last revision: " + currScene.RegionInfo.RegionName);
                }

                //Display new objects to users1
                view.DisplayRecentChanges();
                view.SendSimChatMessage(scene, "Finished updating for DIFF-MODE.");
                m_state &= ~(State.DIRTY);
                m_state |= State.SHOWING_CHANGES;
            }
        }

        /// <summary>
        /// Only called from within the SimChat method.
        /// </summary>
        protected void diffmode(Scene scene, CMModel model, CMView view)
        {
            System.Collections.Generic.List<Scene> proximitySceneList = ScenesInOrderOfProximity(m_sceneList, scene);

            if ((m_state & State.SHOWING_CHANGES) > 0)  // TURN OFF
            {
                view.SendSimChatMessage(scene, "Hiding all meta objects.");
                view.HideAllMetaEntities();
                view.HideAllAuras();
                view.SendSimChatMessage(scene, "Diff-mode = OFF");

                m_state &= ~State.SHOWING_CHANGES;
                return;
            }
            else // TURN ON
            {
                if ((m_state & State.DIRTY) != 0 || m_state == State.NONE)
                {
                    view.SendSimChatMessage(scene, "Hiding meta objects and replacing with latest revision");
                    //Hide objects from users and Forget about them
                    view.HideAllMetaEntities();
                    view.HideAllAuras();
                    model.DeleteAllMetaObjects();
                    //Recreate them from backend files
                    foreach (Object currScene in m_sceneList.Values)
                        model.UpdateCMEntities((Scene) currScene);
                }
                else if ((m_state & State.DIRTY) != 0) {
                    view.SendSimChatMessage(scene, "Forming list of meta entities with latest revision");
                    foreach (Scene currScene in proximitySceneList)
                        model.UpdateCMEntities(currScene);
                }

                view.SendSimChatMessage(scene, "Displaying differences between last revision and current environment");
                foreach (Scene currScene in proximitySceneList)
                    model.CheckForNewEntitiesMissingAuras(currScene);
                view.DisplayRecentChanges();

                view.SendSimChatMessage(scene, "Diff-mode = ON");
                m_state |= State.SHOWING_CHANGES;
                m_state &= ~State.DIRTY;
            }
        }

        /// <summary>
        /// Only called from within the SimChat method. Hides all auras and meta entities,
        /// retrieves the current scene object list with the most recent revision retrieved from the model for each scene,
        /// then lets the view update the clients of the new objects.
        /// </summary>
        protected void rollback(Scene scene, CMModel model, CMView view)
        {
            if ((m_state & State.SHOWING_CHANGES) > 0)
            {
                view.HideAllAuras();
                view.HideAllMetaEntities();
            }

            System.Collections.Generic.List<Scene> proximitySceneList = ScenesInOrderOfProximity(m_sceneList, scene);
            foreach (Scene currScene in proximitySceneList)
                model.RollbackRegion(currScene);

            if ((m_state & State.DIRTY) != 0)
            {
                model.DeleteAllMetaObjects();
                foreach (Scene currScene in proximitySceneList)
                    model.UpdateCMEntities(currScene);
            }

            if ((m_state & State.SHOWING_CHANGES) > 0)
                view.DisplayRecentChanges();
        }

        #endregion Protected Methods

        #region Public Methods

        /// <summary>
        /// Register a new scene object to keep track of for revisioning. Starts the controller monitoring actions of clients within the given scene.
        /// </summary>
        /// <param name="scene">
        /// A <see cref="Scene"/>
        /// </param>
        public void RegisterNewRegion(Scene scene)
        {
            m_sceneList.Add(scene.RegionInfo.RegionID, scene);

            m_log.Debug("[CONTENT MANAGEMENT] Registering new region: " + scene.RegionInfo.RegionID);
            m_log.Debug("[CONTENT MANAGEMENT] Initializing Content Management System.");

            scene.EventManager.OnNewClient += StartManaging;
            scene.EventManager.OnChatFromClient += SimChatSent;
            scene.EventManager.OnRemovePresence += StopManaging;
            //    scene.EventManager.OnAvatarEnteringNewParcel += AvatarEnteringParcel;
            scene.EventManager.OnObjectBeingRemovedFromScene += GroupBeingDeleted;
        }

        /// <summary>
        /// Only called by the MainLoop. Takes the message from a user sent to the channel and executes the proper command.
        /// </summary>
        public void SimChat(CMModel model, CMView view, OSChatMessage e, int channel)
        {
            if (e.Channel != channel)
                return;
            if (e.Sender == null)
                return;

            m_log.Debug("[CONTENT MANAGEMENT] Message received:  " + e.Message);

            IClientAPI client = e.Sender;
            Scene scene = (Scene) e.Scene;
            string message = e.Message;
            string[] args = e.Message.Split(new  char[] {' '});

            ScenePresence avatar = scene.GetScenePresence(client.AgentId);

            if (!(m_estateModule.IsManager(avatar.UUID)))
            {
                m_log.Debug("[CONTENT MANAGEMENT] Message sent from non Estate Manager ... ignoring.");
                view.SendSimChatMessage(scene, "You must be an estate manager to perform that action.");
                return;
            }

            switch (args[0])
            {
            case "ci":
            case "commit":
                commit(message, scene, model, view);
                break;
            case "dm":
            case "diff-mode":
                diffmode(scene, model, view);
                break;
            case "rb":
            case "rollback":
                rollback(scene, model, view);
                break;
            case "help":
                m_view.DisplayHelpMenu(scene);
                break;
            default:
                view.SendSimChatMessage(scene, "Command not found: " + args[0]);
                break;
            }
        }

        #endregion Public Methods

        #region Other

        /// <value>
        /// Used to keep track of whether a list has been produced yet and whether that list is up-to-date compard to latest revision on disk.
        /// </value>
        [Flags]
        private enum State
        {
            NONE = 0,
            DIRTY = 1,  // The meta entities may not correctly represent the last revision.
            SHOWING_CHANGES = 1<<1 // The meta entities are being shown to user.
        }

        /// <value>
        /// The structure that defines the basic unit of work which is produced when a user sends commands to the ContentMangaementSystem.
        /// </value>
        private struct Work
        {
            #region Fields

            public Object Data1; //Just space for holding data.
            public Object Data2; //Just more space for holding data.
            public uint LocalId; //Convenient
            public WorkType Type;
            public UUID UUID; //Convenient

            #endregion Fields
        }

        /// <value>
        /// Identifies what the data in struct Work should be used for.
        /// </value>
        private enum WorkType
        {
            NONE,
            OBJECTATTRIBUTECHANGE,
            PRIMITIVEADDED,
            OBJECTDUPLICATED,
            OBJECTKILLED,
            UNDODID,
            NEWCLIENT,
            SIMCHAT
        }

        #endregion Other
    }
}
