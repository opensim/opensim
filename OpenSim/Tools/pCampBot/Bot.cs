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
using System.Text;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Timers;
using log4net;
using OpenMetaverse;
using OpenMetaverse.Assets;
using Nini.Config;
using OpenSim.Framework;
using OpenSim.Framework.Console;
using pCampBot.Interfaces;
using Timer = System.Timers.Timer;
using PermissionMask = OpenSim.Framework.PermissionMask;

namespace pCampBot
{
    public enum ConnectionState
    {
        Disconnected,
        Connecting,
        Connected,
        Disconnecting
    }

    public class Bot
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public delegate void AnEvent(Bot callbot, EventType someevent); // event delegate for bot events

        /// <summary>
        /// Bot manager.
        /// </summary>
        public BotManager Manager { get; private set; }

        /// <summary>
        /// Bot config, passed from BotManager.
        /// </summary>
        private IConfig startupConfig;

        /// <summary>
        /// Behaviours implemented by this bot.
        /// </summary>
        /// <remarks>
        /// Lock this list before manipulating it.
        /// </remarks>
        public List<IBehaviour> Behaviours { get; private set; }

        /// <summary>
        /// Objects that the bot has discovered.
        /// </summary>
        /// <remarks>
        /// Returns a list copy.  Inserting new objects manually will have no effect.
        /// </remarks>
        public Dictionary<UUID, Primitive> Objects
        {
            get
            {
                lock (m_objects)
                    return new Dictionary<UUID, Primitive>(m_objects);
            }
        }
        private Dictionary<UUID, Primitive> m_objects = new Dictionary<UUID, Primitive>();

        /// <summary>
        /// Is this bot connected to the grid?
        /// </summary>
        public ConnectionState ConnectionState { get; private set; }

        public string FirstName { get; private set; }
        public string LastName { get; private set; }
        public string Name { get; private set; }
        public string Password { get; private set; }
        public string LoginUri { get; private set; }
        public string saveDir;
        public string wear;

        public event AnEvent OnConnected;
        public event AnEvent OnDisconnected;

        /// <summary>
        /// Keep a track of the continuously acting thread so that we can abort it.
        /// </summary>
        private Thread m_actionThread;

        protected List<uint> objectIDs = new List<uint>();

        /// <summary>
        /// Random number generator.
        /// </summary>
        public Random Random { get; private set; }

        /// <summary>
        /// New instance of a SecondLife client
        /// </summary>
        public GridClient Client { get; private set; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="bm"></param>
        /// <param name="behaviours">Behaviours for this bot to perform</param>
        /// <param name="firstName"></param>
        /// <param name="lastName"></param>
        /// <param name="password"></param>
        /// <param name="loginUri"></param>
        /// <param name="behaviours"></param>
        public Bot(
            BotManager bm, List<IBehaviour> behaviours,
            string firstName, string lastName, string password, string loginUri)
        {
            ConnectionState = ConnectionState.Disconnected;

            behaviours.ForEach(b => b.Initialize(this));
            
            Client = new GridClient();

            Random = new Random(Environment.TickCount);// We do stuff randomly here
            FirstName = firstName;
            LastName = lastName;
            Name = string.Format("{0} {1}", FirstName, LastName);
            Password = password;
            LoginUri = loginUri;

            Manager = bm;
            startupConfig = bm.Config;
            readconfig();

            Behaviours = behaviours;
        }

        //We do our actions here.  This is where one would
        //add additional steps and/or things the bot should do
        private void Action()
        {
            while (true)
                lock (Behaviours)
                    Behaviours.ForEach(
                        b =>
                        {
                            Thread.Sleep(Random.Next(3000, 10000));
                        
                            // m_log.DebugFormat("[pCAMPBOT]: For {0} performing action {1}", Name, b.GetType());
                            b.Action();
                        }
                    );
        }

        /// <summary>
        /// Read the Nini config and initialize
        /// </summary>
        public void readconfig()
        {
            wear = startupConfig.GetString("wear", "no");
        }

        /// <summary>
        /// Tells LibSecondLife to logout and disconnect.  Raises the disconnect events once it finishes.
        /// </summary>
        public void shutdown()
        {
            ConnectionState = ConnectionState.Disconnecting;

            if (m_actionThread != null)
                m_actionThread.Abort();

            Client.Network.Logout();
        }

        /// <summary>
        /// This is the bot startup loop.
        /// </summary>
        public void startup()
        {
            Client.Settings.LOGIN_SERVER = LoginUri;
            Client.Settings.ALWAYS_DECODE_OBJECTS = false;
            Client.Settings.AVATAR_TRACKING = false;
            Client.Settings.OBJECT_TRACKING = false;
            Client.Settings.SEND_AGENT_THROTTLE = true;
            Client.Settings.SEND_PINGS = true;
            Client.Settings.STORE_LAND_PATCHES = false;
            Client.Settings.USE_ASSET_CACHE = false;
            Client.Settings.MULTIPLE_SIMS = true;
            Client.Throttle.Asset = 100000;
            Client.Throttle.Land = 100000;
            Client.Throttle.Task = 100000;
            Client.Throttle.Texture = 100000;
            Client.Throttle.Wind = 100000;
            Client.Throttle.Total = 400000;
            Client.Network.LoginProgress += this.Network_LoginProgress;
            Client.Network.SimConnected += this.Network_SimConnected;
            Client.Network.Disconnected += this.Network_OnDisconnected;
            Client.Objects.ObjectUpdate += Objects_NewPrim;

            ConnectionState = ConnectionState.Connecting;

            if (Client.Network.Login(FirstName, LastName, Password, "pCampBot", "Your name"))
            {
                ConnectionState = ConnectionState.Connected;

                Thread.Sleep(Random.Next(1000, 10000));
                m_actionThread = new Thread(Action);
                m_actionThread.Start();

//                    OnConnected(this, EventType.CONNECTED);
                if (wear == "save")
                {
                    Client.Appearance.SetPreviousAppearance();
                    SaveDefaultAppearance();
                }
                else if (wear != "no")
                {
                    MakeDefaultAppearance(wear);
                }

                // Extract nearby region information.
                Client.Grid.GridRegion += Manager.Grid_GridRegion;
                uint xUint, yUint;
                Utils.LongToUInts(Client.Network.CurrentSim.Handle, out xUint, out yUint);
                ushort minX, minY, maxX, maxY;
                minX = (ushort)Math.Min(0, xUint - 5);
                minY = (ushort)Math.Min(0, yUint - 5);
                maxX = (ushort)(xUint + 5);
                maxY = (ushort)(yUint + 5);
                Client.Grid.RequestMapBlocks(GridLayerType.Terrain, minX, minY, maxX, maxY, false);
            }
            else
            {
                ConnectionState = ConnectionState.Disconnected;

                m_log.ErrorFormat(
                    "{0} {1} cannot login: {2}", FirstName, LastName, Client.Network.LoginMessage);

                if (OnDisconnected != null)
                {
                    OnDisconnected(this, EventType.DISCONNECTED);
                }
            }
        }

        public void SaveDefaultAppearance()
        {
            saveDir = "MyAppearance/" + FirstName + "_" + LastName;
            if (!Directory.Exists(saveDir))
            {
                Directory.CreateDirectory(saveDir);
            }

            Array wtypes = Enum.GetValues(typeof(WearableType));
            foreach (WearableType wtype in wtypes)
            {
                UUID wearable = Client.Appearance.GetWearableAsset(wtype);
                if (wearable != UUID.Zero)
                {
                    Client.Assets.RequestAsset(wearable, AssetType.Clothing, false, Asset_ReceivedCallback);
                    Client.Assets.RequestAsset(wearable, AssetType.Bodypart, false, Asset_ReceivedCallback);
                }
            }
        }

        public void SaveAsset(AssetWearable asset)
        {
            if (asset != null)
            {
                try
                {
                    if (asset.Decode())
                    {
                        File.WriteAllBytes(Path.Combine(saveDir, String.Format("{1}.{0}",
                        asset.AssetType.ToString().ToLower(),
                        asset.WearableType)), asset.AssetData);
                    }
                    else
                    {
                        m_log.WarnFormat("Failed to decode {0} asset {1}", asset.AssetType, asset.AssetID);
                    }
                }
                catch (Exception e)
                {
                    m_log.ErrorFormat("Exception: {0}{1}", e.Message, e.StackTrace);
                }
            }
        }

        public WearableType GetWearableType(string path)
        {
            string type = ((((path.Split('/'))[2]).Split('.'))[0]).Trim();
            switch (type)
            {
                case "Eyes":
                    return WearableType.Eyes;
                case "Hair":
                    return WearableType.Hair;
                case "Pants":
                    return WearableType.Pants;
                case "Shape":
                    return WearableType.Shape;
                case "Shirt":
                    return WearableType.Shirt;
                case "Skin":
                    return WearableType.Skin;
                default:
                    return WearableType.Shape;
            }
        }

        public void MakeDefaultAppearance(string wear)
        {
            try
            {
                if (wear == "yes")
                {
                    //TODO: Implement random outfit picking
                    m_log.DebugFormat("Picks a random outfit. Not yet implemented.");
                }
                else if (wear != "save")
                    saveDir = "MyAppearance/" + wear;
                saveDir = saveDir + "/";

                string[] clothing = Directory.GetFiles(saveDir, "*.clothing", SearchOption.TopDirectoryOnly);
                string[] bodyparts = Directory.GetFiles(saveDir, "*.bodypart", SearchOption.TopDirectoryOnly);
                InventoryFolder clothfolder = FindClothingFolder();
                UUID transid = UUID.Random();
                List<InventoryBase> listwearables = new List<InventoryBase>();
                
                for (int i = 0; i < clothing.Length; i++)
                {
                    UUID assetID = UUID.Random();
                    AssetClothing asset = new AssetClothing(assetID, File.ReadAllBytes(clothing[i]));
                    asset.Decode();
                    asset.Owner = Client.Self.AgentID;
                    asset.WearableType = GetWearableType(clothing[i]);
                    asset.Encode();
                    transid = Client.Assets.RequestUpload(asset,true);
                    Client.Inventory.RequestCreateItem(clothfolder.UUID, "MyClothing" + i.ToString(), "MyClothing", AssetType.Clothing,
                         transid, InventoryType.Wearable, asset.WearableType, (OpenMetaverse.PermissionMask)PermissionMask.All, delegate(bool success, InventoryItem item)
                    {
                        if (success)
                        {
                            listwearables.Add(item);
                        }
                        else
                        {
                            m_log.WarnFormat("Failed to create item {0}", item.Name);
                        }
                    }
                    );
                }

                for (int i = 0; i < bodyparts.Length; i++)
                {
                    UUID assetID = UUID.Random();
                    AssetBodypart asset = new AssetBodypart(assetID, File.ReadAllBytes(bodyparts[i]));
                    asset.Decode();
                    asset.Owner = Client.Self.AgentID;
                    asset.WearableType = GetWearableType(bodyparts[i]);
                    asset.Encode();
                    transid = Client.Assets.RequestUpload(asset,true);
                    Client.Inventory.RequestCreateItem(clothfolder.UUID, "MyBodyPart" + i.ToString(), "MyBodyPart", AssetType.Bodypart,
                         transid, InventoryType.Wearable, asset.WearableType, (OpenMetaverse.PermissionMask)PermissionMask.All, delegate(bool success, InventoryItem item)
                    {
                        if (success)
                        {
                            listwearables.Add(item);
                        }
                        else
                        {
                            m_log.WarnFormat("Failed to create item {0}", item.Name);
                        }
                    }
                    );
                }

                Thread.Sleep(1000);

                if (listwearables == null || listwearables.Count == 0)
                {
                    m_log.DebugFormat("Nothing to send on this folder!");
                }
                else
                {
                    m_log.DebugFormat("Sending {0} wearables...", listwearables.Count);
                    Client.Appearance.WearOutfit(listwearables, false);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        public InventoryFolder FindClothingFolder()
        {
            UUID rootfolder = Client.Inventory.Store.RootFolder.UUID;
            List<InventoryBase> listfolders = Client.Inventory.Store.GetContents(rootfolder);
            InventoryFolder clothfolder = new InventoryFolder(UUID.Random());
            foreach (InventoryBase folder in listfolders)
            {
                if (folder.Name == "Clothing")
                {
                    clothfolder = (InventoryFolder)folder;
                    break;
                }
            }
            return clothfolder;
        }

        public void Network_LoginProgress(object sender, LoginProgressEventArgs args)
        {
            m_log.DebugFormat("[BOT]: Bot {0} {1} in Network_LoginProcess", Name, args.Status);

            if (args.Status == LoginStatus.Success)
            {
                if (OnConnected != null)
                {
                    OnConnected(this, EventType.CONNECTED);
                }
            }
        }

        public void Network_SimConnected(object sender, SimConnectedEventArgs args)
        {
            m_log.DebugFormat(
                "[BOT]: Bot {0} connected to {1} at {2}", Name, args.Simulator.Name, args.Simulator.IPEndPoint);
        }

        public void Network_OnDisconnected(object sender, DisconnectedEventArgs args)
        {
            ConnectionState = ConnectionState.Disconnected;

            m_log.DebugFormat(
                "[BOT]: Bot {0} disconnected reason {1}, message {2}", Name, args.Reason, args.Message);

//            m_log.ErrorFormat("Fired Network_OnDisconnected");

//           if (
//               (args.Reason == NetworkManager.DisconnectType.SimShutdown
//                    || args.Reason == NetworkManager.DisconnectType.NetworkTimeout)
//               && OnDisconnected != null)

           if (
               (args.Reason == NetworkManager.DisconnectType.ClientInitiated
                    || args.Reason == NetworkManager.DisconnectType.ServerInitiated
                    || args.Reason == NetworkManager.DisconnectType.NetworkTimeout)
               && OnDisconnected != null)
//            if (OnDisconnected != null)
            {
                OnDisconnected(this, EventType.DISCONNECTED);
            }
        }

        public void Objects_NewPrim(object sender, PrimEventArgs args)
        {
//            if (Name.EndsWith("4"))
//                throw new Exception("Aaargh");

            Primitive prim = args.Prim;

            if (prim != null)
            {
                lock (m_objects)
                    m_objects[prim.ID] = prim;

                if (prim.Textures != null)
                {
                    if (prim.Textures.DefaultTexture.TextureID != UUID.Zero)
                    {
                        GetTexture(prim.Textures.DefaultTexture.TextureID);
                    }

                    for (int i = 0; i < prim.Textures.FaceTextures.Length; i++)
                    {
                        Primitive.TextureEntryFace face = prim.Textures.FaceTextures[i];

                        if (face != null)
                        {
                            UUID textureID = prim.Textures.FaceTextures[i].TextureID;

                            if (textureID != UUID.Zero)
                                GetTexture(textureID);
                        }
                    }
                }

                if (prim.Sculpt != null && prim.Sculpt.SculptTexture != UUID.Zero)
                    GetTexture(prim.Sculpt.SculptTexture);
            }
        }

        private void GetTexture(UUID textureID)
        {
            lock (Manager.AssetsReceived)
            {
                // Don't request assets more than once.
                if (Manager.AssetsReceived.ContainsKey(textureID))
                    return;

                Manager.AssetsReceived[textureID] = false;
                Client.Assets.RequestImage(textureID, ImageType.Normal, Asset_TextureCallback_Texture);
            }
        }
        
        public void Asset_TextureCallback_Texture(TextureRequestState state, AssetTexture assetTexture)
        {
            //TODO: Implement texture saving and applying
        }
        
        public void Asset_ReceivedCallback(AssetDownload transfer, Asset asset)
        {
            lock (Manager.AssetsReceived)
                Manager.AssetsReceived[asset.AssetID] = true;

//            if (wear == "save")
//            {
//                SaveAsset((AssetWearable) asset);
//            }
        }
    }
}
