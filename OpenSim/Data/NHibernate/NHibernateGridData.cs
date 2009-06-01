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
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using log4net;
using NHibernate;
using NHibernate.Criterion;
using OpenMetaverse;
using OpenSim.Framework;

namespace OpenSim.Data.NHibernate
{

    /// <summary>
    /// A GridData Interface to the NHibernate database
    /// </summary>
    public class NHibernateGridData : GridDataBase
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

        public override void Initialise()
        {
            m_log.Info("[NHibernateGridData]: " + Name + " cannot be default-initialized!");
            throw new PluginNotInitialisedException(Name);
        }

        public override void Initialise(string connect)
        {
            m_log.InfoFormat("[NHIBERNATE] Initializing NHibernateGridData");
            manager = new NHibernateManager(connect, "GridStore");
        }

        /***********************************************************************
         *
         *  Public Interface Functions
         *
         **********************************************************************/

        public override void Dispose() { }

        /// <summary>
        /// The plugin being loaded
        /// </summary>
        /// <returns>A string containing the plugin name</returns>
        public override string Name
        {
            get { return "NHibernate Grid Data Interface"; }
        }

        /// <summary>
        /// The plugins version
        /// </summary>
        /// <returns>A string containing the plugin version</returns>
        public override string Version
        {
            get
            {
                Module module = GetType().Module;
                Version dllVersion = module.Assembly.GetName().Version;

                return string.Format("{0}.{1}.{2}.{3}", 
                    dllVersion.Major, dllVersion.Minor, dllVersion.Build, dllVersion.Revision);
            }
        }

        public override bool AuthenticateSim(UUID UUID, ulong regionHandle, string simrecvkey)
        {
            bool throwHissyFit = false; // Should be true by 1.0

            if (throwHissyFit)
                throw new Exception("CRYPTOWEAK AUTHENTICATE: Refusing to authenticate due to replay potential.");

            RegionProfileData data = GetProfileByUUID(UUID);

            return (regionHandle == data.regionHandle && simrecvkey == data.regionSecret);
        }

        public override ReservationData GetReservationAtPoint(uint x, uint y)
        {
            throw new NotImplementedException();
        }

        public override DataResponse AddProfile(RegionProfileData profile)
        {
            if (manager.Get(typeof(RegionProfileData), profile.Uuid) == null)
            {
                manager.Insert(profile);
                return DataResponse.RESPONSE_OK;
            }
            else
            {
                return DataResponse.RESPONSE_ERROR;
            }
        }

        public override DataResponse UpdateProfile(RegionProfileData profile)
        {
            if (manager.Get(typeof(RegionProfileData), profile.Uuid) != null)
            {
                manager.Update(profile);
                return DataResponse.RESPONSE_OK;
            }
            else
            {
                return DataResponse.RESPONSE_ERROR;
            }
        }

        public override DataResponse DeleteProfile(string uuid)
        {
            RegionProfileData regionProfileData = (RegionProfileData)manager.Get(typeof(RegionProfileData), new UUID(uuid));
            if (regionProfileData != null)
            {
                manager.Delete(regionProfileData);
                return DataResponse.RESPONSE_OK;
            }
            return DataResponse.RESPONSE_ERROR;
        }

        public override RegionProfileData GetProfileByUUID(UUID UUID)
        {
            return (RegionProfileData)manager.Get(typeof(RegionProfileData), UUID);
        }

        public override RegionProfileData GetProfileByHandle(ulong regionHandle)
        {
            using (ISession session = manager.GetSession())
            {
                ICriteria criteria = session.CreateCriteria(typeof(RegionProfileData));
                criteria.Add(Expression.Eq("RegionHandle", regionHandle));

                IList regions = criteria.List();

                if (regions.Count == 1)
                {
                    return (RegionProfileData)regions[0];
                }
                else
                {
                    return null;
                }
            }
        }

        public override RegionProfileData GetProfileByString(string regionName)
        {

            using (ISession session = manager.GetSession())
            {
                ICriteria criteria = session.CreateCriteria(typeof(RegionProfileData));
                criteria.Add(Expression.Eq("RegionName", regionName));

                IList regions = criteria.List();

                if (regions.Count == 1)
                {
                    return (RegionProfileData)regions[0];
                }
                else
                {
                    return null;
                }
            }

        }

        public override RegionProfileData[] GetProfilesInRange(uint Xmin, uint Ymin, uint Xmax, uint Ymax)
        {
            using (ISession session = manager.GetSession())
            {
                ICriteria criteria = session.CreateCriteria(typeof(RegionProfileData));
                criteria.Add(Expression.Ge("RegionLocX", Xmin));
                criteria.Add(Expression.Ge("RegionLocY", Ymin));
                criteria.Add(Expression.Le("RegionLocX", Xmax));
                criteria.Add(Expression.Le("RegionLocY", Ymax));

                IList regions = criteria.List();
                RegionProfileData[] regionArray = new RegionProfileData[regions.Count];

                for (int i=0;i<regionArray.Length;i++)
                {
                    regionArray[i] = (RegionProfileData)regions[i];
                }

                return regionArray;
            }
        }

        public override List<RegionProfileData> GetRegionsByName(string namePrefix, uint maxNum)
        {
            using (ISession session = manager.GetSession())
            {
                ICriteria criteria = session.CreateCriteria(typeof(RegionProfileData));
                criteria.SetMaxResults((int)maxNum);

                criteria.Add(Expression.Like("RegionName", namePrefix, MatchMode.Start));

                IList regions = criteria.List();
                List<RegionProfileData> regionList = new List<RegionProfileData>();

                foreach (RegionProfileData regionProfileData in regions)
                {
                    regionList.Add(regionProfileData);
                }

                return regionList;
            }
        }

    }
}
