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
using OpenMetaverse;

namespace OpenSim.Framework
{
    public class EstateSettings
    {
        // private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private readonly ConfigurationMember configMember;

        public delegate void SaveDelegate(EstateSettings rs);

        public event SaveDelegate OnSave;

        // Only the client uses these
        //
        private uint m_EstateID = 100;

        public uint EstateID
        {
            get { return m_EstateID; }
            set { m_EstateID = value; }
        }

        private string m_EstateName;

        public string EstateName
        {
            get { return m_EstateName; }
            set { m_EstateName = value; }
        }

        private uint m_ParentEstateID = 100;

        public uint ParentEstateID
        {
            get { return m_ParentEstateID; }
            set { m_ParentEstateID = value; }
        }

        private float m_BillableFactor;

        public float BillableFactor
        {
            get { return m_BillableFactor; }
            set { m_BillableFactor = value; }
        }

        private int m_PricePerMeter;

        public int PricePerMeter
        {
            get { return m_PricePerMeter; }
            set { m_PricePerMeter = value; }
        }

        private int m_RedirectGridX;

        public int RedirectGridX
        {
            get { return m_RedirectGridX; }
            set { m_RedirectGridX = value; }
        }

        private int m_RedirectGridY;

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
            if (configMember == null)
            {
                try
                {
                    // Load legacy defaults
                    //
                    configMember =
                        new ConfigurationMember(Path.Combine(Util.configDir(),
                                "estate_settings.xml"), "ESTATE SETTINGS",
                                loadConfigurationOptions,
                                handleIncomingConfiguration, true);

                    l_EstateManagers.Clear();
                    configMember.performConfigurationRetrieve();
                }
                catch (Exception)
                {
                }
            }
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

        public bool IsEstateManager(UUID avatarID)
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
            if (IsEstateManager(user))
                return true;

            return l_EstateAccess.Contains(user);
        }

        public void loadConfigurationOptions()
        {
            configMember.addConfigurationOption("billable_factor",
                    ConfigurationOption.ConfigurationTypes.TYPE_FLOAT,
                    String.Empty, "0.0", true);

//            configMember.addConfigurationOption("estate_id",
//                    ConfigurationOption.ConfigurationTypes.TYPE_UINT32,
//                    String.Empty, "100", true);

//            configMember.addConfigurationOption("parent_estate_id",
//                    ConfigurationOption.ConfigurationTypes.TYPE_UINT32,
//                    String.Empty, "1", true);

            configMember.addConfigurationOption("redirect_grid_x",
                    ConfigurationOption.ConfigurationTypes.TYPE_INT32,
                    String.Empty, "0", true);

            configMember.addConfigurationOption("redirect_grid_y",
                    ConfigurationOption.ConfigurationTypes.TYPE_INT32,
                    String.Empty, "0", true);

            configMember.addConfigurationOption("price_per_meter",
                    ConfigurationOption.ConfigurationTypes.TYPE_UINT32,
                    String.Empty, "1", true);

            configMember.addConfigurationOption("estate_name",
                    ConfigurationOption.ConfigurationTypes.TYPE_STRING,
                    String.Empty, "My Estate", true);

            configMember.addConfigurationOption("estate_manager_0",
                    ConfigurationOption.ConfigurationTypes.TYPE_UUID,
                    String.Empty, "00000000-0000-0000-0000-000000000000", true);

            configMember.addConfigurationOption("estate_manager_1",
                    ConfigurationOption.ConfigurationTypes.TYPE_UUID,
                    String.Empty, "00000000-0000-0000-0000-000000000000", true);

            configMember.addConfigurationOption("estate_manager_2",
                    ConfigurationOption.ConfigurationTypes.TYPE_UUID,
                    String.Empty, "00000000-0000-0000-0000-000000000000", true);

            configMember.addConfigurationOption("estate_manager_3",
                    ConfigurationOption.ConfigurationTypes.TYPE_UUID,
                    String.Empty, "00000000-0000-0000-0000-000000000000", true);

            configMember.addConfigurationOption("estate_manager_4",
                    ConfigurationOption.ConfigurationTypes.TYPE_UUID,
                    String.Empty, "00000000-0000-0000-0000-000000000000", true);

            configMember.addConfigurationOption("estate_manager_5",
                    ConfigurationOption.ConfigurationTypes.TYPE_UUID,
                    String.Empty, "00000000-0000-0000-0000-000000000000", true);

            configMember.addConfigurationOption("estate_manager_6",
                    ConfigurationOption.ConfigurationTypes.TYPE_UUID,
                    String.Empty, "00000000-0000-0000-0000-000000000000", true);

            configMember.addConfigurationOption("estate_manager_7",
                    ConfigurationOption.ConfigurationTypes.TYPE_UUID,
                    String.Empty, "00000000-0000-0000-0000-000000000000", true);

            configMember.addConfigurationOption("estate_manager_8",
                    ConfigurationOption.ConfigurationTypes.TYPE_UUID,
                    String.Empty, "00000000-0000-0000-0000-000000000000", true);

            configMember.addConfigurationOption("estate_manager_9",
                    ConfigurationOption.ConfigurationTypes.TYPE_UUID,
                    String.Empty, "00000000-0000-0000-0000-000000000000", true);

            configMember.addConfigurationOption("region_flags",
                    ConfigurationOption.ConfigurationTypes.TYPE_UINT32,
                    String.Empty, "336723974", true);
        }

        public bool handleIncomingConfiguration(string configuration_key, object configuration_result)
        {
            switch (configuration_key)
            {
                case "region_flags":
                    RegionFlags flags = (RegionFlags)(uint)configuration_result;
                    if ((flags & (RegionFlags)(1<<29)) != 0)
                        m_AllowVoice = true;
                    if ((flags & RegionFlags.AllowDirectTeleport) != 0)
                        m_AllowDirectTeleport = true;
                    if ((flags & RegionFlags.DenyAnonymous) != 0)
                         m_DenyAnonymous = true;
                    if ((flags & RegionFlags.DenyIdentified) != 0)
                        m_DenyIdentified = true;
                    if ((flags & RegionFlags.DenyTransacted) != 0)
                        m_DenyTransacted = true;
                    if ((flags & RegionFlags.AbuseEmailToEstateOwner) != 0)
                        m_AbuseEmailToEstateOwner = true;
                    if ((flags & RegionFlags.BlockDwell) != 0)
                        m_BlockDwell = true;
                    if ((flags & RegionFlags.EstateSkipScripts) != 0)
                        m_EstateSkipScripts = true;
                    if ((flags & RegionFlags.ResetHomeOnTeleport) != 0)
                        m_ResetHomeOnTeleport = true;
                    if ((flags & RegionFlags.TaxFree) != 0)
                        m_TaxFree = true;
                    if ((flags & RegionFlags.PublicAllowed) != 0)
                        m_PublicAccess = true;
                    break;
                case "billable_factor":
                    m_BillableFactor = (float) configuration_result;
                    break;
//                case "estate_id":
//                    m_EstateID = (uint) configuration_result;
//                    break;
//                case "parent_estate_id":
//                    m_ParentEstateID = (uint) configuration_result;
//                    break;
                case "redirect_grid_x":
                    m_RedirectGridX = (int) configuration_result;
                    break;
                case "redirect_grid_y":
                    m_RedirectGridY = (int) configuration_result;
                    break;
                case "price_per_meter":
                    m_PricePerMeter = Convert.ToInt32(configuration_result);
                    break;
                case "estate_name":
                    m_EstateName = (string) configuration_result;
                    break;
                case "estate_manager_0":
                    AddEstateManager((UUID)configuration_result);
                    break;
                case "estate_manager_1":
                    AddEstateManager((UUID)configuration_result);
                    break;
                case "estate_manager_2":
                    AddEstateManager((UUID)configuration_result);
                    break;
                case "estate_manager_3":
                    AddEstateManager((UUID)configuration_result);
                    break;
                case "estate_manager_4":
                    AddEstateManager((UUID)configuration_result);
                    break;
                case "estate_manager_5":
                    AddEstateManager((UUID)configuration_result);
                    break;
                case "estate_manager_6":
                    AddEstateManager((UUID)configuration_result);
                    break;
                case "estate_manager_7":
                    AddEstateManager((UUID)configuration_result);
                    break;
                case "estate_manager_8":
                    AddEstateManager((UUID)configuration_result);
                    break;
                case "estate_manager_9":
                    AddEstateManager((UUID)configuration_result);
                    break;
            }

            return true;
        }
    }
}
