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

        private object m_setAppearanceLock = new object();

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
                    // m_log.InfoFormat("[AVFACTORY] configured for {0} save and {1} send",m_savetime,m_sendtime);
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
            client.OnSetAppearance += SetAppearanceFromClient;
            client.OnAvatarNowWearing += AvatarIsWearing;
        }

        public void RemoveClient(IClientAPI client)
        {
            // client.OnAvatarNowWearing -= AvatarIsWearing;
        }

        #endregion

        /// <summary>
        /// Check for the existence of the baked texture assets.
        /// </summary>
        /// <param name="client"></param>
        public bool ValidateBakedTextureCache(IClientAPI client)
        {
            return ValidateBakedTextureCache(client, true);
        }

        /// <summary>
        /// Check for the existence of the baked texture assets. Request a rebake
        /// unless checkonly is true.
        /// </summary>
        /// <param name="client"></param>
        /// <param name="checkonly"></param>
        private bool ValidateBakedTextureCache(IClientAPI client, bool checkonly)
        {
            ScenePresence sp = m_scene.GetScenePresence(client.AgentId);
            if (sp == null)
            {
                m_log.WarnFormat("[AVFACTORY]: SetAppearance unable to find presence for {0}", client.AgentId);
                return false;
            }

            bool defonly = true; // are we only using default textures

            // Process the texture entry
            for (int i = 0; i < AvatarAppearance.BAKE_INDICES.Length; i++)
            {
                int idx = AvatarAppearance.BAKE_INDICES[i];
                Primitive.TextureEntryFace face = sp.Appearance.Texture.FaceTextures[idx];

                // if there is no texture entry, skip it
                if (face == null)
                    continue;

//                m_log.DebugFormat(
//                    "[AVFACTORY]: Looking for texture {0}, id {1} for {2} {3}",
//                    face.TextureID, idx, client.Name, client.AgentId);

                // if the texture is one of the "defaults" then skip it
                // this should probably be more intelligent (skirt texture doesnt matter
                // if the avatar isnt wearing a skirt) but if any of the main baked 
                // textures is default then the rest should be as well
                if (face.TextureID == UUID.Zero || face.TextureID == AppearanceManager.DEFAULT_AVATAR_TEXTURE)
                    continue;
                
                defonly = false; // found a non-default texture reference

                if (!CheckBakedTextureAsset(client, face.TextureID, idx))
                {
                    // the asset didn't exist if we are only checking, then we found a bad
                    // one and we're done otherwise, ask for a rebake
                    if (checkonly)
                        return false;

                    m_log.InfoFormat("[AVFACTORY]: missing baked texture {0}, requesting rebake", face.TextureID);
                    
                    client.SendRebakeAvatarTextures(face.TextureID);
                }
            }

            m_log.DebugFormat("[AVFACTORY]: Completed texture check for {0}", client.AgentId);

            // If we only found default textures, then the appearance is not cached
            return (defonly ? false : true);
        }

        /// <summary>
        /// Set appearance data (texture asset IDs and slider settings) received from the client
        /// </summary>
        /// <param name="client"></param>
        /// <param name="texture"></param>
        /// <param name="visualParam"></param>
        public void SetAppearanceFromClient(IClientAPI client, Primitive.TextureEntry textureEntry, byte[] visualParams)
        {
            ScenePresence sp = m_scene.GetScenePresence(client.AgentId);
            if (sp == null)
            {
                m_log.WarnFormat("[AVFACTORY]: SetAppearance unable to find presence for {0}", client.AgentId);
                return;
            }

//            m_log.InfoFormat("[AVFACTORY]: start SetAppearance for {0}", client.AgentId);

            // TODO: This is probably not necessary any longer, just assume the
            // textureEntry set implies that the appearance transaction is complete
            bool changed = false;

            // Process the texture entry transactionally, this doesn't guarantee that Appearance is
            // going to be handled correctly but it does serialize the updates to the appearance
            lock (m_setAppearanceLock)
            {
                // Process the visual params, this may change height as well
                if (visualParams != null)
                {
                    float oldHeight = sp.Appearance.AvatarHeight;
                    changed = sp.Appearance.SetVisualParams(visualParams);

                    if (sp.Appearance.AvatarHeight != oldHeight && sp.Appearance.AvatarHeight > 0)
                        sp.SetHeight(sp.Appearance.AvatarHeight);
                }

                // Process the baked texture array
                if (textureEntry != null)
                {
                    changed = sp.Appearance.SetTextureEntries(textureEntry) || changed;

                    m_log.InfoFormat("[AVFACTORY]: received texture update for {0}", client.AgentId);
                    Util.FireAndForget(delegate(object o) { ValidateBakedTextureCache(client, false); });

                    // This appears to be set only in the final stage of the appearance
                    // update transaction. In theory, we should be able to do an immediate
                    // appearance send and save here.

                }
				// save only if there were changes, send no matter what (doesn't hurt to send twice)
				if (changed)
					QueueAppearanceSave(client.AgentId);

				QueueAppearanceSend(client.AgentId);
            }

            // m_log.WarnFormat("[AVFACTORY]: complete SetAppearance for {0}:\n{1}",client.AgentId,sp.Appearance.ToString());
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
                m_log.WarnFormat("[AVFACTORY]: Missing baked texture {0} ({1}) for avatar {2}",
                                 textureID, idx, client.Name);
                return false;
            }
            return true;
        }

        public Dictionary<BakeType, Primitive.TextureEntryFace> GetBakedTextureFaces(UUID agentId)
        {
            ScenePresence sp = m_scene.GetScenePresence(agentId);

            if (sp == null)
                return new Dictionary<BakeType, Primitive.TextureEntryFace>();

            return GetBakedTextureFaces(sp);
        }

        private Dictionary<BakeType, Primitive.TextureEntryFace> GetBakedTextureFaces(ScenePresence sp)
        {
            if (sp.IsChildAgent)
                return new Dictionary<BakeType, Primitive.TextureEntryFace>();

            Dictionary<BakeType, Primitive.TextureEntryFace> bakedTextures
                = new Dictionary<BakeType, Primitive.TextureEntryFace>();

            AvatarAppearance appearance = sp.Appearance;
            Primitive.TextureEntryFace[] faceTextures = appearance.Texture.FaceTextures;

            foreach (int i in Enum.GetValues(typeof(BakeType)))
            {
                BakeType bakeType = (BakeType)i;

                if (bakeType == BakeType.Unknown)
                    continue;

//                m_log.DebugFormat(
//                    "[AVFACTORY]: NPC avatar {0} has texture id {1} : {2}",
//                    acd.AgentID, i, acd.Appearance.Texture.FaceTextures[i]);

                int ftIndex = (int)AppearanceManager.BakeTypeToAgentTextureIndex(bakeType);
                bakedTextures[bakeType] = faceTextures[ftIndex];
            }

            return bakedTextures;
        }

        public bool SaveBakedTextures(UUID agentId)
        {
            ScenePresence sp = m_scene.GetScenePresence(agentId);

            if (sp == null)
                return false;

            m_log.DebugFormat(
                "[AV FACTORY]: Permanently saving baked textures for {0} in {1}",
                sp.Name, m_scene.RegionInfo.RegionName);

            Dictionary<BakeType, Primitive.TextureEntryFace> bakedTextures = GetBakedTextureFaces(sp);

            if (bakedTextures.Count == 0)
                return false;

            foreach (BakeType bakeType in bakedTextures.Keys)
            {
                Primitive.TextureEntryFace bakedTextureFace = bakedTextures[bakeType];

                if (bakedTextureFace == null)
                {
                    m_log.WarnFormat(
                        "[AV FACTORY]: No texture ID set for {0} for {1} in {2} not found when trying to save permanently",
                        bakeType, sp.Name, m_scene.RegionInfo.RegionName);

                    continue;
                }

                AssetBase asset = m_scene.AssetService.Get(bakedTextureFace.TextureID.ToString());

                if (asset != null)
                {
                    asset.Temporary = false;
                    asset.Local = false;
                    m_scene.AssetService.Store(asset);
                }
                else
                {
                    m_log.WarnFormat(
                        "[AV FACTORY]: Baked texture id {0} not found for bake {1} for avatar {2} in {3} when trying to save permanently",
                        bakedTextureFace.TextureID, bakeType, sp.Name, m_scene.RegionInfo.RegionName);
                }
            }

//            for (int i = 0; i < faceTextures.Length; i++)
//            {
////                m_log.DebugFormat(
////                    "[AVFACTORY]: NPC avatar {0} has texture id {1} : {2}",
////                    acd.AgentID, i, acd.Appearance.Texture.FaceTextures[i]);
//
//                if (faceTextures[i] == null)
//                    continue;
//
//                AssetBase asset = m_scene.AssetService.Get(faceTextures[i].TextureID.ToString());
//
//                if (asset != null)
//                {
//                    asset.Temporary = false;
//                    m_scene.AssetService.Store(asset);
//                }
//                else
//                {
//                    m_log.WarnFormat(
//                        "[AV FACTORY]: Baked texture {0} for {1} in {2} not found when trying to save permanently",
//                        faceTextures[i].TextureID, sp.Name, m_scene.RegionInfo.RegionName);
//                }
//            }

            return true;
        }

        #region UpdateAppearanceTimer

        /// <summary>
        /// Queue up a request to send appearance, makes it possible to
        /// accumulate changes without sending out each one separately.
        /// </summary>
        public void QueueAppearanceSend(UUID agentid)
        {
            // m_log.WarnFormat("[AVFACTORY]: Queue appearance send for {0}", agentid);

            // 10000 ticks per millisecond, 1000 milliseconds per second
            long timestamp = DateTime.Now.Ticks + Convert.ToInt64(m_sendtime * 1000 * 10000);
            lock (m_sendqueue)
            {
                m_sendqueue[agentid] = timestamp;
                m_updateTimer.Start();
            }
        }

        public void QueueAppearanceSave(UUID agentid)
        {
            // m_log.WarnFormat("[AVFACTORY]: Queue appearance save for {0}", agentid);

            // 10000 ticks per millisecond, 1000 milliseconds per second
            long timestamp = DateTime.Now.Ticks + Convert.ToInt64(m_savetime * 1000 * 10000);
            lock (m_savequeue)
            {
                m_savequeue[agentid] = timestamp;
                m_updateTimer.Start();
            }
        }

        private void SaveAppearance(UUID agentid)
        {
            // We must set appearance parameters in the en_US culture in order to avoid issues where values are saved
            // in a culture where decimal points are commas and then reloaded in a culture which just treats them as
            // number seperators.
            Culture.SetCurrentCulture();

            ScenePresence sp = m_scene.GetScenePresence(agentid);
            if (sp == null)
            {
                m_log.WarnFormat("[AVFACTORY]: Agent {0} no longer in the scene", agentid);
                return;
            }

            // m_log.WarnFormat("[AVFACTORY] avatar {0} save appearance",agentid);

            m_scene.AvatarService.SetAppearance(agentid, sp.Appearance);
        }

        private void HandleAppearanceUpdateTimer(object sender, EventArgs ea)
        {
            long now = DateTime.Now.Ticks;

            lock (m_sendqueue)
            {
                Dictionary<UUID, long> sends = new Dictionary<UUID, long>(m_sendqueue);
                foreach (KeyValuePair<UUID, long> kvp in sends)
                {
                    if (kvp.Value < now)
                    {
                        Util.FireAndForget(delegate(object o) { SendAppearance(kvp.Key); });
                        m_sendqueue.Remove(kvp.Key);
                    }
                }
            }

            lock (m_savequeue)
            {
                Dictionary<UUID, long> saves = new Dictionary<UUID, long>(m_savequeue);
                foreach (KeyValuePair<UUID, long> kvp in saves)
                {
                    if (kvp.Value < now)
                    {
                        Util.FireAndForget(delegate(object o) { SaveAppearance(kvp.Key); });
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
                m_log.WarnFormat("[AVFACTORY]: SendWearables unable to find presence for {0}", client.AgentId);
                return;
            }

//            m_log.DebugFormat("[AVFACTORY]: Received request for wearables of {0}", client.Name);

            client.SendWearables(sp.Appearance.Wearables, sp.Appearance.Serial++);
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
                m_log.WarnFormat("[AVFACTORY]: AvatarIsWearing unable to find presence for {0}", client.AgentId);
                return;
            }

            // m_log.WarnFormat("[AVFACTORY]: AvatarIsWearing called for {0}", client.AgentId);

            // we need to clean out the existing textures
            sp.Appearance.ResetAppearance();

            // operate on a copy of the appearance so we don't have to lock anything yet
            AvatarAppearance avatAppearance = new AvatarAppearance(sp.Appearance, false);

            foreach (AvatarWearingArgs.Wearable wear in e.NowWearing)
            {
                if (wear.Type < AvatarWearable.MAX_WEARABLES)
                    avatAppearance.Wearables[wear.Type].Add(wear.ItemID, UUID.Zero);
            }

            avatAppearance.GetAssetsFrom(sp.Appearance);

            // This could take awhile since it needs to pull inventory
            SetAppearanceAssets(sp.UUID, ref avatAppearance);

            lock (m_setAppearanceLock)
            {
                // Update only those fields that we have changed. This is important because the viewer
                // often sends AvatarIsWearing and SetAppearance packets at once, and AvatarIsWearing
                // shouldn't overwrite the changes made in SetAppearance.
                sp.Appearance.Wearables = avatAppearance.Wearables;
                sp.Appearance.Texture = avatAppearance.Texture;

                // We don't need to send the appearance here since the "iswearing" will trigger a new set
                // of visual param and baked texture changes. When those complete, the new appearance will be sent

                QueueAppearanceSave(client.AgentId);
            }
        }

        public bool SendAppearance(UUID agentId)
        {
            ScenePresence sp = m_scene.GetScenePresence(agentId);
            if (sp == null)
            {
                m_log.WarnFormat("[AVFACTORY]: Agent {0} no longer in the scene", agentId);
                return false;
            }

            // Send the appearance to everyone in the scene
            sp.SendAppearanceToAllOtherAgents();

            // Send animations back to the avatar as well
            sp.Animator.SendAnimPack();

            return true;
        }

        private void SetAppearanceAssets(UUID userID, ref AvatarAppearance appearance)
        {
            IInventoryService invService = m_scene.InventoryService;

            if (invService.GetRootFolder(userID) != null)
            {
                for (int i = 0; i < AvatarWearable.MAX_WEARABLES; i++)
                {
                    for (int j = 0; j < appearance.Wearables[j].Count; j++)
                    {
                        if (appearance.Wearables[i][j].ItemID == UUID.Zero)
                            continue;

                        // Ignore ruth's assets
                        if (appearance.Wearables[i][j].ItemID == AvatarWearable.DefaultWearables[i][0].ItemID)
                            continue;
                        InventoryItemBase baseItem = new InventoryItemBase(appearance.Wearables[i][j].ItemID, userID);
                        baseItem = invService.GetItem(baseItem);

                        if (baseItem != null)
                        {
                            appearance.Wearables[i].Add(appearance.Wearables[i][j].ItemID, baseItem.AssetID);
                        }
                        else
                        {
                            m_log.ErrorFormat(
                                "[AVFACTORY]: Can't find inventory item {0} for {1}, setting to default",
                                appearance.Wearables[i][j].ItemID, (WearableType)i);

                            appearance.Wearables[i].RemoveItem(appearance.Wearables[i][j].ItemID);
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
