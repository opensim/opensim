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
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;
using System.IO;
using System.Web;
using log4net;
using Nini.Config;
using Mono.Addins;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using OpenMetaverse.Imaging;
using OpenSim.Framework;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;
using Caps = OpenSim.Framework.Capabilities.Caps;
using OpenSim.Capabilities.Handlers;

namespace OpenSim.Region.ClientStack.Linden
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "UploadBakedTextureModule")]
    public class UploadBakedTextureModule : INonSharedRegionModule
    {
       private static readonly ILog m_log =
            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// For historical reasons this is fixed, but there
        /// </summary>
        private static readonly string m_uploadBakedTexturePath = "0010/";// This is in the LandManagementModule.

        private Scene m_scene;
        private bool m_persistBakedTextures;

        private IBakedTextureModule m_BakedTextureModule;
        private string m_URL;

        public void Initialise(IConfigSource source)
        {
            IConfig config = source.Configs["ClientStack.LindenCaps"];
            if (config == null)
                return;

            m_URL = config.GetString("Cap_UploadBakedTexture", string.Empty);

            IConfig appearanceConfig = source.Configs["Appearance"];
            if (appearanceConfig != null)
                m_persistBakedTextures = appearanceConfig.GetBoolean("PersistBakedTextures", m_persistBakedTextures);
        }

        public void AddRegion(Scene s)
        {
            m_scene = s;

        }

        public void RemoveRegion(Scene s)
        {
            s.EventManager.OnRegisterCaps -= RegisterCaps;
            s.EventManager.OnNewPresence -= RegisterNewPresence;
            s.EventManager.OnRemovePresence -= DeRegisterPresence;
            m_BakedTextureModule = null;
            m_scene = null;
        }

        public void RegionLoaded(Scene s)
        {
            m_scene.EventManager.OnRegisterCaps += RegisterCaps;
            m_scene.EventManager.OnNewPresence += RegisterNewPresence;
            m_scene.EventManager.OnRemovePresence += DeRegisterPresence;

        }

        private void DeRegisterPresence(UUID agentId)
        {
        }

        private void RegisterNewPresence(ScenePresence presence)
        {
//           presence.ControllingClient.OnSetAppearance += CaptureAppearanceSettings;
        }

/* not in use. work done in AvatarFactoryModule ValidateBakedTextureCache() and UpdateBakedTextureCache()
                private void CaptureAppearanceSettings(IClientAPI remoteClient, Primitive.TextureEntry textureEntry, byte[] visualParams, Vector3 avSize, WearableCacheItem[] cacheItems)
                {
                    // if cacheItems.Length > 0 viewer is giving us current textures information.
                    // baked ones should had been uploaded and in assets cache as local itens


                    if (cacheItems.Length == 0)
                        return;  // no textures information, nothing to do

                    ScenePresence p = null;
                    if (!m_scene.TryGetScenePresence(remoteClient.AgentId, out p))
                        return; // what are we doing if there is no presence to cache for?

                    if (p.IsDeleted)
                        return; // does this really work?

                    int maxCacheitemsLoop = cacheItems.Length;
                    if (maxCacheitemsLoop > 20)
                    {
                        maxCacheitemsLoop = AvatarWearable.MAX_WEARABLES;
                        m_log.WarnFormat("[CACHEDBAKES]: Too Many Cache items Provided {0}, the max is {1}.  Truncating!", cacheItems.Length, AvatarWearable.MAX_WEARABLES);
                    }

                    m_BakedTextureModule = m_scene.RequestModuleInterface<IBakedTextureModule>();


                    // some nice debug
                    m_log.Debug("[Cacheitems]: " + cacheItems.Length);
                    for (int iter = 0; iter < maxCacheitemsLoop; iter++)
                    {
                        m_log.Debug("[Cacheitems] {" + iter + "/" + cacheItems[iter].TextureIndex + "}: c-" + cacheItems[iter].CacheId + ", t-" +
                                          cacheItems[iter].TextureID);
                    }

                    // p.Appearance.WearableCacheItems is in memory primary cashID to textures mapper

                    WearableCacheItem[] existingitems = p.Appearance.WearableCacheItems;

                    if (existingitems == null)
                    {
                        if (m_BakedTextureModule != null)
                        {
                            WearableCacheItem[] savedcache = null;
                            try
                            {
                                if (p.Appearance.WearableCacheItemsDirty)
                                {
                                    savedcache = m_BakedTextureModule.Get(p.UUID);
                                    p.Appearance.WearableCacheItems = savedcache;
                                    p.Appearance.WearableCacheItemsDirty = false;
                                }
                            }

                            catch (Exception)
                            {
                                // The service logs a sufficient error message.
                            }


                            if (savedcache != null)
                                existingitems = savedcache;
                        }
                    }

                    // Existing items null means it's a fully new appearance
                    if (existingitems == null)
                    {
                        for (int i = 0; i < maxCacheitemsLoop; i++)
                        {
                            if (textureEntry.FaceTextures.Length > cacheItems[i].TextureIndex)
                            {
                                Primitive.TextureEntryFace face = textureEntry.FaceTextures[cacheItems[i].TextureIndex];
                                if (face == null)
                                {
                                    textureEntry.CreateFace(cacheItems[i].TextureIndex);
                                    textureEntry.FaceTextures[cacheItems[i].TextureIndex].TextureID =
                                        AppearanceManager.DEFAULT_AVATAR_TEXTURE;
                                    continue;
                                }
                                cacheItems[i].TextureID = face.TextureID;
                                if (m_scene.AssetService != null)
                                    cacheItems[i].TextureAsset =
                                        m_scene.AssetService.GetCached(cacheItems[i].TextureID.ToString());
                            }
                            else
                            {
                                m_log.WarnFormat("[CACHEDBAKES]: Invalid Texture Index Provided, Texture doesn't exist or hasn't been uploaded yet {0}, the max is {1}.  Skipping!", cacheItems[i].TextureIndex, textureEntry.FaceTextures.Length);
                            }
                        }
                    }
                    else
                    {
                        for (int i = 0; i < maxCacheitemsLoop; i++)
                        {
                            if (textureEntry.FaceTextures.Length > cacheItems[i].TextureIndex)
                            {
                                Primitive.TextureEntryFace face = textureEntry.FaceTextures[cacheItems[i].TextureIndex];
                                if (face == null)
                                {
                                    textureEntry.CreateFace(cacheItems[i].TextureIndex);
                                    textureEntry.FaceTextures[cacheItems[i].TextureIndex].TextureID =
                                        AppearanceManager.DEFAULT_AVATAR_TEXTURE;
                                    continue;
                                }
                                cacheItems[i].TextureID =
                                    face.TextureID;
                            }
                            else
                            {
                                m_log.WarnFormat("[CACHEDBAKES]: Invalid Texture Index Provided, Texture doesn't exist or hasn't been uploaded yet {0}, the max is {1}.  Skipping!", cacheItems[i].TextureIndex, textureEntry.FaceTextures.Length);
                            }
                        }

                        for (int i = 0; i < maxCacheitemsLoop; i++)
                        {
                            if (cacheItems[i].TextureAsset == null)
                            {
                                cacheItems[i].TextureAsset =
                                    m_scene.AssetService.GetCached(cacheItems[i].TextureID.ToString());
                            }
                        }
                    }
                    p.Appearance.WearableCacheItems = cacheItems;

                    if (m_BakedTextureModule != null)
                    {
                        m_BakedTextureModule.Store(remoteClient.AgentId, cacheItems);
                        p.Appearance.WearableCacheItemsDirty = true;

                    }
                    else
                        p.Appearance.WearableCacheItemsDirty = false;

                    for (int iter = 0; iter < maxCacheitemsLoop; iter++)
                    {
                        m_log.Debug("[CacheitemsLeaving] {" + iter + "/" + cacheItems[iter].TextureIndex + "}: c-" + cacheItems[iter].CacheId + ", t-" +
                                          cacheItems[iter].TextureID);
                    }
                }
        */
        public void PostInitialise()
        {
        }



        public void Close() { }

        public string Name { get { return "UploadBakedTextureModule"; } }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public void RegisterCaps(UUID agentID, Caps caps)
        {
            //caps.RegisterHandler("GetTexture", new StreamHandler("GET", "/CAPS/" + capID, ProcessGetTexture));
            if (m_URL == "localhost")
            {
                UploadBakedTextureHandler avatarhandler = new UploadBakedTextureHandler(
                    caps, m_scene.AssetService, m_persistBakedTextures);

                caps.RegisterHandler(
                    "UploadBakedTexture",
                    new RestStreamHandler(
                        "POST",
                        "/CAPS/" + caps.CapsObjectPath + m_uploadBakedTexturePath,
                        avatarhandler.UploadBakedTexture,
                        "UploadBakedTexture",
                        agentID.ToString()));

            }
            else
            {
                caps.RegisterHandler("UploadBakedTexture", m_URL);
            }
        }
    }
}
