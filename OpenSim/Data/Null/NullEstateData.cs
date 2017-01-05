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
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework;
using OpenSim.Region.Framework.Interfaces;

namespace OpenSim.Data.Null
{
    public class NullEstateStore : IEstateDataStore
    {
//        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

//        private string m_connectionString;

//        private Dictionary<uint, EstateSettings> m_knownEstates = new Dictionary<uint, EstateSettings>();
        private EstateSettings m_estate = null;

        private EstateSettings GetEstate()
        {
            if (m_estate == null)
            {
                // This fools the initialization caller into thinking an estate was fetched (a check in OpenSimBase).
                // The estate info is pretty empty so don't try banning anyone.
                m_estate = new EstateSettings();
                m_estate.EstateID = 1;
                m_estate.OnSave += StoreEstateSettings;
            }
            return m_estate;
        }

        protected virtual Assembly Assembly
        {
            get { return GetType().Assembly; }
        }

        public NullEstateStore()
        {
        }

        public NullEstateStore(string connectionString)
        {
            Initialise(connectionString);
        }

        public void Initialise(string connectionString)
        {
//            m_connectionString = connectionString;
        }

        private string[] FieldList
        {
            get { return new string[0]; }
        }

        public EstateSettings LoadEstateSettings(UUID regionID, bool create)
        {
            return GetEstate();
        }

        public void StoreEstateSettings(EstateSettings es)
        {
            m_estate = es;
            return;
        }

        public EstateSettings LoadEstateSettings(int estateID)
        {
            return GetEstate();
        }

        public EstateSettings CreateNewEstate()
        {
            return new EstateSettings();
        }

        public List<EstateSettings> LoadEstateSettingsAll()
        {
            List<EstateSettings> allEstateSettings = new List<EstateSettings>();
            allEstateSettings.Add(GetEstate());
            return allEstateSettings;
        }

        public List<int> GetEstatesAll()
        {
            List<int> result = new List<int>();
            result.Add((int)GetEstate().EstateID);
            return result;
        }

        public List<int> GetEstates(string search)
        {
            List<int> result = new List<int>();
            return result;
        }

        public bool LinkRegion(UUID regionID, int estateID)
        {
            return false;
        }

        public List<UUID> GetRegions(int estateID)
        {
            List<UUID> result = new List<UUID>();
            return result;
        }

        public bool DeleteEstate(int estateID)
        {
            return false;
        }

        #region IEstateDataStore Members


        public List<int> GetEstatesByOwner(UUID ownerID)
        {
            return new List<int>();
        }

        #endregion
    }
}
