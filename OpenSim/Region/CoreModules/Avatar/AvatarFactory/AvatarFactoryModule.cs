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

namespace OpenSim.Region.CoreModules.Avatar.AvatarFactory
{
    public class AvatarFactoryModule : IAvatarFactoryModule, IRegionModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public const string BAKED_TEXTURES_REPORT_FORMAT = "{0,-9}  {1}";

        private Scene m_scene = null;

        private int m_savetime = 5; // seconds to wait before saving changed appearance
        private int m_sendtime = 2; // seconds to wait before sending changed appearance

        private int m_checkTime = 500; // milliseconds to wait between checks for appearance updates
        private System.Timers.Timer m_updateTimer = new System.Timers.Timer();
        private Dictionary<UUID,long> m_savequeue = new Dictionary<UUID,long>();
        private Dictionary<UUID,long> m_sendqueue = new Dictionary<UUID,long>();

        private object m_setAppearanceLock = new object();

        #region IRegionModule

        public void Initialise(Scene scene, IConfigSource config)
        {
            scene.RegisterModuleInterface<IAvatarFactoryModule>(this);
            scene.EventManager.OnNewClient += SubscribeToClientEvents;

            IConfig appearanceConfig = config.Configs["Appearance"];
            if (appearanceConfig != null)
            {
                m_savetime = Convert.ToInt32(appearanceConfig.GetString("DelayBeforeAppearanceSave",Convert.ToString(m_savetime)));
                m_sendtime = Convert.ToInt32(appearanceConfig.GetString("DelayBeforeAppearanceSend",Convert.ToString(m_sendtime)));
                // m_log.InfoFormat("[AVFACTORY] configured for {0} save and {1} send",m_savetime,m_sendtime);
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

        private void SubscribeToClientEvents(IClientAPI client)
        {
            client.OnRequestWearables += Client_OnRequestWearables;
            client.OnSetAppearance += Client_OnSetAppearance;
            client.OnAvatarNowWearing += Client_OnAvatarNowWearing;
        }

        #endregion

        #region IAvatarFactoryModule

        /// </summary>
        /// <param name="sp"></param>
        /// <param name="texture"></param>
        /// <param name="visualParam"></param>
        public void SetAppearance(IScenePresence sp, AvatarAppearance appearance)
        {
            SetAppearance(sp, appearance.Texture, appearance.VisualParams);
        }

        /// <summary>
        /// Set appearance data (texture asset IDs and slider settings) 
        /// </summary>
        /// <param name="sp"></param>
        /// <param name="texture"></param>
        /// <param name="visualParam"></param>
        public void SetAppearance(IScenePresence sp, Primitive.TextureEntry textureEntry, byte[] visualParams)
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

                    float oldHeight = sp.Appearance.AvatarHeight;
                    changed = sp.Appearance.SetVisualParams(visualParams);

                    if (sp.Appearance.AvatarHeight != oldHeight && sp.Appearance.AvatarHeight > 0)
                        ((ScenePresence)sp).SetHeight(sp.Appearance.AvatarHeight);
                }

                // Process the baked texture array
                if (textureEntry != null)
                {
//                    m_log.DebugFormat("[AVFACTORY]: Received texture update for {0} {1}", sp.Name, sp.UUID);

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
            sp.SendAppearanceToAllOtherAgents();

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
            // m_log.WarnFormat("[AVFACTORY]: Queue appearance save for {0}", agentid);

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
                        Util.FireAndForget(o => SendAppearance(avatarID));
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
                        Util.FireAndForget(o => SaveAppearance(avatarID));
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

            // m_log.WarnFormat("[AVFACTORY] avatar {0} save appearance",agentid);

            // This could take awhile since it needs to pull inventory
            // We need to do it at the point of save so that there is a sufficient delay for any upload of new body part/shape
            // assets and item asset id changes to complete.
            // I don't think we need to worry about doing this within m_setAppearanceLock since the queueing avoids
            // multiple save requests.
            SetAppearanceAssets(sp.UUID, sp.Appearance);

            m_scene.AvatarService.SetAppearance(agentid, sp.Appearance);

            // Trigger this here because it's the final step in the set/queue/save process for appearance setting. 
            // Everything has been updated and stored. Ensures bakes have been persisted (if option is set to persist bakes).
            m_scene.EventManager.TriggerAvatarAppearanceChanged(sp);
        }

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

        #endregion

        #region Client Event Handlers
        /// <summary>
        /// Tell the client for this scene presence what items it should be wearing now
        /// </summary>
        /// <param name="client"></param>
        private void Client_OnRequestWearables(IClientAPI client)
        {
            // m_log.DebugFormat("[AVFACTORY]: Client_OnRequestWearables called for {0} ({1})", client.Name, client.AgentId);
            ScenePresence sp = m_scene.GetScenePresence(client.AgentId);
            if (sp != null)
                client.SendWearables(sp.Appearance.Wearables, sp.Appearance.Serial++);
            else
                m_log.WarnFormat("[AVFACTORY]: Client_OnRequestWearables unable to find presence for {0}", client.AgentId);
        }
        
        /// <summary>
        /// Set appearance data (texture asset IDs and slider settings) received from a client
        /// </summary>
        /// <param name="client"></param>
        /// <param name="texture"></param>
        /// <param name="visualParam"></param>
        private void Client_OnSetAppearance(IClientAPI client, Primitive.TextureEntry textureEntry, byte[] visualParams)
        {
            // m_log.WarnFormat("[AVFACTORY]: Client_OnSetAppearance called for {0} ({1})", client.Name, client.AgentId);
            ScenePresence sp = m_scene.GetScenePresence(client.AgentId);
            if (sp != null)
                SetAppearance(sp, textureEntry, visualParams);
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
            outputAction("{0} baked appearance texture is {1}", sp.Name, bakedTextureValid ? "OK" : "corrupt");
        }
    }
}
