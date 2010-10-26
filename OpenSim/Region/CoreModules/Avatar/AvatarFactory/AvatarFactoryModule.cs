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
using System.Reflection;
using log4net;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Framework;

using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;

namespace OpenSim.Region.CoreModules.Avatar.AvatarFactory
{
    public class AvatarFactoryModule : IRegionModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private static readonly byte[] BAKE_INDICES = new byte[] { 8, 9, 10, 11, 19, 20 };
        private Scene m_scene = null;

        private bool m_startAnimationSet = false;

        public void Initialise(Scene scene, IConfigSource source)
        {
            scene.EventManager.OnNewClient += NewClient;

            if (m_scene == null)
                m_scene = scene;
        }

        public void PostInitialise()
        {
        }

        public void Close()
        {
        }

        public string Name
        {
            get { return "Default Avatar Factory"; }
        }

        public bool IsSharedModule
        {
            get { return false; }
        }

        public void NewClient(IClientAPI client)
        {
            client.OnRequestWearables += SendWearables;
            client.OnSetAppearance += SetAppearance;
            client.OnAvatarNowWearing += AvatarIsWearing;
        }

        public void RemoveClient(IClientAPI client)
        {
            // client.OnAvatarNowWearing -= AvatarIsWearing;
        }

        public void CheckBakedTextureAssets(IClientAPI client, UUID textureID, int idx)
        {
            if (m_scene.AssetService.Get(textureID.ToString()) == null)
            {
                m_log.WarnFormat("[AVFACTORY]: Missing baked texture {0} ({1}) for avatar {2}",textureID,idx,client.Name);
                client.SendRebakeAvatarTextures(textureID);
            }
        }
        
        /// <summary>
        /// Set appearance data (textureentry and slider settings) received from the client
        /// </summary>
        /// <param name="texture"></param>
        /// <param name="visualParam"></param>
        public void SetAppearance(IClientAPI client, Primitive.TextureEntry textureEntry, byte[] visualParams)
        {
            ScenePresence sp = m_scene.GetScenePresence(client.AgentId);
            if (sp == null)
            {
                m_log.WarnFormat("[AVFACTORY] SetAppearance unable to find presence for {0}",client.AgentId);
                return;
            }
            
// DEBUG ON
            m_log.WarnFormat("[AVFACTORY] SetAppearance for {0}",client.AgentId);
// DEBUG OFF

/*
            if (m_physicsActor != null)
            {
                if (!IsChildAgent)
                {
                    // This may seem like it's redundant, remove the avatar from the physics scene
                    // just to add it back again, but it saves us from having to update
                    // 3 variables 10 times a second.
                    bool flyingTemp = m_physicsActor.Flying;
                    RemoveFromPhysicalScene();
                    //m_scene.PhysicsScene.RemoveAvatar(m_physicsActor);

                    //PhysicsActor = null;

                    AddToPhysicalScene(flyingTemp);
                }
            }
*/
            #region Bake Cache Check

            bool changed = false;
            
            // Process the texture entry
            if (textureEntry != null)
            {
                for (int i = 0; i < BAKE_INDICES.Length; i++)
                {
                    int j = BAKE_INDICES[i];
                    Primitive.TextureEntryFace face = textureEntry.FaceTextures[j];

                    if (face != null && face.TextureID != AppearanceManager.DEFAULT_AVATAR_TEXTURE)
                        Util.FireAndForget(delegate(object o) { CheckBakedTextureAssets(client,face.TextureID,j); });
                }
                changed = sp.Appearance.SetTextureEntries(textureEntry);
             
            }
            
            #endregion Bake Cache Check
                
            changed = sp.Appearance.SetVisualParams(visualParams) || changed;

            // If nothing changed (this happens frequently) just return
            if (changed)
            {
// DEBUG ON
                m_log.Warn("[AVFACTORY] Appearance changed");
// DEBUG OFF                
                sp.Appearance.SetAppearance(textureEntry, visualParams);
                if (sp.Appearance.AvatarHeight > 0)
                    sp.SetHeight(sp.Appearance.AvatarHeight);

                m_scene.AvatarService.SetAppearance(client.AgentId, sp.Appearance);
            }
// DEBUG ON
            else
                m_log.Warn("[AVFACTORY] Appearance did not change");
// DEBUG OFF

            sp.SendAppearanceToAllOtherAgents();
            if (!m_startAnimationSet)
            {
                sp.Animator.UpdateMovementAnimations();
                m_startAnimationSet = true;
            }

            client.SendAvatarDataImmediate(sp);
            client.SendAppearance(sp.Appearance.Owner,sp.Appearance.VisualParams,sp.Appearance.Texture.GetBytes());
        }

        /// <summary>
        /// Tell the client for this scene presence what items it should be wearing now
        /// </summary>
        public void SendWearables(IClientAPI client)
        {
            ScenePresence sp = m_scene.GetScenePresence(client.AgentId);
            if (sp == null)
            {
                m_log.WarnFormat("[AVFACTORY] SendWearables unable to find presence for {0}",client.AgentId);
                return;
            }
            
// DEBUG ON
            m_log.WarnFormat("[AVFACTORY]: Received request for wearables of {0}", client.AgentId);
// DEBUG OFF            
            client.SendWearables(sp.Appearance.Wearables,sp.Appearance.Serial++);
        }

        /// <summary>
        /// Update what the avatar is wearing using an item from their inventory.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void AvatarIsWearing(IClientAPI client, AvatarWearingArgs e)
        {
            ScenePresence sp = m_scene.GetScenePresence(client.AgentId);
            if (sp == null)
            {
                m_log.WarnFormat("[AVFACTORY] AvatarIsWearing unable to find presence for {0}",client.AgentId);
                return;
            }
            
// DEBUG ON
            m_log.WarnFormat("[AVFACTORY]: AvatarIsWearing called for {0}",client.AgentId);
// DEBUG OFF

            AvatarAppearance avatAppearance = new AvatarAppearance(sp.Appearance);

            //if (!TryGetAvatarAppearance(client.AgentId, out avatAppearance)) 
            //{
            //    m_log.Warn("[AVFACTORY]: We didn't seem to find the appearance, falling back to ScenePresence");
            //    avatAppearance = sp.Appearance;
            //}
            
            //m_log.DebugFormat("[AVFACTORY]: Received wearables for {0}", client.Name);
            
            foreach (AvatarWearingArgs.Wearable wear in e.NowWearing)
            {
                if (wear.Type < AvatarWearable.MAX_WEARABLES)
                {
                    AvatarWearable newWearable = new AvatarWearable(wear.ItemID,UUID.Zero);
                    avatAppearance.SetWearable(wear.Type, newWearable);
                }
            }
            
            SetAppearanceAssets(sp.UUID, ref avatAppearance);

            m_scene.AvatarService.SetAppearance(client.AgentId, avatAppearance);
            sp.Appearance = avatAppearance;
        }

        private void SetAppearanceAssets(UUID userID, ref AvatarAppearance appearance)
        {
            IInventoryService invService = m_scene.InventoryService;

            if (invService.GetRootFolder(userID) != null)
            {
                for (int i = 0; i < AvatarWearable.MAX_WEARABLES; i++)
                {
                    if (appearance.Wearables[i].ItemID == UUID.Zero)
                    {
                        appearance.Wearables[i].AssetID = UUID.Zero;
                    }
                    else
                    {
                        InventoryItemBase baseItem = new InventoryItemBase(appearance.Wearables[i].ItemID, userID);
                        baseItem = invService.GetItem(baseItem);

                        if (baseItem != null)
                        {
                            appearance.Wearables[i].AssetID = baseItem.AssetID;
                        }
                        else
                        {
                            m_log.ErrorFormat(
                                "[AVFACTORY]: Can't find inventory item {0} for {1}, setting to default", 
                                appearance.Wearables[i].ItemID, (WearableType)i);
                            
                            appearance.Wearables[i].ItemID = UUID.Zero;
                            appearance.Wearables[i].AssetID = UUID.Zero;
                        }
                    }
                }
            }
            else
            {
                m_log.WarnFormat("[AVFACTORY]: user {0} has no inventory, appearance isn't going to work", userID);
            }
        }
    }
}
