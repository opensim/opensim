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

// CMEntityCollection.cs created with MonoDevelop
// User: bongiojp at 10:09 AM 7/7/2008
//
// Creates, Deletes, Stores ContentManagementEntities
//

#endregion Header

using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;

using OpenMetaverse;

using Nini.Config;

using OpenSim;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Physics.Manager;

using log4net;

namespace OpenSim.Region.OptionalModules.ContentManagement
{
    public class CMEntityCollection
    {
        #region Fields

        //    private static readonly log4net.ILog m_log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        // Any ContentManagementEntities that represent old versions of current SceneObjectGroups or
        // old versions of deleted SceneObjectGroups will be stored in this hash table.
        // The UUID keys are from the SceneObjectGroup RootPart UUIDs
        protected Hashtable m_CMEntityHash = Hashtable.Synchronized(new Hashtable()); //UUID to ContentManagementEntity

        // SceneObjectParts that have not been revisioned will be given green auras stored in this hashtable
        // The UUID keys are from the SceneObjectPart that they are supposed to be on.
        protected Hashtable m_NewlyCreatedEntityAura = Hashtable.Synchronized(new Hashtable()); //UUID to AuraMetaEntity

        #endregion Fields

        #region Constructors

        public CMEntityCollection()
        {
        }

        #endregion Constructors

        #region Public Properties

        public Hashtable Auras
        {
            get {return m_NewlyCreatedEntityAura; }
        }

        public Hashtable Entities
        {
            get { return m_CMEntityHash; }
        }

        #endregion Public Properties

        #region Public Methods

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

        // Check if there are SceneObjectGroups in the list that do not have corresponding ContentManagementGroups in the CMEntityHash
        public System.Collections.ArrayList CheckForMissingEntities(System.Collections.Generic.List<EntityBase> currList)
        {
            System.Collections.ArrayList missingList = new System.Collections.ArrayList();
            SceneObjectGroup temp = null;
            foreach (EntityBase currObj in currList)
            {
                if (!(currObj is SceneObjectGroup))
                    continue;
                temp = (SceneObjectGroup) currObj;

                lock (temp.Children)
                {
                    if (m_CMEntityHash.ContainsKey(temp.UUID))
                    {
                        foreach (SceneObjectPart part in temp.Children.Values)
                            if (!((ContentManagementEntity)m_CMEntityHash[temp.UUID]).HasChildPrim(part.UUID))
                                missingList.Add(part);
                    }
                    else //Entire group is missing from revision. (and is a new part in region)
                    {
                        foreach (SceneObjectPart part in temp.Children.Values)
                            missingList.Add(part);
                    }
                }
            }
            return missingList;
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
                                                    part.GetWorldPosition(),
                                                    MetaEntity.TRANSLUCENT,
                                                    new Vector3(0,254,0),
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

        public bool RemoveEntity(UUID uuid)
        {
            if (!m_CMEntityHash.ContainsKey(uuid))
                return false;
            m_CMEntityHash.Remove(uuid);
            return true;
        }

        public bool RemoveNewlyCreatedEntityAura(UUID uuid)
        {
            if (!m_NewlyCreatedEntityAura.ContainsKey(uuid))
                return false;
            m_NewlyCreatedEntityAura.Remove(uuid);
            return true;
        }

        #endregion Public Methods
    }
}
