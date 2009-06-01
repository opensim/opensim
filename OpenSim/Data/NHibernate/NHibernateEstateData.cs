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

using System.Reflection;
using log4net;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using NHibernate;
using NHibernate.Criterion;
using System.Collections;
using System;

namespace OpenSim.Data.NHibernate
{
    /// <summary>
    /// A User storage interface for the DB4o database system
    /// </summary>
    public class NHibernateEstateData : IEstateDataStore
    {

        #region Fields

        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private NHibernateManager manager;
        public NHibernateManager Manager
        {
            get
            {
                return manager;
            }
        }

        public string Name
        {
            get { return "NHibernateEstateData"; }
        }

        public string Version
        {
            get { return "0.1"; }
        }

        #endregion

        #region Startup and shutdown.

        public void Initialise()
        {
            m_log.Info("[NHIBERNATE]: " + Name + " cannot be default-initialized!");
            throw new PluginNotInitialisedException(Name);
        }

        public void Initialise(string connect)
        {

            m_log.InfoFormat("[NHIBERNATE] Initializing " + Name + ".");
            manager = new NHibernateManager(connect, "EstateStore");
        }

        public void Dispose() { }

        #endregion

        #region IEstateDataStore Members

        public EstateSettings LoadEstateSettings(UUID regionID)
        {
            EstateRegionLink link = LoadEstateRegionLink(regionID);

            // Ensure that estate settings exist for the link
            if (link != null)
            {
                if (manager.GetWithStatefullSession(typeof(EstateSettings), link.EstateID) == null)
                {
                    // Delete broken link
                    manager.Delete(link);
                    link = null;
                }
            }

            // If estate link does not exist create estate settings and link it to region.
            if (link == null)
            {
                EstateSettings estateSettings = new EstateSettings();
                //estateSettings.EstateOwner = UUID.Random();
                //estateSettings.BlockDwell = false;
                object identifier = manager.Insert(estateSettings);

                if (identifier == null)
                {
                    // Saving failed. Error is logged in the manager.
                    return null;
                }

                uint estateID = (uint)identifier;
                link = new EstateRegionLink();
                link.EstateRegionLinkID = UUID.Random();
                link.RegionID = regionID;
                link.EstateID = estateID;
                manager.InsertWithStatefullSession(link);
            }

            // Load estate settings according to the existing or created link.
            return (EstateSettings)manager.GetWithStatefullSession(typeof(EstateSettings), link.EstateID);
        }

        public void StoreEstateSettings(EstateSettings estateSettings)
        {
            // Estates are always updated when stored.
            // Insert is always done via. load method as with the current API
            // this is explicitly the only way to create region link.
            manager.UpdateWithStatefullSession(estateSettings);
        }

        #endregion

        #region Private Utility Methods
        private EstateRegionLink LoadEstateRegionLink(UUID regionID)
        {
            ICriteria criteria = manager.GetSession().CreateCriteria(typeof(EstateRegionLink));
            criteria.Add(Expression.Eq("RegionID", regionID));
            IList links = criteria.List();

            // Fail fast if more than one estate links exist
            if (links.Count > 1)
            {
                m_log.Error("[NHIBERNATE]: Region had more than one estate linked: " + regionID);
                throw new Exception("[NHIBERNATE]: Region had more than one estate linked: " + regionID);
            }

            if (links.Count == 1)
            {
                return (EstateRegionLink)links[0];
            }
            else
            {
                return null;
            }
        }
        #endregion 
    }
}
