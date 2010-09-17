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

// CMView.cs created with MonoDevelop
// User: bongiojp at 11:57 AM 7/3/2008
//
// To change standard headers go to Edit->Preferences->Coding->Standard Headers
//

#endregion Header

using System;
using System.Collections;
using System.Collections.Generic;

using OpenMetaverse;

using OpenSim;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Physics.Manager;

using log4net;

namespace OpenSim.Region.OptionalModules.ContentManagement
{
    public class CMView
    {
        #region Static Fields

        private static readonly log4net.ILog m_log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        #endregion Static Fields

        #region Fields

        CMModel m_model = null;

        #endregion Fields

        #region Constructors

        public CMView()
        {
        }

        #endregion Constructors

        #region Public Methods

        // Auras To
        public void DisplayAuras(CMEntityCollection auraCollection)
        {
            foreach (Object ent in auraCollection.Auras.Values)
                ((AuraMetaEntity)ent).SendFullUpdateToAll();
        }

        // Auras To Client
        public void DisplayAuras(CMEntityCollection auraCollection, IClientAPI client)
        {
            foreach (Object ent in auraCollection.Auras.Values)
                ((AuraMetaEntity)ent).SendFullUpdate(client);
        }

        // Auras from List To ALL
        public void DisplayAuras(ArrayList list)
        {
            foreach (Object ent in list)
            {
                m_log.Debug("[CONTENT MANAGEMENT] displaying new aura riiiiiiiiiiiight NOW");
                ((AuraMetaEntity)ent).SendFullUpdateToAll();
            }
        }

        // Entities to ALL
        public void DisplayEntities(CMEntityCollection entityCollection)
        {
            foreach (Object ent in entityCollection.Entities.Values)
                ((ContentManagementEntity)ent).SendFullDiffUpdateToAll();
        }

        // Entities to Client
        public void DisplayEntities(CMEntityCollection entityCollection, IClientAPI client)
        {
            foreach (Object ent in entityCollection.Entities.Values)
                ((ContentManagementEntity)ent).SendFullDiffUpdate(client);
        }

        // Entities from List to ALL
        public void DisplayEntities(ArrayList list)
        {
            foreach (Object ent in list)
                ((ContentManagementEntity)ent).SendFullDiffUpdateToAll();
        }

        // Entity to ALL
        public void DisplayEntity(ContentManagementEntity ent)
        {
            ent.SendFullDiffUpdateToAll();
        }

        public void DisplayHelpMenu(Scene scene)
        {
            string menu = "Menu:\n";
            menu += "commit (ci) - saves current state of the region to a database on the server\n";
            menu += "diff-mode (dm) - displays those aspects of region that have not been saved but changed since the very last revision. Will dynamically update as you change environment.\n";
            SendSimChatMessage(scene, menu);
        }

        public void DisplayMetaEntity(UUID uuid)
        {
            ContentManagementEntity group = m_model.GetMetaGroupByPrim(uuid);
            if (group != null)
                group.SendFullDiffUpdateToAll();
        }

        /// <summary>
        /// update all clients of red/green/blue auras and meta entities that the model knows about.
        /// </summary>
        public void DisplayRecentChanges()
        {
            m_log.Debug("[CONTENT MANAGEMENT] Sending update to clients for " + m_model.MetaEntityCollection.Entities.Count + " objects.");
            DisplayEntities(m_model.MetaEntityCollection);
            DisplayAuras(m_model.MetaEntityCollection);
        }

        public void Hide(ContentManagementEntity ent)
        {
            ent.HideFromAll();
        }

        public void HideAllAuras()
        {
            foreach (Object obj in m_model.MetaEntityCollection.Auras.Values)
                ((MetaEntity)obj).HideFromAll();
        }

        public void HideAllMetaEntities()
        {
            foreach (Object obj in m_model.MetaEntityCollection.Entities.Values)
                ((ContentManagementEntity)obj).HideFromAll();
        }

        public void Initialise(CMModel model)
        {
            m_model = model;
        }

        /// <summary>
        /// Figures out if the part deleted was a new scene object part or a revisioned part that's been deleted.
        /// If it's a new scene object, any green aura attached to it is deleted.
        /// If a revisioned part is deleted, a new full update is sent to the environment of the meta entity, which will
        /// figure out that there should be a red aura and not a blue aura/beam.
        /// </summary>
        public void RemoveOrUpdateDeletedEntity(SceneObjectGroup group)
        {
            // Deal with revisioned parts that have been deleted.
            if (m_model.MetaEntityCollection.Entities.ContainsKey(group.UUID))
                ((ContentManagementEntity)m_model.MetaEntityCollection.Entities[group.UUID]).SendFullDiffUpdateToAll();

            // Deal with new parts not revisioned that have been deleted.
            foreach (SceneObjectPart part in group.Parts)
                if (m_model.MetaEntityCollection.Auras.ContainsKey(part.UUID))
                    ((AuraMetaEntity)m_model.MetaEntityCollection.Auras[part.UUID]).HideFromAll();
        }

        public void SendMetaEntitiesToNewClient(IClientAPI client)
        {
        }

        public void SendSimChatMessage(Scene scene, string message)
        {
            scene.SimChat(Utils.StringToBytes(message),
                          ChatTypeEnum.Broadcast, 0, new Vector3(0,0,0), "Content Manager", UUID.Zero, false);
        }

        #endregion Public Methods
    }
}
