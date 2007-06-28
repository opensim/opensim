/*
* Copyright (c) Contributors, http://www.openmetaverse.org/
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
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS AND ANY
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
using OpenSim.Terrain;
using OpenSim.Caches;

namespace OpenSim.Region.Scenes
{
    public abstract class SceneBase :  IWorld 
    {
        public Dictionary<libsecondlife.LLUUID, Entity> Entities;
        protected Dictionary<uint, IClientAPI> m_clientThreads;
        protected ulong m_regionHandle;
        protected string m_regionName;
        protected RegionInfo m_regInfo;

        public TerrainEngine Terrain;

        public string m_datastore;
        public ILocalStorage localStorage;

        protected object m_syncRoot = new object();
        private uint m_nextLocalId = 8880000;
        protected AssetCache assetCache;

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
        /// Loads a new storage subsystem from a named library
        /// </summary>
        /// <param name="dllName">Storage Library</param>
        /// <returns>Successful or not</returns>
        public bool LoadStorageDLL(string dllName)
        {
            try
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

                                store.Initialise(this.m_datastore);
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
            catch (Exception e)
            {
                OpenSim.Framework.Console.MainLog.Instance.Warn("World.cs: LoadStorageDLL() - Failed with exception " + e.ToString());
                return false;
            }
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
        public virtual void SendLayerData(int px, int py, IClientAPI RemoteClient)
        {
            RemoteClient.SendLayerData(px, py, Terrain.getHeights1D());
        }

        #endregion

        #region Add/Remove Agent/Avatar
        /// <summary>
        /// 
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="agentID"></param>
        /// <param name="child"></param>
        public abstract void AddNewClient(IClientAPI remoteClient, LLUUID agentID, bool child);
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="agentID"></param>
        public abstract void RemoveClient(LLUUID agentID);
       
        #endregion

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public virtual RegionInfo RegionInfo
        {
            get { return this.m_regInfo; }
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
        public virtual void Close()
        {
            try
            {
                this.localStorage.ShutDown();
            }
            catch (Exception e)
            {
                OpenSim.Framework.Console.MainLog.Instance.WriteLine(OpenSim.Framework.Console.LogPriority.HIGH, "World.cs: Close() - Failed with exception " + e.ToString());
            }
        }

        #endregion

 
    }
}
