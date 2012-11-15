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
using System.Drawing.Imaging;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.Imaging;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using log4net;
using System.Reflection;

namespace OpenSim.Region.CoreModules.Scripting.DynamicTexture
{
    public class DynamicTextureModule : IRegionModule, IDynamicTextureManager
    {
//        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private const int ALL_SIDES = -1;

        public const int DISP_EXPIRE = 1;
        public const int DISP_TEMP   = 2;

        /// <summary>
        /// If true then where possible dynamic textures are reused.
        /// </summary>
        public bool ReuseTextures { get; set; }

        /// <summary>
        /// If false, then textures which have a low data size are not reused when ReuseTextures = true.
        /// </summary>
        /// <remarks>
        /// LL viewers 3.3.4 and before appear to not fully render textures pulled from the viewer cache if those
        /// textures have a relatively high pixel surface but a small data size.  Typically, this appears to happen
        /// if the data size is smaller than the viewer's discard level 2 size estimate.  So if this is setting is
        /// false, textures smaller than the calculation in IsSizeReuseable are always regenerated rather than reused
        /// to work around this problem.</remarks>
        public bool ReuseLowDataTextures { get; set; }

        private Dictionary<UUID, Scene> RegisteredScenes = new Dictionary<UUID, Scene>();

        private Dictionary<string, IDynamicTextureRender> RenderPlugins =
            new Dictionary<string, IDynamicTextureRender>();

        private Dictionary<UUID, DynamicTextureUpdater> Updaters = new Dictionary<UUID, DynamicTextureUpdater>();

        /// <summary>
        /// Record dynamic textures that we can reuse for a given data and parameter combination rather than
        /// regenerate.
        /// </summary>
        /// <remarks>
        /// Key is string.Format("{0}{1}", data
        /// </remarks>
        private Cache m_reuseableDynamicTextures;

        #region IDynamicTextureManager Members

        public void RegisterRender(string handleType, IDynamicTextureRender render)
        {
            if (!RenderPlugins.ContainsKey(handleType))
            {
                RenderPlugins.Add(handleType, render);
            }
        }

        /// <summary>
        /// Called by code which actually renders the dynamic texture to supply texture data.
        /// </summary>
        /// <param name="updaterId"></param>
        /// <param name="texture"></param>
        public void ReturnData(UUID updaterId, IDynamicTexture texture)
        {
            DynamicTextureUpdater updater = null;

            lock (Updaters)
            {
                if (Updaters.ContainsKey(updaterId))
                {
                    updater = Updaters[updaterId];
                }
            }

            if (updater != null)
            {
                if (RegisteredScenes.ContainsKey(updater.SimUUID))
                {
                    Scene scene = RegisteredScenes[updater.SimUUID];
                    UUID newTextureID = updater.DataReceived(texture.Data, scene);

                    if (ReuseTextures
                        && !updater.BlendWithOldTexture
                        && texture.IsReuseable
                        && (ReuseLowDataTextures || IsDataSizeReuseable(texture)))
                    {
                        m_reuseableDynamicTextures.Store(
                            GenerateReusableTextureKey(texture.InputCommands, texture.InputParams), newTextureID);
                    }
                }
            }

            if (updater.UpdateTimer == 0)
            {
                lock (Updaters)
                {
                    if (!Updaters.ContainsKey(updater.UpdaterID))
                    {
                        Updaters.Remove(updater.UpdaterID);
                    }
                }
            }
        }

        /// <summary>
        /// Determines whether the texture is reuseable based on its data size.
        /// </summary>
        /// <remarks>
        /// This is a workaround for a viewer bug where very small data size textures relative to their pixel size
        /// are not redisplayed properly when pulled from cache.  The calculation here is based on the typical discard
        /// level of 2, a 'rate' of 0.125 and 4 components (which makes for a factor of 0.5).
        /// </remarks>
        /// <returns></returns>
        private bool IsDataSizeReuseable(IDynamicTexture texture)
        {
//            Console.WriteLine("{0} {1}", texture.Size.Width, texture.Size.Height);
            int discardLevel2DataThreshold = (int)Math.Ceiling((texture.Size.Width >> 2) * (texture.Size.Height >> 2) * 0.5);

//            m_log.DebugFormat(
//                "[DYNAMIC TEXTURE MODULE]: Discard level 2 threshold {0}, texture data length {1}",
//                discardLevel2DataThreshold, texture.Data.Length);

            return discardLevel2DataThreshold < texture.Data.Length;
        }

        public UUID AddDynamicTextureURL(UUID simID, UUID primID, string contentType, string url,
                                         string extraParams, int updateTimer)
        {
            return AddDynamicTextureURL(simID, primID, contentType, url, extraParams, updateTimer, false, 255);
        }

        public UUID AddDynamicTextureURL(UUID simID, UUID primID, string contentType, string url,
                                         string extraParams, int updateTimer, bool SetBlending, byte AlphaValue)
        {
            return AddDynamicTextureURL(simID, primID, contentType, url,
                                          extraParams, updateTimer, SetBlending, 
                                         (int)(DISP_TEMP|DISP_EXPIRE), AlphaValue, ALL_SIDES);
        }

        public UUID AddDynamicTextureURL(UUID simID, UUID primID, string contentType, string url,
                                         string extraParams, int updateTimer, bool SetBlending, 
                                         int disp, byte AlphaValue, int face)
        {
            if (RenderPlugins.ContainsKey(contentType))
            {
                DynamicTextureUpdater updater = new DynamicTextureUpdater();
                updater.SimUUID = simID;
                updater.PrimID = primID;
                updater.ContentType = contentType;
                updater.Url = url;
                updater.UpdateTimer = updateTimer;
                updater.UpdaterID = UUID.Random();
                updater.Params = extraParams;
                updater.BlendWithOldTexture = SetBlending;
                updater.FrontAlpha = AlphaValue;
                updater.Face = face;
                updater.Disp = disp;

                lock (Updaters)
                {
                    if (!Updaters.ContainsKey(updater.UpdaterID))
                    {
                        Updaters.Add(updater.UpdaterID, updater);
                    }
                }

                RenderPlugins[contentType].AsyncConvertUrl(updater.UpdaterID, url, extraParams);
                return updater.UpdaterID;
            }
            return UUID.Zero;
        }

        public UUID AddDynamicTextureData(UUID simID, UUID primID, string contentType, string data,
                                          string extraParams, int updateTimer)
        {
            return AddDynamicTextureData(simID, primID, contentType, data, extraParams, updateTimer, false, 255);
        }

        public UUID AddDynamicTextureData(UUID simID, UUID primID, string contentType, string data,
                                          string extraParams, int updateTimer, bool SetBlending, byte AlphaValue)
        {
            return AddDynamicTextureData(simID, primID, contentType, data, extraParams, updateTimer, SetBlending, 
                                          (int) (DISP_TEMP|DISP_EXPIRE), AlphaValue, ALL_SIDES);
        }

        public UUID AddDynamicTextureData(UUID simID, UUID primID, string contentType, string data,
                                          string extraParams, int updateTimer, bool SetBlending, int disp, byte AlphaValue, int face)
        {
            if (!RenderPlugins.ContainsKey(contentType))
                return UUID.Zero;

            Scene scene;
            RegisteredScenes.TryGetValue(simID, out scene);

            if (scene == null)
                return UUID.Zero;

            SceneObjectPart part = scene.GetSceneObjectPart(primID);

            if (part == null)
                return UUID.Zero;

            // If we want to reuse dynamic textures then we have to ignore any request from the caller to expire
            // them.
            if (ReuseTextures)
                disp = disp & ~DISP_EXPIRE;

            DynamicTextureUpdater updater = new DynamicTextureUpdater();
            updater.SimUUID = simID;
            updater.PrimID = primID;
            updater.ContentType = contentType;
            updater.BodyData = data;
            updater.UpdateTimer = updateTimer;
            updater.UpdaterID = UUID.Random();
            updater.Params = extraParams;
            updater.BlendWithOldTexture = SetBlending;
            updater.FrontAlpha = AlphaValue;
            updater.Face = face;
            updater.Url = "Local image";
            updater.Disp = disp;

            object objReusableTextureUUID = null;

            if (ReuseTextures && !updater.BlendWithOldTexture)
            {
                string reuseableTextureKey = GenerateReusableTextureKey(data, extraParams);
                objReusableTextureUUID = m_reuseableDynamicTextures.Get(reuseableTextureKey);

                if (objReusableTextureUUID != null)
                {
                    // If something else has removed this temporary asset from the cache, detect and invalidate
                    // our cached uuid.
                    if (scene.AssetService.GetMetadata(objReusableTextureUUID.ToString()) == null)
                    {
                        m_reuseableDynamicTextures.Invalidate(reuseableTextureKey);
                        objReusableTextureUUID = null;
                    }
                }
            }

            // We cannot reuse a dynamic texture if the data is going to be blended with something already there.
            if (objReusableTextureUUID == null)
            {
                lock (Updaters)
                {
                    if (!Updaters.ContainsKey(updater.UpdaterID))
                    {
                        Updaters.Add(updater.UpdaterID, updater);
                    }
                }

//                m_log.DebugFormat(
//                    "[DYNAMIC TEXTURE MODULE]: Requesting generation of new dynamic texture for {0} in {1}",
//                    part.Name, part.ParentGroup.Scene.Name);

                RenderPlugins[contentType].AsyncConvertData(updater.UpdaterID, data, extraParams);
            }
            else
            {
//                m_log.DebugFormat(
//                    "[DYNAMIC TEXTURE MODULE]: Reusing cached texture {0} for {1} in {2}",
//                    objReusableTextureUUID, part.Name, part.ParentGroup.Scene.Name);

                // No need to add to updaters as the texture is always the same.  Not that this functionality
                // apppears to be implemented anyway.
                updater.UpdatePart(part, (UUID)objReusableTextureUUID);
            }

            return updater.UpdaterID;
        }

        private string GenerateReusableTextureKey(string data, string extraParams)
        {
            return string.Format("{0}{1}", data, extraParams);
        }

        public void GetDrawStringSize(string contentType, string text, string fontName, int fontSize,
                                      out double xSize, out double ySize)
        {
            xSize = 0;
            ySize = 0;
            if (RenderPlugins.ContainsKey(contentType))
            {
                RenderPlugins[contentType].GetDrawStringSize(text, fontName, fontSize, out xSize, out ySize);
            }
        }

        #endregion

        #region IRegionModule Members

        public void Initialise(Scene scene, IConfigSource config)
        {
            IConfig texturesConfig = config.Configs["Textures"];
            if (texturesConfig != null)
            {
                ReuseTextures = texturesConfig.GetBoolean("ReuseDynamicTextures", false);
                ReuseLowDataTextures = texturesConfig.GetBoolean("ReuseDynamicLowDataTextures", false);
            }

            if (!RegisteredScenes.ContainsKey(scene.RegionInfo.RegionID))
            {
                RegisteredScenes.Add(scene.RegionInfo.RegionID, scene);
                scene.RegisterModuleInterface<IDynamicTextureManager>(this);
            }
        }

        public void PostInitialise()
        {
            if (ReuseTextures)
            {
                m_reuseableDynamicTextures = new Cache(CacheMedium.Memory, CacheStrategy.Conservative);
                m_reuseableDynamicTextures.DefaultTTL = new TimeSpan(24, 0, 0);
            }
        }

        public void Close()
        {
        }

        public string Name
        {
            get { return "DynamicTextureModule"; }
        }

        public bool IsSharedModule
        {
            get { return true; }
        }

        #endregion

        #region Nested type: DynamicTextureUpdater

        public class DynamicTextureUpdater
        {
            private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

            public bool BlendWithOldTexture = false;
            public string BodyData;
            public string ContentType;
            public byte FrontAlpha = 255;
            public string Params;
            public UUID PrimID;
            public bool SetNewFrontAlpha = false;
            public UUID SimUUID;
            public UUID UpdaterID;
            public int UpdateTimer;
            public int Face;
            public int Disp;
            public string Url;

            public DynamicTextureUpdater()
            {
                UpdateTimer = 0;
                BodyData = null;
            }

            /// <summary>
            /// Update the given part with the new texture.
            /// </summary>
            /// <returns>
            /// The old texture UUID.
            /// </returns>
            public UUID UpdatePart(SceneObjectPart part, UUID textureID)
            {
                UUID oldID;

                lock (part)
                {
                    // mostly keep the values from before
                    Primitive.TextureEntry tmptex = part.Shape.Textures;

                    // FIXME: Need to return the appropriate ID if only a single face is replaced.
                    oldID = tmptex.DefaultTexture.TextureID;

                    if (Face == ALL_SIDES)
                    {
                        oldID = tmptex.DefaultTexture.TextureID;
                        tmptex.DefaultTexture.TextureID = textureID;
                    }
                    else
                    {
                        try
                        {
                            Primitive.TextureEntryFace texface = tmptex.CreateFace((uint)Face);
                            texface.TextureID = textureID;
                            tmptex.FaceTextures[Face] = texface;
                        }
                        catch (Exception)
                        {
                            tmptex.DefaultTexture.TextureID = textureID;
                        }
                    }

                    // I'm pretty sure we always want to force this to true
                    // I'm pretty sure noone whats to set fullbright true if it wasn't true before.
                    // tmptex.DefaultTexture.Fullbright = true;

                    part.UpdateTextureEntry(tmptex.GetBytes());
                }

                return oldID;
            }

            /// <summary>
            /// Called once new texture data has been received for this updater.
            /// </summary>
            /// <param name="data"></param>
            /// <param name="scene"></param>
            /// <param name="isReuseable">True if the data given is reuseable.</param>
            /// <returns>The asset UUID given to the incoming data.</returns>
            public UUID DataReceived(byte[] data, Scene scene)
            {
                SceneObjectPart part = scene.GetSceneObjectPart(PrimID);

                if (part == null || data == null || data.Length <= 1)
                {
                    string msg = 
                        String.Format("DynamicTextureModule: Error preparing image using URL {0}", Url);
                    scene.SimChat(Utils.StringToBytes(msg), ChatTypeEnum.Say,
                                  0, part.ParentGroup.RootPart.AbsolutePosition, part.Name, part.UUID, false);
                    
                    return UUID.Zero;
                }

                byte[] assetData = null;
                AssetBase oldAsset = null;
                
                if (BlendWithOldTexture)
                {
                    Primitive.TextureEntryFace defaultFace = part.Shape.Textures.DefaultTexture;
                    if (defaultFace != null)
                    {
                        oldAsset = scene.AssetService.Get(defaultFace.TextureID.ToString());

                        if (oldAsset != null)
                            assetData = BlendTextures(data, oldAsset.Data, SetNewFrontAlpha, FrontAlpha);
                    }
                }

                if (assetData == null)
                {
                    assetData = new byte[data.Length];
                    Array.Copy(data, assetData, data.Length);
                }

                // Create a new asset for user
                AssetBase asset
                    = new AssetBase(
                        UUID.Random(), "DynamicImage" + Util.RandomClass.Next(1, 10000), (sbyte)AssetType.Texture,
                        scene.RegionInfo.RegionID.ToString());
                asset.Data = assetData;
                asset.Description = String.Format("URL image : {0}", Url);
                asset.Local = false;
                asset.Temporary = ((Disp & DISP_TEMP) != 0);
                scene.AssetService.Store(asset);

                IJ2KDecoder cacheLayerDecode = scene.RequestModuleInterface<IJ2KDecoder>();
                if (cacheLayerDecode != null)
                {
                    if (!cacheLayerDecode.Decode(asset.FullID, asset.Data))
                        m_log.WarnFormat(
                            "[DYNAMIC TEXTURE MODULE]: Decoding of dynamically generated asset {0} for {1} in {2} failed",
                            asset.ID, part.Name, part.ParentGroup.Scene.Name);
                }

                UUID oldID = UpdatePart(part, asset.FullID);

                if (oldID != UUID.Zero && ((Disp & DISP_EXPIRE) != 0))
                {
                    if (oldAsset == null)
                        oldAsset = scene.AssetService.Get(oldID.ToString());

                    if (oldAsset != null)
                    {
                        if (oldAsset.Temporary)
                        {
                            scene.AssetService.Delete(oldID.ToString());
                        }
                    }
                }

                return asset.FullID;
            }

            private byte[] BlendTextures(byte[] frontImage, byte[] backImage, bool setNewAlpha, byte newAlpha)
            {
                ManagedImage managedImage;
                Image image;

                if (OpenJPEG.DecodeToImage(frontImage, out managedImage, out image))
                {
                    Bitmap image1 = new Bitmap(image);

                    if (OpenJPEG.DecodeToImage(backImage, out managedImage, out image))
                    {
                        Bitmap image2 = new Bitmap(image);

                        if (setNewAlpha)
                            SetAlpha(ref image1, newAlpha);

                        Bitmap joint = MergeBitMaps(image1, image2);

                        byte[] result = new byte[0];

                        try
                        {
                            result = OpenJPEG.EncodeFromImage(joint, true);
                        }
                        catch (Exception e)
                        {
                            m_log.ErrorFormat(
                                "[DYNAMICTEXTUREMODULE]: OpenJpeg Encode Failed.  Exception {0}{1}",
                                e.Message, e.StackTrace);
                        }

                        return result;
                    }
                }

                return null;
            }

            public Bitmap MergeBitMaps(Bitmap front, Bitmap back)
            {
                Bitmap joint;
                Graphics jG;

                joint = new Bitmap(back.Width, back.Height, PixelFormat.Format32bppArgb);
                jG = Graphics.FromImage(joint);

                jG.DrawImage(back, 0, 0, back.Width, back.Height);
                jG.DrawImage(front, 0, 0, back.Width, back.Height);

                return joint;
            }

            private void SetAlpha(ref Bitmap b, byte alpha)
            {
                for (int w = 0; w < b.Width; w++)
                {
                    for (int h = 0; h < b.Height; h++)
                    {
                        b.SetPixel(w, h, Color.FromArgb(alpha, b.GetPixel(w, h)));
                    }
                }
            }
        }

        #endregion
    }
}
