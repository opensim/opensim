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
using System.Drawing;

using OpenMetaverse;

using Nini.Config;

using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Framework.Scenes.Serialization;
using OpenSim.Region.Physics.Manager;

using log4net;

namespace OpenSim.Region.OptionalModules.ContentManagement
{
    public class ContentManagementEntity : MetaEntity
    {
        #region Static Fields

//        static float TimeToDiff = 0;
//        static float TimeToCreateEntities = 0;

        #endregion Static Fields

        #region Fields

        protected Dictionary<UUID, AuraMetaEntity> m_AuraEntities = new Dictionary<UUID, AuraMetaEntity>();
        protected Dictionary<UUID, BeamMetaEntity> m_BeamEntities = new Dictionary<UUID, BeamMetaEntity>();

        // The LinkNum of parts in m_Entity and m_UnchangedEntity are the same though UUID and LocalId are different.
        // This can come in handy.
        protected SceneObjectGroup m_UnchangedEntity = null;

        /// <value>
        /// Should be set to true when there is a difference between m_UnchangedEntity and the corresponding scene object group in the scene entity list.
        /// </value>
        bool DiffersFromSceneGroup = false;

        #endregion Fields

        #region Constructors

        public ContentManagementEntity(SceneObjectGroup Unchanged, bool physics)
            : base(Unchanged, false)
        {
            m_UnchangedEntity = Unchanged.Copy(false);
        }

        public ContentManagementEntity(string objectXML, Scene scene,  bool physics)
            : base(objectXML, scene, false)
        {
            m_UnchangedEntity = SceneObjectSerializer.FromXml2Format(objectXML);
        }

        #endregion Constructors

        #region Public Properties

        public SceneObjectGroup UnchangedEntity
        {
            get { return m_UnchangedEntity; }
        }

        #endregion Public Properties

        #region Private Methods

        /// <summary>
        /// Check if an entitybase list (like that returned by scene.GetEntities()) contains a group with the rootpart uuid that matches the current uuid.
        /// </summary>
        private bool ContainsKey(List<EntityBase> list, UUID uuid)
        {
            foreach (EntityBase part in list)
                if (part.UUID == uuid)
                    return true;
            return false;
        }

        private SceneObjectGroup GetGroupByUUID(System.Collections.Generic.List<EntityBase> list, UUID uuid)
        {
            foreach (EntityBase ent in list)
            {
                if (ent is SceneObjectGroup)
                    if (ent.UUID == uuid)
                        return (SceneObjectGroup)ent;
            }
            return null;
        }

        #endregion Private Methods

        #region Public Methods

        /// <summary>
        /// Search for a corresponding group UUID in the scene. If not found, then the revisioned group this CMEntity represents has been deleted. Mark the metaentity appropriately.
        /// If a matching UUID is found in a scene object group, compare the two for differences. If differences exist, Mark the metaentity appropriately.
        /// </summary>
        public void FindDifferences()
        {
            List<EntityBase> sceneEntityList = new List<EntityBase>(m_Entity.Scene.GetEntities());
            DiffersFromSceneGroup = false;
            // if group is not contained in scene's list
            if (!ContainsKey(sceneEntityList, m_UnchangedEntity.UUID))
            {
                foreach (SceneObjectPart part in m_UnchangedEntity.Parts)
                {
                    // if scene list no longer contains this part, display translucent part and mark with red aura
                    if (!ContainsKey(sceneEntityList, part.UUID))
                    {
                        // if already displaying a red aura over part, make sure its red
                        if (m_AuraEntities.ContainsKey(part.UUID))
                        {
                            m_AuraEntities[part.UUID].SetAura(new Vector3(254, 0, 0), part.Scale);
                        }
                        else
                        {
                            AuraMetaEntity auraGroup = new AuraMetaEntity(m_Entity.Scene,
                                                                          part.GetWorldPosition(),
                                                                          MetaEntity.TRANSLUCENT,
                                                                          new Vector3(254, 0, 0),
                                                                          part.Scale
                                                                          );
                            m_AuraEntities.Add(part.UUID, auraGroup);
                        }
                        SceneObjectPart metaPart = m_Entity.GetLinkNumPart(part.LinkNum);
                        SetPartTransparency(metaPart, MetaEntity.TRANSLUCENT);
                    }
                    // otherwise, scene will not contain the part. note: a group can not remove a part without changing group id
                }

                // a deleted part has no where to point a beam particle system,
                // if a metapart had a particle system (maybe it represented a moved part) remove it
                if (m_BeamEntities.ContainsKey(m_UnchangedEntity.RootPart.UUID))
                {
                    m_BeamEntities[m_UnchangedEntity.RootPart.UUID].HideFromAll();
                    m_BeamEntities.Remove(m_UnchangedEntity.RootPart.UUID);
                }

                DiffersFromSceneGroup = true;
            }
            // if scene list does contain group, compare each part in group for differences and display beams and auras appropriately
            else
            {
                MarkWithDifferences((SceneObjectGroup)GetGroupByUUID(sceneEntityList, m_UnchangedEntity.UUID));
            }
        }

        /// <summary>
        /// Check if the revisioned scene object group that this CMEntity is based off of contains a child with the given UUID.
        /// </summary>
        public bool HasChildPrim(UUID uuid)
        {
            return m_UnchangedEntity.ContainsPart(uuid);
        }

        /// <summary>
        /// Check if the revisioned scene object group that this CMEntity is based off of contains a child with the given LocalId.
        /// </summary>
        public bool HasChildPrim(uint localID)
        {
            foreach (SceneObjectPart part in m_UnchangedEntity.Parts)
                if (part.LocalId == localID)
                    return true;
            
            return false;
        }

        public override void Hide(IClientAPI client)
        {
            base.Hide(client);
            foreach (MetaEntity group in m_AuraEntities.Values)
                group.Hide(client);
            foreach (MetaEntity group in m_BeamEntities.Values)
                group.Hide(client);
        }

        public override void HideFromAll()
        {
            base.HideFromAll();
            foreach (MetaEntity group in m_AuraEntities.Values)
                group.HideFromAll();
            foreach (MetaEntity group in m_BeamEntities.Values)
                group.HideFromAll();
        }

        /// <summary>
        /// Returns true if there was a change between meta entity and the entity group, false otherwise.
        /// If true is returned, it is assumed the metaentity's appearance has changed to reflect the difference (though clients haven't been updated).
        /// </summary>
        public bool MarkWithDifferences(SceneObjectGroup sceneEntityGroup)
        {
            SceneObjectPart sceneEntityPart;
            SceneObjectPart metaEntityPart;
            Diff differences;
            bool changed = false;

            // Use "UnchangedEntity" to do comparisons because its text, transparency, and other attributes will be just as the user
            // had originally saved.
            // m_Entity will NOT necessarily be the same entity as the user had saved.
            foreach (SceneObjectPart UnchangedPart in m_UnchangedEntity.Parts)
            {
                //This is the part that we use to show changes.
                metaEntityPart = m_Entity.GetLinkNumPart(UnchangedPart.LinkNum);
                if (sceneEntityGroup.ContainsPart(UnchangedPart.UUID))
                {
                    sceneEntityPart = sceneEntityGroup.GetChildPart(UnchangedPart.UUID);
                    differences = Difference.FindDifferences(UnchangedPart, sceneEntityPart);
                    if (differences != Diff.NONE)
                        metaEntityPart.Text = "CHANGE: " + differences.ToString();
                    if (differences != 0)
                    {
                        // Root Part that has been modified
                        if ((differences & Diff.POSITION) > 0)
                        {
                            // If the position of any part has changed, make sure the RootPart of the
                            // meta entity is pointing with a beam particle system
                            if (m_BeamEntities.ContainsKey(m_UnchangedEntity.RootPart.UUID))
                            {
                                m_BeamEntities[m_UnchangedEntity.RootPart.UUID].HideFromAll();
                                m_BeamEntities.Remove(m_UnchangedEntity.RootPart.UUID);
                            }
                            BeamMetaEntity beamGroup = new BeamMetaEntity(m_Entity.Scene,
                                                                          m_UnchangedEntity.RootPart.GetWorldPosition(),
                                                                          MetaEntity.TRANSLUCENT,
                                                                          sceneEntityPart,
                                                                          new Vector3(0, 0, 254)
                                                                          );
                            m_BeamEntities.Add(m_UnchangedEntity.RootPart.UUID, beamGroup);
                        }

                        if (m_AuraEntities.ContainsKey(UnchangedPart.UUID))
                        {
                            m_AuraEntities[UnchangedPart.UUID].HideFromAll();
                            m_AuraEntities.Remove(UnchangedPart.UUID);
                        }
                        AuraMetaEntity auraGroup = new AuraMetaEntity(m_Entity.Scene,
                                                                      UnchangedPart.GetWorldPosition(),
                                                                      MetaEntity.TRANSLUCENT,
                                                                      new Vector3(0, 0, 254),
                                                                      UnchangedPart.Scale
                                                                      );
                        m_AuraEntities.Add(UnchangedPart.UUID, auraGroup);
                        SetPartTransparency(metaEntityPart, MetaEntity.TRANSLUCENT);

                        DiffersFromSceneGroup = true;
                    }
                    else // no differences between scene part and meta part
                    {
                        if (m_BeamEntities.ContainsKey(m_UnchangedEntity.RootPart.UUID))
                        {
                            m_BeamEntities[m_UnchangedEntity.RootPart.UUID].HideFromAll();
                            m_BeamEntities.Remove(m_UnchangedEntity.RootPart.UUID);
                        }
                        if (m_AuraEntities.ContainsKey(UnchangedPart.UUID))
                        {
                            m_AuraEntities[UnchangedPart.UUID].HideFromAll();
                            m_AuraEntities.Remove(UnchangedPart.UUID);
                        }
                        SetPartTransparency(metaEntityPart, MetaEntity.NONE);
                    }
                }
                else  //The entity currently in the scene is missing parts from the metaentity saved, so mark parts red as deleted.
                {
                    if (m_AuraEntities.ContainsKey(UnchangedPart.UUID))
                    {
                        m_AuraEntities[UnchangedPart.UUID].HideFromAll();
                        m_AuraEntities.Remove(UnchangedPart.UUID);
                    }
                    AuraMetaEntity auraGroup = new AuraMetaEntity(m_Entity.Scene,
                                                                  UnchangedPart.GetWorldPosition(),
                                                                  MetaEntity.TRANSLUCENT,
                                                                  new Vector3(254, 0, 0),
                                                                  UnchangedPart.Scale
                                                                  );
                    m_AuraEntities.Add(UnchangedPart.UUID, auraGroup);
                    SetPartTransparency(metaEntityPart, MetaEntity.TRANSLUCENT);

                    DiffersFromSceneGroup = true;
                }
            }
            
            return changed;
        }

        public void SendFullAuraUpdate(IClientAPI client)
        {
            if (DiffersFromSceneGroup)
            {
                foreach (AuraMetaEntity group in m_AuraEntities.Values)
                    group.SendFullUpdate(client);
            }
        }

        public void SendFullAuraUpdateToAll()
        {
            if (DiffersFromSceneGroup)
            {
                foreach (AuraMetaEntity group in m_AuraEntities.Values)
                    group.SendFullUpdateToAll();
            }
        }

        public void SendFullBeamUpdate(IClientAPI client)
        {
            if (DiffersFromSceneGroup)
            {
                foreach (BeamMetaEntity group in m_BeamEntities.Values)
                    group.SendFullUpdate(client);
            }
        }

        public void SendFullBeamUpdateToAll()
        {
            if (DiffersFromSceneGroup)
            {
                foreach (BeamMetaEntity group in m_BeamEntities.Values)
                    group.SendFullUpdateToAll();
            }
        }

        public void SendFullDiffUpdate(IClientAPI client)
        {
            FindDifferences();
            if (DiffersFromSceneGroup)
            {
                SendFullUpdate(client);
                SendFullAuraUpdate(client);
                SendFullBeamUpdate(client);
            }
        }

        public void SendFullDiffUpdateToAll()
        {
            FindDifferences();
            if (DiffersFromSceneGroup)
            {
                SendFullUpdateToAll();
                SendFullAuraUpdateToAll();
                SendFullBeamUpdateToAll();
            }
        }

        #endregion Public Methods
    }
}
