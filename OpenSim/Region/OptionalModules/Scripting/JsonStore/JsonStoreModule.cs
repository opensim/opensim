/*
 * Copyright (c) Contributors 
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
using Mono.Addins;

using System;
using System.Reflection;
using System.Threading;
using System.Text;
using System.Net;
using System.Net.Sockets;
using log4net;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using System.Collections.Generic;
using System.Text.RegularExpressions;

            
namespace OpenSim.Region.OptionalModules.Scripting.JsonStore
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "JsonStoreModule")]

    public class JsonStoreModule  : INonSharedRegionModule, IJsonStoreModule
    {
        private static readonly ILog m_log =
            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private IConfig m_config = null;
        private bool m_enabled = false;
        private Scene m_scene = null;

        private Dictionary<UUID,JsonStore> m_JsonValueStore;
        private UUID m_sharedStore;

#region IRegionModule Members

        // -----------------------------------------------------------------
        /// <summary>
        /// Name of this shared module is it's class name
        /// </summary>
        // -----------------------------------------------------------------
        public string Name
        {
            get { return this.GetType().Name; }
        }

        // -----------------------------------------------------------------
        /// <summary>
        /// Initialise this shared module
        /// </summary>
        /// <param name="scene">this region is getting initialised</param>
        /// <param name="source">nini config, we are not using this</param>
        // -----------------------------------------------------------------
        public void Initialise(IConfigSource config)
        {
            try 
            {
                if ((m_config = config.Configs["JsonStore"]) == null)
                {
                    // There is no configuration, the module is disabled
                    // m_log.InfoFormat("[JsonStore] no configuration info");
                    return;
                }

                m_enabled = m_config.GetBoolean("Enabled", m_enabled);
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[JsonStore] initialization error: {0}",e.Message);
                return;
            }

            if (m_enabled)
                m_log.DebugFormat("[JsonStore] module is enabled");
        }

        // -----------------------------------------------------------------
        /// <summary>
        /// everything is loaded, perform post load configuration
        /// </summary>
        // -----------------------------------------------------------------
        public void PostInitialise()
        {
        }

        // -----------------------------------------------------------------
        /// <summary>
        /// Nothing to do on close
        /// </summary>
        // -----------------------------------------------------------------
        public void Close()
        {
        }

        // -----------------------------------------------------------------
        /// <summary>
        /// </summary>
        // -----------------------------------------------------------------
        public void AddRegion(Scene scene)
        {
            if (m_enabled)
            {
                m_scene = scene;
                m_scene.RegisterModuleInterface<IJsonStoreModule>(this);

                m_sharedStore = UUID.Zero;
                m_JsonValueStore = new Dictionary<UUID,JsonStore>();
                m_JsonValueStore.Add(m_sharedStore,new JsonStore(""));
            }
        }

        // -----------------------------------------------------------------
        /// <summary>
        /// </summary>
        // -----------------------------------------------------------------
        public void RemoveRegion(Scene scene)
        {
            // need to remove all references to the scene in the subscription
            // list to enable full garbage collection of the scene object
        }

        // -----------------------------------------------------------------
        /// <summary>
        /// Called when all modules have been added for a region. This is 
        /// where we hook up events
        /// </summary>
        // -----------------------------------------------------------------
        public void RegionLoaded(Scene scene)
        {
            if (m_enabled) {}
        }

        /// -----------------------------------------------------------------
        /// <summary>
        /// </summary>
        // -----------------------------------------------------------------
        public Type ReplaceableInterface
        {
            get { return null; }
        }

#endregion

#region ScriptInvocationInteface

        // -----------------------------------------------------------------
        /// <summary>
        /// 
        /// </summary>
        // -----------------------------------------------------------------
        public bool CreateStore(string value, out UUID result)
        {
            result = UUID.Zero;
            
            if (! m_enabled) return false;
            
            UUID uuid = UUID.Random();
            JsonStore map = null;

            try
            { 
                map = new JsonStore(value);
            }
            catch (Exception e)
            {
                m_log.InfoFormat("[JsonStore] Unable to initialize store from {0}; {1}",value,e.Message);
                return false;
            }

            lock (m_JsonValueStore)
                m_JsonValueStore.Add(uuid,map);
            
            result = uuid;
            return true;
        }

        // -----------------------------------------------------------------
        /// <summary>
        /// 
        /// </summary>
        // -----------------------------------------------------------------
        public bool DestroyStore(UUID storeID)
        {
            if (! m_enabled) return false;

            lock (m_JsonValueStore)
                m_JsonValueStore.Remove(storeID);
            
            return true;
        }

        // -----------------------------------------------------------------
        /// <summary>
        /// 
        /// </summary>
        // -----------------------------------------------------------------
        public bool TestPath(UUID storeID, string path, bool useJson)
        {
            if (! m_enabled) return false;

            JsonStore map = null;
            lock (m_JsonValueStore)
            {
                if (! m_JsonValueStore.TryGetValue(storeID,out map))
                {
                    m_log.InfoFormat("[JsonStore] Missing store {0}",storeID);
                    return true;
                }
            }
            
            try
            {
                lock (map)
                    return map.TestPath(path,useJson);
            }
            catch (Exception e)
            {
                m_log.InfoFormat("[JsonStore] Path test failed for {0} in {1}; {2}",path,storeID,e.Message);
            }

            return false;
        }

        // -----------------------------------------------------------------
        /// <summary>
        /// 
        /// </summary>
        // -----------------------------------------------------------------
        public bool SetValue(UUID storeID, string path, string value, bool useJson)
        {
            if (! m_enabled) return false;

            JsonStore map = null;
            lock (m_JsonValueStore)
            {
                if (! m_JsonValueStore.TryGetValue(storeID,out map))
                {
                    m_log.InfoFormat("[JsonStore] Missing store {0}",storeID);
                    return false;
                }
            }
            
            try
            {
                lock (map)
                    if (map.SetValue(path,value,useJson))
                        return true;
            }
            catch (Exception e)
            {
                m_log.InfoFormat("[JsonStore] Unable to assign {0} to {1} in {2}; {3}",value,path,storeID,e.Message);
            }

            return false;
        }
        
        // -----------------------------------------------------------------
        /// <summary>
        /// 
        /// </summary>
        // -----------------------------------------------------------------
        public bool RemoveValue(UUID storeID, string path)
        {
            if (! m_enabled) return false;

            JsonStore map = null;
            lock (m_JsonValueStore)
            {
                if (! m_JsonValueStore.TryGetValue(storeID,out map))
                {
                    m_log.InfoFormat("[JsonStore] Missing store {0}",storeID);
                    return false;
                }
            }
            
            try
            {
                lock (map)
                    if (map.RemoveValue(path))
                        return true;
            }
            catch (Exception e)
            {
                m_log.InfoFormat("[JsonStore] Unable to remove {0} in {1}; {2}",path,storeID,e.Message);
            }

            return false;
        }
        
        // -----------------------------------------------------------------
        /// <summary>
        /// 
        /// </summary>
        // -----------------------------------------------------------------
        public bool GetValue(UUID storeID, string path, bool useJson, out string value)
        {
            value = String.Empty;
            
            if (! m_enabled) return false;

            JsonStore map = null;
            lock (m_JsonValueStore)
            {
                if (! m_JsonValueStore.TryGetValue(storeID,out map))
                    return false;
            }

            try
            {
                lock (map)
                {
                    return map.GetValue(path, out value, useJson);
                }
            }
            catch (Exception e)
            {
                m_log.InfoFormat("[JsonStore] unable to retrieve value; {0}",e.Message);
            }
            
            return false;
        }

        // -----------------------------------------------------------------
        /// <summary>
        /// 
        /// </summary>
        // -----------------------------------------------------------------
        public void TakeValue(UUID storeID, string path, bool useJson, TakeValueCallback cback)
        {
            if (! m_enabled)
            {
                cback(String.Empty);
                return;
            }

            JsonStore map = null;
            lock (m_JsonValueStore)
            {
                if (! m_JsonValueStore.TryGetValue(storeID,out map))
                {
                    cback(String.Empty);
                    return;
                }
            }

            try
            {
                lock (map)
                {
                    map.TakeValue(path, useJson, cback);
                    return;
                }
            }
            catch (Exception e)
            {
                m_log.InfoFormat("[JsonStore] unable to retrieve value; {0}",e.ToString());
            }
            
            cback(String.Empty);
        }

        // -----------------------------------------------------------------
        /// <summary>
        /// 
        /// </summary>
        // -----------------------------------------------------------------
        public void ReadValue(UUID storeID, string path, bool useJson, TakeValueCallback cback)
        {
            if (! m_enabled)
            {
                cback(String.Empty);
                return;
            }

            JsonStore map = null;
            lock (m_JsonValueStore)
            {
                if (! m_JsonValueStore.TryGetValue(storeID,out map))
                {
                    cback(String.Empty);
                    return;
                }
            }

            try
            {
                lock (map)
                {
                    map.ReadValue(path, useJson, cback);
                    return;
                }
            }
            catch (Exception e)
            {
                m_log.InfoFormat("[JsonStore] unable to retrieve value; {0}",e.ToString());
            }
            
            cback(String.Empty);
        }

#endregion
    }
}
