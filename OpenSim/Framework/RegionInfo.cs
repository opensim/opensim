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
using System.Net;
using System.Net.Sockets;
using System.Xml;
using System.IO;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using OpenSim.Framework.Console;

namespace OpenSim.Framework
{
    [Serializable]
    public class SimpleRegionInfo
    {
        // private static readonly log4net.ILog m_log
        //     = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// The port by which http communication occurs with the region (most noticeably, CAPS communication)
        /// </summary>
        public uint HttpPort
        {
            get { return m_httpPort; }
            set { m_httpPort = value; }
        }
        protected uint m_httpPort;

        /// <summary>
        /// A well-formed URI for the host region server (namely "http://" + ExternalHostName)
        /// </summary>
        public string ServerURI
        {
            get { return m_serverURI; }
            set { m_serverURI = value; }
        }
        protected string m_serverURI;

        public string RegionName
        {
            get { return m_regionName; }
            set { m_regionName = value; }
        }
        protected string m_regionName = String.Empty;

        protected bool Allow_Alternate_Ports;
        public bool m_allow_alternate_ports;
        protected string m_externalHostName;

        protected IPEndPoint m_internalEndPoint;
        protected uint? m_regionLocX;
        protected uint? m_regionLocY;
        protected uint m_remotingPort;
        public UUID RegionID = UUID.Zero;
        public string RemotingAddress;
        public UUID ScopeID = UUID.Zero;

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
            m_regionName = ConvertFrom.RegionName;
            m_regionLocX = ConvertFrom.RegionLocX;
            m_regionLocY = ConvertFrom.RegionLocY;
            m_internalEndPoint = ConvertFrom.InternalEndPoint;
            m_externalHostName = ConvertFrom.ExternalHostName;
            m_remotingPort = ConvertFrom.RemotingPort;
            m_httpPort = ConvertFrom.HttpPort;
            m_allow_alternate_ports = ConvertFrom.m_allow_alternate_ports;
            RemotingAddress = ConvertFrom.RemotingAddress;
            RegionID = UUID.Zero;
            ServerURI = ConvertFrom.ServerURI;
        }

        public uint RemotingPort
        {
            get { return m_remotingPort; }
            set { m_remotingPort = value; }
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
                try
                {
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
                }
                catch (SocketException e)
                {
                    throw new Exception(
                        "Unable to resolve local hostname " + m_externalHostName + " innerException of type '" +
                        e + "' attached to this exception", e);
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

        public Dictionary<string, object> ToKeyValuePairs()
        {
            Dictionary<string, object> kvp = new Dictionary<string, object>();
            kvp["uuid"] = RegionID.ToString();
            kvp["locX"] = RegionLocX.ToString();
            kvp["locY"] = RegionLocY.ToString();
            kvp["external_ip_address"] = ExternalEndPoint.Address.ToString();
            kvp["external_port"] = ExternalEndPoint.Port.ToString();
            kvp["external_host_name"] = ExternalHostName;
            kvp["http_port"] = HttpPort.ToString();
            kvp["internal_ip_address"] = InternalEndPoint.Address.ToString();
            kvp["internal_port"] = InternalEndPoint.Port.ToString();
            kvp["alternate_ports"] = m_allow_alternate_ports.ToString();
            kvp["server_uri"] = ServerURI;

            return kvp;
        }

        public SimpleRegionInfo(Dictionary<string, object> kvp)
        {
            if ((kvp["external_ip_address"] != null) && (kvp["external_port"] != null))
            {
                int port = 0;
                Int32.TryParse((string)kvp["external_port"], out port);
                IPEndPoint ep = new IPEndPoint(IPAddress.Parse((string)kvp["external_ip_address"]), port);
                ExternalEndPoint = ep;
            }
            else
                ExternalEndPoint = new IPEndPoint(IPAddress.Parse("0.0.0.0"), 0);

            if (kvp["external_host_name"] != null)
                ExternalHostName = (string)kvp["external_host_name"];

            if (kvp["http_port"] != null)
            {
                UInt32 port = 0;
                UInt32.TryParse((string)kvp["http_port"], out port);
                HttpPort = port;
            }

            if ((kvp["internal_ip_address"] != null) && (kvp["internal_port"] != null))
            {
                int port = 0;
                Int32.TryParse((string)kvp["internal_port"], out port);
                IPEndPoint ep = new IPEndPoint(IPAddress.Parse((string)kvp["internal_ip_address"]), port);
                InternalEndPoint = ep;
            }
            else
                InternalEndPoint = new IPEndPoint(IPAddress.Parse("0.0.0.0"), 0);

            if (kvp["alternate_ports"] != null)
            {
                bool alts = false;
                Boolean.TryParse((string)kvp["alternate_ports"], out alts);
                m_allow_alternate_ports = alts;
            }

            if (kvp["server_uri"] != null)
                ServerURI = (string)kvp["server_uri"];
        }
    }

    public class RegionInfo : SimpleRegionInfo
    {
        // private static readonly log4net.ILog m_log
        //     = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public bool commFailTF = false;
        public ConfigurationMember configMember;
        public string DataStore = String.Empty;
        public string RegionFile = String.Empty;
        public bool isSandbox = false;
        public bool Persistent = true;

        private EstateSettings m_estateSettings;
        private RegionSettings m_regionSettings;
        // private IConfigSource m_configSource = null;

        public UUID MasterAvatarAssignedUUID = UUID.Zero;
        public string MasterAvatarFirstName = String.Empty;
        public string MasterAvatarLastName = String.Empty;
        public string MasterAvatarSandboxPassword = String.Empty;
        public UUID originRegionID = UUID.Zero;
        public string proxyUrl = "";
        public int ProxyOffset = 0;
        public string regionSecret = UUID.Random().ToString();
        
        public string osSecret;

        public UUID lastMapUUID = UUID.Zero;
        public string lastMapRefresh = "0";

        private int m_nonphysPrimMax = 0;
        private int m_physPrimMax = 0;
        private bool m_clampPrimSize = false;
        private int m_objectCapacity = 0;


        // Apparently, we're applying the same estatesettings regardless of whether it's local or remote.

        // MT: Yes. Estates can't span trust boundaries. Therefore, it can be
        // assumed that all instances belonging to one estate are able to
        // access the same database server. Since estate settings are lodaed
        // from there, that should be sufficient for full remote administration

        // File based loading
        //
        public RegionInfo(string description, string filename, bool skipConsoleConfig, IConfigSource configSource) : this(description, filename, skipConsoleConfig, configSource, String.Empty)
        {
        }

        public RegionInfo(string description, string filename, bool skipConsoleConfig, IConfigSource configSource, string configName)
        {
            // m_configSource = configSource;

            if (filename.ToLower().EndsWith(".ini"))
            {
                if (!File.Exists(filename)) // New region config request
                {
                    IniConfigSource newFile = new IniConfigSource();
                    ReadNiniConfig(newFile, String.Empty);

                    newFile.Save(filename);

                    RegionFile = filename;

                    return;
                }

                IniConfigSource source = new IniConfigSource(filename);

                bool saveFile = false;
                if (source.Configs[configName] == null)
                    saveFile = true;

                ReadNiniConfig(source, configName);

                if (configName != String.Empty && saveFile)
                    source.Save(filename);

                RegionFile = filename;

                return;
            }

            try
            {
                // This will throw if it's not legal Nini XML format
                // and thereby toss it to the legacy loader
                //
                IConfigSource xmlsource = new XmlConfigSource(filename);

                ReadNiniConfig(xmlsource, configName);

                RegionFile = filename;

                return;
            }
            catch (Exception)
            {
            }

            configMember =
                new ConfigurationMember(filename, description, loadConfigurationOptions, handleIncomingConfiguration, !skipConsoleConfig);
            configMember.performConfigurationRetrieve();
            RegionFile = filename;
        }

        // The web loader uses this
        //
        public RegionInfo(string description, XmlNode xmlNode, bool skipConsoleConfig, IConfigSource configSource)
        {
            // m_configSource = configSource;
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
            RegionID = UUID.Zero;
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
            RegionID = UUID.Zero;
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

            set { m_estateSettings = value; }
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

        public int NonphysPrimMax
        {
            get { return m_nonphysPrimMax; }
        }

        public int PhysPrimMax
        {
            get { return m_physPrimMax; }
        }

        public bool ClampPrimSize
        {
            get { return m_clampPrimSize; }
        }

        public int ObjectCapacity
        {
            get { return m_objectCapacity; }
        }

        public byte AccessLevel
        {
            get { return (byte)Util.ConvertMaturityToAccessLevel((uint)RegionSettings.Maturity); }
        }

        public void SetEndPoint(string ipaddr, int port)
        {
            IPAddress tmpIP = IPAddress.Parse(ipaddr);
            IPEndPoint tmpEPE = new IPEndPoint(tmpIP, port);
            m_internalEndPoint = tmpEPE;
        }

        private void ReadNiniConfig(IConfigSource source, string name)
        {
            bool creatingNew = false;

            if (source.Configs.Count == 0)
            {
                MainConsole.Instance.Output("=====================================\n");
                MainConsole.Instance.Output("We are now going to ask a couple of questions about your region.\n");
                MainConsole.Instance.Output("You can press 'enter' without typing anything to use the default\n");
                MainConsole.Instance.Output("the default is displayed between [ ] brackets.\n");
                MainConsole.Instance.Output("=====================================\n");

                if (name == String.Empty)
                    name = MainConsole.Instance.CmdPrompt("New region name", name);
                if (name == String.Empty)
                    throw new Exception("Cannot interactively create region with no name");

                source.AddConfig(name);

                creatingNew = true;
            }

            if (name == String.Empty)
                name = source.Configs[0].Name;

            if (source.Configs[name] == null)
            {
                source.AddConfig(name);

                creatingNew = true;
            }

            IConfig config = source.Configs[name];

            // UUID
            //
            string regionUUID = config.GetString("RegionUUID", string.Empty);

            if (regionUUID == String.Empty)
            {
                UUID newID = UUID.Random();

                regionUUID = MainConsole.Instance.CmdPrompt("Region UUID", newID.ToString());
                config.Set("RegionUUID", regionUUID);
            }

            RegionID = new UUID(regionUUID);
            originRegionID = RegionID; // What IS this?!

            
            // Region name
            //
            RegionName = name;

            
            // Region location
            //
            string location = config.GetString("Location", String.Empty);

            if (location == String.Empty)
            {
                location = MainConsole.Instance.CmdPrompt("Region Location", "1000,1000");
                config.Set("Location", location);
            }

            string[] locationElements = location.Split(new char[] {','});

            m_regionLocX = Convert.ToUInt32(locationElements[0]);
            m_regionLocY = Convert.ToUInt32(locationElements[1]);


            // Datastore (is this implemented? Omitted from example!)
            //
            DataStore = config.GetString("Datastore", String.Empty);


            // Internal IP
            //
            IPAddress address;
            
            if (config.Contains("InternalAddress"))
            {
                address = IPAddress.Parse(config.GetString("InternalAddress", String.Empty));
            }
            else
            {
                address = IPAddress.Parse(MainConsole.Instance.CmdPrompt("Internal IP address", "0.0.0.0"));
                config.Set("InternalAddress", address.ToString());
            }

            int port;

            if (config.Contains("InternalPort"))
            {
                port = config.GetInt("InternalPort", 9000);
            }
            else
            {
                port = Convert.ToInt32(MainConsole.Instance.CmdPrompt("Internal port", "9000"));
                config.Set("InternalPort", port);
            }

            m_internalEndPoint = new IPEndPoint(address, port);

            if (config.Contains("AllowAlternatePorts"))
            {
                m_allow_alternate_ports = config.GetBoolean("AllowAlternatePorts", true);
            }
            else
            {
                m_allow_alternate_ports = Convert.ToBoolean(MainConsole.Instance.CmdPrompt("Allow alternate ports", "False"));

                config.Set("AllowAlternatePorts", m_allow_alternate_ports.ToString());
            }

            // External IP
            //
            string externalName;

            if (config.Contains("ExternalHostName"))
            {
                externalName = config.GetString("ExternalHostName", "SYSTEMIP");
            }
            else
            {
                externalName = MainConsole.Instance.CmdPrompt("External host name", "SYSTEMIP");
                config.Set("ExternalHostName", externalName);
            }

            if (externalName == "SYSTEMIP")
                m_externalHostName = Util.GetLocalHost().ToString();
            else
                m_externalHostName = externalName;


            // Master avatar cruft
            //
            string masterAvatarUUID;
            if (!creatingNew)
            {
                masterAvatarUUID = config.GetString("MasterAvatarUUID", UUID.Zero.ToString());
                MasterAvatarFirstName = config.GetString("MasterAvatarFirstName", String.Empty);
                MasterAvatarLastName = config.GetString("MasterAvatarLastName", String.Empty);
                MasterAvatarSandboxPassword = config.GetString("MasterAvatarSandboxPassword", String.Empty);
            }
            else
            {
                masterAvatarUUID = MainConsole.Instance.CmdPrompt("Master Avatar UUID", UUID.Zero.ToString());
                if (masterAvatarUUID != UUID.Zero.ToString())
                {
                    config.Set("MasterAvatarUUID", masterAvatarUUID);
                }
                else
                {
                    MasterAvatarFirstName = MainConsole.Instance.CmdPrompt("Master Avatar first name (enter for no master avatar)", String.Empty);
                    if (MasterAvatarFirstName != String.Empty)
                    {
                        MasterAvatarLastName = MainConsole.Instance.CmdPrompt("Master Avatar last name", String.Empty);
                        MasterAvatarSandboxPassword = MainConsole.Instance.CmdPrompt("Master Avatar sandbox password", String.Empty);
                        
                        config.Set("MasterAvatarFirstName", MasterAvatarFirstName);
                        config.Set("MasterAvatarLastName", MasterAvatarLastName);
                        config.Set("MasterAvatarSandboxPassword", MasterAvatarSandboxPassword);
                    }
                }
            }

            MasterAvatarAssignedUUID = new UUID(masterAvatarUUID);


            
            // Prim stuff
            //
            m_nonphysPrimMax = config.GetInt("NonphysicalPrimMax", 256);

            m_physPrimMax = config.GetInt("PhysicalPrimMax", 10);

            m_clampPrimSize = config.GetBoolean("ClampPrimSize", false);

            m_objectCapacity = config.GetInt("MaxPrims", 15000);


            // Multi-tenancy
            //
            ScopeID = new UUID(config.GetString("ScopeID", UUID.Zero.ToString()));
        }

        private void WriteNiniConfig(IConfigSource source)
        {
            IConfig config = source.Configs[RegionName];

            if (config != null)
                source.Configs.Remove(RegionName);

            config = source.AddConfig(RegionName);

            config.Set("RegionUUID", RegionID.ToString());

            string location = String.Format("{0},{1}", m_regionLocX, m_regionLocY);
            config.Set("Location", location);

            if (DataStore != String.Empty)
                config.Set("Datastore", DataStore);

            config.Set("InternalAddress", m_internalEndPoint.Address.ToString());
            config.Set("InternalPort", m_internalEndPoint.Port);

            config.Set("AllowAlternatePorts", m_allow_alternate_ports.ToString());

            config.Set("ExternalHostName", m_externalHostName);

            if (MasterAvatarAssignedUUID != UUID.Zero)
            {
                config.Set("MasterAvatarUUID", MasterAvatarAssignedUUID.ToString());
            }
            else if (MasterAvatarFirstName != String.Empty && MasterAvatarLastName != String.Empty)
            {
                config.Set("MasterAvatarFirstName", MasterAvatarFirstName);
                config.Set("MasterAvatarLastName", MasterAvatarLastName);
            }
            if (MasterAvatarSandboxPassword != String.Empty)
            {
                config.Set("MasterAvatarSandboxPassword", MasterAvatarSandboxPassword);
            }

            if (m_nonphysPrimMax != 0)
                config.Set("NonphysicalPrimMax", m_nonphysPrimMax);
            if (m_physPrimMax != 0)
                config.Set("PhysicalPrimMax", m_physPrimMax);
            config.Set("ClampPrimSize", m_clampPrimSize.ToString());

            if (m_objectCapacity != 0)
                config.Set("MaxPrims", m_objectCapacity);

            if (ScopeID != UUID.Zero)
                config.Set("ScopeID", ScopeID.ToString());
        }

        public bool ignoreIncomingConfiguration(string configuration_key, object configuration_result)
        {
            return true;
        }

        public void SaveRegionToFile(string description, string filename)
        {
            if (filename.ToLower().EndsWith(".ini"))
            {
                IniConfigSource source = new IniConfigSource();
                try
                {
                    source = new IniConfigSource(filename); // Load if it exists
                }
                catch (Exception)
                {
                }

                WriteNiniConfig(source);

                source.Save(filename);

                return;
            }
            configMember = new ConfigurationMember(filename, description, loadConfigurationOptionsFromMe,
                                                   ignoreIncomingConfiguration, false);
            configMember.performConfigurationRetrieve();
            RegionFile = filename;
        }

        public void loadConfigurationOptionsFromMe()
        {
            configMember.addConfigurationOption("sim_UUID", ConfigurationOption.ConfigurationTypes.TYPE_UUID_NULL_FREE,
                                                "UUID of Region (Default is recommended, random UUID)",
                                                RegionID.ToString(), true);
            configMember.addConfigurationOption("sim_name", ConfigurationOption.ConfigurationTypes.TYPE_STRING_NOT_EMPTY,
                                                "Region Name", RegionName, true);
            configMember.addConfigurationOption("sim_location_x", ConfigurationOption.ConfigurationTypes.TYPE_UINT32,
                                                "Grid Location (X Axis)", m_regionLocX.ToString(), true);
            configMember.addConfigurationOption("sim_location_y", ConfigurationOption.ConfigurationTypes.TYPE_UINT32,
                                                "Grid Location (Y Axis)", m_regionLocY.ToString(), true);
            //m_configMember.addConfigurationOption("datastore", ConfigurationOption.ConfigurationTypes.TYPE_STRING_NOT_EMPTY, "Filename for local storage", "OpenSim.db", false);
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
            configMember.addConfigurationOption("master_avatar_uuid", ConfigurationOption.ConfigurationTypes.TYPE_UUID,
                                                "Master Avatar UUID", MasterAvatarAssignedUUID.ToString(), true);
            configMember.addConfigurationOption("master_avatar_first",
                                                ConfigurationOption.ConfigurationTypes.TYPE_STRING_NOT_EMPTY,
                                                "First Name of Master Avatar", MasterAvatarFirstName, true);
            configMember.addConfigurationOption("master_avatar_last",
                                                ConfigurationOption.ConfigurationTypes.TYPE_STRING_NOT_EMPTY,
                                                "Last Name of Master Avatar", MasterAvatarLastName, true);
            configMember.addConfigurationOption("master_avatar_pass", ConfigurationOption.ConfigurationTypes.TYPE_STRING,
                                                "(Sandbox Mode Only)Password for Master Avatar account",
                                                MasterAvatarSandboxPassword, true);
            configMember.addConfigurationOption("lastmap_uuid", ConfigurationOption.ConfigurationTypes.TYPE_UUID,
                                                "Last Map UUID", lastMapUUID.ToString(), true);
            configMember.addConfigurationOption("lastmap_refresh", ConfigurationOption.ConfigurationTypes.TYPE_STRING_NOT_EMPTY,
                                                "Last Map Refresh", Util.UnixTimeSinceEpoch().ToString(), true);

            configMember.addConfigurationOption("nonphysical_prim_max", ConfigurationOption.ConfigurationTypes.TYPE_INT32,
                                                "Maximum size for nonphysical prims", m_nonphysPrimMax.ToString(), true);
            
            configMember.addConfigurationOption("physical_prim_max", ConfigurationOption.ConfigurationTypes.TYPE_INT32,
                                                "Maximum size for physical prims", m_physPrimMax.ToString(), true);
            
            configMember.addConfigurationOption("clamp_prim_size", ConfigurationOption.ConfigurationTypes.TYPE_BOOLEAN,
                                                "Clamp prims to max size", m_clampPrimSize.ToString(), true);
            
            configMember.addConfigurationOption("object_capacity", ConfigurationOption.ConfigurationTypes.TYPE_INT32,
                                                "Max objects this sim will hold", m_objectCapacity.ToString(), true);
            
            configMember.addConfigurationOption("scope_id", ConfigurationOption.ConfigurationTypes.TYPE_UUID,
                                                "Scope ID for this region", ScopeID.ToString(), true);
        }

        public void loadConfigurationOptions()
        {
            configMember.addConfigurationOption("sim_UUID", ConfigurationOption.ConfigurationTypes.TYPE_UUID,
                                                "UUID of Region (Default is recommended, random UUID)",
                                                UUID.Random().ToString(), true);
            configMember.addConfigurationOption("sim_name", ConfigurationOption.ConfigurationTypes.TYPE_STRING_NOT_EMPTY,
                                                "Region Name", "OpenSim Test", false);
            configMember.addConfigurationOption("sim_location_x", ConfigurationOption.ConfigurationTypes.TYPE_UINT32,
                                                "Grid Location (X Axis)", "1000", false);
            configMember.addConfigurationOption("sim_location_y", ConfigurationOption.ConfigurationTypes.TYPE_UINT32,
                                                "Grid Location (Y Axis)", "1000", false);
            //m_configMember.addConfigurationOption("datastore", ConfigurationOption.ConfigurationTypes.TYPE_STRING_NOT_EMPTY, "Filename for local storage", "OpenSim.db", false);
            configMember.addConfigurationOption("internal_ip_address",
                                                ConfigurationOption.ConfigurationTypes.TYPE_IP_ADDRESS,
                                                "Internal IP Address for incoming UDP client connections", "0.0.0.0",
                                                false);
            configMember.addConfigurationOption("internal_ip_port", ConfigurationOption.ConfigurationTypes.TYPE_INT32,
                                                "Internal IP Port for incoming UDP client connections",
                                                ConfigSettings.DefaultRegionHttpPort.ToString(), false);
            configMember.addConfigurationOption("allow_alternate_ports", ConfigurationOption.ConfigurationTypes.TYPE_BOOLEAN,
                                                "Allow sim to find alternate UDP ports when ports are in use?",
                                                "false", true);
            configMember.addConfigurationOption("external_host_name",
                                                ConfigurationOption.ConfigurationTypes.TYPE_STRING_NOT_EMPTY,
                                                "External Host Name", "127.0.0.1", false);
            configMember.addConfigurationOption("master_avatar_uuid", ConfigurationOption.ConfigurationTypes.TYPE_UUID,
                                                "Master Avatar UUID", UUID.Zero.ToString(), true);
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
            configMember.addConfigurationOption("lastmap_uuid", ConfigurationOption.ConfigurationTypes.TYPE_UUID,
                                    "Last Map UUID", lastMapUUID.ToString(), true);

            configMember.addConfigurationOption("lastmap_refresh", ConfigurationOption.ConfigurationTypes.TYPE_STRING_NOT_EMPTY,
                                                "Last Map Refresh", Util.UnixTimeSinceEpoch().ToString(), true);
            
            configMember.addConfigurationOption("nonphysical_prim_max", ConfigurationOption.ConfigurationTypes.TYPE_INT32,
                                                "Maximum size for nonphysical prims", "0", true);
            
            configMember.addConfigurationOption("physical_prim_max", ConfigurationOption.ConfigurationTypes.TYPE_INT32,
                                                "Maximum size for physical prims", "0", true);
            
            configMember.addConfigurationOption("clamp_prim_size", ConfigurationOption.ConfigurationTypes.TYPE_BOOLEAN,
                                                "Clamp prims to max size", "false", true);
            
            configMember.addConfigurationOption("object_capacity", ConfigurationOption.ConfigurationTypes.TYPE_INT32,
                                                "Max objects this sim will hold", "0", true);

            configMember.addConfigurationOption("scope_id", ConfigurationOption.ConfigurationTypes.TYPE_UUID,
                                                "Scope ID for this region", UUID.Zero.ToString(), true);
        }

        public bool shouldMasterAvatarDetailsBeAsked(string configuration_key)
        {
            return MasterAvatarAssignedUUID == UUID.Zero;
        }

        public bool handleIncomingConfiguration(string configuration_key, object configuration_result)
        {
            switch (configuration_key)
            {
                case "sim_UUID":
                    RegionID = (UUID) configuration_result;
                    originRegionID = (UUID) configuration_result;
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
                    MasterAvatarAssignedUUID = (UUID) configuration_result;
                    break;
                case "master_avatar_first":
                    MasterAvatarFirstName = (string) configuration_result;
                    break;
                case "master_avatar_last":
                    MasterAvatarLastName = (string) configuration_result;
                    break;
                case "master_avatar_pass":
                    MasterAvatarSandboxPassword = (string)configuration_result;
                    break;
                case "lastmap_uuid":
                    lastMapUUID = (UUID)configuration_result;
                    break;
                case "lastmap_refresh":
                    lastMapRefresh = (string)configuration_result;
                    break;
                case "nonphysical_prim_max":
                    m_nonphysPrimMax = (int)configuration_result;
                    break;
                case "physical_prim_max":
                    m_physPrimMax = (int)configuration_result;
                    break;
                case "clamp_prim_size":
                    m_clampPrimSize = (bool)configuration_result;
                    break;
                case "object_capacity":
                    m_objectCapacity = (int)configuration_result;
                    break;
                case "scope_id":
                    ScopeID = (UUID)configuration_result;
                    break;
            }

            return true;
        }

        public void SaveLastMapUUID(UUID mapUUID)
        {
            if (null == configMember) return;

            lastMapUUID = mapUUID;
            lastMapRefresh = Util.UnixTimeSinceEpoch().ToString();

            configMember.forceSetConfigurationOption("lastmap_uuid", mapUUID.ToString());
            configMember.forceSetConfigurationOption("lastmap_refresh", lastMapRefresh);
        }

        public OSDMap PackRegionInfoData()
        {
            OSDMap args = new OSDMap();
            args["region_id"] = OSD.FromUUID(RegionID);
            if ((RegionName != null) && !RegionName.Equals(""))
                args["region_name"] = OSD.FromString(RegionName);
            args["external_host_name"] = OSD.FromString(ExternalHostName);
            args["http_port"] = OSD.FromString(HttpPort.ToString());
            args["server_uri"] = OSD.FromString(ServerURI);
            args["region_xloc"] = OSD.FromString(RegionLocX.ToString());
            args["region_yloc"] = OSD.FromString(RegionLocY.ToString());
            args["internal_ep_address"] = OSD.FromString(InternalEndPoint.Address.ToString());
            args["internal_ep_port"] = OSD.FromString(InternalEndPoint.Port.ToString());
            if ((RemotingAddress != null) && !RemotingAddress.Equals(""))
                args["remoting_address"] = OSD.FromString(RemotingAddress);
            args["remoting_port"] = OSD.FromString(RemotingPort.ToString());
            args["allow_alt_ports"] = OSD.FromBoolean(m_allow_alternate_ports);
            if ((proxyUrl != null) && !proxyUrl.Equals(""))
                args["proxy_url"] = OSD.FromString(proxyUrl);

            return args;
        }

        public void UnpackRegionInfoData(OSDMap args)
        {
            if (args["region_id"] != null)
                RegionID = args["region_id"].AsUUID();
            if (args["region_name"] != null)
                RegionName = args["region_name"].AsString();
            if (args["external_host_name"] != null)
                ExternalHostName = args["external_host_name"].AsString();
            if (args["http_port"] != null)
                UInt32.TryParse(args["http_port"].AsString(), out m_httpPort);
            if (args["server_uri"] != null)
                ServerURI = args["server_uri"].AsString();
            if (args["region_xloc"] != null)
            {
                uint locx;
                UInt32.TryParse(args["region_xloc"].AsString(), out locx);
                RegionLocX = locx;
            }
            if (args["region_yloc"] != null)
            {
                uint locy;
                UInt32.TryParse(args["region_yloc"].AsString(), out locy);
                RegionLocY = locy;
            }
            IPAddress ip_addr = null;
            if (args["internal_ep_address"] != null)
            {
                IPAddress.TryParse(args["internal_ep_address"].AsString(), out ip_addr);
            }
            int port = 0;
            if (args["internal_ep_port"] != null)
            {
                Int32.TryParse(args["internal_ep_port"].AsString(), out port);
            }
            InternalEndPoint = new IPEndPoint(ip_addr, port);
            if (args["remoting_address"] != null)
                RemotingAddress = args["remoting_address"].AsString();
            if (args["remoting_port"] != null)
                UInt32.TryParse(args["remoting_port"].AsString(), out m_remotingPort);
            if (args["allow_alt_ports"] != null)
                m_allow_alternate_ports = args["allow_alt_ports"].AsBoolean();
            if (args["proxy_url"] != null)
                proxyUrl = args["proxy_url"].AsString();
        }

        public static RegionInfo Create(UUID regionID, string regionName, uint regX, uint regY, string externalHostName, uint httpPort, uint simPort, uint remotingPort, string serverURI)
        {
            RegionInfo regionInfo;
            IPEndPoint neighbourInternalEndPoint = new IPEndPoint(Util.GetHostFromDNS(externalHostName), (int)simPort);
            regionInfo = new RegionInfo(regX, regY, neighbourInternalEndPoint, externalHostName);
            regionInfo.RemotingPort = remotingPort;
            regionInfo.RemotingAddress = externalHostName;
            regionInfo.HttpPort = httpPort;
            regionInfo.RegionID = regionID;
            regionInfo.RegionName = regionName;
            regionInfo.ServerURI = serverURI;
            return regionInfo;
        }

    }
}
