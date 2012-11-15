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
        //private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private const int ALL_SIDES = -1;

        public const int DISP_EXPIRE = 1;
        public const int DISP_TEMP   = 2;

        private Dictionary<UUID, Scene> RegisteredScenes = new Dictionary<UUID, Scene>();

        private Dictionary<string, IDynamicTextureRender> RenderPlugins =
            new Dictionary<string, IDynamicTextureRender>();

        private Dictionary<UUID, DynamicTextureUpdater> Updaters = new Dictionary<UUID, DynamicTextureUpdater>();

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
        /// <param name="id"></param>
        /// <param name="data"></param>
        public void ReturnData(UUID id, byte[] data)
        {
            DynamicTextureUpdater updater = null;

            lock (Updaters)
            {
                if (Updaters.ContainsKey(id))
                {
                    updater = Updaters[id];
                }
            }

            if (updater != null)
            {
                if (RegisteredScenes.ContainsKey(updater.SimUUID))
                {
                    Scene scene = RegisteredScenes[updater.SimUUID];
                    updater.DataReceived(data, scene);
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
            if (RenderPlugins.ContainsKey(contentType))
            {
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

                lock (Updaters)
                {
                    if (!Updaters.ContainsKey(updater.UpdaterID))
                    {
                        Updaters.Add(updater.UpdaterID, updater);
                    }
                }

                RenderPlugins[contentType].AsyncConvertData(updater.UpdaterID, data, extraParams);
                return updater.UpdaterID;
            }
            
            return UUID.Zero;
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
            if (!RegisteredScenes.ContainsKey(scene.RegionInfo.RegionID))
            {
                RegisteredScenes.Add(scene.RegionInfo.RegionID, scene);
                scene.RegisterModuleInterface<IDynamicTextureManager>(this);
            }
        }

        public void PostInitialise()
        {
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
            /// Called once new texture data has been received for this updater.
            /// </summary>
            public void DataReceived(byte[] data, Scene scene)
            {
                SceneObjectPart part = scene.GetSceneObjectPart(PrimID);

                if (part == null || data == null || data.Length <= 1)
                {
                    string msg = 
                        String.Format("DynamicTextureModule: Error preparing image using URL {0}", Url);
                    scene.SimChat(Utils.StringToBytes(msg), ChatTypeEnum.Say,
                                  0, part.ParentGroup.RootPart.AbsolutePosition, part.Name, part.UUID, false);
                    return;
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
                    cacheLayerDecode.Decode(asset.FullID, asset.Data);
                    cacheLayerDecode = null;
                }

                UUID oldID = UUID.Zero;

                lock (part)
                {
                    // mostly keep the values from before
                    Primitive.TextureEntry tmptex = part.Shape.Textures;

                    // remove the old asset from the cache
                    oldID = tmptex.DefaultTexture.TextureID;
                    
                    if (Face == ALL_SIDES)
                    {
                        tmptex.DefaultTexture.TextureID = asset.FullID;
                    }
                    else
                    {
                        try
                        {
                            Primitive.TextureEntryFace texface = tmptex.CreateFace((uint)Face);
                            texface.TextureID = asset.FullID;
                            tmptex.FaceTextures[Face] = texface;
                        }
                        catch (Exception)
                        {
                            tmptex.DefaultTexture.TextureID = asset.FullID;
                        }
                    }

                    // I'm pretty sure we always want to force this to true
                    // I'm pretty sure noone whats to set fullbright true if it wasn't true before.
                    // tmptex.DefaultTexture.Fullbright = true;

                    part.UpdateTextureEntry(tmptex.GetBytes());
                }

                if (oldID != UUID.Zero && ((Disp & DISP_EXPIRE) != 0))
                {
                    if (oldAsset == null) oldAsset = scene.AssetService.Get(oldID.ToString());
                    if (oldAsset != null)
                    {
                        if (oldAsset.Temporary == true)
                        {
                            scene.AssetService.Delete(oldID.ToString());
                        }
                    }
                }
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
