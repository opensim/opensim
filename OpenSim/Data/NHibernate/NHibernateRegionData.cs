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
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using libsecondlife;
using log4net;
using NHibernate;
using NHibernate.Cfg;
using NHibernate.Expression;
using NHibernate.Mapping.Attributes;
using NHibernate.Tool.hbm2ddl;
using OpenSim.Framework;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Scenes;
using Environment=NHibernate.Cfg.Environment;

namespace OpenSim.Data.NHibernate
{
    /// <summary>
    /// A RegionData Interface to the NHibernate database
    /// </summary>
    public class NHibernateRegionData : IRegionDataStore
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private Configuration cfg;
        private ISessionFactory factory;
        private ISession session;

        // public void Initialise()
        // {
        //     Initialise("SQLiteDialect;SqliteClientDriver;URI=file:OpenSim.db,version=3", true);
        // }

        public void Initialise(string connect, bool persistpriminventories)
        {
            // Split out the dialect, driver, and connect string
            char[] split = {';'};
            string[] parts = connect.Split(split, 3);
            if (parts.Length != 3)
            {
                // TODO: make this a real exception type
                throw new Exception("Malformed Region connection string '" + connect + "'");
            }

            string dialect = parts[0];

            // NHibernate setup
            cfg = new Configuration();
            cfg.SetProperty(Environment.ConnectionProvider,
                            "NHibernate.Connection.DriverConnectionProvider");
            cfg.SetProperty(Environment.Dialect,
                            "NHibernate.Dialect." + dialect);
            cfg.SetProperty(Environment.ConnectionDriver,
                            "NHibernate.Driver." + parts[1]);
            cfg.SetProperty(Environment.ConnectionString, parts[2]);
            cfg.AddAssembly("OpenSim.Data.NHibernate");

            HbmSerializer.Default.Validate = true;
            using (MemoryStream stream =
                   HbmSerializer.Default.Serialize(Assembly.GetExecutingAssembly()))
                cfg.AddInputStream(stream);
            
            factory  = cfg.BuildSessionFactory();
            session = factory.OpenSession();
            
            // This actually does the roll forward assembly stuff
            Assembly assem = GetType().Assembly;
            Migration m = new Migration((System.Data.Common.DbConnection)factory.ConnectionProvider.GetConnection(), assem, dialect, "RegionStore");
            m.Update();
        }

        /***********************************************************************
         *
         *  Public Interface Functions
         *
         **********************************************************************/

        public void StoreRegionSettings(RegionSettings rs)
        {
        }

        public RegionSettings LoadRegionSettings(LLUUID regionUUID)
        {
            return null;
        }
        
        private void SaveOrUpdate(SceneObjectPart p)
        {
            try
            {
                ICriteria criteria = session.CreateCriteria(typeof(SceneObjectPart));
                criteria.Add(Expression.Eq("UUID", p.UUID));
                ArrayList l = (ArrayList)criteria.List();
                if (l.Count < 1) 
                {
                    session.Save(p);
                }
                else if (l.Count == 1)
                {
                    SceneObjectPart old = (SceneObjectPart)l[0];
                    session.Evict(old);
                    session.Update(p);
                }
                else 
                {
                    m_log.Error("Not unique");
                }
            }
            catch (Exception e)
            {
                m_log.Error("[NHIBERNATE] issue saving asset", e);
            }
        }

        private void SaveOrUpdate(Terrain t)
        {
            try
            {
                ICriteria criteria = session.CreateCriteria(typeof(Terrain));
                criteria.Add(Expression.Eq("RegionID", t.RegionID));
                ArrayList l = (ArrayList)criteria.List();
                if (l.Count < 1) 
                {
                    session.Save(t);
                }
                else if (l.Count == 1)
                {
                    Terrain old = (Terrain)l[0];
                    session.Evict(old);
                    session.Update(t);
                }
                else 
                {
                    m_log.Error("Not unique");
                }
            }
            catch (Exception e)
            {
                m_log.Error("[NHIBERNATE] issue saving asset", e);
            }
        }


        /// <summary>
        /// Adds an object into region storage
        /// </summary>
        /// <param name="obj">the object</param>
        /// <param name="regionUUID">the region UUID</param>
        public void StoreObject(SceneObjectGroup obj, LLUUID regionUUID)
        {
            try 
            {
                foreach (SceneObjectPart part in obj.Children.Values)
                {
                    m_log.InfoFormat("Storing part {0}", part.UUID);
                    SaveOrUpdate(part);
                }
                session.Flush();
            }
            catch (Exception e) 
            {
                m_log.Error("Can't save: ", e);
            }
        }

        private SceneObjectGroup LoadObject(LLUUID uuid, LLUUID region)
        {
            SceneObjectGroup group = new SceneObjectGroup();

            ICriteria criteria = session.CreateCriteria(typeof(SceneObjectPart));
            criteria.Add(Expression.Eq("RegionID", region));
            criteria.Add(Expression.Eq("ParentUUID", uuid));
            criteria.AddOrder( Order.Asc("ParentID") );

            foreach (SceneObjectPart p in criteria.List())
            {
                // root part
                if (p.UUID == uuid)
                {
                    group.AddPart(p);
                    group.RootPart = p;
                }
                else 
                {
                    group.AddPart(p);
                }
            }

            return group;
        }

        /// <summary>
        /// Removes an object from region storage
        /// </summary>
        /// <param name="obj">the object</param>
        /// <param name="regionUUID">the region UUID</param>
        public void RemoveObject(LLUUID obj, LLUUID regionUUID)
        {
            SceneObjectGroup g = LoadObject(obj, regionUUID);
            foreach (SceneObjectPart p in g.Children.Values)
            {
                session.Delete(p);
            }
            session.Flush();

            m_log.InfoFormat("[REGION DB]: Removing obj: {0} from region: {1}", obj.UUID, regionUUID);

        }

        /// <summary>
        /// Load persisted objects from region storage.
        /// </summary>
        /// <param name="regionUUID">The region UUID</param>
        /// <returns>List of loaded groups</returns>
        public List<SceneObjectGroup> LoadObjects(LLUUID regionUUID)
        {
            Dictionary<LLUUID, SceneObjectGroup> SOG = new Dictionary<LLUUID, SceneObjectGroup>();
            List<SceneObjectGroup> ret = new List<SceneObjectGroup>();
            
            ICriteria criteria = session.CreateCriteria(typeof(SceneObjectPart));
            criteria.Add(Expression.Eq("RegionID", regionUUID));
            criteria.AddOrder( Order.Asc("ParentID") );
            foreach (SceneObjectPart p in criteria.List())
            {
                // root part
                if (p.UUID == p.ParentUUID)
                {
                    SceneObjectGroup group = new SceneObjectGroup();
                    group.AddPart(p);
                    group.RootPart = p;
                    SOG.Add(p.ParentUUID, group);
                }
                else 
                {
                    SOG[p.ParentUUID].AddPart(p);
                }
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
        public void StoreTerrain(double[,] ter, LLUUID regionID)
        {
            Terrain t = new Terrain(regionID, ter);
            SaveOrUpdate(t);
        }

        /// <summary>
        /// Load the latest terrain revision from region storage
        /// </summary>
        /// <param name="regionID">the region UUID</param>
        /// <returns>Heightfield data</returns>
        public double[,] LoadTerrain(LLUUID regionID)
        {
            try
            {
                Terrain t = session.Load(typeof(Terrain), regionID) as Terrain;
                return t.Doubles;
            }
            catch (Exception e)
            {
                m_log.Error("[NHIBERNATE] issue loading terrain", e);
                
                double[,] terret = new double[256,256];
                terret.Initialize();
                return terret;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="globalID"></param>
        public void RemoveLandObject(LLUUID globalID)
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
        public List<LandData> LoadLandObjects(LLUUID regionUUID)
        {
            List<LandData> landDataForRegion = new List<LandData>();

            return landDataForRegion;
        }


        /// <summary>
        /// See <see cref="Commit"/>
        /// </summary>
        public void Shutdown()
        {
            session.Flush();
        }
        
        /// <summary>
        /// Load a region banlist
        /// </summary>
        /// <param name="regionUUID">the region UUID</param>
        /// <returns>The banlist</returns>
        public List<RegionBanListItem> LoadRegionBanList(LLUUID regionUUID)
        {
            List<RegionBanListItem> regionbanlist = new List<RegionBanListItem>();

            return regionbanlist;
        }

        /// <summary>
        /// Add en entry into region banlist
        /// </summary>
        /// <param name="item"></param>
        public void AddToRegionBanlist(RegionBanListItem item)
        {

        }

        /// <summary>
        /// remove an entry from the region banlist
        /// </summary>
        /// <param name="item"></param>
        public void RemoveFromRegionBanlist(RegionBanListItem item)
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
//            for (int x = 0; x < 256; x++)
//                for (int y = 0; y < 256; y++)
//                    bw.Write(val[x, y]);
//
//            return str.ToArray();
//        }

        /// <summary>
        /// see IRegionDatastore
        /// </summary>
        /// <param name="primID"></param>
        /// <param name="items"></param>
        public void StorePrimInventory(LLUUID primID, ICollection<TaskInventoryItem> items)
        {

        }
    }
}
