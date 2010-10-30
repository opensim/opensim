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

using System.Threading;
using System.Timers;
using System.Collections.Generic;

using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;

namespace OpenSim.Region.CoreModules.Avatar.AvatarFactory
{
    public class AvatarFactoryModule : IAvatarFactory, IRegionModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private Scene m_scene = null;

        private int m_savetime = 5; // seconds to wait before saving changed appearance
        private int m_sendtime = 2; // seconds to wait before sending changed appearance

        private int m_checkTime = 500; // milliseconds to wait between checks for appearance updates
        private System.Timers.Timer m_updateTimer = new System.Timers.Timer();
        private Dictionary<UUID,long> m_savequeue = new Dictionary<UUID,long>();
        private Dictionary<UUID,long> m_sendqueue = new Dictionary<UUID,long>();

        #region RegionModule Members

        public void Initialise(Scene scene, IConfigSource config)
        {
            scene.RegisterModuleInterface<IAvatarFactory>(this);
            scene.EventManager.OnNewClient += NewClient;
             
            if (config != null)
            {
                IConfig sconfig = config.Configs["Startup"];
                if (sconfig != null)
                {
                    m_savetime = Convert.ToInt32(sconfig.GetString("DelayBeforeAppearanceSave",Convert.ToString(m_savetime)));
                    m_sendtime = Convert.ToInt32(sconfig.GetString("DelayBeforeAppearanceSend",Convert.ToString(m_sendtime)));
                }
            }

            if (m_scene == null)
                m_scene = scene;
        }

        public void PostInitialise()
        {
            m_updateTimer.Enabled = false;
            m_updateTimer.AutoReset = true;
            m_updateTimer.Interval = m_checkTime; // 500 milliseconds wait to start async ops
            m_updateTimer.Elapsed += new ElapsedEventHandler(HandleAppearanceUpdateTimer);
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

        #endregion

        public bool ValidateBakedTextureCache(IClientAPI client)
        {
            ScenePresence sp = m_scene.GetScenePresence(client.AgentId);
            if (sp == null)
            {
                m_log.WarnFormat("[AVATAR FACTORY MODULE]: SetAppearance unable to find presence for {0}", client.AgentId);
                return false;
            }

            bool cached = true;

            // Process the texture entry
            for (int i = 0; i < AvatarAppearance.BAKE_INDICES.Length; i++)
            {
                int idx = AvatarAppearance.BAKE_INDICES[i];
                Primitive.TextureEntryFace face = sp.Appearance.Texture.FaceTextures[idx];
                if (face != null && face.TextureID != AppearanceManager.DEFAULT_AVATAR_TEXTURE)
                    if (! CheckBakedTextureAsset(client,face.TextureID,idx))
                    {
                        sp.Appearance.Texture.FaceTextures[idx] = null;
                        cached = false;
                    }
            }

            return cached;
        }
        

        /// <summary>
        /// Set appearance data (textureentry and slider settings) received from the client
        /// </summary>
        /// <param name="texture"></param>
        /// <param name="visualParam"></param>
        public void SetAppearance(IClientAPI client, Primitive.TextureEntry textureEntry, byte[] visualParams)
        {
//            m_log.WarnFormat("[AVATAR FACTORY MODULE]: SetAppearance for {0}",client.AgentId);

            ScenePresence sp = m_scene.GetScenePresence(client.AgentId);
            if (sp == null)
            {
                m_log.WarnFormat("[AVATAR FACTORY MODULE]: SetAppearance unable to find presence for {0}",client.AgentId);
                return;
            }
            
            bool changed = false;
            
            // Process the texture entry
            if (textureEntry != null)
            {
                changed = sp.Appearance.SetTextureEntries(textureEntry);

                for (int i = 0; i < AvatarAppearance.BAKE_INDICES.Length; i++)
                {
                    int idx = AvatarAppearance.BAKE_INDICES[i];
                    Primitive.TextureEntryFace face = sp.Appearance.Texture.FaceTextures[idx];
                    if (face != null && face.TextureID != AppearanceManager.DEFAULT_AVATAR_TEXTURE)
                        Util.FireAndForget(delegate(object o) {
                                if (! CheckBakedTextureAsset(client,face.TextureID,idx))
                                    client.SendRebakeAvatarTextures(face.TextureID);
                            });
                }
            }
            
            // Process the visual params, this may change height as well
            if (visualParams != null)
            {
                if (sp.Appearance.SetVisualParams(visualParams))
                {
                    changed = true;
                    if (sp.Appearance.AvatarHeight > 0)
                        sp.SetHeight(sp.Appearance.AvatarHeight);
                }
            }
            
            // If something changed in the appearance then queue an appearance save
            if (changed)
                QueueAppearanceSave(client.AgentId);

            // And always queue up an appearance update to send out
            QueueAppearanceSend(client.AgentId);
            
            // Send the appearance back to the avatar
            // AvatarAppearance avp = sp.Appearance;
            // sp.ControllingClient.SendAvatarDataImmediate(sp);
            // sp.ControllingClient.SendAppearance(avp.Owner,avp.VisualParams,avp.Texture.GetBytes());
        }

        /// <summary>
        /// Checks for the existance of a baked texture asset and
        /// requests the viewer rebake if the asset is not found
        /// </summary>
        /// <param name="client"></param>
        /// <param name="textureID"></param>
        /// <param name="idx"></param>
        private bool CheckBakedTextureAsset(IClientAPI client, UUID textureID, int idx)
        {
            if (m_scene.AssetService.Get(textureID.ToString()) == null)
            {
                m_log.WarnFormat("[AVATAR FACTORY MODULE]: Missing baked texture {0} ({1}) for avatar {2}",
                                 textureID, idx, client.Name);
                return false;
            }
            return true;
        }
        
        #region UpdateAppearanceTimer

        public void QueueAppearanceSend(UUID agentid)
        {
//            m_log.WarnFormat("[AVATAR FACTORY MODULE]: Queue appearance send for {0}",agentid);              

            // 100 nanoseconds (ticks) we should wait
            long timestamp = DateTime.Now.Ticks + Convert.ToInt64(m_sendtime * 10000000); 
            lock (m_sendqueue)
            {
                m_sendqueue[agentid] = timestamp;
                m_updateTimer.Start();
            }
        }
        
        public void QueueAppearanceSave(UUID agentid)
        {
//            m_log.WarnFormat("[AVATAR FACTORY MODULE]: Queue appearance save for {0}",agentid);             

            // 100 nanoseconds (ticks) we should wait
            long timestamp = DateTime.Now.Ticks + Convert.ToInt64(m_savetime * 10000000); 
            lock (m_savequeue)
            {
                m_savequeue[agentid] = timestamp;
                m_updateTimer.Start();
            }
        }
        
        private void HandleAppearanceSend(UUID agentid)
        {
            ScenePresence sp = m_scene.GetScenePresence(agentid);
            if (sp == null)
            {
                m_log.WarnFormat("[AVATAR FACTORY MODULE]: Agent {0} no longer in the scene", agentid);
                return;
            }

//            m_log.WarnFormat("[AVATAR FACTORY MODULE]: Handle appearance send for {0}", agentid);             

            // Send the appearance to everyone in the scene
            sp.SendAppearanceToAllOtherAgents();
            sp.ControllingClient.SendAvatarDataImmediate(sp);

            // Send the appearance back to the avatar
            // AvatarAppearance avp = sp.Appearance;
            // sp.ControllingClient.SendAppearance(avp.Owner,avp.VisualParams,avp.Texture.GetBytes());

/*
//  this needs to be fixed, the flag should be on scene presence not the region module
            // Start the animations if necessary
            if (!m_startAnimationSet)
            {
                sp.Animator.UpdateMovementAnimations();
                m_startAnimationSet = true;
            }
*/
        }

        private void HandleAppearanceSave(UUID agentid)
        {
            ScenePresence sp = m_scene.GetScenePresence(agentid);
            if (sp == null)
            {
                m_log.WarnFormat("[AVATAR FACTORY MODULE]: Agent {0} no longer in the scene", agentid);
                return;
            }

            m_scene.AvatarService.SetAppearance(agentid, sp.Appearance);
        }

        private void HandleAppearanceUpdateTimer(object sender, EventArgs ea)
        {
            long now = DateTime.Now.Ticks;
            
            lock (m_sendqueue)
            {
                Dictionary<UUID,long> sends = new Dictionary<UUID,long>(m_sendqueue);
                foreach (KeyValuePair<UUID,long> kvp in sends)
                {
                    if (kvp.Value < now)
                    {
                        Util.FireAndForget(delegate(object o) { HandleAppearanceSend(kvp.Key); });
                        m_sendqueue.Remove(kvp.Key);
                    }
                }
            }

            lock (m_savequeue)
            {
                Dictionary<UUID,long> saves = new Dictionary<UUID,long>(m_savequeue);
                foreach (KeyValuePair<UUID,long> kvp in saves)
                {
                    if (kvp.Value < now)
                    {
                        Util.FireAndForget(delegate(object o) { HandleAppearanceSave(kvp.Key); });
                        m_savequeue.Remove(kvp.Key);
                    }
                }
            }

            if (m_savequeue.Count == 0 && m_sendqueue.Count == 0)
                m_updateTimer.Stop();
        }
        
        #endregion

        /// <summary>
        /// Tell the client for this scene presence what items it should be wearing now
        /// </summary>
        public void SendWearables(IClientAPI client)
        {
            ScenePresence sp = m_scene.GetScenePresence(client.AgentId);
            if (sp == null)
            {
                m_log.WarnFormat("[AVATAR FACTORY MODULE]: SendWearables unable to find presence for {0}", client.AgentId);
                return;
            }

//            m_log.WarnFormat("[AVATAR FACTORY MODULE]: Received request for wearables of {0}", client.AgentId);
           
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
                m_log.WarnFormat("[AVATAR FACTORY MODULE]: AvatarIsWearing unable to find presence for {0}", client.AgentId);
                return;
            }
            
//            m_log.WarnFormat("[AVATAR FACTORY MODULE]: AvatarIsWearing called for {0}",client.AgentId);

            AvatarAppearance avatAppearance = new AvatarAppearance(sp.Appearance);

            foreach (AvatarWearingArgs.Wearable wear in e.NowWearing)
            {
                if (wear.Type < AvatarWearable.MAX_WEARABLES)
                {
                    AvatarWearable newWearable = new AvatarWearable(wear.ItemID,UUID.Zero);
                    avatAppearance.SetWearable(wear.Type, newWearable);
                }
            }
            
            // This could take awhile since it needs to pull inventory
            SetAppearanceAssets(sp.UUID, ref avatAppearance);

            sp.Appearance = avatAppearance;
            m_scene.AvatarService.SetAppearance(client.AgentId, sp.Appearance);
        }

        private void SetAppearanceAssets(UUID userID, ref AvatarAppearance appearance)
        {
            IInventoryService invService = m_scene.InventoryService;

            if (invService.GetRootFolder(userID) != null)
            {
                for (int i = 0; i < AvatarWearable.MAX_WEARABLES; i++)
                {
                    for (int j = 0 ; j < appearance.Wearables[j].Count ; j ++ )
                    {
                        InventoryItemBase baseItem = new InventoryItemBase(appearance.Wearables[i][j].ItemID, userID);
                        baseItem = invService.GetItem(baseItem);

                        if (baseItem != null)
                        {
                            appearance.Wearables[i].Add(appearance.Wearables[i][j].ItemID, baseItem.AssetID);
                        }
                        else
                        {
                            m_log.ErrorFormat(
                                "[AVATAR FACTORY MODULE]: Can't find inventory item {0} for {1}, setting to default", 
                                appearance.Wearables[i][j].ItemID, (WearableType)i);
                            
                            appearance.Wearables[i].RemoveItem(appearance.Wearables[i][j].ItemID);
                        }
                    }
                }
            }
            else
            {
                m_log.WarnFormat("[AVATAR FACTORY MODULE]: user {0} has no inventory, appearance isn't going to work", userID);
            }
        }
    }
}
