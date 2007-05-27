using System;
using libsecondlife;
using libsecondlife.Packets;
using System.Collections.Generic;
using System.Text;
using System.Reflection;
using System.IO;
using System.Threading;
using OpenSim.Physics.Manager;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Types;
using OpenSim.Framework.Terrain;
using OpenSim.Framework.Inventory;
using OpenSim.Assets;
using OpenSim.RegionServer.world.scripting;
using OpenSim.Terrain;

namespace OpenSim.world
{
    public class WorldBase
    {
        public Dictionary<libsecondlife.LLUUID, Entity> Entities;
        protected Dictionary<uint, ClientView> m_clientThreads;
        protected ulong m_regionHandle;
        protected string m_regionName;
        protected InventoryCache _inventoryCache;
        protected AssetCache _assetCache;
        public RegionInfo m_regInfo;

        public TerrainEngine Terrain; //TODO: Replace TerrainManager with this.
        protected libsecondlife.TerrainManager TerrainManager; // To be referenced via TerrainEngine

        #region Properties
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
        #endregion

        #region Constructors
        public WorldBase()
        {

        }
        #endregion

        #region Setup Methods
        /// <summary>
        /// Register Packet handler Methods with the packet server (which will register them with the SimClient)
        /// </summary>
        /// <param name="packetServer"></param>
        public virtual void RegisterPacketHandlers(PacketServer packetServer)
        {

        }
        #endregion

        #region Update Methods
        /// <summary>
        /// Normally called once every frame/tick to let the world preform anything required (like running the physics simulation)
        /// </summary>
        public virtual void Update()
        {

        }
        #endregion

        #region Terrain Methods

        /// <summary>
        /// Loads the World heightmap
        /// </summary>
        public virtual void LoadWorldMap()
        {

        }

        /// <summary>
        /// Send the region heightmap to the client
        /// </summary>
        /// <param name="RemoteClient">Client to send to</param>
        public virtual void SendLayerData(ClientView RemoteClient)
        {
            try
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

                        Packet layerpack = TerrainManager.CreateLandPacket(Terrain.getHeights1D(), patches);
                        RemoteClient.OutPacket(layerpack);
                    }
                }
            }
            catch (Exception e)
            {
                OpenSim.Framework.Console.MainConsole.Instance.WriteLine(OpenSim.Framework.Console.LogPriority.MEDIUM, "World.cs: SendLayerData() - Failed with exception " + e.ToString());
            }
        }

        /// <summary>
        /// Sends a specified patch to a client
        /// </summary>
        /// <param name="px">Patch coordinate (x) 0..16</param>
        /// <param name="py">Patch coordinate (y) 0..16</param>
        /// <param name="RemoteClient">The client to send to</param>
        public void SendLayerData(int px, int py, ClientView RemoteClient)
        {
            try
            {
                int[] patches = new int[1];
                int patchx, patchy;
                patchx = px / 16;
                patchy = py / 16;

                patches[0] = patchx + 0 + patchy * 16;

                Packet layerpack = TerrainManager.CreateLandPacket(Terrain.getHeights1D(), patches);
                RemoteClient.OutPacket(layerpack);
            }
            catch (Exception e)
            {
                OpenSim.Framework.Console.MainConsole.Instance.WriteLine(OpenSim.Framework.Console.LogPriority.MEDIUM, "World.cs: SendLayerData() - Failed with exception " + e.ToString());
            }
        }
        #endregion

        #region Add/Remove Agent/Avatar
        /// <summary>
        /// Add a new Agent's avatar
        /// </summary>
        /// <param name="agentClient"></param>
        public virtual Avatar AddViewerAgent(ClientView agentClient)
        {
            return null;
        }

        /// <summary>
        /// Remove a Agent's avatar
        /// </summary>
        /// <param name="agentClient"></param>
        public virtual void RemoveViewerAgent(ClientView agentClient)
        {

        }
        #endregion

        #region Shutdown
        /// <summary>
        /// Tidy before shutdown
        /// </summary>
        public virtual void Close()
        {

        }
        #endregion
    }
}
