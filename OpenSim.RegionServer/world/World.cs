using System;
using libsecondlife;
using libsecondlife.Packets;
using System.Collections.Generic;
using System.Text;
using System.Reflection;
using System.IO;
using OpenSim.Physics.Manager;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Assets;
using OpenSim.Framework.Terrain;
using OpenSim.Framework.Inventory;
using OpenSim.Assets;

namespace OpenSim.world
{
    public class World : ILocalStorageReceiver
    {
        public object LockPhysicsEngine = new object();
        public Dictionary<libsecondlife.LLUUID, Entity> Entities;
        public float[] LandMap;
        public ScriptEngine Scripts;
        public uint _localNumber = 0;
        private PhysicsScene phyScene;
        private float timeStep = 0.1f;
        private libsecondlife.TerrainManager TerrainManager;
        public ILocalStorage localStorage;
        private Random Rand = new Random();
        private uint _primCount = 702000;
        private int storageCount;
        private Dictionary<uint, SimClient> m_clientThreads;
        private ulong m_regionHandle;
        private string m_regionName;
        private InventoryCache _inventoryCache;
        private AssetCache _assetCache;

        public World(Dictionary<uint, SimClient> clientThreads, ulong regionHandle, string regionName)
        {
            m_clientThreads = clientThreads;
            m_regionHandle = regionHandle;
            m_regionName = regionName;

            OpenSim.Framework.Console.MainConsole.Instance.WriteLine("World.cs - creating new entitities instance");
            Entities = new Dictionary<libsecondlife.LLUUID, Entity>();

            OpenSim.Framework.Console.MainConsole.Instance.WriteLine("World.cs - creating LandMap");
            TerrainManager = new TerrainManager(new SecondLife());
            Avatar.SetupTemplate("avatar-template.dat");
            //	MainConsole.Instance.WriteLine("World.cs - Creating script engine instance");
            // Initialise this only after the world has loaded
            //	Scripts = new ScriptEngine(this);
            Avatar.LoadAnims();
        }

        public InventoryCache InventoryCache
        {
            set
            {
                this._inventoryCache = value;
            }
        }

        public AssetCache AssetCache
        {
            set
            {
                this._assetCache = value;
            }
        }
        public PhysicsScene PhysScene
        {
            set
            {
                this.phyScene = value;
            }
            get
            {
                return (this.phyScene);
            }
        }

        public void Update()
        {
            if (this.phyScene.IsThreaded)
            {
                this.phyScene.GetResults();

            }

            foreach (libsecondlife.LLUUID UUID in Entities.Keys)
            {
                Entities[UUID].addForces();
            }

            lock (this.LockPhysicsEngine)
            {
                this.phyScene.Simulate(timeStep);
            }

            foreach (libsecondlife.LLUUID UUID in Entities.Keys)
            {
                Entities[UUID].update();
            }

            //backup world data
            this.storageCount++;
            if (storageCount > 1200) //set to how often you want to backup 
            {
                this.Backup();
                storageCount = 0;
            }
        }

        public bool LoadStorageDLL(string dllName)
        {
            Assembly pluginAssembly = Assembly.LoadFrom(dllName);
            ILocalStorage store = null;

            foreach (Type pluginType in pluginAssembly.GetTypes())
            {
                if (pluginType.IsPublic)
                {
                    if (!pluginType.IsAbstract)
                    {
                        Type typeInterface = pluginType.GetInterface("ILocalStorage", true);

                        if (typeInterface != null)
                        {
                            ILocalStorage plug = (ILocalStorage)Activator.CreateInstance(pluginAssembly.GetType(pluginType.ToString()));
                            store = plug;
                            break;
                        }

                        typeInterface = null;
                    }
                }
            }
            pluginAssembly = null;
            this.localStorage = store;
            return (store == null);
        }

        public void RegenerateTerrain()
        {
            HeightmapGenHills hills = new HeightmapGenHills();
            this.LandMap = hills.GenerateHeightmap(200, 4.0f, 80.0f, false);
            lock (this.LockPhysicsEngine)
            {
                this.phyScene.SetTerrain(this.LandMap);
            }
            this.localStorage.SaveMap(this.LandMap);

            foreach (SimClient client in m_clientThreads.Values)
            {
                this.SendLayerData(client);
            }

            foreach (libsecondlife.LLUUID UUID in Entities.Keys)
            {
                Entities[UUID].LandRenegerated();
            }
        }

        public void RegenerateTerrain(float[] newMap)
        {

            this.LandMap = newMap;
            lock (this.LockPhysicsEngine)
            {
                this.phyScene.SetTerrain(this.LandMap);
            }
            this.localStorage.SaveMap(this.LandMap);

            foreach (SimClient client in m_clientThreads.Values)
            {
                this.SendLayerData(client);
            }

            foreach (libsecondlife.LLUUID UUID in Entities.Keys)
            {
                Entities[UUID].LandRenegerated();
            }
        }

        public void RegenerateTerrain(bool changes, int pointx, int pointy)
        {
            if (changes)
            {
                lock (this.LockPhysicsEngine)
                {
                    this.phyScene.SetTerrain(this.LandMap);
                }
                this.localStorage.SaveMap(this.LandMap);

                foreach (SimClient client in m_clientThreads.Values)
                {
                    this.SendLayerData(pointx , pointy , client);
                }
            }
        }

        public void LoadWorldMap()
        {
            LandMap = this.localStorage.LoadWorld();
        }

        public void LoadPrimsFromStorage()
        {
            OpenSim.Framework.Console.MainConsole.Instance.WriteLine("World.cs: LoadPrimsFromStorage() - Loading primitives");
            this.localStorage.LoadPrimitives(this);
        }

        public void PrimFromStorage(PrimData prim)
        {
            if (prim.LocalID >= this._primCount)
            {
                _primCount = prim.LocalID + 1;
            }
            OpenSim.Framework.Console.MainConsole.Instance.WriteLine("World.cs: PrimFromStorage() - Reloading prim (localId " + prim.LocalID + " ) from storage");
            Primitive nPrim = new Primitive(m_clientThreads, m_regionHandle, this);
            nPrim.CreateFromStorage(prim);
            this.Entities.Add(nPrim.uuid, nPrim);
        }

        public void Close()
        {
            this.localStorage.ShutDown();
        }

        public void SendLayerData(SimClient RemoteClient)
        {
            int[] patches = new int[4];

            for (int y = 0; y < 16; y++)
            {
                for (int x = 0; x < 16; x = x + 4)
                {
                    patches[0] = x + 0 + y * 16;
                    patches[1] = x + 1 + y * 16;
                    patches[2] = x + 2 + y * 16;
                    patches[3] = x + 3 + y * 16;

                    Packet layerpack = TerrainManager.CreateLandPacket(LandMap, patches);
                    RemoteClient.OutPacket(layerpack);
                }
            }
        }

        public void SendLayerData(int px, int py, SimClient RemoteClient)
        {
            int[] patches = new int[1];
            int patchx, patchy;
            patchx = px / 16;
           /* if (patchx > 12)
            {
                patchx = 12;
            }*/
            patchy = py / 16;

            patches[0] = patchx + 0 + patchy * 16;
            //patches[1] = patchx + 1 + patchy * 16;
            //patches[2] = patchx + 2 + patchy * 16;
            //patches[3] = patchx + 3 + patchy * 16;

            Packet layerpack = TerrainManager.CreateLandPacket(LandMap, patches);
            RemoteClient.OutPacket(layerpack);

        }
        public void GetInitialPrims(SimClient RemoteClient)
        {
            foreach (libsecondlife.LLUUID UUID in Entities.Keys)
            {
                if (Entities[UUID].ToString() == "OpenSim.world.Primitive")
                {
                    ((OpenSim.world.Primitive)Entities[UUID]).UpdateClient(RemoteClient);
                }
            }
        }

        public void AddViewerAgent(SimClient AgentClient)
        {
            OpenSim.Framework.Console.MainConsole.Instance.WriteLine("World.cs:AddViewerAgent() - Creating new avatar for remote viewer agent");
            Avatar NewAvatar = new Avatar(AgentClient, this, m_regionName, m_clientThreads, m_regionHandle);
            OpenSim.Framework.Console.MainConsole.Instance.WriteLine("World.cs:AddViewerAgent() - Adding new avatar to world");
            OpenSim.Framework.Console.MainConsole.Instance.WriteLine("World.cs:AddViewerAgent() - Starting RegionHandshake ");
            NewAvatar.SendRegionHandshake(this);
            PhysicsVector pVec = new PhysicsVector(NewAvatar.position.X, NewAvatar.position.Y, NewAvatar.position.Z);
            lock (this.LockPhysicsEngine)
            {
                NewAvatar.PhysActor = this.phyScene.AddAvatar(pVec);
            }
            this.Entities.Add(AgentClient.AgentID, NewAvatar);
        }

        public void AddNewPrim(ObjectAddPacket addPacket, SimClient AgentClient)
        {
            OpenSim.Framework.Console.MainConsole.Instance.WriteLine("World.cs: AddNewPrim() - Creating new prim");
            Primitive prim = new Primitive(m_clientThreads, m_regionHandle, this);
            prim.CreateFromPacket(addPacket, AgentClient.AgentID, this._primCount);
            PhysicsVector pVec = new PhysicsVector(prim.position.X, prim.position.Y, prim.position.Z);
            PhysicsVector pSize = new PhysicsVector(0.255f, 0.255f, 0.255f);
            if (OpenSim.world.Avatar.PhysicsEngineFlying)
            {
                lock (this.LockPhysicsEngine)
                {
                    prim.PhysActor = this.phyScene.AddPrim(pVec, pSize);
                }
            }
            //prim.PhysicsEnabled = true;
            this.Entities.Add(prim.uuid, prim);
            this._primCount++;
        }

        public bool DeRezObject(SimClient simClient, Packet packet)
        {
            DeRezObjectPacket DeRezPacket = (DeRezObjectPacket)packet;
           // Console.WriteLine(DeRezPacket);
            //Needs to delete object from physics at a later date
            if (DeRezPacket.AgentBlock.DestinationID == LLUUID.Zero)
            {
                libsecondlife.LLUUID[] DeRezEnts;
                DeRezEnts = new libsecondlife.LLUUID[DeRezPacket.ObjectData.Length];
                int i = 0;
                foreach (DeRezObjectPacket.ObjectDataBlock Data in DeRezPacket.ObjectData)
                {

                    //OpenSim.Framework.Console.MainConsole.Instance.WriteLine("LocalID:" + Data.ObjectLocalID.ToString());
                    foreach (Entity ent in this.Entities.Values)
                    {
                        if (ent.localid == Data.ObjectLocalID)
                        {
                            DeRezEnts[i++] = ent.uuid;
                            this.localStorage.RemovePrim(ent.uuid);
                            KillObjectPacket kill = new KillObjectPacket();
                            kill.ObjectData = new KillObjectPacket.ObjectDataBlock[1];
                            kill.ObjectData[0] = new KillObjectPacket.ObjectDataBlock();
                            kill.ObjectData[0].ID = ent.localid;
                            foreach (SimClient client in m_clientThreads.Values)
                            {
                                client.OutPacket(kill);
                            }
                            //Uncommenting this means an old UUID will be re-used, thus crashing the asset server
                            //Uncomment when prim/object UUIDs are random or such
                            //2007-03-22 - Randomskk
                            //this._primCount--;
                            OpenSim.Framework.Console.MainConsole.Instance.WriteLine("Deleted UUID " + ent.uuid);
                        }
                    }
                }
                foreach (libsecondlife.LLUUID uuid in DeRezEnts)
                {
                    lock (Entities)
                    {
                        Entities.Remove(uuid);
                    }
                }
            }
            else
            {
                foreach (DeRezObjectPacket.ObjectDataBlock Data in DeRezPacket.ObjectData)
                {
                    Entity selectedEnt = null;
                    //OpenSim.Framework.Console.MainConsole.Instance.WriteLine("LocalID:" + Data.ObjectLocalID.ToString());
                    foreach (Entity ent in this.Entities.Values)
                    {
                        if (ent.localid == Data.ObjectLocalID)
                        {
                            AssetBase primAsset = new AssetBase();
                            primAsset.FullID = LLUUID.Random();//DeRezPacket.AgentBlock.TransactionID.Combine(LLUUID.Zero); //should be combining with securesessionid
                            primAsset.InvType = 6;
                            primAsset.Type = 6;
                            primAsset.Name = "Prim";
                            primAsset.Description = "";
                            primAsset.Data = ((Primitive)ent).GetByteArray();
                            this._assetCache.AddAsset(primAsset);
                            this._inventoryCache.AddNewInventoryItem(simClient, DeRezPacket.AgentBlock.DestinationID, primAsset);
                            selectedEnt = ent;
                            break;
                        }
                    }
                    if (selectedEnt != null)
                    {
                        this.localStorage.RemovePrim(selectedEnt.uuid);
                        KillObjectPacket kill = new KillObjectPacket();
                        kill.ObjectData = new KillObjectPacket.ObjectDataBlock[1];
                        kill.ObjectData[0] = new KillObjectPacket.ObjectDataBlock();
                        kill.ObjectData[0].ID = selectedEnt.localid;
                        foreach (SimClient client in m_clientThreads.Values)
                        {
                            client.OutPacket(kill);
                        }
                        lock (Entities)
                        {
                            Entities.Remove(selectedEnt.uuid);
                        }
                    }
                }
            }
            return true;
        }

        public bool Backup()
        {

            OpenSim.Framework.Console.MainConsole.Instance.WriteLine("World.cs: Backup() - Backing up Primitives");
            foreach (libsecondlife.LLUUID UUID in Entities.Keys)
            {
                Entities[UUID].BackUp();
            }
            return true;
        }

        #region Packet Handlers
        public bool ModifyTerrain(SimClient simClient, Packet packet)
        {
            ModifyLandPacket modify = (ModifyLandPacket)packet;
           
            switch (modify.ModifyBlock.Action)
            {
                case 1:
                    // raise terrain
                    if (modify.ParcelData.Length > 0)
                    {
                        int mody = (int)modify.ParcelData[0].North;
                        int modx = (int)modify.ParcelData[0].West;
                        lock (LandMap)
                        {
                            LandMap[(mody * 256) + modx - 1] += 0.05f;
                            LandMap[(mody * 256) + modx] += 0.1f;
                            LandMap[(mody * 256) + modx + 1] += 0.05f;
                            LandMap[((mody + 1) * 256) + modx] += 0.05f;
                            LandMap[((mody - 1) * 256) + modx] += 0.05f;
                        }
                        RegenerateTerrain(true, modx, mody);
                    }
                    break;
                case 2:
                    //lower terrain
                    if (modify.ParcelData.Length > 0)
                    {
                        int mody = (int)modify.ParcelData[0].North;
                        int modx = (int)modify.ParcelData[0].West;
                        lock (LandMap)
                        {
                            LandMap[(mody * 256) + modx - 1] -= 0.05f;
                            LandMap[(mody * 256) + modx] -= 0.1f;
                            LandMap[(mody * 256) + modx + 1] -= 0.05f;
                            LandMap[((mody + 1) * 256) + modx] -= 0.05f;
                            LandMap[((mody - 1) * 256) + modx] -= 0.05f;
                        }
                        RegenerateTerrain(true, modx, mody);
                    }
                    break;
            }
            return true;
        }

        public bool SimChat(SimClient simClient, Packet packet)
        {
            System.Text.Encoding enc = System.Text.Encoding.ASCII;
            ChatFromViewerPacket inchatpack = (ChatFromViewerPacket)packet;
            if (Helpers.FieldToString(inchatpack.ChatData.Message) == "")
            {
                //empty message so don't bother with it
                return true;
            }

            libsecondlife.Packets.ChatFromSimulatorPacket reply = new ChatFromSimulatorPacket();
            reply.ChatData.Audible = 1;
            reply.ChatData.Message = inchatpack.ChatData.Message;
            reply.ChatData.ChatType = 1;
            reply.ChatData.SourceType = 1;
            reply.ChatData.Position = simClient.ClientAvatar.position;
            reply.ChatData.FromName = enc.GetBytes(simClient.ClientAvatar.firstname + " " + simClient.ClientAvatar.lastname + "\0");
            reply.ChatData.OwnerID = simClient.AgentID;
            reply.ChatData.SourceID = simClient.AgentID;
            foreach (SimClient client in m_clientThreads.Values)
            {
                client.OutPacket(reply);
            }
            return true;
        }

        public bool RezObject(SimClient simClient, Packet packet)
        {
            RezObjectPacket rezPacket = (RezObjectPacket)packet;
            AgentInventory inven = this._inventoryCache.GetAgentsInventory(simClient.AgentID);
            if (inven != null)
            {
                if (inven.InventoryItems.ContainsKey(rezPacket.InventoryData.ItemID))
                {
                    AssetBase asset = this._assetCache.GetAsset(inven.InventoryItems[rezPacket.InventoryData.ItemID].AssetID);
                    if (asset != null)
                    {
                        PrimData primd = new PrimData(asset.Data);
                        Primitive nPrim = new Primitive(m_clientThreads, m_regionHandle, this);
                        nPrim.CreateFromStorage(primd, rezPacket.RezData.RayEnd, this._primCount, true);
                        this.Entities.Add(nPrim.uuid, nPrim);
                        this._primCount++;
                        this._inventoryCache.DeleteInventoryItem(simClient, rezPacket.InventoryData.ItemID);
                    }
                }
            }
            return true;
        }

        #endregion
    }
}
