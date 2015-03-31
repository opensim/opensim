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
using System.Threading;
using System.Text;
using System.Timers;
using log4net;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;

using Mono.Addins;
using PermissionMask = OpenSim.Framework.PermissionMask;

namespace OpenSim.Region.CoreModules.Avatar.AvatarFactory
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "AvatarFactoryModule")]
    public class AvatarFactoryModule : IAvatarFactoryModule, INonSharedRegionModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public const string BAKED_TEXTURES_REPORT_FORMAT = "{0,-9}  {1}";

        private Scene m_scene = null;

        private int m_savetime = 5; // seconds to wait before saving changed appearance
        private int m_sendtime = 2; // seconds to wait before sending changed appearance
        private bool m_reusetextures = false;

        private int m_checkTime = 500; // milliseconds to wait between checks for appearance updates
        private System.Timers.Timer m_updateTimer = new System.Timers.Timer();
        private Dictionary<UUID,long> m_savequeue = new Dictionary<UUID,long>();
        private Dictionary<UUID,long> m_sendqueue = new Dictionary<UUID,long>();

        private object m_setAppearanceLock = new object();

        #region Region Module interface

        public void Initialise(IConfigSource config)
        {

            IConfig appearanceConfig = config.Configs["Appearance"];
            if (appearanceConfig != null)
            {
                m_savetime = Convert.ToInt32(appearanceConfig.GetString("DelayBeforeAppearanceSave",Convert.ToString(m_savetime)));
                m_sendtime = Convert.ToInt32(appearanceConfig.GetString("DelayBeforeAppearanceSend",Convert.ToString(m_sendtime)));
                m_reusetextures = appearanceConfig.GetBoolean("ReuseTextures",m_reusetextures);
                
                // m_log.InfoFormat("[AVFACTORY] configured for {0} save and {1} send",m_savetime,m_sendtime);
            }

        }

        public void AddRegion(Scene scene)
        {
            if (m_scene == null)
                m_scene = scene;

            scene.RegisterModuleInterface<IAvatarFactoryModule>(this);
            scene.EventManager.OnNewClient += SubscribeToClientEvents;
        }

        public void RemoveRegion(Scene scene)
        {
            if (scene == m_scene)
            {
                scene.UnregisterModuleInterface<IAvatarFactoryModule>(this);
                scene.EventManager.OnNewClient -= SubscribeToClientEvents;
            }

            m_scene = null;
        }

        public void RegionLoaded(Scene scene)
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

        public Type ReplaceableInterface
        {
            get { return null; }
        }


        private void SubscribeToClientEvents(IClientAPI client)
        {
            client.OnRequestWearables += Client_OnRequestWearables;
            client.OnSetAppearance += Client_OnSetAppearance;
            client.OnAvatarNowWearing += Client_OnAvatarNowWearing;
            client.OnCachedTextureRequest += Client_OnCachedTextureRequest;
        }

        #endregion

        #region IAvatarFactoryModule

        /// </summary>
        /// <param name="sp"></param>
        /// <param name="texture"></param>
        /// <param name="visualParam"></param>
        public void SetAppearance(IScenePresence sp, AvatarAppearance appearance, WearableCacheItem[] cacheItems)
        {
            SetAppearance(sp, appearance.Texture, appearance.VisualParams, cacheItems);
        }


        public void SetAppearance(IScenePresence sp, Primitive.TextureEntry textureEntry, byte[] visualParams, Vector3 avSize, WearableCacheItem[] cacheItems)
        {
            float oldoff = sp.Appearance.AvatarFeetOffset;
            Vector3 oldbox = sp.Appearance.AvatarBoxSize;

            SetAppearance(sp, textureEntry, visualParams, cacheItems);
            sp.Appearance.SetSize(avSize);

            float off = sp.Appearance.AvatarFeetOffset;
            Vector3 box = sp.Appearance.AvatarBoxSize;
            if (oldoff != off || oldbox != box)
                ((ScenePresence)sp).SetSize(box, off);
        }

        /// <summary>
        /// Set appearance data (texture asset IDs and slider settings) 
        /// </summary>
        /// <param name="sp"></param>
        /// <param name="texture"></param>
        /// <param name="visualParam"></param>
        public void SetAppearance(IScenePresence sp, Primitive.TextureEntry textureEntry, byte[] visualParams, WearableCacheItem[] cacheItems)
        {
//            m_log.DebugFormat(
//                "[AVFACTORY]: start SetAppearance for {0}, te {1}, visualParams {2}",
//                sp.Name, textureEntry, visualParams);

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
                    //                    string[] visualParamsStrings = new string[visualParams.Length];
                    //                    for (int i = 0; i < visualParams.Length; i++)
                    //                        visualParamsStrings[i] = visualParams[i].ToString();
                    //                    m_log.DebugFormat(
                    //                        "[AVFACTORY]: Setting visual params for {0} to {1}",
                    //                        client.Name, string.Join(", ", visualParamsStrings));
/*
                    float oldHeight = sp.Appearance.AvatarHeight;
                    changed = sp.Appearance.SetVisualParams(visualParams);

                    if (sp.Appearance.AvatarHeight != oldHeight && sp.Appearance.AvatarHeight > 0)
                        ((ScenePresence)sp).SetHeight(sp.Appearance.AvatarHeight);
 */
//                    float oldoff = sp.Appearance.AvatarFeetOffset;
//                    Vector3 oldbox = sp.Appearance.AvatarBoxSize;
                    changed = sp.Appearance.SetVisualParams(visualParams);
//                    float off = sp.Appearance.AvatarFeetOffset;
//                    Vector3 box = sp.Appearance.AvatarBoxSize;
//                    if(oldoff != off || oldbox != box)
//                        ((ScenePresence)sp).SetSize(box,off);

                }
            
                // Process the baked texture array
                if (textureEntry != null)
                {
                    m_log.DebugFormat("[AVFACTORY]: Received texture update for {0} {1}", sp.Name, sp.UUID);

//                    WriteBakedTexturesReport(sp, m_log.DebugFormat);

                    changed = sp.Appearance.SetTextureEntries(textureEntry) || changed;

//                    WriteBakedTexturesReport(sp, m_log.DebugFormat);

                    // If bake textures are missing and this is not an NPC, request a rebake from client
                    if (!ValidateBakedTextureCache(sp) && (((ScenePresence)sp).PresenceType != PresenceType.Npc))
                        RequestRebake(sp, true);

                    // This appears to be set only in the final stage of the appearance
                    // update transaction. In theory, we should be able to do an immediate
                    // appearance send and save here.
                }

                // NPC should send to clients immediately and skip saving appearance
                if (((ScenePresence)sp).PresenceType == PresenceType.Npc)
                {
                    SendAppearance((ScenePresence)sp);
                    return;
                }

                // save only if there were changes, send no matter what (doesn't hurt to send twice)
                if (changed)
                    QueueAppearanceSave(sp.ControllingClient.AgentId);

                QueueAppearanceSend(sp.ControllingClient.AgentId);
            }

            // m_log.WarnFormat("[AVFACTORY]: complete SetAppearance for {0}:\n{1}",client.AgentId,sp.Appearance.ToString());
        }

        private void SendAppearance(ScenePresence sp)
        {
            // Send the appearance to everyone in the scene
            sp.SendAppearanceToAllOtherClients();

            // Send animations back to the avatar as well
            sp.Animator.SendAnimPack();
        }

        public bool SendAppearance(UUID agentId)
        {
//            m_log.DebugFormat("[AVFACTORY]: Sending appearance for {0}", agentId);

            ScenePresence sp = m_scene.GetScenePresence(agentId);
            if (sp == null)
            {
                // This is expected if the user has gone away.
//                m_log.DebugFormat("[AVFACTORY]: Agent {0} no longer in the scene", agentId);
                return false;
            }

            SendAppearance(sp);
            return true;
        }

        public Dictionary<BakeType, Primitive.TextureEntryFace> GetBakedTextureFaces(UUID agentId)
        {
            ScenePresence sp = m_scene.GetScenePresence(agentId);

            if (sp == null)
                return new Dictionary<BakeType, Primitive.TextureEntryFace>();

            return GetBakedTextureFaces(sp);
        }

        public WearableCacheItem[] GetCachedItems(UUID agentId)
        {
            ScenePresence sp = m_scene.GetScenePresence(agentId);
            WearableCacheItem[] items = sp.Appearance.WearableCacheItems;
            //foreach (WearableCacheItem item in items)
            //{
               
            //}
            return items;
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
                    // This can happen legitimately, since some baked textures might not exist
                    //m_log.WarnFormat(
                    //    "[AV FACTORY]: No texture ID set for {0} for {1} in {2} not found when trying to save permanently",
                    //    bakeType, sp.Name, m_scene.RegionInfo.RegionName);
                    continue;
                }

                AssetBase asset = m_scene.AssetService.Get(bakedTextureFace.TextureID.ToString());

                if (asset != null)
                {
                    // Replace an HG ID with the simple asset ID so that we can persist textures for foreign HG avatars
                    asset.ID = asset.FullID.ToString();

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
            return true;
        }

        /// <summary>
        /// Queue up a request to send appearance.
        /// </summary>
        /// <remarks>
        /// Makes it possible to accumulate changes without sending out each one separately.
        /// </remarks>
        /// <param name="agentId"></param>
        public void QueueAppearanceSend(UUID agentid)
        {
//            m_log.DebugFormat("[AVFACTORY]: Queue appearance send for {0}", agentid);

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
//            m_log.DebugFormat("[AVFACTORY]: Queueing appearance save for {0}", agentid);

            // 10000 ticks per millisecond, 1000 milliseconds per second
            long timestamp = DateTime.Now.Ticks + Convert.ToInt64(m_savetime * 1000 * 10000);
            lock (m_savequeue)
            {
                m_savequeue[agentid] = timestamp;
                m_updateTimer.Start();
            }
        }

        public bool ValidateBakedTextureCache(IScenePresence sp)
        {
            bool defonly = true; // are we only using default textures
            IImprovedAssetCache cache = m_scene.RequestModuleInterface<IImprovedAssetCache>();
            IBakedTextureModule bakedModule = m_scene.RequestModuleInterface<IBakedTextureModule>();
            WearableCacheItem[] wearableCache = null;

            // Cache wearable data for teleport.
            // Only makes sense if there's a bake module and a cache module
            if (bakedModule != null && cache != null)
            {
                try
                {
                    wearableCache = bakedModule.Get(sp.UUID);
                }
                catch (Exception)
                {

                }
                if (wearableCache != null)
                {
                    for (int i = 0; i < wearableCache.Length; i++)
                    {
                       cache.Cache(wearableCache[i].TextureAsset);
                    }
                }
            }
            /*
             IBakedTextureModule bakedModule = m_scene.RequestModuleInterface<IBakedTextureModule>();
            if (invService.GetRootFolder(userID) != null)
            {
                WearableCacheItem[] wearableCache = null;
                if (bakedModule != null)
                {
                    try
                    {
                        wearableCache = bakedModule.Get(userID);
                        appearance.WearableCacheItems = wearableCache;
                        appearance.WearableCacheItemsDirty = false;
                        foreach (WearableCacheItem item in wearableCache)
                        {
                            appearance.Texture.FaceTextures[item.TextureIndex].TextureID = item.TextureID;
                        }
                    }
                    catch (Exception)
                    {
                        
                    }
                }
             */

            // Process the texture entry
            for (int i = 0; i < AvatarAppearance.BAKE_INDICES.Length; i++)
            {
                int idx = AvatarAppearance.BAKE_INDICES[i];
                Primitive.TextureEntryFace face = sp.Appearance.Texture.FaceTextures[idx];

                // No face, so lets check our baked service cache, teleport or login.
                if (face == null)
                {
                    if (wearableCache != null)
                    {
                        // If we find the an appearance item, set it as the textureentry and the face
                        WearableCacheItem searchitem = WearableCacheItem.SearchTextureIndex((uint) idx, wearableCache);
                        if (searchitem != null)
                        {
                            sp.Appearance.Texture.FaceTextures[idx] = sp.Appearance.Texture.CreateFace((uint) idx);
                            sp.Appearance.Texture.FaceTextures[idx].TextureID = searchitem.TextureID;
                            face = sp.Appearance.Texture.FaceTextures[idx];
                        }
                        else
                        {
                            // if there is no texture entry and no baked cache, skip it
                            continue;
                        }
                    }
                    else
                    {
                        //No texture entry face and no cache.  Skip this face.
                        continue;
                    }
                }
                    
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

                if (m_scene.AssetService.Get(face.TextureID.ToString()) == null)
                    return false;
            }

//            m_log.DebugFormat("[AVFACTORY]: Completed texture check for {0} {1}", sp.Name, sp.UUID);

            // If we only found default textures, then the appearance is not cached
            return (defonly ? false : true);
        }

        public int RequestRebake(IScenePresence sp, bool missingTexturesOnly)
        {
            int texturesRebaked = 0;
//            IImprovedAssetCache cache = m_scene.RequestModuleInterface<IImprovedAssetCache>();

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

                if (missingTexturesOnly)
                {
                    if (m_scene.AssetService.Get(face.TextureID.ToString()) != null)
                    {
                        continue;
                    }
                    else
                    {
                        // On inter-simulator teleports, this occurs if baked textures are not being stored by the
                        // grid asset service (which means that they are not available to the new region and so have
                        // to be re-requested from the client).
                        //
                        // The only available core OpenSimulator behaviour right now
                        // is not to store these textures, temporarily or otherwise.
                        m_log.DebugFormat(
                            "[AVFACTORY]: Missing baked texture {0} ({1}) for {2}, requesting rebake.",
                            face.TextureID, idx, sp.Name);
                    }
                }
                else
                {
                    m_log.DebugFormat(
                        "[AVFACTORY]: Requesting rebake of {0} ({1}) for {2}.",
                        face.TextureID, idx, sp.Name);
                }

                texturesRebaked++;
                sp.ControllingClient.SendRebakeAvatarTextures(face.TextureID);
            }

            return texturesRebaked;
        }

        #endregion

        #region AvatarFactoryModule private methods

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
                Primitive.TextureEntryFace texture = faceTextures[ftIndex];    // this will be null if there's no such baked texture
                bakedTextures[bakeType] = texture;
            }

            return bakedTextures;
        }

        private void HandleAppearanceUpdateTimer(object sender, EventArgs ea)
        {
            long now = DateTime.Now.Ticks;

            lock (m_sendqueue)
            {
                Dictionary<UUID, long> sends = new Dictionary<UUID, long>(m_sendqueue);
                foreach (KeyValuePair<UUID, long> kvp in sends)
                {
                    // We have to load the key and value into local parameters to avoid a race condition if we loop
                    // around and load kvp with a different value before FireAndForget has launched its thread.
                    UUID avatarID = kvp.Key;
                    long sendTime = kvp.Value;

//                    m_log.DebugFormat("[AVFACTORY]: Handling queued appearance updates for {0}, update delta to now is {1}", avatarID, sendTime - now);

                    if (sendTime < now)
                    {
                        Util.FireAndForget(o => SendAppearance(avatarID), null, "AvatarFactoryModule.SendAppearance");
                        m_sendqueue.Remove(avatarID);
                    }
                }
            }

            lock (m_savequeue)
            {
                Dictionary<UUID, long> saves = new Dictionary<UUID, long>(m_savequeue);
                foreach (KeyValuePair<UUID, long> kvp in saves)
                {
                    // We have to load the key and value into local parameters to avoid a race condition if we loop
                    // around and load kvp with a different value before FireAndForget has launched its thread.                    
                    UUID avatarID = kvp.Key;
                    long sendTime = kvp.Value;

                    if (sendTime < now)
                    {
                        Util.FireAndForget(o => SaveAppearance(avatarID), null, "AvatarFactoryModule.SaveAppearance");
                        m_savequeue.Remove(avatarID);
                    }
                }

                // We must lock both queues here so that QueueAppearanceSave() or *Send() don't m_updateTimer.Start() on
                // another thread inbetween the first count calls and m_updateTimer.Stop() on this thread.
                lock (m_sendqueue)
                    if (m_savequeue.Count == 0 && m_sendqueue.Count == 0)
                        m_updateTimer.Stop();
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
                // This is expected if the user has gone away.
//                m_log.DebugFormat("[AVFACTORY]: Agent {0} no longer in the scene", agentid);
                return;
            }

//            m_log.DebugFormat("[AVFACTORY]: Saving appearance for avatar {0}", agentid);

            // This could take awhile since it needs to pull inventory
            // We need to do it at the point of save so that there is a sufficient delay for any upload of new body part/shape
            // assets and item asset id changes to complete.
            // I don't think we need to worry about doing this within m_setAppearanceLock since the queueing avoids
            // multiple save requests.
            SetAppearanceAssets(sp.UUID, sp.Appearance);

//            List<AvatarAttachment> attachments = sp.Appearance.GetAttachments();
//            foreach (AvatarAttachment att in attachments)
//            {
//                m_log.DebugFormat(
//                    "[AVFACTORY]: For {0} saving attachment {1} at point {2}",
//                    sp.Name, att.ItemID, att.AttachPoint);
//            }

            m_scene.AvatarService.SetAppearance(agentid, sp.Appearance);

            // Trigger this here because it's the final step in the set/queue/save process for appearance setting. 
            // Everything has been updated and stored. Ensures bakes have been persisted (if option is set to persist bakes).
            m_scene.EventManager.TriggerAvatarAppearanceChanged(sp);
        }

        /// <summary>
        /// For a given set of appearance items, check whether the items are valid and add their asset IDs to 
        /// appearance data.
        /// </summary>
        /// <param name='userID'></param>
        /// <param name='appearance'></param>
        private void SetAppearanceAssets(UUID userID, AvatarAppearance appearance)
        {
            IInventoryService invService = m_scene.InventoryService;

            if (invService.GetRootFolder(userID) != null)
            {
                for (int i = 0; i < AvatarWearable.MAX_WEARABLES; i++)
                {
                    for (int j = 0; j < appearance.Wearables[i].Count; j++)
                    {
                        if (appearance.Wearables[i][j].ItemID == UUID.Zero)
                        {
                            m_log.WarnFormat(
                                "[AVFACTORY]: Wearable item {0}:{1} for user {2} unexpectedly UUID.Zero.  Ignoring.", 
                                i, j, userID);

                            continue;
                        }

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
                            m_log.WarnFormat(
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

//            IInventoryService invService = m_scene.InventoryService;
//            bool resetwearable = false;
//            if (invService.GetRootFolder(userID) != null)
//            {
//                for (int i = 0; i < AvatarWearable.MAX_WEARABLES; i++)
//                {
//                    for (int j = 0; j < appearance.Wearables[i].Count; j++)
//                    {
//                        // Check if the default wearables are not set
//                        if (appearance.Wearables[i][j].ItemID == UUID.Zero)
//                        {
//                            switch ((WearableType) i)
//                            {
//                                case WearableType.Eyes:
//                                case WearableType.Hair:
//                                case WearableType.Shape:
//                                case WearableType.Skin:
//                                //case WearableType.Underpants:
//                                    TryAndRepairBrokenWearable((WearableType)i, invService, userID, appearance);
//                                    resetwearable = true;
//                                    m_log.Warn("[AVFACTORY]: UUID.Zero Wearables, passing fake values.");
//                                    resetwearable = true;
//                                    break;
//
//                            }
//                            continue;
//                        }
//
//                        // Ignore ruth's assets except for the body parts! missing body parts fail avatar appearance on V1
//                        if (appearance.Wearables[i][j].ItemID == AvatarWearable.DefaultWearables[i][0].ItemID)
//                        {
//                            switch ((WearableType)i)
//                            {
//                                case WearableType.Eyes:
//                                case WearableType.Hair:
//                                case WearableType.Shape:
//                                case WearableType.Skin:
//                                //case WearableType.Underpants:
//                                    TryAndRepairBrokenWearable((WearableType)i, invService, userID, appearance);
//                            
//                                    m_log.WarnFormat("[AVFACTORY]: {0} Default Wearables, passing existing values.", (WearableType)i);
//                                    resetwearable = true;
//                                    break;
//
//                            }
//                            continue;
//                        }
//                        
//                        InventoryItemBase baseItem = new InventoryItemBase(appearance.Wearables[i][j].ItemID, userID);
//                        baseItem = invService.GetItem(baseItem);
//
//                        if (baseItem != null)
//                        {
//                            appearance.Wearables[i].Add(appearance.Wearables[i][j].ItemID, baseItem.AssetID);
//                            int unmodifiedWearableIndexForClosure = i;
//                            m_scene.AssetService.Get(baseItem.AssetID.ToString(), this,
//                                                                      delegate(string x, object y, AssetBase z)
//                                                                      {
//                                                                          if (z == null)
//                                                                          {
//                                                                              TryAndRepairBrokenWearable(
//                                                                                  (WearableType)unmodifiedWearableIndexForClosure, invService,
//                                                                                  userID, appearance);
//                                                                          }
//                                                                      });
//                        }
//                        else
//                        {
//                            m_log.ErrorFormat(
//                                "[AVFACTORY]: Can't find inventory item {0} for {1}, setting to default",
//                                appearance.Wearables[i][j].ItemID, (WearableType)i);
//
//                            TryAndRepairBrokenWearable((WearableType)i, invService, userID, appearance);
//                            resetwearable = true;
//                            
//                        }
//                    }
//                }
//
//                // I don't know why we have to test for this again...  but the above switches do not capture these scenarios for some reason....
//                if (appearance.Wearables[(int) WearableType.Eyes] == null)
//                {
//                    m_log.WarnFormat("[AVFACTORY]: {0} Eyes are Null, passing existing values.", (WearableType.Eyes));
//                    
//                    TryAndRepairBrokenWearable(WearableType.Eyes, invService, userID, appearance);
//                    resetwearable = true;
//                }
//                else
//                {
//                    if (appearance.Wearables[(int) WearableType.Eyes][0].ItemID == UUID.Zero)
//                    {
//                        m_log.WarnFormat("[AVFACTORY]: Eyes are UUID.Zero are broken, {0} {1}",
//                                         appearance.Wearables[(int) WearableType.Eyes][0].ItemID,
//                                         appearance.Wearables[(int) WearableType.Eyes][0].AssetID);
//                        TryAndRepairBrokenWearable(WearableType.Eyes, invService, userID, appearance);
//                        resetwearable = true;
//
//                    }
//
//                }
//                // I don't know why we have to test for this again...  but the above switches do not capture these scenarios for some reason....
//                if (appearance.Wearables[(int)WearableType.Shape] == null)
//                {
//                    m_log.WarnFormat("[AVFACTORY]: {0} shape is Null, passing existing values.", (WearableType.Shape));
//
//                    TryAndRepairBrokenWearable(WearableType.Shape, invService, userID, appearance);
//                    resetwearable = true;
//                }
//                else
//                {
//                    if (appearance.Wearables[(int)WearableType.Shape][0].ItemID == UUID.Zero)
//                    {
//                        m_log.WarnFormat("[AVFACTORY]: Shape is UUID.Zero and broken, {0} {1}",
//                                         appearance.Wearables[(int)WearableType.Shape][0].ItemID,
//                                         appearance.Wearables[(int)WearableType.Shape][0].AssetID);
//                        TryAndRepairBrokenWearable(WearableType.Shape, invService, userID, appearance);
//                        resetwearable = true;
//
//                    }
//
//                }
//                // I don't know why we have to test for this again...  but the above switches do not capture these scenarios for some reason....
//                if (appearance.Wearables[(int)WearableType.Hair] == null)
//                {
//                    m_log.WarnFormat("[AVFACTORY]: {0} Hair is Null, passing existing values.", (WearableType.Hair));
//
//                    TryAndRepairBrokenWearable(WearableType.Hair, invService, userID, appearance);
//                    resetwearable = true;
//                }
//                else
//                {
//                    if (appearance.Wearables[(int)WearableType.Hair][0].ItemID == UUID.Zero)
//                    {
//                        m_log.WarnFormat("[AVFACTORY]: Hair is UUID.Zero and broken, {0} {1}",
//                                         appearance.Wearables[(int)WearableType.Hair][0].ItemID,
//                                         appearance.Wearables[(int)WearableType.Hair][0].AssetID);
//                        TryAndRepairBrokenWearable(WearableType.Hair, invService, userID, appearance);
//                        resetwearable = true;
//
//                    }
//
//                }
//                // I don't know why we have to test for this again...  but the above switches do not capture these scenarios for some reason....
//                if (appearance.Wearables[(int)WearableType.Skin] == null)
//                {
//                    m_log.WarnFormat("[AVFACTORY]: {0} Skin is Null, passing existing values.", (WearableType.Skin));
//
//                    TryAndRepairBrokenWearable(WearableType.Skin, invService, userID, appearance);
//                    resetwearable = true;
//                }
//                else
//                {
//                    if (appearance.Wearables[(int)WearableType.Skin][0].ItemID == UUID.Zero)
//                    {
//                        m_log.WarnFormat("[AVFACTORY]: Skin is UUID.Zero and broken, {0} {1}",
//                                         appearance.Wearables[(int)WearableType.Skin][0].ItemID,
//                                         appearance.Wearables[(int)WearableType.Skin][0].AssetID);
//                        TryAndRepairBrokenWearable(WearableType.Skin, invService, userID, appearance);
//                        resetwearable = true;
//
//                    }
//
//                }
//                if (resetwearable)
//                {
//                    ScenePresence presence = null;
//                    if (m_scene.TryGetScenePresence(userID, out presence))
//                    {
//                        presence.ControllingClient.SendWearables(presence.Appearance.Wearables,
//                                                                 presence.Appearance.Serial++);
//                    }
//                }
//
//            }
//            else
//            {
//                m_log.WarnFormat("[AVFACTORY]: user {0} has no inventory, appearance isn't going to work", userID);
//            }
        }

        private void TryAndRepairBrokenWearable(WearableType type, IInventoryService invService, UUID userID,AvatarAppearance appearance)
        {
            UUID defaultwearable = GetDefaultItem(type);
            if (defaultwearable != UUID.Zero)
            {
                UUID newInvItem = UUID.Random();
                InventoryItemBase itembase = new InventoryItemBase(newInvItem, userID)
                                                 {
                                                     AssetID =
                                                         defaultwearable,
                                                     AssetType
                                                         =
                                                         (int)
                                                         AssetType
                                                             .Bodypart,
                                                     CreatorId
                                                         =
                                                         userID
                                                         .ToString
                                                         (),
                                                     //InvType = (int)InventoryType.Wearable,

                                                     Description
                                                         =
                                                         "Failed Wearable Replacement",
                                                     Folder =
                                                         invService
                                                         .GetFolderForType
                                                         (userID,
                                                          AssetType
                                                              .Bodypart)
                                                         .ID,
                                                     Flags = (uint) type,
                                                     Name = Enum.GetName(typeof (WearableType), type),
                                                     BasePermissions = (uint) PermissionMask.Copy,
                                                     CurrentPermissions = (uint) PermissionMask.Copy,
                                                     EveryOnePermissions = (uint) PermissionMask.Copy,
                                                     GroupPermissions = (uint) PermissionMask.Copy,
                                                     NextPermissions = (uint) PermissionMask.Copy
                                                 };
                invService.AddItem(itembase);
                UUID LinkInvItem = UUID.Random();
                itembase = new InventoryItemBase(LinkInvItem, userID)
                               {
                                   AssetID =
                                       newInvItem,
                                   AssetType
                                       =
                                       (int)
                                       AssetType
                                           .Link,
                                   CreatorId
                                       =
                                       userID
                                       .ToString
                                       (),
                                   InvType = (int) InventoryType.Wearable,

                                   Description
                                       =
                                       "Failed Wearable Replacement",
                                   Folder =
                                       invService
                                       .GetFolderForType
                                       (userID,
                                        AssetType
                                            .CurrentOutfitFolder)
                                       .ID,
                                   Flags = (uint) type,
                                   Name = Enum.GetName(typeof (WearableType), type),
                                   BasePermissions = (uint) PermissionMask.Copy,
                                   CurrentPermissions = (uint) PermissionMask.Copy,
                                   EveryOnePermissions = (uint) PermissionMask.Copy,
                                   GroupPermissions = (uint) PermissionMask.Copy,
                                   NextPermissions = (uint) PermissionMask.Copy
                               };
                invService.AddItem(itembase);
                appearance.Wearables[(int)type] = new AvatarWearable(newInvItem, GetDefaultItem(type));
                ScenePresence presence = null;
                if (m_scene.TryGetScenePresence(userID, out presence))
                {
                    m_scene.SendInventoryUpdate(presence.ControllingClient,
                                                invService.GetFolderForType(userID,
                                                                            AssetType
                                                                                .CurrentOutfitFolder),
                                                false, true);
                }
            }
        }

        private UUID GetDefaultItem(WearableType wearable)
        {
            // These are ruth
            UUID ret = UUID.Zero;
            switch (wearable)
            {
                case WearableType.Eyes:
                    ret = new UUID("4bb6fa4d-1cd2-498a-a84c-95c1a0e745a7");
                    break;
                case WearableType.Hair:
                    ret = new UUID("d342e6c0-b9d2-11dc-95ff-0800200c9a66");
                    break;
                case WearableType.Pants:
                    ret = new UUID("00000000-38f9-1111-024e-222222111120");
                    break;
                case WearableType.Shape:
                    ret = new UUID("66c41e39-38f9-f75a-024e-585989bfab73");
                    break;
                case WearableType.Shirt:
                    ret = new UUID("00000000-38f9-1111-024e-222222111110");
                    break;
                case WearableType.Skin:
                    ret = new UUID("77c41e39-38f9-f75a-024e-585989bbabbb");
                    break;
                case WearableType.Undershirt:
                    ret = new UUID("16499ebb-3208-ec27-2def-481881728f47");
                    break;
                case WearableType.Underpants:
                    ret = new UUID("4ac2e9c7-3671-d229-316a-67717730841d");
                    break;
            }

            return ret;
        }
        #endregion

        #region Client Event Handlers
        /// <summary>
        /// Tell the client for this scene presence what items it should be wearing now
        /// </summary>
        /// <param name="client"></param>
        private void Client_OnRequestWearables(IClientAPI client)
        {
            Util.FireAndForget(delegate(object x)
            {
                Thread.Sleep(4000);

                // m_log.DebugFormat("[AVFACTORY]: Client_OnRequestWearables called for {0} ({1})", client.Name, client.AgentId);
                ScenePresence sp = m_scene.GetScenePresence(client.AgentId);
                if (sp != null)
                    client.SendWearables(sp.Appearance.Wearables, sp.Appearance.Serial++);
                else
                    m_log.WarnFormat("[AVFACTORY]: Client_OnRequestWearables unable to find presence for {0}", client.AgentId);
            }, null, "AvatarFactoryModule.OnClientRequestWearables");
        }
        
        /// <summary>
        /// Set appearance data (texture asset IDs and slider settings) received from a client
        /// </summary>
        /// <param name="client"></param>
        /// <param name="texture"></param>
        /// <param name="visualParam"></param>
        private void Client_OnSetAppearance(IClientAPI client, Primitive.TextureEntry textureEntry, byte[] visualParams, Vector3 avSize, WearableCacheItem[] cacheItems)
        {
            // m_log.WarnFormat("[AVFACTORY]: Client_OnSetAppearance called for {0} ({1})", client.Name, client.AgentId);
            ScenePresence sp = m_scene.GetScenePresence(client.AgentId);
            if (sp != null)
                SetAppearance(sp, textureEntry, visualParams,avSize, cacheItems);
            else
                m_log.WarnFormat("[AVFACTORY]: Client_OnSetAppearance unable to find presence for {0}", client.AgentId);
        }

        /// <summary>
        /// Update what the avatar is wearing using an item from their inventory.
        /// </summary>
        /// <param name="client"></param>
        /// <param name="e"></param>
        private void Client_OnAvatarNowWearing(IClientAPI client, AvatarWearingArgs e)
        {
            // m_log.WarnFormat("[AVFACTORY]: Client_OnAvatarNowWearing called for {0} ({1})", client.Name, client.AgentId);
            ScenePresence sp = m_scene.GetScenePresence(client.AgentId);
            if (sp == null)
            {
                m_log.WarnFormat("[AVFACTORY]: Client_OnAvatarNowWearing unable to find presence for {0}", client.AgentId);
                return;
            }

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

        /// <summary>
        /// Respond to the cached textures request from the client
        /// </summary>
        /// <param name="client"></param>
        /// <param name="serial"></param>
        /// <param name="cachedTextureRequest"></param>
        private void Client_OnCachedTextureRequest(IClientAPI client, int serial, List<CachedTextureRequestArg> cachedTextureRequest)
        {
            // m_log.WarnFormat("[AVFACTORY]: Client_OnCachedTextureRequest called for {0} ({1})", client.Name, client.AgentId);
            ScenePresence sp = m_scene.GetScenePresence(client.AgentId);

            List<CachedTextureResponseArg> cachedTextureResponse = new List<CachedTextureResponseArg>();
            foreach (CachedTextureRequestArg request in cachedTextureRequest)
            {
                UUID texture = UUID.Zero;
                int index = request.BakedTextureIndex;
                
                if (m_reusetextures)
                {
                    // this is the most insanely dumb way to do this... however it seems to
                    // actually work. if the appearance has been reset because wearables have
                    // changed then the texture entries are zero'd out until the bakes are 
                    // uploaded. on login, if the textures exist in the cache (eg if you logged
                    // into the simulator recently, then the appearance will pull those and send
                    // them back in the packet and you won't have to rebake. if the textures aren't
                    // in the cache then the intial makeroot() call in scenepresence will zero
                    // them out.
                    //
                    // a better solution (though how much better is an open question) is to
                    // store the hashes in the appearance and compare them. Thats's coming.

                    Primitive.TextureEntryFace face = sp.Appearance.Texture.FaceTextures[index];
                    if (face != null)
                        texture = face.TextureID;

                    // m_log.WarnFormat("[AVFACTORY]: reuse texture {0} for index {1}",texture,index);
                }
                
                CachedTextureResponseArg response = new CachedTextureResponseArg();
                response.BakedTextureIndex = index;
                response.BakedTextureID = texture;
                response.HostName = null;

                cachedTextureResponse.Add(response);
            }
            
            // m_log.WarnFormat("[AVFACTORY]: serial is {0}",serial);
            // The serial number appears to be used to match requests and responses
            // in the texture transaction. We just send back the serial number
            // that was provided in the request. The viewer bumps this for us.
            client.SendCachedTextureResponse(sp, serial, cachedTextureResponse);
        }


        #endregion

        public void WriteBakedTexturesReport(IScenePresence sp, ReportOutputAction outputAction)
        {
            outputAction("For {0} in {1}", sp.Name, m_scene.RegionInfo.RegionName);
            outputAction(BAKED_TEXTURES_REPORT_FORMAT, "Bake Type", "UUID");

            Dictionary<BakeType, Primitive.TextureEntryFace> bakedTextures = GetBakedTextureFaces(sp.UUID);

            foreach (BakeType bt in bakedTextures.Keys)
            {
                string rawTextureID;

                if (bakedTextures[bt] == null)
                {
                    rawTextureID = "not set";
                }
                else
                {
                    rawTextureID = bakedTextures[bt].TextureID.ToString();

                    if (m_scene.AssetService.Get(rawTextureID) == null)
                        rawTextureID += " (not found)";
                    else
                        rawTextureID += " (uploaded)";
                }

                outputAction(BAKED_TEXTURES_REPORT_FORMAT, bt, rawTextureID);
            }

            bool bakedTextureValid = m_scene.AvatarFactory.ValidateBakedTextureCache(sp);
            outputAction("{0} baked appearance texture is {1}", sp.Name, bakedTextureValid ? "OK" : "incomplete");
        }
    }
}
