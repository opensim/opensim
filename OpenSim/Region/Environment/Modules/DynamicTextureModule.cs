using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.IO;
using System.Collections.Generic;
using libsecondlife;
using OpenSim.Region.Environment.Scenes;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Utilities;
using OpenSim.Framework.Console;
using OpenSim.Framework.Types;

namespace OpenSim.Region.Environment.Modules
{
    public class DynamicTextureModule :IRegionModule, IDynamicTextureManager
    {
        private Dictionary<LLUUID, Scene> RegisteredScenes = new Dictionary<LLUUID, Scene>();
        private Dictionary<string, IDynamicTextureRender> RenderPlugins= new Dictionary<string, IDynamicTextureRender>();
        private Dictionary<LLUUID, DynamicTextureUpdater> Updaters = new Dictionary<LLUUID, DynamicTextureUpdater>();

        public void Initialise(Scene scene)
        {
            if (!RegisteredScenes.ContainsKey(scene.RegionInfo.SimUUID))
            {
                RegisteredScenes.Add(scene.RegionInfo.SimUUID, scene);
                scene.RegisterModuleInterface<IDynamicTextureManager>(this);
            }
        }

        public void PostInitialise()
        {
           
        }

        public void CloseDown()
        {
        }

        public string GetName()
        {
            return "DynamicTextureModule";
        }

        public bool IsSharedModule()
        {
            return true;
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

        public LLUUID AddDynamicTextureURL(LLUUID simID, LLUUID primID, string contentType, string url, string extraParams, int updateTimer)
        {
            System.Console.WriteLine("dynamic texture being created: " + url + " of type " + contentType);
            if (this.RenderPlugins.ContainsKey(contentType))
            {
                DynamicTextureUpdater updater = new DynamicTextureUpdater();
                updater.SimUUID = simID;
                updater.PrimID = primID;
                updater.ContentType = contentType;
                updater.Url = url;
                updater.UpdateTimer = updateTimer;
                updater.UpdaterID = LLUUID.Random();
                updater.Params = extraParams;

                if (!this.Updaters.ContainsKey(updater.UpdaterID))
                {
                    Updaters.Add(updater.UpdaterID, updater);
                }

                RenderPlugins[contentType].AsyncConvertUrl(updater.UpdaterID, url, extraParams);
                return updater.UpdaterID;
            }
            return LLUUID.Zero;
        }

        public LLUUID AddDynamicTextureData(LLUUID simID, LLUUID primID, string contentType, string data, string extraParams, int updateTimer)
        {
            if (this.RenderPlugins.ContainsKey(contentType))
            {
                DynamicTextureUpdater updater = new DynamicTextureUpdater();
                updater.SimUUID = simID;
                updater.PrimID = primID;
                updater.ContentType = contentType;
                updater.BodyData = data;
                updater.UpdateTimer = updateTimer;
                updater.UpdaterID = LLUUID.Random();
                updater.Params = extraParams;

                if (!this.Updaters.ContainsKey(updater.UpdaterID))
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
 
                AssetBase asset = new AssetBase();
                asset.FullID = LLUUID.Random();
                asset.Data = data;
                asset.Name = "DynamicImage" + Util.RandomClass.Next(1, 10000);
                asset.Type = 0;
                scene.commsManager.AssetCache.AddAsset(asset);

                this.LastAssetID = asset.FullID;

                SceneObjectPart part = scene.GetSceneObjectPart(PrimID);
                part.Shape.TextureEntry = new LLObject.TextureEntry(asset.FullID).ToBytes();
                part.ScheduleFullUpdate();
            }
        }
    }
}
