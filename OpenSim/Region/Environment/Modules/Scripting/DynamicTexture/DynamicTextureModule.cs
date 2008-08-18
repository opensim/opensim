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
 *     * Neither the name of the OpenSim Project nor the
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
using libsecondlife;
using Nini.Config;
using OpenJPEGNet;
using OpenSim.Framework;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Scenes;

namespace OpenSim.Region.Environment.Modules.Scripting.DynamicTexture
{
    public class DynamicTextureModule : IRegionModule, IDynamicTextureManager
    {
        private Dictionary<LLUUID, Scene> RegisteredScenes = new Dictionary<LLUUID, Scene>();

        private Dictionary<string, IDynamicTextureRender> RenderPlugins =
            new Dictionary<string, IDynamicTextureRender>();

        private Dictionary<LLUUID, DynamicTextureUpdater> Updaters = new Dictionary<LLUUID, DynamicTextureUpdater>();

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
        public void ReturnData(LLUUID id, byte[] data)
        {
            if (Updaters.ContainsKey(id))
            {
                DynamicTextureUpdater updater = Updaters[id];
                if (RegisteredScenes.ContainsKey(updater.SimUUID))
                {
                    Scene scene = RegisteredScenes[updater.SimUUID];
                    updater.DataReceived(data, scene);
                }
            }
        }

        public LLUUID AddDynamicTextureURL(LLUUID simID, LLUUID primID, string contentType, string url,
                                           string extraParams, int updateTimer)
        {
            return AddDynamicTextureURL(simID, primID, contentType, url, extraParams, updateTimer, false, 255);
        }

        public LLUUID AddDynamicTextureURL(LLUUID simID, LLUUID primID, string contentType, string url,
                                           string extraParams, int updateTimer, bool SetBlending, byte AlphaValue)
        {
            if (RenderPlugins.ContainsKey(contentType))
            {
                //Console.WriteLine("dynamic texture being created: " + url + " of type " + contentType);

                DynamicTextureUpdater updater = new DynamicTextureUpdater();
                updater.SimUUID = simID;
                updater.PrimID = primID;
                updater.ContentType = contentType;
                updater.Url = url;
                updater.UpdateTimer = updateTimer;
                updater.UpdaterID = LLUUID.Random();
                updater.Params = extraParams;
                updater.BlendWithOldTexture = SetBlending;
                updater.FrontAlpha = AlphaValue;

                if (!Updaters.ContainsKey(updater.UpdaterID))
                {
                    Updaters.Add(updater.UpdaterID, updater);
                }

                RenderPlugins[contentType].AsyncConvertUrl(updater.UpdaterID, url, extraParams);
                return updater.UpdaterID;
            }
            return LLUUID.Zero;
        }

        public LLUUID AddDynamicTextureData(LLUUID simID, LLUUID primID, string contentType, string data,
                                            string extraParams, int updateTimer)
        {
            return AddDynamicTextureData(simID, primID, contentType, data, extraParams, updateTimer, false, 255);
        }

        public LLUUID AddDynamicTextureData(LLUUID simID, LLUUID primID, string contentType, string data,
                                            string extraParams, int updateTimer, bool SetBlending, byte AlphaValue)
        {
            if (RenderPlugins.ContainsKey(contentType))
            {
                DynamicTextureUpdater updater = new DynamicTextureUpdater();
                updater.SimUUID = simID;
                updater.PrimID = primID;
                updater.ContentType = contentType;
                updater.BodyData = data;
                updater.UpdateTimer = updateTimer;
                updater.UpdaterID = LLUUID.Random();
                updater.Params = extraParams;
                updater.BlendWithOldTexture = SetBlending;
                updater.FrontAlpha = AlphaValue;

                if (!Updaters.ContainsKey(updater.UpdaterID))
                {
                    Updaters.Add(updater.UpdaterID, updater);
                }

                RenderPlugins[contentType].AsyncConvertData(updater.UpdaterID, data, extraParams);
                return updater.UpdaterID;
            }
            return LLUUID.Zero;
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
            public bool BlendWithOldTexture = false;
            public string BodyData;
            public string ContentType;
            public byte FrontAlpha = 255;
            public LLUUID LastAssetID;
            public string Params;
            public LLUUID PrimID;
            public bool SetNewFrontAlpha = false;
            public LLUUID SimUUID;
            public LLUUID UpdaterID;
            public int UpdateTimer;
            public string Url;

            public DynamicTextureUpdater()
            {
                LastAssetID = LLUUID.Zero;
                UpdateTimer = 0;
                BodyData = null;
            }

            /// <summary>
            /// Called once new texture data has been received for this updater.
            /// </summary>
            public void DataReceived(byte[] data, Scene scene)
            {
                SceneObjectPart part = scene.GetSceneObjectPart(PrimID);
                byte[] assetData;
                AssetBase oldAsset = null;

                if (BlendWithOldTexture)
                {
                    LLUUID lastTextureID = part.Shape.Textures.DefaultTexture.TextureID;
                    oldAsset = scene.AssetCache.GetAsset(lastTextureID, true);
                    if (oldAsset != null)
                    {
                        assetData = BlendTextures(data, oldAsset.Data, SetNewFrontAlpha, FrontAlpha);
                    }
                    else
                    {
                        assetData = new byte[data.Length];
                        Array.Copy(data, assetData, data.Length);
                    }
                }
                else
                {
                    assetData = new byte[data.Length];
                    Array.Copy(data, assetData, data.Length);
                }

                // Create a new asset for user
                AssetBase asset = new AssetBase();
                asset.FullID = LLUUID.Random();
                asset.Data = assetData;
                asset.Name = "DynamicImage" + Util.RandomClass.Next(1, 10000);
                asset.Type = 0;
                asset.Description = "dynamic image";
                asset.Local = false;
                asset.Temporary = true;
                scene.AssetCache.AddAsset(asset);

                LastAssetID = asset.FullID;

                // mostly keep the values from before
                LLObject.TextureEntry tmptex = part.Shape.Textures;

                // remove the old asset from the cache
                LLUUID oldID = tmptex.DefaultTexture.TextureID;
                scene.AssetCache.ExpireAsset(oldID);

                tmptex.DefaultTexture.TextureID = asset.FullID;
                // I'm pretty sure we always want to force this to true
                tmptex.DefaultTexture.Fullbright = true;

                part.Shape.Textures = tmptex;
                part.ScheduleFullUpdate();
            }

            private byte[] BlendTextures(byte[] frontImage, byte[] backImage, bool setNewAlpha, byte newAlpha)
            {
                Bitmap image1 = new Bitmap(OpenJPEG.DecodeToImage(frontImage));
                Bitmap image2 = new Bitmap(OpenJPEG.DecodeToImage(backImage));
                if (setNewAlpha)
                {
                    SetAlpha(ref image1, newAlpha);
                }
                Bitmap joint = MergeBitMaps(image1, image2);

                return OpenJPEG.EncodeFromImage(joint, true);
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
