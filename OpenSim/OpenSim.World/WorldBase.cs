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
using OpenSim.Framework.Inventory;
using OpenSim.RegionServer.world.scripting;
using OpenSim.Terrain;

namespace OpenSim.world
{
    public class WorldBase : IWorld
    {
        public Dictionary<libsecondlife.LLUUID, Entity> Entities;
        protected Dictionary<uint, IClientAPI> m_clientThreads;
        protected ulong m_regionHandle;
        protected string m_regionName;
       // protected InventoryCache _inventoryCache;
       // protected AssetCache _assetCache;
        protected RegionInfo m_regInfo;

        public TerrainEngine Terrain; //TODO: Replace TerrainManager with this.
        protected libsecondlife.TerrainManager TerrainManager; // To be referenced via TerrainEngine

        #region Properties
        /*
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
          */
        #endregion

        #region Constructors
        /// <summary>
        /// 
        /// </summary>
        public WorldBase()
        {

        }
        #endregion

        #region Setup Methods
       
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
        public virtual void SendLayerData(IClientAPI RemoteClient)
        {
            RemoteClient.SendLayerData(Terrain.getHeights1D());
        }

        /// <summary>
        /// Sends a specified patch to a client
        /// </summary>
        /// <param name="px">Patch coordinate (x) 0..16</param>
        /// <param name="py">Patch coordinate (y) 0..16</param>
        /// <param name="RemoteClient">The client to send to</param>
        public void SendLayerData(int px, int py, IClientAPI RemoteClient)
        {
           
        }
        #endregion

        #region Add/Remove Agent/Avatar
        /// <summary>
        /// 
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="agentID"></param>
        /// <param name="child"></param>
        public virtual void AddNewAvatar(IClientAPI remoteClient, LLUUID agentID, bool child)
        {
            return ;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="agentID"></param>
        public virtual void RemoveAvatar(LLUUID agentID)
        {
            return ;
        }

        #endregion

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public virtual RegionInfo GetRegionInfo()
        {
            return null;
        }

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
