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
using System.IO;
using System.Reflection;
using OpenMetaverse;

namespace OpenSim.Framework
{
    public class EstateSettings
    {
        // private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public delegate void SaveDelegate(EstateSettings rs);

        public event SaveDelegate OnSave;

        // Only the client uses these
        //
        private uint m_EstateID = 0;

        public uint EstateID
        {
            get { return m_EstateID; }
            set { m_EstateID = value; }
        }

        private string m_EstateName = "My Estate";

        public string EstateName
        {
            get { return m_EstateName; }
            set { m_EstateName = value; }
        }

        private bool m_AllowLandmark = true;

        public bool AllowLandmark
        {
            get { return m_AllowLandmark; }
            set { m_AllowLandmark = value; }
        }

        private bool m_AllowParcelChanges = true;

        public bool AllowParcelChanges
        {
            get { return m_AllowParcelChanges; }
            set { m_AllowParcelChanges = value; }
        }

        private bool m_AllowSetHome = true;

        public bool AllowSetHome
        {
            get { return m_AllowSetHome; }
            set { m_AllowSetHome = value; }
        }

        private uint m_ParentEstateID = 1;

        public uint ParentEstateID
        {
            get { return m_ParentEstateID; }
            set { m_ParentEstateID = value; }
        }

        private float m_BillableFactor = 0.0f;

        public float BillableFactor
        {
            get { return m_BillableFactor; }
            set { m_BillableFactor = value; }
        }

        private int m_PricePerMeter = 1;

        public int PricePerMeter
        {
            get { return m_PricePerMeter; }
            set { m_PricePerMeter = value; }
        }

        private int m_RedirectGridX = 0;

        public int RedirectGridX
        {
            get { return m_RedirectGridX; }
            set { m_RedirectGridX = value; }
        }

        private int m_RedirectGridY = 0;

        public int RedirectGridY
        {
            get { return m_RedirectGridY; }
            set { m_RedirectGridY = value; }
        }

        // Used by the sim
        //
        private bool m_UseGlobalTime = true;

        public bool UseGlobalTime
        {
            get { return m_UseGlobalTime; }
            set { m_UseGlobalTime = value; }
        }

        private bool m_FixedSun = false;

        public bool FixedSun
        {
            get { return m_FixedSun; }
            set { m_FixedSun = value; }
        }

        private double m_SunPosition = 0.0;

        public double SunPosition
        {
            get { return m_SunPosition; }
            set { m_SunPosition = value; }
        }

        private bool m_AllowVoice = true;

        public bool AllowVoice
        {
            get { return m_AllowVoice; }
            set { m_AllowVoice = value; }
        }

        private bool m_AllowDirectTeleport = true;

        public bool AllowDirectTeleport
        {
            get { return m_AllowDirectTeleport; }
            set { m_AllowDirectTeleport = value; }
        }

        private bool m_DenyAnonymous = false;

        public bool DenyAnonymous
        {
            get { return m_DenyAnonymous; }
            set { m_DenyAnonymous = value; }
        }

        private bool m_DenyIdentified = false;

        public bool DenyIdentified
        {
            get { return m_DenyIdentified; }
            set { m_DenyIdentified = value; }
        }

        private bool m_DenyTransacted = false;

        public bool DenyTransacted
        {
            get { return m_DenyTransacted; }
            set { m_DenyTransacted = value; }
        }

        private bool m_AbuseEmailToEstateOwner = false;

        public bool AbuseEmailToEstateOwner
        {
            get { return m_AbuseEmailToEstateOwner; }
            set { m_AbuseEmailToEstateOwner = value; }
        }

        private bool m_BlockDwell = false;

        public bool BlockDwell
        {
            get { return m_BlockDwell; }
            set { m_BlockDwell = value; }
        }

        private bool m_EstateSkipScripts = false;

        public bool EstateSkipScripts
        {
            get { return m_EstateSkipScripts; }
            set { m_EstateSkipScripts = value; }
        }

        private bool m_ResetHomeOnTeleport = false;

        public bool ResetHomeOnTeleport
        {
            get { return m_ResetHomeOnTeleport; }
            set { m_ResetHomeOnTeleport = value; }
        }

        private bool m_TaxFree = false;

        public bool TaxFree
        {
            get { return m_TaxFree; }
            set { m_TaxFree = value; }
        }

        private bool m_PublicAccess = true;

        public bool PublicAccess
        {
            get { return m_PublicAccess; }
            set { m_PublicAccess = value; }
        }

        private string m_AbuseEmail = String.Empty;

        public string AbuseEmail
        {
            get { return m_AbuseEmail; }
            set { m_AbuseEmail= value; }
        }

        private UUID m_EstateOwner = UUID.Zero;

        public UUID EstateOwner
        {
            get { return m_EstateOwner; }
            set { m_EstateOwner = value; }
        }

        private bool m_DenyMinors = false;

        public bool DenyMinors
        {
            get { return m_DenyMinors; }
            set { m_DenyMinors = value; }
        }

        // All those lists...
        //
        private List<UUID> l_EstateManagers = new List<UUID>();

        public UUID[] EstateManagers
        {
            get { return l_EstateManagers.ToArray(); }
            set { l_EstateManagers = new List<UUID>(value); }
        }

        private List<EstateBan> l_EstateBans = new List<EstateBan>();

        public EstateBan[] EstateBans
        {
            get { return l_EstateBans.ToArray(); }
            set { l_EstateBans = new List<EstateBan>(value); }
        }

        private List<UUID> l_EstateAccess = new List<UUID>();

        public UUID[] EstateAccess
        {
            get { return l_EstateAccess.ToArray(); }
            set { l_EstateAccess = new List<UUID>(value); }
        }

        private List<UUID> l_EstateGroups = new List<UUID>();

        public UUID[] EstateGroups
        {
            get { return l_EstateGroups.ToArray(); }
            set { l_EstateGroups = new List<UUID>(value); }
        }

        public EstateSettings()
        {
        }

        public void Save()
        {
            if (OnSave != null)
                OnSave(this);
        }

        public void AddEstateUser(UUID avatarID)
        {
            if (avatarID == UUID.Zero)
                return;
            if (!l_EstateAccess.Contains(avatarID))
                l_EstateAccess.Add(avatarID);
        }

        public void RemoveEstateUser(UUID avatarID)
        {
            if (l_EstateAccess.Contains(avatarID))
                l_EstateAccess.Remove(avatarID);
        }

        public void AddEstateGroup(UUID avatarID)
        {
            if (avatarID == UUID.Zero)
                return;
            if (!l_EstateGroups.Contains(avatarID))
                l_EstateGroups.Add(avatarID);
        }

        public void RemoveEstateGroup(UUID avatarID)
        {
            if (l_EstateGroups.Contains(avatarID))
                l_EstateGroups.Remove(avatarID);
        }

        public void AddEstateManager(UUID avatarID)
        {
            if (avatarID == UUID.Zero)
                return;
            if (!l_EstateManagers.Contains(avatarID))
                l_EstateManagers.Add(avatarID);
        }

        public void RemoveEstateManager(UUID avatarID)
        {
            if (l_EstateManagers.Contains(avatarID))
                l_EstateManagers.Remove(avatarID);
        }

        public bool IsEstateManagerOrOwner(UUID avatarID)
        {
            if (IsEstateOwner(avatarID))
                return true;

            return l_EstateManagers.Contains(avatarID);
        }

        public bool IsEstateOwner(UUID avatarID)
        {
            if (avatarID == m_EstateOwner)
                return true;

            return false;
        }

        public bool IsBanned(UUID avatarID)
        {
            foreach (EstateBan ban in l_EstateBans)
                if (ban.BannedUserID == avatarID)
                    return true;
            return false;
        }

        public void AddBan(EstateBan ban)
        {
            if (ban == null)
                return;
            if (!IsBanned(ban.BannedUserID))
                l_EstateBans.Add(ban);
        }

        public void ClearBans()
        {
            l_EstateBans.Clear();
        }

        public void RemoveBan(UUID avatarID)
        {
            foreach (EstateBan ban in new List<EstateBan>(l_EstateBans))
                if (ban.BannedUserID == avatarID)
                    l_EstateBans.Remove(ban);
        }

        public bool HasAccess(UUID user)
        {
            if (IsEstateManagerOrOwner(user))
                return true;

            return l_EstateAccess.Contains(user);
        }

        public void SetFromFlags(ulong regionFlags)
        {
            ResetHomeOnTeleport = ((regionFlags & (ulong)OpenMetaverse.RegionFlags.ResetHomeOnTeleport) == (ulong)OpenMetaverse.RegionFlags.ResetHomeOnTeleport);
            BlockDwell = ((regionFlags & (ulong)OpenMetaverse.RegionFlags.BlockDwell) == (ulong)OpenMetaverse.RegionFlags.BlockDwell);
            AllowLandmark = ((regionFlags & (ulong)OpenMetaverse.RegionFlags.AllowLandmark) == (ulong)OpenMetaverse.RegionFlags.AllowLandmark);
            AllowParcelChanges = ((regionFlags & (ulong)OpenMetaverse.RegionFlags.AllowParcelChanges) == (ulong)OpenMetaverse.RegionFlags.AllowParcelChanges);
            AllowSetHome = ((regionFlags & (ulong)OpenMetaverse.RegionFlags.AllowSetHome) == (ulong)OpenMetaverse.RegionFlags.AllowSetHome);
        }

        public bool GroupAccess(UUID groupID)
        {
            return l_EstateGroups.Contains(groupID);
        }

        public Dictionary<string, object> ToMap()
        {
            Dictionary<string, object> map = new Dictionary<string, object>();
            PropertyInfo[] properties = this.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (PropertyInfo p in properties)
            {
                // EstateBans is a complex type, let's treat it as special
                if (p.Name == "EstateBans")
                    continue;

                object value = p.GetValue(this, null);
                if (value != null)
                {
                    if (p.PropertyType.IsArray) // of UUIDs
                    {
                        if (((Array)value).Length > 0)
                        {
                            string[] args = new string[((Array)value).Length];
                            int index = 0;
                            foreach (object o in (Array)value)
                                args[index++] = o.ToString();
                            map[p.Name] = String.Join(",", args);
                        }
                    }
                    else // simple types
                        map[p.Name] = value;
                }
            }

            // EstateBans are special
            if (EstateBans.Length > 0)
            {
                Dictionary<string, object> bans = new Dictionary<string, object>();
                int i = 0;
                foreach (EstateBan ban in EstateBans)
                    bans["ban" + i++] = ban.ToMap();
                map["EstateBans"] = bans;
            }

            return map;
        }

        /// <summary>
        /// For debugging
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            Dictionary<string, object> map = ToMap();
            String result = String.Empty;

            foreach (KeyValuePair<string, object> kvp in map)
            {
                if (kvp.Key == "EstateBans")
                {
                    result += "EstateBans:" + Environment.NewLine;
                    foreach (KeyValuePair<string, object> ban in (Dictionary<string, object>)kvp.Value)
                        result += ban.Value.ToString();
                }
                else
                    result += string.Format("{0}: {1} {2}", kvp.Key, kvp.Value.ToString(), Environment.NewLine);
            }

            return result;
        }

        public EstateSettings(Dictionary<string, object> map)
        {
            foreach (KeyValuePair<string, object> kvp in map)
            {
                PropertyInfo p = this.GetType().GetProperty(kvp.Key, BindingFlags.Public | BindingFlags.Instance);
                if (p == null)
                    continue;

                // EstateBans is a complex type, let's treat it as special
                if (p.Name == "EstateBans")
                    continue;

                if (p.PropertyType.IsArray)
                {
                    string[] elements = ((string)map[p.Name]).Split(new char[] { ',' });
                    UUID[] uuids = new UUID[elements.Length];
                    int i = 0;
                    foreach (string e in elements)
                        uuids[i++] = new UUID(e);
                    p.SetValue(this, uuids, null);
                }
                else
                {
                    object value = p.GetValue(this, null);
                    if (value is String)
                        p.SetValue(this, map[p.Name], null);
                    else if (value is UInt32)
                        p.SetValue(this, UInt32.Parse((string)map[p.Name]), null);
                    else if (value is Boolean)
                        p.SetValue(this, Boolean.Parse((string)map[p.Name]), null);
                    else if (value is UUID)
                        p.SetValue(this, UUID.Parse((string)map[p.Name]), null);
                }
            }

            // EstateBans are special
            if (map.ContainsKey("EstateBans"))
            {
                var banData = ((Dictionary<string, object>)map["EstateBans"]).Values;
                EstateBan[] bans = new EstateBan[banData.Count];
                int b = 0;
                foreach (Dictionary<string, object> ban in banData)
                    bans[b++] = new EstateBan(ban);
                PropertyInfo bansProperty = this.GetType().GetProperty("EstateBans", BindingFlags.Public | BindingFlags.Instance);
                bansProperty.SetValue(this, bans, null);
            }
        }
    }
}
