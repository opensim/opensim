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
using System.Reflection;
using log4net;
using NHibernate;
using NHibernate.Criterion;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Data.NHibernate
{
    /// <summary>
    /// A RegionData Interface to the NHibernate database
    /// </summary>
    public class NHibernateRegionData : IRegionDataStore
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private NHibernateManager manager;
        public NHibernateManager Manager
        {
            get
            {
                return manager;
            }
        }

        public void Initialise(string connect)
        {
            m_log.InfoFormat("[NHIBERNATE] Initializing NHibernateRegionData");
            manager = new NHibernateManager(connect, "RegionStore");
        }

        /***********************************************************************
         *
         *  Public Interface Functions
         *
         **********************************************************************/

        public void Dispose() {}

        public void StoreRegionSettings(RegionSettings rs)
        {
            RegionSettings oldRegionSettings = (RegionSettings)manager.Get(typeof(RegionSettings), rs.RegionUUID);
            if (oldRegionSettings != null)
            {
                manager.Update(rs);
            }
            else
            {
                manager.Insert(rs);
            }
        }

        public RegionSettings LoadRegionSettings(UUID regionUUID)
        {
            RegionSettings regionSettings = (RegionSettings) manager.Get(typeof(RegionSettings), regionUUID);

            if (regionSettings == null)
            {
                regionSettings = new RegionSettings();
                regionSettings.RegionUUID = regionUUID;
                manager.Insert(regionSettings);
            }

            regionSettings.OnSave += StoreRegionSettings;
            
            return regionSettings;
        }

        // This looks inefficient, but it turns out that it isn't
        // based on trial runs with nhibernate 1.2
        private void SaveOrUpdate(SceneObjectPart p)
        {
            try
            {
                SceneObjectPart old = (SceneObjectPart)manager.Get(typeof(SceneObjectPart), p.UUID);
                if (old != null)
                {
                    m_log.InfoFormat("[NHIBERNATE] updating object {0}", p.UUID);
                    manager.Update(p);
                } 
                else
                {
                    m_log.InfoFormat("[NHIBERNATE] saving object {0}", p.UUID);
                    manager.Insert(p);
                }
                
            }
            catch (Exception e)
            {
                m_log.Error("[NHIBERNATE] issue saving part", e);
            }
        }

        private void SaveOrUpdate(Terrain t)
        {
            try
            {
                
                Terrain old = (Terrain)manager.Get(typeof(Terrain), t.RegionID);
                if (old != null)
                {
                    m_log.InfoFormat("[NHIBERNATE] updating terrain {0}", t.RegionID);
                    manager.Update(t);
                }
                else
                {
                    m_log.InfoFormat("[NHIBERNATE] saving terrain {0}", t.RegionID);
                    manager.Insert(t);
                }

            }
            catch (Exception e)
            {
                m_log.Error("[NHIBERNATE] issue saving terrain", e);
            }
        }


        /// <summary>
        /// Adds an object into region storage
        /// </summary>
        /// <param name="obj">the object</param>
        /// <param name="regionUUID">the region UUID</param>
        public void StoreObject(SceneObjectGroup obj, UUID regionUUID)
        {
            uint flags = obj.RootPart.GetEffectiveObjectFlags();

            // Eligibility check
            if ((flags & (uint)PrimFlags.Temporary) != 0)
                return;
            if ((flags & (uint)PrimFlags.TemporaryOnRez) != 0)
                return;

            try
            {
                foreach (SceneObjectPart part in obj.Children.Values)
                {
                    m_log.InfoFormat("Storing part {0}", part.UUID);
                    SaveOrUpdate(part);
                }
            }
            catch (Exception e)
            {
                m_log.Error("Can't save: ", e);
            }
        }

        private SceneObjectGroup LoadObject(UUID uuid, UUID region)
        {
            ICriteria criteria = manager.GetSession().CreateCriteria(typeof(SceneObjectPart));
            criteria.Add(Expression.Eq("RegionID", region));
            criteria.Add(Expression.Eq("ParentUUID", uuid));
            criteria.AddOrder(Order.Asc("ParentID"));

            IList<SceneObjectPart> parts = criteria.List<SceneObjectPart>();

            SceneObjectGroup group = null;

            // Find the root part
            for (int i = 0; i < parts.Count; i++)
            {
                if (parts[i].UUID == uuid)
                {
                    group = new SceneObjectGroup(parts[i]);
                    break;
                }
            }

            // Add the children parts
            if (group != null)
            {
                for (int i = 0; i < parts.Count; i++)
                {
                    if (parts[i].UUID != uuid)
                        group.AddPart(parts[i]);
                }
            }
            else
            {
                m_log.Error("[NHIBERNATE]: LoadObject() Attempted to load a SceneObjectGroup with no root SceneObjectPart ");
            }

            return group;
        }

        /// <summary>
        /// Removes an object from region storage
        /// </summary>
        /// <param name="obj">the object</param>
        /// <param name="regionUUID">the region UUID</param>
        public void RemoveObject(UUID obj, UUID regionUUID)
        {
            SceneObjectGroup g = LoadObject(obj, regionUUID);
            foreach (SceneObjectPart p in g.Children.Values)
            {
                manager.Delete(p);
            }

            // m_log.InfoFormat("[REGION DB]: Removing obj: {0} from region: {1}", obj.Guid, regionUUID);

        }

        /// <summary>
        /// Load persisted objects from region storage.
        /// </summary>
        /// <param name="regionUUID">The region UUID</param>
        /// <returns>List of loaded groups</returns>
        public List<SceneObjectGroup> LoadObjects(UUID regionUUID)
        {
            Dictionary<UUID, SceneObjectGroup> SOG = new Dictionary<UUID, SceneObjectGroup>();
            List<SceneObjectGroup> ret = new List<SceneObjectGroup>();

            ICriteria criteria = manager.GetSession().CreateCriteria(typeof(SceneObjectPart));
            criteria.Add(Expression.Eq("RegionID", regionUUID));
            criteria.AddOrder(Order.Asc("ParentID"));
            criteria.AddOrder(Order.Asc("LinkNum"));
            foreach (SceneObjectPart p in criteria.List())
            {
                // root part
                if (p.UUID == p.ParentUUID)
                {
                    SceneObjectGroup group = new SceneObjectGroup(p);
                    SOG.Add(p.ParentUUID, group);
                }
                else
                {
                    SOG[p.ParentUUID].AddPart(p);
                }
                // get the inventory

                ICriteria InvCriteria = manager.GetSession().CreateCriteria(typeof(TaskInventoryItem));
                InvCriteria.Add(Expression.Eq("ParentPartID", p.UUID));
                IList<TaskInventoryItem> inventory = new List<TaskInventoryItem>();
                foreach (TaskInventoryItem i in InvCriteria.List())
                {
                    inventory.Add(i);
                }

                if (inventory.Count > 0)
                    p.Inventory.RestoreInventoryItems(inventory);
            }
            foreach (SceneObjectGroup g in SOG.Values)
            {
                ret.Add(g);
            }

            return ret;
        }

        /// <summary>
        /// Store a terrain revision in region storage
        /// </summary>
        /// <param name="ter">terrain heightfield</param>
        /// <param name="regionID">region UUID</param>
        public void StoreTerrain(double[,] ter, UUID regionID)
        {
            lock (this) {
                Terrain t = new Terrain(regionID, ter);
                SaveOrUpdate(t);
            }
        }

        /// <summary>
        /// Load the latest terrain revision from region storage
        /// </summary>
        /// <param name="regionID">the region UUID</param>
        /// <returns>Heightfield data</returns>
        public double[,] LoadTerrain(UUID regionID)
        {
            Terrain t = (Terrain)manager.Get(typeof(Terrain), regionID);
            if (t != null)
            {
                return t.Doubles;
            }
               
            m_log.Info("No terrain yet");
            return null;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="globalID"></param>
        public void RemoveLandObject(UUID globalID)
        {

        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="parcel"></param>
        public void StoreLandObject(ILandObject parcel)
        {

        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="regionUUID"></param>
        /// <returns></returns>
        public List<LandData> LoadLandObjects(UUID regionUUID)
        {
            List<LandData> landDataForRegion = new List<LandData>();

            return landDataForRegion;
        }


        /// <summary>
        /// See <see cref="Commit"/>
        /// </summary>
        public void Shutdown()
        {
            //session.Flush();
        }

        /// <summary>
        /// Load a region banlist
        /// </summary>
        /// <param name="regionUUID">the region UUID</param>
        /// <returns>The banlist</returns>
        public List<EstateBan> LoadRegionBanList(UUID regionUUID)
        {
            List<EstateBan> regionbanlist = new List<EstateBan>();

            return regionbanlist;
        }

        /// <summary>
        /// Add en entry into region banlist
        /// </summary>
        /// <param name="item"></param>
        public void AddToRegionBanlist(EstateBan item)
        {

        }

        /// <summary>
        /// remove an entry from the region banlist
        /// </summary>
        /// <param name="item"></param>
        public void RemoveFromRegionBanlist(EstateBan item)
        {

        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="val"></param>
        /// <returns></returns>
//        private static Array serializeTerrain(double[,] val)
//        {
//            MemoryStream str = new MemoryStream(65536*sizeof (double));
//            BinaryWriter bw = new BinaryWriter(str);
//
//            // TODO: COMPATIBILITY - Add byte-order conversions
//            for (int x = 0; x < (int)Constants.RegionSize; x++)
//                for (int y = 0; y < (int)Constants.RegionSize; y++)
//                    bw.Write(val[x, y]);
//
//            return str.ToArray();
//        }

        /// <summary>
        /// see IRegionDatastore
        /// </summary>
        /// <param name="primID"></param>
        /// <param name="items"></param>
        public void StorePrimInventory(UUID primID, ICollection<TaskInventoryItem> items)
        {
             ICriteria criteria = manager.GetSession().CreateCriteria(typeof(TaskInventoryItem));
             criteria.Add(Expression.Eq("ParentPartID", primID));
             try
             {
                 foreach (TaskInventoryItem i in criteria.List())
                 {
                     manager.Delete(i);
                 }

                 foreach (TaskInventoryItem i in items)
                 {
                     manager.Insert(i);

                 }
             }
             catch (Exception e)
             {
                 m_log.Error("[NHIBERNATE] StoreInvetory", e);
             }
        }
    }
}
