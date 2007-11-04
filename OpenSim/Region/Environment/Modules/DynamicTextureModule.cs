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
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS AS IS AND ANY
* EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
* WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
* DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
* DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
* (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
* LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
* ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
* (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
* SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
* 
*/

using System;
using System.Collections.Generic;
using libsecondlife;
using Nini.Config;
using OpenSim.Framework;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Scenes;

namespace OpenSim.Region.Environment.Modules
{
    public class DynamicTextureModule : IRegionModule, IDynamicTextureManager
    {
        private Dictionary<LLUUID, Scene> RegisteredScenes = new Dictionary<LLUUID, Scene>();

        private Dictionary<string, IDynamicTextureRender> RenderPlugins =
            new Dictionary<string, IDynamicTextureRender>();

        private Dictionary<LLUUID, DynamicTextureUpdater> Updaters = new Dictionary<LLUUID, DynamicTextureUpdater>();

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

        public void RegisterRender(string handleType, IDynamicTextureRender render)
        {
            if (!RenderPlugins.ContainsKey(handleType))
            {
                RenderPlugins.Add(handleType, render);
            }
        }

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

                if (!Updaters.ContainsKey(updater.UpdaterID))
                {
                    Updaters.Add(updater.UpdaterID, updater);
                }

                RenderPlugins[contentType].AsyncConvertData(updater.UpdaterID, data, extraParams);
                return updater.UpdaterID;
            }
            return LLUUID.Zero;
        }

        public class DynamicTextureUpdater
        {
            public LLUUID SimUUID;
            public LLUUID UpdaterID;
            public string ContentType;
            public string Url;
            public string BodyData;
            public LLUUID PrimID;
            public int UpdateTimer;
            public LLUUID LastAssetID;
            public string Params;

            public DynamicTextureUpdater()
            {
                LastAssetID = LLUUID.Zero;
                UpdateTimer = 0;
                BodyData = null;
            }

            public void DataReceived(byte[] data, Scene scene)
            {
                //TODO delete the last asset(data), if it was a dynamic texture
                byte[] assetData = new byte[data.Length];
                Array.Copy(data, assetData, data.Length);
                AssetBase asset = new AssetBase();
                asset.FullID = LLUUID.Random();
                asset.Data = assetData;
                asset.Name = "DynamicImage" + Util.RandomClass.Next(1, 10000);
                asset.Type = 0;
                asset.Description = "dynamic image";
                asset.Local = false;
                asset.Temporary = false;
                scene.AssetCache.AddAsset(asset);

                LastAssetID = asset.FullID;

                SceneObjectPart part = scene.GetSceneObjectPart(PrimID);
                part.Shape.TextureEntry = new LLObject.TextureEntry(asset.FullID).ToBytes();
                part.ScheduleFullUpdate();
            }
        }
    }
}