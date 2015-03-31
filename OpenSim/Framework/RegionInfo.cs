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
using System.Reflection;
using System.Xml;
using System.IO;
using log4net;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
//using OpenSim.Framework.Console;

namespace OpenSim.Framework
{
    public class RegionLightShareData : ICloneable
    {
        public bool valid = false;
        public UUID regionID = UUID.Zero;
        public Vector3 waterColor = new Vector3(4.0f,38.0f,64.0f);
        public float waterFogDensityExponent = 4.0f;
        public float underwaterFogModifier = 0.25f;
        public Vector3 reflectionWaveletScale = new Vector3(2.0f,2.0f,2.0f);
        public float fresnelScale = 0.40f;
        public float fresnelOffset = 0.50f;
        public float refractScaleAbove = 0.03f;
        public float refractScaleBelow = 0.20f;
        public float blurMultiplier = 0.040f;
        public Vector2 bigWaveDirection = new Vector2(1.05f,-0.42f);
        public Vector2 littleWaveDirection = new Vector2(1.11f,-1.16f);
        public UUID normalMapTexture = new UUID("822ded49-9a6c-f61c-cb89-6df54f42cdf4");
        public Vector4 horizon = new Vector4(0.25f, 0.25f, 0.32f, 0.32f);
        public float hazeHorizon = 0.19f;
        public Vector4 blueDensity = new Vector4(0.12f, 0.22f, 0.38f, 0.38f);
        public float hazeDensity = 0.70f;
        public float densityMultiplier = 0.18f;
        public float distanceMultiplier = 0.8f;
        public UInt16 maxAltitude = 1605;
        public Vector4 sunMoonColor = new Vector4(0.24f, 0.26f, 0.30f, 0.30f);
        public float sunMoonPosition = 0.317f;
        public Vector4 ambient = new Vector4(0.35f,0.35f,0.35f,0.35f);
        public float eastAngle = 0.0f;
        public float sunGlowFocus = 0.10f;
        public float sunGlowSize = 1.75f;
        public float sceneGamma = 1.0f;
        public float starBrightness = 0.0f;
        public Vector4 cloudColor = new Vector4(0.41f, 0.41f, 0.41f, 0.41f);
        public Vector3 cloudXYDensity = new Vector3(1.00f, 0.53f, 1.00f);
        public float cloudCoverage = 0.27f;
        public float cloudScale = 0.42f;
        public Vector3 cloudDetailXYDensity = new Vector3(1.00f, 0.53f, 0.12f);
        public float cloudScrollX = 0.20f;
        public bool cloudScrollXLock = false;
        public float cloudScrollY = 0.01f;
        public bool cloudScrollYLock = false;
        public bool drawClassicClouds = true;

        public delegate void SaveDelegate(RegionLightShareData wl);
        public event SaveDelegate OnSave;
        public void Save()
        {
            if (OnSave != null)
                OnSave(this);
        }
        public object Clone()
        {
            return this.MemberwiseClone();      // call clone method
        }

    }

    public class RegionInfo
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private static readonly string LogHeader = "[REGION INFO]";

        public string RegionFile = String.Empty;
        public bool isSandbox = false;
        public bool Persistent = true;

        private EstateSettings m_estateSettings;
        private RegionSettings m_regionSettings;
        // private IConfigSource m_configSource = null;

        public UUID originRegionID = UUID.Zero;
        public string proxyUrl = "";
        public int ProxyOffset = 0;
        public string regionSecret = UUID.Random().ToString();

        public string osSecret;

        public UUID lastMapUUID = UUID.Zero;
        public string lastMapRefresh = "0";

        private float m_nonphysPrimMin = 0;
        private int m_nonphysPrimMax = 0;
        private float m_physPrimMin = 0;
        private int m_physPrimMax = 0;
        private bool m_clampPrimSize = false;
        private int m_objectCapacity = 0;
        private int m_maxPrimsPerUser = -1;
        private int m_linksetCapacity = 0;
        private string m_regionType = String.Empty;
        private RegionLightShareData m_windlight = new RegionLightShareData();
        protected uint m_httpPort;
        protected string m_serverURI;
        protected string m_regionName = String.Empty;
        protected bool Allow_Alternate_Ports;
        public bool m_allow_alternate_ports;
        protected string m_externalHostName;
        protected IPEndPoint m_internalEndPoint;
        protected uint m_remotingPort;
        public UUID RegionID = UUID.Zero;
        public string RemotingAddress;
        public UUID ScopeID = UUID.Zero;
        private UUID m_maptileStaticUUID = UUID.Zero;

        public uint WorldLocX = 0;
        public uint WorldLocY = 0;
        public uint WorldLocZ = 0;

        /// <summary>
        /// X dimension of the region.
        /// </summary>
        /// <remarks>
        /// If this is a varregion then the default size set here will be replaced when we load the region config.
        /// </remarks>
        public uint RegionSizeX = Constants.RegionSize;

        /// <summary>
        /// X dimension of the region.
        /// </summary>
        /// <remarks>
        /// If this is a varregion then the default size set here will be replaced when we load the region config.
        /// </remarks>
        public uint RegionSizeY = Constants.RegionSize;

        /// <summary>
        /// Z dimension of the region.
        /// </summary>
        /// <remarks>
        /// XXX: Unknown if this accounts for regions with negative Z.
        /// </remarks>
        public uint RegionSizeZ = Constants.RegionHeight;

        private Dictionary<String, String> m_extraSettings = new Dictionary<string, string>();

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
                    ReadNiniConfig(newFile, configName);

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
                //
                IConfigSource xmlsource = new XmlConfigSource(filename);

                ReadNiniConfig(xmlsource, configName);

                RegionFile = filename;

                return;
            }
            catch (Exception)
            {
            }
        }

        // The web loader uses this
        //
        public RegionInfo(string description, XmlNode xmlNode, bool skipConsoleConfig, IConfigSource configSource)
        {
            XmlElement elem = (XmlElement)xmlNode;
            string name = elem.GetAttribute("Name");
            string xmlstr = "<Nini>" + xmlNode.OuterXml + "</Nini>";
            XmlConfigSource source = new XmlConfigSource(XmlReader.Create(new StringReader(xmlstr)));
            ReadNiniConfig(source, name);

            m_serverURI = string.Empty;
        }

        public RegionInfo(uint legacyRegionLocX, uint legacyRegionLocY, IPEndPoint internalEndPoint, string externalUri)
        {
            RegionLocX = legacyRegionLocX;
            RegionLocY = legacyRegionLocY;
            RegionSizeX = Constants.RegionSize;
            RegionSizeY = Constants.RegionSize;
            m_internalEndPoint = internalEndPoint;
            m_externalHostName = externalUri;
            m_serverURI = string.Empty;
        }

        public RegionInfo()
        {
            m_serverURI = string.Empty;
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

        public RegionLightShareData WindlightSettings
        {
            get
            {
                if (m_windlight == null)
                {
                    m_windlight = new RegionLightShareData();
                }

                return m_windlight;
            }

            set { m_windlight = value; }
        }

        public float NonphysPrimMin
        {
            get { return m_nonphysPrimMin; }
        }

        public int NonphysPrimMax
        {
            get { return m_nonphysPrimMax; }
        }

        public float PhysPrimMin
        {
            get { return m_physPrimMin; }
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

        public int MaxPrimsPerUser
        {
            get { return m_maxPrimsPerUser; }
        }

        public int LinksetCapacity
        {
            get { return m_linksetCapacity; }
        }

        public int AgentCapacity { get; set; }

        public byte AccessLevel
        {
            get { return (byte)Util.ConvertMaturityToAccessLevel((uint)RegionSettings.Maturity); }
        }

        public string RegionType
        {
            get { return m_regionType; }
        }

        public UUID MaptileStaticUUID
        {
            get { return m_maptileStaticUUID; }
        }

        public string MaptileStaticFile { get; private set; }
        
        /// <summary>
        /// The port by which http communication occurs with the region (most noticeably, CAPS communication)
        /// </summary>
        public uint HttpPort
        {
            get { return m_httpPort; }
            set { m_httpPort = value; }
        }

        /// <summary>
        /// A well-formed URI for the host region server (namely "http://" + ExternalHostName)
        /// </summary>
        
        public string ServerURI
        {
            get { 
                if ( m_serverURI != string.Empty ) {
                    return m_serverURI;
                } else {
                    return "http://" + m_externalHostName + ":" + m_httpPort + "/";
                }
            }            
            set { 
                if ( value.EndsWith("/") ) {
                    m_serverURI = value;
                } else {
                    m_serverURI = value + '/';
                }
            }
        }

        public string RegionName
        {
            get { return m_regionName; }
            set { m_regionName = value; }
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

        /// <summary>
        /// The x co-ordinate of this region in map tiles (e.g. 1000).
        /// Coordinate is scaled as world coordinates divided by the legacy region size
        /// and is thus is the number of legacy regions.
        /// </summary>
        public uint RegionLocX
        {
            get { return WorldLocX / Constants.RegionSize; }
            set { WorldLocX = value * Constants.RegionSize; }
        }

        /// <summary>
        /// The y co-ordinate of this region in map tiles (e.g. 1000).
        /// Coordinate is scaled as world coordinates divided by the legacy region size
        /// and is thus is the number of legacy regions.
        /// </summary>
        public uint RegionLocY
        {
            get { return WorldLocY / Constants.RegionSize; }
            set { WorldLocY = value * Constants.RegionSize; }
        }

        public void SetDefaultRegionSize()
        {
            WorldLocX = 0;
            WorldLocY = 0;
            WorldLocZ = 0;
            RegionSizeX = Constants.RegionSize;
            RegionSizeY = Constants.RegionSize;
            RegionSizeZ = Constants.RegionHeight;
        }

        // A unique region handle is created from the region's world coordinates.
        // This cannot be changed because some code expects to receive the region handle and then
        //    compute the region coordinates from it.
        public ulong RegionHandle
        {
            get { return Util.UIntsToLong(WorldLocX, WorldLocY); }
        }

        public void SetEndPoint(string ipaddr, int port)
        {
            IPAddress tmpIP = IPAddress.Parse(ipaddr);
            IPEndPoint tmpEPE = new IPEndPoint(tmpIP, port);
            m_internalEndPoint = tmpEPE;
        }

        public string GetSetting(string key)
        {
            string val;
            string keylower = key.ToLower();
            if (m_extraSettings.TryGetValue(keylower, out val))
                return val;
            m_log.DebugFormat("[RegionInfo] Could not locate value for parameter {0}", key);
            return null;
        }

        private void SetExtraSetting(string key, string value)
        {
            string keylower = key.ToLower();
            m_extraSettings[keylower] = value;
        }

        private void ReadNiniConfig(IConfigSource source, string name)
        {
//            bool creatingNew = false;

            if (source.Configs.Count == 0)
            {
                MainConsole.Instance.Output("=====================================\n");
                MainConsole.Instance.Output("We are now going to ask a couple of questions about your region.\n");
                MainConsole.Instance.Output("You can press 'enter' without typing anything to use the default\n");
                MainConsole.Instance.Output("the default is displayed between [ ] brackets.\n");
                MainConsole.Instance.Output("=====================================\n");

                if (name == String.Empty)
                {
                    while (name.Trim() == string.Empty)
                    {
                        name = MainConsole.Instance.CmdPrompt("New region name", name);
                        if (name.Trim() == string.Empty)
                        {
                            MainConsole.Instance.Output("Cannot interactively create region with no name");
                        }
                    }
                }

                source.AddConfig(name);

//                creatingNew = true;
            }

            if (name == String.Empty)
                name = source.Configs[0].Name;

            if (source.Configs[name] == null)
            {
                source.AddConfig(name);
            }

            RegionName = name;
            IConfig config = source.Configs[name];

            // Track all of the keys in this config and remove as they are processed
            // The remaining keys will be added to generic key-value storage for
            // whoever might need it
            HashSet<String> allKeys = new HashSet<String>();
            foreach (string s in config.GetKeys())
            {
                allKeys.Add(s);
            }

            // RegionUUID
            //
            allKeys.Remove("RegionUUID");
            string regionUUID = config.GetString("RegionUUID", string.Empty);
            if (!UUID.TryParse(regionUUID.Trim(), out RegionID))
            {
                UUID newID = UUID.Random();
                while (RegionID == UUID.Zero)
                {
                    regionUUID = MainConsole.Instance.CmdPrompt("RegionUUID", newID.ToString());
                    if (!UUID.TryParse(regionUUID.Trim(), out RegionID))
                    {
                        MainConsole.Instance.Output("RegionUUID must be a valid UUID");
                    }
                }
                config.Set("RegionUUID", regionUUID);
            }

            originRegionID = RegionID; // What IS this?! (Needed for RegionCombinerModule?)

            // Location
            //
            allKeys.Remove("Location");
            string location = config.GetString("Location", String.Empty);
            if (location == String.Empty)
            {
                location = MainConsole.Instance.CmdPrompt("Region Location", "1000,1000");
                config.Set("Location", location);
            }

            string[] locationElements = location.Split(new char[] {','});

            RegionLocX = Convert.ToUInt32(locationElements[0]);
            RegionLocY = Convert.ToUInt32(locationElements[1]);

            // Region size
            // Default to legacy region size if not specified.
            allKeys.Remove("SizeX");
            string configSizeX = config.GetString("SizeX", Constants.RegionSize.ToString());
            config.Set("SizeX", configSizeX);
            RegionSizeX = Convert.ToUInt32(configSizeX);
            allKeys.Remove("SizeY");
            string configSizeY = config.GetString("SizeY", Constants.RegionSize.ToString());
            config.Set("SizeY", configSizeX);
            RegionSizeY = Convert.ToUInt32(configSizeY);
            allKeys.Remove("SizeZ");
            string configSizeZ = config.GetString("SizeZ", Constants.RegionHeight.ToString());
            config.Set("SizeZ", configSizeX);
            RegionSizeZ = Convert.ToUInt32(configSizeZ);

            DoRegionSizeSanityChecks();

            // InternalAddress
            //
            IPAddress address;
            allKeys.Remove("InternalAddress");
            if (config.Contains("InternalAddress"))
            {
                address = IPAddress.Parse(config.GetString("InternalAddress", String.Empty));
            }
            else
            {
                address = IPAddress.Parse(MainConsole.Instance.CmdPrompt("Internal IP address", "0.0.0.0"));
                config.Set("InternalAddress", address.ToString());
            }

            // InternalPort
            //
            int port;
            allKeys.Remove("InternalPort");
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

            // AllowAlternatePorts
            //
            allKeys.Remove("AllowAlternatePorts");
            if (config.Contains("AllowAlternatePorts"))
            {
                m_allow_alternate_ports = config.GetBoolean("AllowAlternatePorts", true);
            }
            else
            {
                m_allow_alternate_ports = Convert.ToBoolean(MainConsole.Instance.CmdPrompt("Allow alternate ports", "False"));

                config.Set("AllowAlternatePorts", m_allow_alternate_ports.ToString());
            }

            // ExternalHostName
            //
            allKeys.Remove("ExternalHostName");
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
            {
                m_externalHostName = Util.GetLocalHost().ToString();
                m_log.InfoFormat(
                    "[REGIONINFO]: Resolving SYSTEMIP to {0} for external hostname of region {1}",
                    m_externalHostName, name);
            }
            else
            {
                m_externalHostName = externalName;
            }

            // RegionType
            m_regionType = config.GetString("RegionType", String.Empty);
            allKeys.Remove("RegionType");

            #region Prim and map stuff

            m_nonphysPrimMin = config.GetFloat("NonPhysicalPrimMin", 0);
            allKeys.Remove("NonPhysicalPrimMin");

            m_nonphysPrimMax = config.GetInt("NonPhysicalPrimMax", 0);
            allKeys.Remove("NonPhysicalPrimMax");

            m_physPrimMin = config.GetFloat("PhysicalPrimMin", 0);
            allKeys.Remove("PhysicalPrimMin");

            m_physPrimMax = config.GetInt("PhysicalPrimMax", 0);
            allKeys.Remove("PhysicalPrimMax");
            
            m_clampPrimSize = config.GetBoolean("ClampPrimSize", false);
            allKeys.Remove("ClampPrimSize");
            
            m_objectCapacity = config.GetInt("MaxPrims", 15000);
            allKeys.Remove("MaxPrims");

            m_maxPrimsPerUser = config.GetInt("MaxPrimsPerUser", -1);
            allKeys.Remove("MaxPrimsPerUser");

            m_linksetCapacity = config.GetInt("LinksetPrims", 0);
            allKeys.Remove("LinksetPrims");

            allKeys.Remove("MaptileStaticUUID");
            string mapTileStaticUUID = config.GetString("MaptileStaticUUID", UUID.Zero.ToString());
            if (UUID.TryParse(mapTileStaticUUID.Trim(), out m_maptileStaticUUID))
            {
                config.Set("MaptileStaticUUID", m_maptileStaticUUID.ToString()); 
            }

            MaptileStaticFile = config.GetString("MaptileStaticFile", String.Empty);
            allKeys.Remove("MaptileStaticFile");
            
            #endregion

            AgentCapacity = config.GetInt("MaxAgents", 100);
            allKeys.Remove("MaxAgents");

            // Multi-tenancy
            //
            ScopeID = new UUID(config.GetString("ScopeID", UUID.Zero.ToString()));
            allKeys.Remove("ScopeID");

            foreach (String s in allKeys)
            {
                SetExtraSetting(s, config.GetString(s));
            }
        }

        // Make sure user specified region sizes are sane.
        // Must be multiples of legacy region size (256).
        private void DoRegionSizeSanityChecks()
        {
            if (RegionSizeX != Constants.RegionSize || RegionSizeY != Constants.RegionSize)
            {
                // Doing non-legacy region sizes.
                // Enforce region size to be multiples of the legacy region size (256)
                uint partial = RegionSizeX % Constants.RegionSize;
                if (partial != 0)
                {
                    RegionSizeX -= partial;
                    if (RegionSizeX == 0)
                        RegionSizeX = Constants.RegionSize;
                    m_log.ErrorFormat("{0} Region size must be multiple of {1}. Enforcing {2}.RegionSizeX={3} instead of specified {4}",
                        LogHeader, Constants.RegionSize, m_regionName, RegionSizeX, RegionSizeX + partial);
                }
                partial = RegionSizeY % Constants.RegionSize;
                if (partial != 0)
                {
                    RegionSizeY -= partial;
                    if (RegionSizeY == 0)
                        RegionSizeY = Constants.RegionSize;
                    m_log.ErrorFormat("{0} Region size must be multiple of {1}. Enforcing {2}.RegionSizeY={3} instead of specified {4}",
                        LogHeader, Constants.RegionSize, m_regionName, RegionSizeY, RegionSizeY + partial);
                }

                // Because of things in the viewer, regions MUST be square.
                // Remove this check when viewers have been updated.
                if (RegionSizeX != RegionSizeY)
                {
                    uint minSize = Math.Min(RegionSizeX, RegionSizeY);
                    RegionSizeX = minSize;
                    RegionSizeY = minSize;
                    m_log.ErrorFormat("{0} Regions must be square until viewers are updated. Forcing region {1} size to <{2},{3}>",
                                        LogHeader, m_regionName, RegionSizeX, RegionSizeY);
                }

                // There is a practical limit to region size.
                if (RegionSizeX > Constants.MaximumRegionSize || RegionSizeY > Constants.MaximumRegionSize)
                {
                    RegionSizeX = Util.Clamp<uint>(RegionSizeX, Constants.RegionSize, Constants.MaximumRegionSize);
                    RegionSizeY = Util.Clamp<uint>(RegionSizeY, Constants.RegionSize, Constants.MaximumRegionSize);
                    m_log.ErrorFormat("{0} Region dimensions must be less than {1}. Clamping {2}'s size to <{3},{4}>",
                                        LogHeader, Constants.MaximumRegionSize, m_regionName, RegionSizeX, RegionSizeY);
                }

                m_log.InfoFormat("{0} Region {1} size set to <{2},{3}>", LogHeader, m_regionName, RegionSizeX, RegionSizeY);
            }
        }

        private void WriteNiniConfig(IConfigSource source)
        {
            IConfig config = source.Configs[RegionName];

            if (config != null)
                source.Configs.Remove(config);

            config = source.AddConfig(RegionName);

            config.Set("RegionUUID", RegionID.ToString());

            string location = String.Format("{0},{1}", RegionLocX, RegionLocY);
            config.Set("Location", location);

            if (RegionSizeX > 0)
                config.Set("SizeX", RegionSizeX);

            if (RegionSizeY > 0)
                config.Set("SizeY", RegionSizeY);

//            if (RegionSizeZ > 0)
//                config.Set("SizeZ", RegionSizeZ);

            config.Set("InternalAddress", m_internalEndPoint.Address.ToString());
            config.Set("InternalPort", m_internalEndPoint.Port);

            config.Set("AllowAlternatePorts", m_allow_alternate_ports.ToString());

            config.Set("ExternalHostName", m_externalHostName);

            if (m_nonphysPrimMin > 0)
                config.Set("NonphysicalPrimMax", m_nonphysPrimMin);

            if (m_nonphysPrimMax > 0)
                config.Set("NonphysicalPrimMax", m_nonphysPrimMax);

            if (m_physPrimMin > 0)
                config.Set("PhysicalPrimMax", m_physPrimMin);
            
            if (m_physPrimMax > 0)
                config.Set("PhysicalPrimMax", m_physPrimMax);
                        
            config.Set("ClampPrimSize", m_clampPrimSize.ToString());

            if (m_objectCapacity > 0)
                config.Set("MaxPrims", m_objectCapacity);

            if (m_maxPrimsPerUser > -1)
                config.Set("MaxPrimsPerUser", m_maxPrimsPerUser);

            if (m_linksetCapacity > 0)
                config.Set("LinksetPrims", m_linksetCapacity);

            if (AgentCapacity > 0)
                config.Set("MaxAgents", AgentCapacity);

            if (ScopeID != UUID.Zero)
                config.Set("ScopeID", ScopeID.ToString());

            if (RegionType != String.Empty)
                config.Set("RegionType", RegionType);

            if (m_maptileStaticUUID != UUID.Zero)
                config.Set("MaptileStaticUUID", m_maptileStaticUUID.ToString());

            if (MaptileStaticFile != null && MaptileStaticFile != String.Empty)
                config.Set("MaptileStaticFile", MaptileStaticFile);
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
            else
                throw new Exception("Invalid file type for region persistence.");
        }

        public void SaveLastMapUUID(UUID mapUUID)
        {
            lastMapUUID = mapUUID;
            lastMapRefresh = Util.UnixTimeSinceEpoch().ToString();
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
            args["region_size_x"] = OSD.FromString(RegionSizeX.ToString());
            args["region_size_y"] = OSD.FromString(RegionSizeY.ToString());
            args["region_size_z"] = OSD.FromString(RegionSizeZ.ToString());

            args["internal_ep_address"] = OSD.FromString(InternalEndPoint.Address.ToString());
            args["internal_ep_port"] = OSD.FromString(InternalEndPoint.Port.ToString());
            if ((RemotingAddress != null) && !RemotingAddress.Equals(""))
                args["remoting_address"] = OSD.FromString(RemotingAddress);
            args["remoting_port"] = OSD.FromString(RemotingPort.ToString());
            args["allow_alt_ports"] = OSD.FromBoolean(m_allow_alternate_ports);
            if ((proxyUrl != null) && !proxyUrl.Equals(""))
                args["proxy_url"] = OSD.FromString(proxyUrl);
            if (RegionType != String.Empty)
                args["region_type"] = OSD.FromString(RegionType);

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
            if (args.ContainsKey("region_size_x"))
                RegionSizeX = (uint)args["region_size_x"].AsInteger();
            if (args.ContainsKey("region_size_y"))
                RegionSizeY = (uint)args["region_size_y"].AsInteger();
            if (args.ContainsKey("region_size_z"))
                RegionSizeZ = (uint)args["region_size_z"].AsInteger();

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
            if (args["region_type"] != null)
                m_regionType = args["region_type"].AsString();
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
