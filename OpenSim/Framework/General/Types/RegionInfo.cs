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
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using Nini.Config;
using libsecondlife;
using OpenSim.Framework.Console;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Utilities;

using OpenSim.Framework.Configuration;


namespace OpenSim.Framework.Types
{
    public class RegionInfo
    {
        public LLUUID SimUUID = new LLUUID();
        public string RegionName = "";

        private IPEndPoint m_internalEndPoint;
        public IPEndPoint InternalEndPoint
        {
            get
            {
                return m_internalEndPoint;
            }
        }

        public IPEndPoint ExternalEndPoint
        {
            get
            {
                // Old one defaults to IPv6
                //return new IPEndPoint( Dns.GetHostAddresses( m_externalHostName )[0], m_internalEndPoint.Port );

                // New method favors IPv4
                IPAddress ia = null;
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
        }

        private string m_externalHostName;
        public string ExternalHostName
        {
            get
            {
                return m_externalHostName;
            }
        }

        private uint? m_regionLocX;
        public uint RegionLocX
        {
            get
            {
                return m_regionLocX.Value;
            }
        }

        private uint? m_regionLocY;
        public uint RegionLocY
        {
            get
            {
                return m_regionLocY.Value;
            }
        }

        private ulong? m_regionHandle;
        public ulong RegionHandle
        {
            get
            {
                if (!m_regionHandle.HasValue)
                {
                    m_regionHandle = Util.UIntsToLong((RegionLocX * 256), (RegionLocY * 256));
                }

                return m_regionHandle.Value;
            }
        }

        private uint m_remotingPort;
        public uint RemotingPort
        {
            get
            {
                return m_remotingPort;
            }
            set
            {
                m_remotingPort = value;
            }
        }
        public string RemotingAddress;

        public string DataStore = "";
        public bool isSandbox = false;

        public LLUUID MasterAvatarAssignedUUID = new LLUUID();
        public string MasterAvatarFirstName = "";
        public string MasterAvatarLastName = "";
        public string MasterAvatarSandboxPassword = "";

        public EstateSettings estateSettings;

        public ConfigurationMember configMember;
        public RegionInfo(string description, string filename)
        {
            estateSettings = new EstateSettings();
            configMember = new ConfigurationMember(filename, description, loadConfigurationOptions, handleIncomingConfiguration);
            configMember.performConfigurationRetrieve();
        }

        public RegionInfo(uint regionLocX, uint regionLocY, IPEndPoint internalEndPoint, string externalUri)
        {

            estateSettings = new EstateSettings();
            m_regionLocX = regionLocX;
            m_regionLocY = regionLocY;

            m_internalEndPoint = internalEndPoint;
            m_externalHostName = externalUri;
        }

        public void LoadFromNiniSource(IConfigSource source)
        {
            this.LoadFromNiniSource(source, "RegionInfo");
        }

        public void LoadFromNiniSource(IConfigSource source, string sectionName)
        {
            string errorMessage = "";
            this.SimUUID = new LLUUID(source.Configs[sectionName].GetString("sim_UUID", LLUUID.Random().ToStringHyphenated()));
            this.RegionName = source.Configs[sectionName].GetString("sim_name", "OpenSim Test");
            this.m_regionLocX = Convert.ToUInt32(source.Configs[sectionName].GetString("sim_location_x", "1000"));
            this.m_regionLocY = Convert.ToUInt32(source.Configs[sectionName].GetString("sim_location_y", "1000"));
            this.DataStore = source.Configs[sectionName].GetString("datastore", "OpenSim.db");

            string ipAddress = source.Configs[sectionName].GetString("internal_ip_address", "0.0.0.0");
            IPAddress ipAddressResult;
            if (IPAddress.TryParse(ipAddress, out ipAddressResult))
            {
                this.m_internalEndPoint = new IPEndPoint(ipAddressResult, 0);
            }
            else
            {
                errorMessage = "needs an IP Address (IPAddress)";
            }
            this.m_internalEndPoint.Port = source.Configs[sectionName].GetInt("internal_ip_port",(int) 9000);

            string externalHost = source.Configs[sectionName].GetString("external_host_name", "127.0.0.1");
            if (externalHost != "SYSTEMIP")
            {
                this.m_externalHostName = externalHost;
            }
            else
            {
                this.m_externalHostName = Util.GetLocalHost().ToString();
            }

            this.MasterAvatarFirstName = source.Configs[sectionName].GetString("master_avatar_first", "Test");
            this.MasterAvatarLastName = source.Configs[sectionName].GetString("master_avatar_last", "User");
            this.MasterAvatarSandboxPassword = source.Configs[sectionName].GetString("master_avatar_pass", "test");

            if (errorMessage != "")
            {
                // a error 
            }
        }

        public void loadConfigurationOptions()
        {
            configMember.addConfigurationOption("sim_UUID", ConfigurationOption.ConfigurationTypes.TYPE_LLUUID, "UUID of Simulator (Default is recommended, random UUID)", LLUUID.Random().ToString(), true);
            configMember.addConfigurationOption("sim_name", ConfigurationOption.ConfigurationTypes.TYPE_STRING_NOT_EMPTY, "Simulator Name", "OpenSim Test", false);
            configMember.addConfigurationOption("sim_location_x", ConfigurationOption.ConfigurationTypes.TYPE_UINT32, "Grid Location (X Axis)", "1000", false);
            configMember.addConfigurationOption("sim_location_y", ConfigurationOption.ConfigurationTypes.TYPE_UINT32, "Grid Location (Y Axis)", "1000", false);
            configMember.addConfigurationOption("datastore", ConfigurationOption.ConfigurationTypes.TYPE_STRING_NOT_EMPTY, "Filename for local storage", "OpenSim.db", false);
            configMember.addConfigurationOption("internal_ip_address", ConfigurationOption.ConfigurationTypes.TYPE_IP_ADDRESS, "Internal IP Address for incoming UDP client connections", "0.0.0.0", false);
            configMember.addConfigurationOption("internal_ip_port", ConfigurationOption.ConfigurationTypes.TYPE_INT32, "Internal IP Port for incoming UDP client connections", "9000", false);
            configMember.addConfigurationOption("external_host_name", ConfigurationOption.ConfigurationTypes.TYPE_STRING_NOT_EMPTY, "External Host Name", "127.0.0.1", false);
            configMember.addConfigurationOption("master_avatar_first", ConfigurationOption.ConfigurationTypes.TYPE_STRING_NOT_EMPTY, "First Name of Master Avatar", "Test", false);
            configMember.addConfigurationOption("master_avatar_last", ConfigurationOption.ConfigurationTypes.TYPE_STRING_NOT_EMPTY, "Last Name of Master Avatar", "User", false);
            configMember.addConfigurationOption("master_avatar_pass", ConfigurationOption.ConfigurationTypes.TYPE_STRING, "(Sandbox Mode Only)Password for Master Avatar account", "test", false);

        }

        public bool handleIncomingConfiguration(string configuration_key, object configuration_result)
        {
            switch (configuration_key)
            {
                case "sim_UUID":
                    this.SimUUID = (LLUUID)configuration_result;
                    break;
                case "sim_name":
                    this.RegionName = (string)configuration_result;
                    break;
                case "sim_location_x":
                    this.m_regionLocX = (uint)configuration_result;
                    break;
                case "sim_location_y":
                    this.m_regionLocY = (uint)configuration_result;
                    break;
                case "datastore":
                    this.DataStore = (string)configuration_result;
                    break;
                case "internal_ip_address":
                    IPAddress address = (IPAddress)configuration_result;
                    this.m_internalEndPoint = new IPEndPoint(address, 0);
                    break;
                case "internal_ip_port":
                    this.m_internalEndPoint.Port = (int)configuration_result;
                    break;
                case "external_host_name":
                    if ((string)configuration_result != "SYSTEMIP")
                    {
                        this.m_externalHostName = (string)configuration_result;
                    }
                    else
                    {
                        this.m_externalHostName = Util.GetLocalHost().ToString();
                    }
                    break;
                case "master_avatar_first":
                    this.MasterAvatarFirstName = (string)configuration_result;
                    break;
                case "master_avatar_last":
                    this.MasterAvatarLastName = (string)configuration_result;
                    break;
                case "master_avatar_pass":
                    string tempMD5Passwd = (string)configuration_result;
                    this.MasterAvatarSandboxPassword = Util.Md5Hash(Util.Md5Hash(tempMD5Passwd) + ":" + "");
                    break;
            }

            return true;
        }

    }
}
