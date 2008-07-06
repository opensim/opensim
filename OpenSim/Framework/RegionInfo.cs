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
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Xml;
using libsecondlife;
using Nini.Config;

namespace OpenSim.Framework
{
    [Serializable]
    public class SimpleRegionInfo
    {
        // private static readonly log4net.ILog m_log
        //     = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        protected bool Allow_Alternate_Ports;
        public bool m_allow_alternate_ports;
        protected string m_externalHostName;

        /// <value>
        /// The port by which http communication occurs with the region (most noticeably, CAPS communication)
        /// </value>
        protected uint m_httpPort;

        protected IPEndPoint m_internalEndPoint;
        protected uint? m_regionLocX;
        protected uint? m_regionLocY;
        protected uint m_remotingPort;
        protected string m_serverURI;
        public LLUUID RegionID = LLUUID.Zero;
        public string RemotingAddress;

        public SimpleRegionInfo()
        {
        }

        public SimpleRegionInfo(uint regionLocX, uint regionLocY, IPEndPoint internalEndPoint, string externalUri)
        {
            m_regionLocX = regionLocX;
            m_regionLocY = regionLocY;

            m_internalEndPoint = internalEndPoint;
            m_externalHostName = externalUri;
        }

        public SimpleRegionInfo(uint regionLocX, uint regionLocY, string externalUri, uint port)
        {
            m_regionLocX = regionLocX;
            m_regionLocY = regionLocY;

            m_externalHostName = externalUri;

            m_internalEndPoint = new IPEndPoint(IPAddress.Parse("0.0.0.0"), (int) port);
        }

        public SimpleRegionInfo(RegionInfo ConvertFrom)
        {
            m_regionLocX = ConvertFrom.RegionLocX;
            m_regionLocY = ConvertFrom.RegionLocY;
            m_internalEndPoint = ConvertFrom.InternalEndPoint;
            m_externalHostName = ConvertFrom.ExternalHostName;
            m_remotingPort = ConvertFrom.RemotingPort;
            m_httpPort = ConvertFrom.HttpPort;
            m_allow_alternate_ports = ConvertFrom.m_allow_alternate_ports;
            RemotingAddress = ConvertFrom.RemotingAddress;
            RegionID = LLUUID.Zero;
            ServerURI = ConvertFrom.ServerURI;
        }

        public uint RemotingPort
        {
            get { return m_remotingPort; }
            set { m_remotingPort = value; }
        }

        public uint HttpPort
        {
            get { return m_httpPort; }
            set { m_httpPort = value; }
        }

        public string ServerURI
        {
            get { return m_serverURI; }
            set { m_serverURI = value; }
        }

        /// <value>
        /// This accessor can throw all the exceptions that Dns.GetHostAddresses can throw.
        ///
        /// XXX Isn't this really doing too much to be a simple getter, rather than an explict method?
        /// </value>
        public IPEndPoint ExternalEndPoint
        {
            get
            {
                // Old one defaults to IPv6
                //return new IPEndPoint(Dns.GetHostAddresses(m_externalHostName)[0], m_internalEndPoint.Port);

                IPAddress ia = null;
                // If it is already an IP, don't resolve it - just return directly
                if (IPAddress.TryParse(m_externalHostName, out ia))
                    return new IPEndPoint(ia, m_internalEndPoint.Port);

                // Reset for next check
                ia = null;

                foreach (IPAddress Adr in Dns.GetHostAddresses(m_externalHostName))
                {
                    if (ia == null)
                        ia = Adr;

                    if (Adr.AddressFamily == AddressFamily.InterNetwork)
                    {
                        ia = Adr;
                        break;
                    }
                }

                return new IPEndPoint(ia, m_internalEndPoint.Port);
            }

            set { m_externalHostName = value.ToString(); }
        }

        public string ExternalHostName
        {
            get { return m_externalHostName; }
            set { m_externalHostName = value; }
        }

        public IPEndPoint InternalEndPoint
        {
            get { return m_internalEndPoint; }
            set { m_internalEndPoint = value; }
        }

        public uint RegionLocX
        {
            get { return m_regionLocX.Value; }
            set { m_regionLocX = value; }
        }

        public uint RegionLocY
        {
            get { return m_regionLocY.Value; }
            set { m_regionLocY = value; }
        }

        public ulong RegionHandle
        {
            get { return Util.UIntsToLong((RegionLocX * (uint) Constants.RegionSize), (RegionLocY * (uint) Constants.RegionSize)); }
        }

        public int getInternalEndPointPort()
        {
            return m_internalEndPoint.Port;
        }
    }

    public class RegionInfo : SimpleRegionInfo
    {
        // private static readonly log4net.ILog m_log
        //     = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public bool commFailTF = false;
        public ConfigurationMember configMember;
        public LLUUID CovenantID = LLUUID.Zero;
        public string DataStore = String.Empty;
        public bool isSandbox = false;
        private EstateSettings m_estateSettings;
        private RegionSettings m_regionSettings;

//        public bool m_allow_alternate_ports;

        public LLUUID MasterAvatarAssignedUUID = LLUUID.Zero;
        public string MasterAvatarFirstName = String.Empty;
        public string MasterAvatarLastName = String.Empty;
        public string MasterAvatarSandboxPassword = String.Empty;
        public LLUUID originRegionID = LLUUID.Zero;
        public string proxyUrl = "";
        public string RegionName = String.Empty;
        public string regionSecret = LLUUID.Random().ToString();

        public LLUUID lastMapUUID = LLUUID.Zero;
        public string lastMapRefresh = "0";
        public List<RegionBanListItem> regionBanlist = new List<RegionBanListItem>();

        // Apparently, we're applying the same estatesettings regardless of whether it's local or remote.

        public RegionInfo(string description, string filename, bool skipConsoleConfig)
        {
            configMember =
                new ConfigurationMember(filename, description, loadConfigurationOptions, handleIncomingConfiguration, !skipConsoleConfig);
            configMember.performConfigurationRetrieve();
        }

        public RegionInfo(string description, XmlNode xmlNode, bool skipConsoleConfig)
        {
            configMember =
                new ConfigurationMember(xmlNode, description, loadConfigurationOptions, handleIncomingConfiguration, !skipConsoleConfig);
            configMember.performConfigurationRetrieve();
        }

        public RegionInfo(uint regionLocX, uint regionLocY, IPEndPoint internalEndPoint, string externalUri) :
            base(regionLocX, regionLocY, internalEndPoint, externalUri)
        {
        }

        public RegionInfo()
        {
        }

        public RegionInfo(SerializableRegionInfo ConvertFrom)
        {
            m_regionLocX = ConvertFrom.RegionLocX;
            m_regionLocY = ConvertFrom.RegionLocY;
            m_internalEndPoint = ConvertFrom.InternalEndPoint;
            m_externalHostName = ConvertFrom.ExternalHostName;
            m_remotingPort = ConvertFrom.RemotingPort;
            m_allow_alternate_ports = ConvertFrom.m_allow_alternate_ports;
            RemotingAddress = ConvertFrom.RemotingAddress;
            RegionID = LLUUID.Zero;
            proxyUrl = ConvertFrom.ProxyUrl;
            originRegionID = ConvertFrom.OriginRegionID;
            RegionName = ConvertFrom.RegionName;
            ServerURI = ConvertFrom.ServerURI;
        }

        public RegionInfo(SimpleRegionInfo ConvertFrom)
        {
            m_regionLocX = ConvertFrom.RegionLocX;
            m_regionLocY = ConvertFrom.RegionLocY;
            m_internalEndPoint = ConvertFrom.InternalEndPoint;
            m_externalHostName = ConvertFrom.ExternalHostName;
            m_remotingPort = ConvertFrom.RemotingPort;
            m_allow_alternate_ports = ConvertFrom.m_allow_alternate_ports;
            RemotingAddress = ConvertFrom.RemotingAddress;
            RegionID = LLUUID.Zero;
            ServerURI = ConvertFrom.ServerURI;
        }

        public EstateSettings EstateSettings
        {
            get
            {
                if (m_estateSettings == null)
                {
                    m_estateSettings = new EstateSettings();
                }

                return m_estateSettings;
            }
        }

        public RegionSettings RegionSettings
        {
            get
            {
                if (m_regionSettings == null)
                {
                    m_regionSettings = new RegionSettings();
                }

                return m_regionSettings;
            }

            set { m_regionSettings = value; }
        }

        public void SetEndPoint(string ipaddr, int port)
        {
            IPAddress tmpIP = IPAddress.Parse(ipaddr);
            IPEndPoint tmpEPE = new IPEndPoint(tmpIP, port);
            m_internalEndPoint = tmpEPE;
        }

        //not in use, should swap to nini though.
        public void LoadFromNiniSource(IConfigSource source)
        {
            LoadFromNiniSource(source, "RegionInfo");
        }

        //not in use, should swap to nini though.
        public void LoadFromNiniSource(IConfigSource source, string sectionName)
        {
            string errorMessage = String.Empty;
            RegionID = new LLUUID(source.Configs[sectionName].GetString("Region_ID", LLUUID.Random().ToString()));
            RegionName = source.Configs[sectionName].GetString("sim_name", "OpenSim Test");
            m_regionLocX = Convert.ToUInt32(source.Configs[sectionName].GetString("sim_location_x", "1000"));
            m_regionLocY = Convert.ToUInt32(source.Configs[sectionName].GetString("sim_location_y", "1000"));
            // this.DataStore = source.Configs[sectionName].GetString("datastore", "OpenSim.db");

            string ipAddress = source.Configs[sectionName].GetString("internal_ip_address", "0.0.0.0");
            IPAddress ipAddressResult;
            if (IPAddress.TryParse(ipAddress, out ipAddressResult))
            {
                m_internalEndPoint = new IPEndPoint(ipAddressResult, 0);
            }
            else
            {
                errorMessage = "needs an IP Address (IPAddress)";
            }
            m_internalEndPoint.Port =
                source.Configs[sectionName].GetInt("internal_ip_port", (int) NetworkServersInfo.DefaultHttpListenerPort);

            string externalHost = source.Configs[sectionName].GetString("external_host_name", "127.0.0.1");
            if (externalHost != "SYSTEMIP")
            {
                m_externalHostName = externalHost;
            }
            else
            {
                m_externalHostName = Util.GetLocalHost().ToString();
            }

            MasterAvatarFirstName = source.Configs[sectionName].GetString("master_avatar_first", "Test");
            MasterAvatarLastName = source.Configs[sectionName].GetString("master_avatar_last", "User");
            MasterAvatarSandboxPassword = source.Configs[sectionName].GetString("master_avatar_pass", "test");

            if (errorMessage != String.Empty)
            {
                // a error
            }
        }

        public bool ignoreIncomingConfiguration(string configuration_key, object configuration_result)
        {
            return true;
        }

        public void SaveRegionToFile(string description, string filename)
        {
            configMember = new ConfigurationMember(filename, description, loadConfigurationOptionsFromMe,
                                                   ignoreIncomingConfiguration, false);
            configMember.performConfigurationRetrieve();
        }

        public bool CheckIfUserBanned(LLUUID user)
        {

            RegionBanListItem[] bl = regionBanlist.ToArray();
            
            bool banned = false;

            for (int i = 0; i < bl.Length; i++)
            {
                if (bl[i] == null)
                    continue;
            
                if (bl[i].bannedUUID == user)
                {
                    banned = true;
                    break;
                }
            }

            return banned;
        }

        public void loadConfigurationOptionsFromMe()
        {
            configMember.addConfigurationOption("sim_UUID", ConfigurationOption.ConfigurationTypes.TYPE_LLUUID_NULL_FREE,
                                                "UUID of Region (Default is recommended, random UUID)",
                                                RegionID.ToString(), true);
            configMember.addConfigurationOption("sim_name", ConfigurationOption.ConfigurationTypes.TYPE_STRING_NOT_EMPTY,
                                                "Region Name", RegionName, true);
            configMember.addConfigurationOption("sim_location_x", ConfigurationOption.ConfigurationTypes.TYPE_UINT32,
                                                "Grid Location (X Axis)", m_regionLocX.ToString(), true);
            configMember.addConfigurationOption("sim_location_y", ConfigurationOption.ConfigurationTypes.TYPE_UINT32,
                                                "Grid Location (Y Axis)", m_regionLocY.ToString(), true);
            //configMember.addConfigurationOption("datastore", ConfigurationOption.ConfigurationTypes.TYPE_STRING_NOT_EMPTY, "Filename for local storage", "OpenSim.db", false);
            configMember.addConfigurationOption("internal_ip_address",
                                                ConfigurationOption.ConfigurationTypes.TYPE_IP_ADDRESS,
                                                "Internal IP Address for incoming UDP client connections",
                                                m_internalEndPoint.Address.ToString(),
                                                true);
            configMember.addConfigurationOption("internal_ip_port", ConfigurationOption.ConfigurationTypes.TYPE_INT32,
                                                "Internal IP Port for incoming UDP client connections",
                                                m_internalEndPoint.Port.ToString(), true);
            configMember.addConfigurationOption("allow_alternate_ports",
                                                ConfigurationOption.ConfigurationTypes.TYPE_BOOLEAN,
                                                "Allow sim to find alternate UDP ports when ports are in use?",
                                                m_allow_alternate_ports.ToString(), true);
            configMember.addConfigurationOption("external_host_name",
                                                ConfigurationOption.ConfigurationTypes.TYPE_STRING_NOT_EMPTY,
                                                "External Host Name", m_externalHostName, true);
            configMember.addConfigurationOption("master_avatar_uuid", ConfigurationOption.ConfigurationTypes.TYPE_LLUUID,
                                                "Master Avatar UUID", MasterAvatarAssignedUUID.ToString(), true);
            configMember.addConfigurationOption("estate_covanant_uuid",
                                                ConfigurationOption.ConfigurationTypes.TYPE_LLUUID, "Estate Covenant",
                                                CovenantID.ToString(), true);
            configMember.addConfigurationOption("master_avatar_first",
                                                ConfigurationOption.ConfigurationTypes.TYPE_STRING_NOT_EMPTY,
                                                "First Name of Master Avatar", MasterAvatarFirstName, true);
            configMember.addConfigurationOption("master_avatar_last",
                                                ConfigurationOption.ConfigurationTypes.TYPE_STRING_NOT_EMPTY,
                                                "Last Name of Master Avatar", MasterAvatarLastName, true);
            configMember.addConfigurationOption("master_avatar_pass", ConfigurationOption.ConfigurationTypes.TYPE_STRING,
                                                "(Sandbox Mode Only)Password for Master Avatar account",
                                                MasterAvatarSandboxPassword, true);
            configMember.addConfigurationOption("lastmap_uuid", ConfigurationOption.ConfigurationTypes.TYPE_LLUUID, 
                                                "Last Map UUID", lastMapUUID.ToString(), true);
            configMember.addConfigurationOption("lastmap_refresh", ConfigurationOption.ConfigurationTypes.TYPE_STRING_NOT_EMPTY, 
                                                "Last Map Refresh", Util.UnixTimeSinceEpoch().ToString(), true);
            
        }

        public void loadConfigurationOptions()
        {
            configMember.addConfigurationOption("sim_UUID", ConfigurationOption.ConfigurationTypes.TYPE_LLUUID,
                                                "UUID of Region (Default is recommended, random UUID)",
                                                LLUUID.Random().ToString(), true);
            configMember.addConfigurationOption("sim_name", ConfigurationOption.ConfigurationTypes.TYPE_STRING_NOT_EMPTY,
                                                "Region Name", "OpenSim Test", false);
            configMember.addConfigurationOption("sim_location_x", ConfigurationOption.ConfigurationTypes.TYPE_UINT32,
                                                "Grid Location (X Axis)", "1000", false);
            configMember.addConfigurationOption("sim_location_y", ConfigurationOption.ConfigurationTypes.TYPE_UINT32,
                                                "Grid Location (Y Axis)", "1000", false);
            //configMember.addConfigurationOption("datastore", ConfigurationOption.ConfigurationTypes.TYPE_STRING_NOT_EMPTY, "Filename for local storage", "OpenSim.db", false);
            configMember.addConfigurationOption("internal_ip_address",
                                                ConfigurationOption.ConfigurationTypes.TYPE_IP_ADDRESS,
                                                "Internal IP Address for incoming UDP client connections", "0.0.0.0",
                                                false);
            configMember.addConfigurationOption("internal_ip_port", ConfigurationOption.ConfigurationTypes.TYPE_INT32,
                                                "Internal IP Port for incoming UDP client connections",
                                                NetworkServersInfo.DefaultHttpListenerPort.ToString(), false);
            configMember.addConfigurationOption("allow_alternate_ports", ConfigurationOption.ConfigurationTypes.TYPE_BOOLEAN,
                                                "Allow sim to find alternate UDP ports when ports are in use?",
                                                "false", true);
            configMember.addConfigurationOption("external_host_name",
                                                ConfigurationOption.ConfigurationTypes.TYPE_STRING_NOT_EMPTY,
                                                "External Host Name", "127.0.0.1", false);
            configMember.addConfigurationOption("master_avatar_uuid", ConfigurationOption.ConfigurationTypes.TYPE_LLUUID,
                                                "Master Avatar UUID", LLUUID.Zero.ToString(), true);
            configMember.addConfigurationOption("estate_covanant_uuid",
                                                ConfigurationOption.ConfigurationTypes.TYPE_LLUUID, "Estate Covenant",
                                                LLUUID.Zero.ToString(), true);
            configMember.addConfigurationOption("master_avatar_first",
                                                ConfigurationOption.ConfigurationTypes.TYPE_STRING_NOT_EMPTY,
                                                "First Name of Master Avatar", "Test", false,
                                                (ConfigurationOption.ConfigurationOptionShouldBeAsked)
                                                shouldMasterAvatarDetailsBeAsked);
            configMember.addConfigurationOption("master_avatar_last",
                                                ConfigurationOption.ConfigurationTypes.TYPE_STRING_NOT_EMPTY,
                                                "Last Name of Master Avatar", "User", false,
                                                (ConfigurationOption.ConfigurationOptionShouldBeAsked)
                                                shouldMasterAvatarDetailsBeAsked);
            configMember.addConfigurationOption("master_avatar_pass", ConfigurationOption.ConfigurationTypes.TYPE_STRING,
                                                "(Sandbox Mode Only)Password for Master Avatar account", "test", false,
                                                (ConfigurationOption.ConfigurationOptionShouldBeAsked)
                                                shouldMasterAvatarDetailsBeAsked);
            configMember.addConfigurationOption("lastmap_uuid", ConfigurationOption.ConfigurationTypes.TYPE_LLUUID,
                                    "Last Map UUID", lastMapUUID.ToString(), true);
            
            configMember.addConfigurationOption("lastmap_refresh", ConfigurationOption.ConfigurationTypes.TYPE_STRING_NOT_EMPTY, 
                                                "Last Map Refresh", Util.UnixTimeSinceEpoch().ToString(), true);
            
        }

        public bool shouldMasterAvatarDetailsBeAsked(string configuration_key)
        {
            if (MasterAvatarAssignedUUID == LLUUID.Zero)
            {
                return true;
            }
            return false;
        }

        public bool handleIncomingConfiguration(string configuration_key, object configuration_result)
        {
            switch (configuration_key)
            {
                case "sim_UUID":
                    RegionID = (LLUUID) configuration_result;
                    originRegionID = (LLUUID) configuration_result;
                    break;
                case "sim_name":
                    RegionName = (string) configuration_result;
                    break;
                case "sim_location_x":
                    m_regionLocX = (uint) configuration_result;
                    break;
                case "sim_location_y":
                    m_regionLocY = (uint) configuration_result;
                    break;
                case "datastore":
                    DataStore = (string) configuration_result;
                    break;
                case "internal_ip_address":
                    IPAddress address = (IPAddress) configuration_result;
                    m_internalEndPoint = new IPEndPoint(address, 0);
                    break;
                case "internal_ip_port":
                    m_internalEndPoint.Port = (int) configuration_result;
                    break;
                case "allow_alternate_ports":
                    m_allow_alternate_ports = (bool) configuration_result;
                    break;
                case "external_host_name":
                    if ((string) configuration_result != "SYSTEMIP")
                    {
                        m_externalHostName = (string) configuration_result;
                    }
                    else
                    {
                        m_externalHostName = Util.GetLocalHost().ToString();
                    }
                    break;
                case "master_avatar_uuid":
                    MasterAvatarAssignedUUID = (LLUUID) configuration_result;
                    break;
                case "estate_covanant_uuid":
                    CovenantID = (LLUUID) configuration_result;
                    break;

                case "master_avatar_first":
                    MasterAvatarFirstName = (string) configuration_result;
                    break;
                case "master_avatar_last":
                    MasterAvatarLastName = (string) configuration_result;
                    break;
                case "master_avatar_pass":
                    string tempMD5Passwd = (string) configuration_result;
                    MasterAvatarSandboxPassword = Util.Md5Hash(Util.Md5Hash(tempMD5Passwd) + ":" + String.Empty);
                    break;
                case "lastmap_uuid":
                    lastMapUUID = (LLUUID)configuration_result;
                    break;
                case "lastmap_refresh":
                    lastMapRefresh = (string)configuration_result;
                    break;
            }

            return true;
        }

        public void SaveEstatecovenantUUID(LLUUID notecard)
        {
            configMember.forceSetConfigurationOption("estate_covanant_uuid", notecard.ToString());
        }
        public void SaveLastMapUUID(LLUUID mapUUID)
        {
            lastMapUUID = mapUUID;
            lastMapRefresh = Util.UnixTimeSinceEpoch().ToString();

            configMember.forceSetConfigurationOption("lastmap_uuid", mapUUID.ToString());
            configMember.forceSetConfigurationOption("lastmap_refresh", lastMapRefresh);
        }
    }
}
