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
using OpenSim.Region.Scripting;
using OpenSim.Terrain;

namespace OpenSim.Region
{
    public abstract class WorldBase : IWorld
    {
        public Dictionary<libsecondlife.LLUUID, Entity> Entities;
        protected Dictionary<uint, IClientAPI> m_clientThreads;
        protected ulong m_regionHandle;
        protected string m_regionName;
        protected RegionInfo m_regInfo;

        public TerrainEngine Terrain; //TODO: Replace TerrainManager with this.
        protected libsecondlife.TerrainManager TerrainManager; // To be referenced via TerrainEngine
        protected object m_syncRoot = new object();
        private uint m_nextLocalId = 8880000;

        #region Update Methods
        /// <summary>
        /// Normally called once every frame/tick to let the world preform anything required (like running the physics simulation)
        /// </summary>
        public abstract void Update();

        #endregion

        #region Terrain Methods

        /// <summary>
        /// Loads the World heightmap
        /// </summary>
        public abstract void LoadWorldMap();
        
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
        public abstract void SendLayerData(int px, int py, IClientAPI RemoteClient);

        #endregion

        #region Add/Remove Agent/Avatar
        /// <summary>
        /// 
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="agentID"></param>
        /// <param name="child"></param>
        public abstract void AddNewAvatar(IClientAPI remoteClient, LLUUID agentID, bool child);
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="agentID"></param>
        public abstract void RemoveAvatar(LLUUID agentID);
       
        #endregion

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public virtual RegionInfo RegionInfo
        {
            get { return null; }
        }

        public object SyncRoot
        {
            get { return m_syncRoot; }
        }

        public uint NextLocalId
        {
            get { return m_nextLocalId++; }
        }

        #region Shutdown
        /// <summary>
        /// Tidy before shutdown
        /// </summary>
        public abstract void Close();

        #endregion

 
    }
}
