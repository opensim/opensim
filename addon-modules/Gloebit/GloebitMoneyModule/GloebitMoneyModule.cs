/*
 * GloebitMoneyModule.cs is part of OpenSim-MoneyModule-Gloebit 
 * Copyright (C) 2015 Gloebit LLC
 *
 * OpenSim-MoneyModule-Gloebit is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Lesser General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * OpenSim-MoneyModule-Gloebit is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with OpenSim-MoneyModule-Gloebit.  If not, see <https://www.gnu.org/licenses/>.
 *
 *
 * This file was initially based off of OpenSim's SampleMoneyModule
 * which effectively serves as a template for the interface to
 * create an OpenSim money module plugin.
 * To the extent that any of that original SampleMoneyModlue code 
 * still exists, we wanted to explicitly retain the following 
 * copyright, license and disclaimer which cover that code which can be found at
 * https://github.com/opensim/opensim/blob/master/OpenSim/Region/OptionalModules/World/MoneyModule/SampleMoneyModule.cs
 * 
 * The following solely applies to any code from OpenSim's SampleMoneyModule from 2015.
 * The rest of this file and this repository are licensed and copyrighted as stated above.
 * -------------------------------------------------------------------------
 * Copyright (c) Contributors, http://opensimulator.org/
 * See https://github.com/opensim/opensim/blob/master/CONTRIBUTORS.txt for a full list of copyright holders.
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
 * -------------------------------------------------------------------------
 */

/*
 * GloebitMoneyModule.cs
 *
 * This file is the glue between the OpenSim platform and the Gloebit Money Module
 *
 * For porting to other systems or implementing new transaction types/flows,
 * this file will likely require major modification or replacement.
 */

#define NEWHTTPFLOW

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Text;
using log4net;
using Nini.Config;
using Nwc.XmlRpc;
using Mono.Addins;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.OptionalModules.ViewerSupport;   // Necessary for SimulatorFeaturesHelper
using OpenSim.Services.Interfaces;
using OpenMetaverse.StructuredData;     // TODO: turn transactionData into a dictionary of <string, object> and remove this.
using OpenSim.Region.ScriptEngine.Shared.ScriptBase;    // For ScriptBaseClass permissions constants

[assembly: Addin("Gloebit", "0.1")]
[assembly: AddinDependency("OpenSim.Region.Framework", OpenSim.VersionInfo.VersionNumber)]
[assembly: AddinDescription("OpenSim Addin for Gloebit Money Module")]
[assembly: AddinAuthor("Gloebit LLC gloebit@gloebit.com")]
//[assembly: ImportAddinFile("Gloebit.ini")]


namespace Gloebit.GloebitMoneyModule
{
    // TODO: Should this enum be inside of class or just the namespace?
    //       What about the other enums at the bottom of the file?
    enum GLBEnv {
        None = 0,
        Custom = 1,
        Sandbox = 2,
        Production = 3,
    }

    /// <summary>
    /// This is only the Gloebit Money Module which enables monetary transactions in OpenSim
    /// via the Gloebit API and Gloebit Services.
    /// </summary>

    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "GloebitMoneyModule")]
    public class GloebitMoneyModule : IMoneyModule, ISharedRegionModule, GloebitTransaction.IAssetCallback, GloebitAPIWrapper.IUriLoader, GloebitAPIWrapper.IPlatformAccessor, GloebitAPIWrapper.IUserAlert, GloebitAPIWrapper.ITransactionAlert, GloebitAPIWrapper.ISubscriptionAlert
    {
        
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        
        /*** GMM CONFIGURATION ***/
        private IConfigSource m_gConfig;

        // TODO: Consider moving these into the GAPI
        // Gloebit Service URLs
        private const string SANDBOX_URL = "https://sandbox.gloebit.com/";
        private const string PRODUCTION_URL = "https://www.gloebit.com/";

        // populated from Startup and Economy
        private string m_startupEconomyModule;
        private string m_economyEconomyModule;

        // m_enabled = true signifies that GMM is enabled across entire sim process
        // Combination of [Economy]/[Startup] EconomyModule set to "Gloebit" and [Gloebit] Enabled set to true
        // If false, may still be enabled on individual regions.  see m_enabledRegions below.
        private bool m_enabled = true;
        // Set to false if anything is misconfigured
        private bool m_configured = true;
        
        // Populated from Gloebit.ini
        private UUID[] m_enabledRegions = null;         // Regions on sim to individually enable GMM.
                                                        // Only necessary if m_enabled is false
        private GLBEnv m_environment = GLBEnv.None;
        private string m_keyAlias;
        private string m_key;
        private string m_secret;
        private string m_apiUrl;
        private Uri m_overrideBaseURI;
        private static string m_contactGloebit = "Gloebit at OpenSimTransactionIssue@gloebit.com";
        private string m_contactOwner = "region or grid owner";
        private bool m_disablePerSimCurrencyExtras = false;
        private bool m_showNewSessionPurchaseIM = false;
        private bool m_showNewSessionAuthIM = true;
        private bool m_showWelcomeMessage = true;
        private bool m_forceNewLandPassFlow = false;
        private bool m_forceNewHTTPFlow = false;
        
        // Populated from grid info
        private string m_gridnick = "unknown_grid";
        private string m_gridname = "unknown_grid_name";
        private Uri m_economyURL;
        private string m_dbProvider = null;
        private string m_dbConnectionString = null;
        
        // OpenSim Economic Data
        private bool m_sellEnabled = false;     // If not true, Object Buy txns won't work
        private float EnergyEfficiency = 0f;
        private int ObjectCount = 0;
        private int PriceEnergyUnit = 0;
        private int PriceGroupCreate = 0;
        private int PriceObjectClaim = 0;
        private float PriceObjectRent = 0f;
        private float PriceObjectScaleFactor = 0f;
        private int PriceParcelClaim = 0;
        private float PriceParcelClaimFactor = 0f;
        private int PriceParcelRent = 0;
        private int PricePublicObjectDecay = 0;
        private int PricePublicObjectDelete = 0;
        private int PriceRentLight = 0;
        private int PriceUpload = 0;
        private int TeleportMinPrice = 0;
        private float TeleportPriceExponent = 0f;


        /*** GMM RUNTIME API AND DATA ***/
        // One API per sim to handle web calls to and from the Gloebit service
        private GloebitAPIWrapper m_apiW;

        /// <summary>
        /// Scenes by Region Handle
        /// </summary>
        private Dictionary<ulong, Scene> m_scenel = new Dictionary<ulong, Scene>();
        
        // Store land buy args necessary for completing land transactions
        private Dictionary<UUID, Object[]> m_landAssetMap = new Dictionary<UUID, Object[]>();
        // TODO: should assets be data stores or just in local memory? probably just local.
        // TODO: determine what info should be in transaction table and what should move to asset maps for other transactions
        
        // Store link to client we can't yet create auth for because there is no sub on file.  Handle in return from sub creation.
        private Dictionary<UUID, IClientAPI> m_authWaitingForSubMap = new Dictionary<UUID, IClientAPI>();

        // New dictionary to hold newer http handlers
        private Dictionary<string, XmlRpcMethod> m_rpcHandlers;
        
        // Some internal storage to only retrieve info once
        private string m_opensimVersion = String.Empty;
        private string m_opensimVersionNumber = String.Empty;
        private bool m_newLandPassFlow = false;
        private bool m_newHTTPFlow = false;


        #region IRegionModuleBase Interface

        /**********************
         * region handles 
         * --- reading of config and triggering initialization of GloebitAPI and DB connections
         * --- enabling of GMM on regions for this sim when added
         * --- registering Scene events for enabled regions to handle user management and commerce functionality
         **********************/
        
        public string Name {
            get { return "GloebitMoneyModule"; }
        }
        
        public Type ReplaceableInterface {
            get { return null; }
        }

        /// <summary>
        /// Called on startup so the module can be configured.
        /// </summary>
        /// <param name="config">Configuration source.</param>
        public void Initialise(IConfigSource config)
        {
            m_log.Info ("[GLOEBITMONEYMODULE] Initialising.");
            m_gConfig = config;

            LoadConfig(m_gConfig);

            string[] sections = {"Startup", "Economy", "Gloebit"};
            foreach (string section in sections) {
                IConfig sec_config = m_gConfig.Configs[section];

                if (null == sec_config) {
                    m_log.WarnFormat("[GLOEBITMONEYMODULE] Config section {0} is missing. Skipping.", section);
                    continue;
                }
                ReadConfigAndPopulate(sec_config, section);
            }
            
            // Load Grid info from GridInfoService if Standalone and GridInfo if Robust
            IConfig standalone_config = m_gConfig.Configs["GridInfoService"];
            IConfig robust_config = m_gConfig.Configs["GridInfo"];
            if (standalone_config == null && robust_config == null) {
                m_log.Warn("[GLOEBITMONEYMODULE] GridInfoService and GridInfo are both missing.  Can not retrieve GridInfoURI, GridName and GridNick.");
                // NOTE: we can continue and enable as this will just cause transaction history records to be missing some data.
            } else {
                if(standalone_config != null && robust_config != null) {
                    m_log.Warn("[GLOEBITMONEYMODULE] GridInfoService and GridInfo are both present.  Deferring to GridInfo to retrieve GridInfoURI, GridName and GridNick.");
                }
                if (robust_config != null) {
                    ReadConfigAndPopulate(robust_config, "GridInfo");
                } else {
                    ReadConfigAndPopulate(standalone_config, "GridInfoService");
                }
            }
            

            m_log.InfoFormat("[GLOEBITMONEYMODULE] Initialised. Gloebit enabled: {0}, GLBEnvironment: {1}, GLBApiUrl: {2} GLBKeyAlias {3}, GLBKey: {4}, GLBSecret {5}",
                m_enabled, m_environment, m_apiUrl, m_keyAlias, m_key, (m_secret == null ? "null" : "configured"));

            // TODO: I've added GLBEnv.Custom for testing.  Remove before we ship
            if(m_environment != GLBEnv.Sandbox && m_environment != GLBEnv.Production && m_environment != GLBEnv.Custom) {
                m_log.ErrorFormat("[GLOEBITMONEYMODULE] Unsupported environment selected: {0}, disabling GloebitMoneyModule", m_environment);
                m_enabled = false;
                m_configured = false;
            }

            if (String.IsNullOrEmpty(m_dbProvider)) {
                // GLBSpecificStorageProvider wasn't specified so fall back to using the global
                // DatabaseService settings
                m_log.Info("[GLOEBITMONEYMODULE] using default StorageProvider and ConnectionString from DatabaseService");
                m_dbProvider = m_gConfig.Configs["DatabaseService"].GetString("StorageProvider");
                m_dbConnectionString = m_gConfig.Configs["DatabaseService"].GetString("ConnectionString");
            } else {
                m_log.Info("[GLOEBITMONEYMODULE] using GLBSpecificStorageProvider and GLBSpecificConnectionString");
            }

            if(String.IsNullOrEmpty(m_dbProvider) || String.IsNullOrEmpty(m_dbConnectionString)) {
                m_log.Error("[GLOEBITMONEYMODULE] database connection misconfigured, disabling GloebitMoneyModule");
                m_enabled = false;
                m_configured = false;
            }

            if(m_configured) {
                InitGloebitAPI();
            }
        }

        /// <summary>
        /// Load Addin Configuration from Addin config dir
        /// </summary>
        /// <param name="config"></param>
        private void LoadConfig(IConfigSource config)
        {
           string configPath = string.Empty;
           bool created;
           string assemblyDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
           if (!Util.MergeConfigurationFile(config, "Gloebit.ini", Path.Combine(assemblyDirectory, "Gloebit.ini.example"), out configPath, out created))
           {
               m_log.WarnFormat("[GLOEBITMONEYMODULE]: Gloebit.ini configuration file not merged");
               return;
           }
           if (created)
           {
               m_log.ErrorFormat("[GLOEBITMONEYMODULE]: PLEASE EDIT {0} BEFORE RUNNING THIS ADDIN", configPath);
               throw new Exception("Addin must be configured prior to running");
           }
        }

        /// <summary>
        /// Parse Standard MoneyModule Configuration
        /// </summary>
        /// <param name="config"></param>
        /// <param name="section"></param>
        private void ReadConfigAndPopulate(IConfig config, string section)
        {
            /********** [Startup] ************/
            if (section == "Startup") {
                m_startupEconomyModule = config.GetString("EconomyModule", String.Empty);
                m_log.InfoFormat("[GLOEBITMONEYMODULE] Startup EconomyModule = {0}.", m_startupEconomyModule);
            }

            /********** [Economy] ************/
            if (section == "Economy") {
                m_economyEconomyModule = config.GetString("EconomyModule", String.Empty);
                m_log.InfoFormat("[GLOEBITMONEYMODULE] Economy EconomyModule = {0}.", m_economyEconomyModule);

                /*** Get OpenSim built in pricing configuration info ***/
                PriceEnergyUnit = config.GetInt("PriceEnergyUnit", 100);
                PriceObjectClaim = config.GetInt("PriceObjectClaim", 10);
                PricePublicObjectDecay = config.GetInt("PricePublicObjectDecay", 4);
                PricePublicObjectDelete = config.GetInt("PricePublicObjectDelete", 4);
                PriceParcelClaim = config.GetInt("PriceParcelClaim", 1);
                PriceParcelClaimFactor = config.GetFloat("PriceParcelClaimFactor", 1f);
                PriceUpload = config.GetInt("PriceUpload", 0);
                PriceRentLight = config.GetInt("PriceRentLight", 5);
                TeleportMinPrice = config.GetInt("TeleportMinPrice", 2);
                TeleportPriceExponent = config.GetFloat("TeleportPriceExponent", 2f);
                EnergyEfficiency = config.GetFloat("EnergyEfficiency", 1);
                PriceObjectRent = config.GetFloat("PriceObjectRent", 1);
                PriceObjectScaleFactor = config.GetFloat("PriceObjectScaleFactor", 10);
                PriceParcelRent = config.GetInt("PriceParcelRent", 1);
                PriceGroupCreate = config.GetInt("PriceGroupCreate", -1);
                m_sellEnabled = config.GetBoolean("SellEnabled", true);
            }
            
            /********** [Gloebit] ************/
            if (section == "Gloebit") {
                /*** Determine what the sim EconomyModule setting is ***/
                // Original standard is to be in the Startup section.  New standard is to be in Economy section.  Need to check both.
                // Two money modules should never be enabled on the same region as they'll conflict.
                // If all respect this param then this can't happen across the entire sim process.
                // Unfortunately, this does not handle per-region configuration on a multi-region sim.
                // Gloebit for instance allows enabling by region if not enabled globally,
                // but many other money modules can not be enabled/disabled by region.
                string economyModule; 
                if (String.IsNullOrEmpty(m_startupEconomyModule) && String.IsNullOrEmpty(m_economyEconomyModule)) {
                    m_log.Warn("[GLOEBITMONEYMODULE] no sim-wide EconomyModule is set.  Defaulting to Gloebit since dll is present.");
                    economyModule = "Gloebit";
                } else if (!String.IsNullOrEmpty(m_startupEconomyModule) && !String.IsNullOrEmpty(m_economyEconomyModule)) {
                    m_log.Warn("[GLOEBITMONEYMODULE] EconomyModule is set in 2 places.  Should only be defined once.");
                    if (m_startupEconomyModule != m_economyEconomyModule) {
                        m_log.Error("[GLOEBITMONEYMODULE] EconomyModule in [Startup] does not match setting in [Economy].  Sim-wide setting is undefined.");
                        economyModule = String.Empty;
                    } else {
                        m_log.InfoFormat("[GLOEBITMONEYMODULE] EconomyModule settings match as {0}", m_startupEconomyModule);
                        economyModule = m_startupEconomyModule;
                    }
                } else if (!String.IsNullOrEmpty(m_startupEconomyModule)) {
                    m_log.InfoFormat("[GLOEBITMONEYMODULE] EconomyModule is {0}.", m_startupEconomyModule);
                    economyModule = m_startupEconomyModule;
                } else {
                    m_log.InfoFormat("[GLOEBITMONEYMODULE] EconomyModule is {0}.", m_economyEconomyModule);
                    economyModule = m_economyEconomyModule;
                }
                        
                if (economyModule == "Gloebit") {
                    m_log.Info("[GLOEBITMONEYMODULE] selected as global sim EconomyModule.");
                    m_enabled = true;
                } else {
                    m_log.Info("[GLOEBITMONEYMODULE] not selected as global sim EconomyModule.");
                    m_enabled = false;
                }

                /*** Get GloebitMoneyModule configuration details ***/
                // Is Gloebit disabled, enabled across the entire sim process, or on certain regions?
                bool enabled = config.GetBoolean("Enabled", false);
                m_log.InfoFormat("[GLOEBITMONEYMODULE] [Gloebit] Enabled flag set to {0}.", enabled);
                m_enabled = m_enabled && enabled;
                if (!m_enabled) {
                    m_log.Info("[GLOEBITMONEYMODULE] Not enabled globally for sim. (to enable set \"Enabled = true\" in [Gloebit] and \"EconomyModule = Gloebit\" in [Economy])");
                }
                string enabledRegionIdsStr = config.GetString("GLBEnabledOnlyInRegions");
                if(!String.IsNullOrEmpty(enabledRegionIdsStr)) {
                    // null for the delimiter argument means split on whitespace
                    string[] enabledRegionIds = enabledRegionIdsStr.Split((string[])null, StringSplitOptions.RemoveEmptyEntries);
                    int numRegions = enabledRegionIds.Length;
                    m_log.InfoFormat("[GLOEBITMONEYMODULE] GLBEnabledOnlyInRegions num regions: {0}", numRegions);
                    m_enabledRegions = new UUID[numRegions];
                    for(int i = 0; i < numRegions; i++) {
                        m_enabledRegions[i] = UUID.Parse(enabledRegionIds[i]);
                        m_log.InfoFormat("[GLOEBITMONEYMODULE] selected as local EconomyModule for region {0}", enabledRegionIds[i]);
                    }
                    if ((numRegions > 0) && (economyModule != "Gloebit")) {
                        m_log.WarnFormat("[GLOEBITMONEYMODULE] Gloebit enabled by region on sim with global EconomyModule set to {0}.  Ensure that sim-wide EconomyModule is disabled in Gloebit enabled regions.", economyModule);
                    }
                }
                // Get region/grid owner contact details for transaction failure contact instructions.
                string ownerName = config.GetString("GLBOwnerName", "region or grid owner");
                string ownerEmail = config.GetString("GLBOwnerEmail", null);
                m_contactOwner = ownerName;
                if (!String.IsNullOrEmpty(ownerEmail)) {
                    m_contactOwner = String.Format("{0} at {1}", ownerName, ownerEmail);
                }
                // Should we disable adding info to OpenSimExtras map
                m_disablePerSimCurrencyExtras = config.GetBoolean("DisablePerSimCurrencyExtras", false);
                // Should we send new session IMs informing user how to auth or purchase gloebits
                m_showNewSessionPurchaseIM = config.GetBoolean("GLBShowNewSessionPurchaseIM", false);
                m_showNewSessionAuthIM = config.GetBoolean("GLBShowNewSessionAuthIM", true);
                // Should we send a welcome message informing user that Gloebit is enabled
                m_showWelcomeMessage = config.GetBoolean("GLBShowWelcomeMessage", true);
                string nsms_msg = "\n\t";
                nsms_msg = String.Format("{0}Welcome Message: {1},\tTo modify, set GLBShowWelcomeMessage in [Gloebit] section of config\n\t", nsms_msg, m_showWelcomeMessage);
                nsms_msg = String.Format("{0}Auth Message: {1},\tTo modify, set GLBShowNewSessionAuthIM in [Gloebit] section of config\n\t", nsms_msg, m_showNewSessionAuthIM);
                nsms_msg = String.Format("{0}Purchase Message: {1},\tTo modify, set GLBShowNewSessionPurchaseIM in [Gloebit] section of config", nsms_msg, m_showNewSessionPurchaseIM);
                m_log.InfoFormat("[GLOEBITMONEYMODULE] [Gloebit] is configured with the following settings for messaging users connecting to a new session{0}", nsms_msg);
                // If version cannot be detected override workflow selection via config
                // Currently not documented because last resort if all version checking fails
                m_forceNewLandPassFlow = config.GetBoolean("GLBNewLandPassFlow", false);
                m_forceNewHTTPFlow = config.GetBoolean("GLBNewHTTPFlow", false);
                // Are we using custom db connection info
                m_dbProvider = config.GetString("GLBSpecificStorageProvider");
                m_dbConnectionString = config.GetString("GLBSpecificConnectionString");
                
                /*** Get Gloebit API configuration details ***/
                string envString = config.GetString("GLBEnvironment", "sandbox");
                switch(envString) {
                    case "sandbox":
                        m_environment = GLBEnv.Sandbox;
                        m_apiUrl = SANDBOX_URL;
                        break;
                    case "production":
                        m_environment = GLBEnv.Production;
                        m_apiUrl = PRODUCTION_URL;
                        break;
                    case "custom":
                        m_environment = GLBEnv.Custom;
                        m_apiUrl = config.GetString("GLBApiUrl", SANDBOX_URL);
                        string overrideBaseURIStr = config.GetString("GLBCallbackBaseURI", null);
                        if(overrideBaseURIStr != null) {
                            m_overrideBaseURI = new Uri(overrideBaseURIStr);
                        }
                        m_log.Warn("[GLOEBITMONEYMODULE] GLBEnvironment \"custom\" unsupported, things will probably fail later");
                        break;
                    default:
                        m_environment = GLBEnv.None;
                        m_apiUrl = null;
                        m_log.WarnFormat("[GLOEBITMONEYMODULE] GLBEnvironment \"{0}\" unrecognized, setting to None", envString); 
                        break;
                }
                m_keyAlias = config.GetString("GLBKeyAlias", null);
                m_key = config.GetString("GLBKey", null);
                m_secret = config.GetString("GLBSecret", null);
            }

            /********** [GridInfoService] ************/
            if (section == "GridInfoService") {
                // If we're here, this is a standalone mode grid
                /*** Grab the grid info locally ***/
                setGridInfo(config.GetString("gridname", m_gridname), config.GetString("gridnick", m_gridnick), config.GetString("economy", null));
            }
            
            /********** [GridInfo] ************/
            if (section == "GridInfo") {
                // If we're here, this is a robust mode grid
                /*** Grab the grid info via the grid info uri ***/
                string gridInfoURI = config.GetString("GridInfoURI", null);
                // TODO: Should we store the info url?
                m_log.InfoFormat("[GLOEBITMONEYMODULE] GRID INFO URL = {0}", gridInfoURI);
                if (String.IsNullOrEmpty(gridInfoURI)) {
                    m_log.ErrorFormat("[GloebitMoneyModule] Failed to retrieve GridInfoURI from [GridInfo] section of config.");
                    return;
                }
                // Create http web request from URL
                Uri requestURI = new Uri(new Uri(gridInfoURI), "json_grid_info");
                m_log.InfoFormat("[GLOEBITMONEYMODULE] Constructed and requesting URI = {0}", requestURI);
                HttpWebRequest request = (HttpWebRequest) WebRequest.Create(requestURI);
                request.Method = "GET";
                try {
                    // Get the response
                    HttpWebResponse response = (HttpWebResponse) request.GetResponse();
                    string status = response.StatusDescription;
                    m_log.InfoFormat("[GLOEBITMONEYMODULE] Grid Info status:{0}", status);
                    using(StreamReader response_stream = new StreamReader(response.GetResponseStream())) {
                        string response_str = response_stream.ReadToEnd();
                        m_log.InfoFormat("[GLOEBITMONEYMODULE] Grid Info:{0}", response_str);
                        // Parse the response
                        OSDMap responseData = (OSDMap)OSDParser.DeserializeJson(response_str);
                        // TODO: Can we assume these will all always be present, or do we need to use a TryGet?
                        setGridInfo(responseData["gridname"], responseData["gridnick"], responseData["economy"]);
                        // TODO: do we want anything else from grid info?
                    }
                } catch (Exception e) {
                    m_log.ErrorFormat("[GloebitMoneyModule] Failed to retrieve Grid Info. {0}", e);
                }
            }
        }
        
        /// <summary>
        /// Helper function to store grid info regardless of whether the source is local (Standalone) or remote (Rubust).
        /// Stores gridName, gridNick, and economy config values
        /// </summary>
        /// <param name="gridName">Name of the Grid this sim process is connecting to.</param>
        /// <param name="gridNick">Nickname of Grid this sim process is connecting to.</param>
        /// <param name="ecoUrl">URL where the landtool.php and currency.php helper files are located.  These are necessary
        ///     for remnants of OpenSim which expected the grid to control these uniformly, not via the region.  Hopefully,
        ///     this will eventually be deprecated.</param>
        private void setGridInfo(string gridName, string gridNick, string ecoUrl) {
            m_log.InfoFormat("[GLOEBITMONEYMODULE] Storing Grid Info: GridName:[{0}] GridNick:[{1}] EconomyURL:[{2}]", gridName, gridNick, ecoUrl);
            m_gridname = gridName;
            m_gridnick = gridNick;
            if (!String.IsNullOrEmpty(ecoUrl)) {
                m_economyURL = new Uri(ecoUrl);
            } else {
                m_economyURL = null;
                // We're not using this for the GloebitMoneyModule, but it is necessary for land sales.
                // It is also necessary for the buy-currency button to operate, though functionality of this flow is limited.
                m_log.WarnFormat("[GLOEBITMONEYMODULE] [GridInfoService] or [GridInfo] economy setting is not configured!  Land sales will not work.");
            }
        }
        
        public void Close()
        {
            m_enabled = false;
            m_configured = false;
        }

        // Helper funciton used in AddRegion for post 0.9.2.0 XML RPC Handlers 
        public void processPHP(IOSHttpRequest request, IOSHttpResponse response)
        {
#if NEWHTTPFLOW
            MainServer.Instance.HandleXmlRpcRequests((OSHttpRequest)request, (OSHttpResponse)response, m_rpcHandlers);
#endif
        }

        public void AddRegion(Scene scene)
        {
            if(!m_configured) {
                return;
            }

            if (m_enabled || (m_enabledRegions != null && m_enabledRegions.Contains(scene.RegionInfo.RegionID)))
            {
                m_log.InfoFormat("[GLOEBITMONEYMODULE] region added {0}", scene.RegionInfo.RegionID.ToString());
                scene.RegisterModuleInterface<IMoneyModule>(this);
                IHttpServer httpServer = MainServer.Instance;

                lock (m_scenel)
                {
                    // TODO: What happens if all regions are removed and one is re-created without a sim restart?
                    //       Should we use a bool instead of the count here?
                    if (m_scenel.Count == 0)
                    {
                        /*
                         * For land sales, buy-currency button, and the insufficient funds flows to operate,
                         * the economy helper uri needs to be present.
                         * 
                         * The GMM provides this helper-uri and the currency symbol via the OpenSim Extras.  
                         * Some viewers (Firestorm & Alchemy at time of writing) consume these so this requires no
                         * configuration to work for a user on a Gloebit enabled region.  For users with other or older viewers,
                         * the helper-uri will have to be configured properly, and if not pointed at a Gloebit enabled sim,
                         * the grid will have to handle these calls, which it has traditionally done with an XMLRPC server and
                         * currency.php and landtool.php helper scripts.  That is rather complex, so we recommend that all 
                         * viewers adopt this patch and that grids request that their users update to a viewer with this patch.
                         * --- Patch Info: http://dev.gloebit.com/blog/Upgrade-Viewer/
                         * --- Patch Info: https://medium.com/@colosi/multi-currency-support-coming-to-opensim-viewers-cd20e75f7990
                         * --- Patch Download: http://dev.gloebit.com/opensim/downloads/ColosiOpenSimMultiCurrencySupport.patch
                         * --- Firestorm Jira: https://jira.phoenixviewer.com/browse/FIRE-21587
                         */

                        // These functions can handle the calls to the economy helper-uri if it is configured to point at the sim.  
                        // They will enable land purchasing, buy-currency, and insufficient-funds flows.
                        // *NOTE* gloebits can not currently be purchased from a viewer, but this allows Gloebit to control the
                        // messaging in this flow and send users to the Gloebit website for purchasing.

                        // Post version 0.9.2.0 the httpserver changed requiring different approach to the preflights
                        if (m_newHTTPFlow == true)
                        {
                            m_rpcHandlers = new Dictionary<string, XmlRpcMethod>();
                            m_rpcHandlers.Add("getCurrencyQuote", quote_func);
                            m_rpcHandlers.Add("buyCurrency", buy_func);
                            m_rpcHandlers.Add("preflightBuyLandPrep", preflightBuyLandPrep_func);
                            m_rpcHandlers.Add("buyLandPrep", landBuy_func);
#if NEWHTTPFLOW
                            MainServer.Instance.AddSimpleStreamHandler(new SimpleStreamHandler("/landtool.php", processPHP));
                            MainServer.Instance.AddSimpleStreamHandler(new SimpleStreamHandler("/currency.php", processPHP));
#endif
                        } else {
                            httpServer.AddXmlRPCHandler("getCurrencyQuote", quote_func);
                            httpServer.AddXmlRPCHandler("buyCurrency", buy_func);
                            httpServer.AddXmlRPCHandler("preflightBuyLandPrep", preflightBuyLandPrep_func);
                            httpServer.AddXmlRPCHandler("buyLandPrep", landBuy_func);
                        }

                        /********** Register endpoints the Gloebit Service will call back into **********/
                        RegisterGloebitWebhooks(httpServer);
                    }

                    if (m_scenel.ContainsKey(scene.RegionInfo.RegionHandle))
                    {
                        m_scenel[scene.RegionInfo.RegionHandle] = scene;
                    }
                    else
                    {
                        m_scenel.Add(scene.RegionInfo.RegionHandle, scene);
                    }
                }

                // Register for events for user management
                scene.EventManager.OnNewClient += OnNewClient;                              // Registers client events
                scene.EventManager.OnClientLogin += OnClientLogin;                          // Handles a login issue

                // Register for commerce events that come through scene
                scene.EventManager.OnMoneyTransfer += OnMoneyTransfer;                      // Handles 5001 (pay user) & 5008 (pay object) events
                scene.EventManager.OnValidateLandBuy += ValidateLandBuy;                    // Handles validation for free land transactions
                scene.EventManager.OnLandBuy += ProcessLandBuy;                             // Handles land purchases
                
            } else {
                if(m_enabledRegions != null) {
                    m_log.InfoFormat("[GLOEBITMONEYMODULE] SKIPPING region add {0} is not in enabled region list", scene.RegionInfo.RegionID.ToString());
                }
            }
        }

        public void RemoveRegion(Scene scene)
        {
            lock (m_scenel)
            {
                m_scenel.Remove(scene.RegionInfo.RegionHandle);
            }
        }

        public void RegionLoaded(Scene scene)
        {
            if (!m_enabled && (m_enabledRegions == null || !m_enabledRegions.Contains(scene.RegionInfo.RegionID))) {
                m_log.InfoFormat("[GLOEBITMONEYMODULE] region not loaded as not enabled {0}", scene.RegionInfo.RegionID.ToString());
                return;
            }
            m_log.InfoFormat("[GLOEBITMONEYMODULE] region loaded {0}", scene.RegionInfo.RegionID.ToString());
            
            ISimulatorFeaturesModule featuresModule = scene.RequestModuleInterface<ISimulatorFeaturesModule>();
            bool enabled = !m_disablePerSimCurrencyExtras;
            if (enabled && featuresModule != null) {
                featuresModule.OnSimulatorFeaturesRequest += (UUID x, ref OSDMap y) => OnSimulatorFeaturesRequest(x, ref y, scene);
            }
        }
        
        private void OnSimulatorFeaturesRequest(UUID agentID, ref OSDMap features, Scene scene)
        {
            UUID regionID = scene.RegionInfo.RegionID;

            if (m_enabled || (m_enabledRegions != null && m_enabledRegions.Contains(regionID))) {
                // Get or create the extras section of the features map
                OSDMap extrasMap;
                if (features.ContainsKey("OpenSimExtras")) {
                    extrasMap = (OSDMap)features["OpenSimExtras"];
                } else {
                    extrasMap = new OSDMap();
                    features["OpenSimExtras"] = extrasMap;
                }
                
                // Add our values to the extras map
                extrasMap["currency"] = "G$";
                // replaced G$ with ₲ (hex 0x20B2 / unicode U+20B2), but screwed up balance display in Firestorm
                extrasMap["currency-base-uri"] = GetCurrencyBaseURI(scene);
            }
        }
        
        private string GetCurrencyBaseURI(Scene scene) {
            return scene.RegionInfo.ServerURI;
        }
        
        #endregion // IRegionModuleBase Interface
        
        #region ISharedRegionModule Interface

        // TODO: Find a better method to do version testing, do not rely on version number, it can be edited easily and does not reflect code
        //       instead the capabilities of functions and httpserver should be tested directly to determine which workflows to use
        public void PostInitialise()
        {
            // Setting to large negative so if not found is not 0
            int vn1 = -9999;
            int vn2 = -9999;
            int vn3 = -9999;
            int vn4 = -9999;
            string detectedOSVersion = "unknown";
            m_opensimVersion = OpenSim.VersionInfo.Version;
            m_opensimVersionNumber = OpenSim.VersionInfo.VersionNumber;
            char[] delimiterChars = { '.' };
            string[] numbers = m_opensimVersionNumber.Split(delimiterChars, System.StringSplitOptions.RemoveEmptyEntries);

            // See if we can parse the string at all
            try {
                vn1 = int.Parse(numbers[0]);
                vn2 = int.Parse(numbers[1]);
                vn3 = int.Parse(numbers[2]);
            } catch {
                m_log.DebugFormat("[GLOEBITMONEYMODULE] Unable to parse version information, Gloebit cannot handle unofficial versions");
            }
            // Fourth number may not be present on all versions
            try {
                vn4 = int.Parse(numbers[3]);
            }
            catch {}

            /*** Version Tests ***/
            // changes to httpserver which require different workflows >= 0.9.2.0
            // new land pass flow >= 0.9.1; 0.9.0 releae; 

            if ((vn1 > 0) || (vn2 > 9) || (vn2 == 9 && vn3 >= 2)) {
                // Test for version 0.9.2.0 and beyond which contains changes to httpserver and thus needs different workflows also
                detectedOSVersion = "=>0.9.2";
                m_newLandPassFlow = true;
                m_newHTTPFlow = true;
            } else if ((vn1 == 0) && (vn2 == 9) && (vn3 > 0)) {
                // 0.9.1 and beyond are the new land pass flow.
                // Note, there are some early versions of 0.9.1 before any release candidate which do not have the new
                // flow, but we can't easily determine those and no one should be running those in production.
                detectedOSVersion = "=>0.9.1";
                m_newLandPassFlow = true;
            } else if (vn1 == 0 && vn2 == 9 && vn3 == 0) {
                // 0.9.0-release pulled in 0.9.1 changes and is new flow, but rest of 0.9.0 is not.
                // assume dev on 0.9.0.1, 0.9.0.2 will be new flow
                if (vn4 > 0) {
                    // 0.9.0.1, 0.9.0.2, etc.
                    detectedOSVersion = "=>0.9.0.1";
                    m_newLandPassFlow = true;
                } else {
                    // Need to pull version flavour and check it.
                    // TODO: may need to split on spaces or hyphens and then pull last field because flavour is not friggin public
                    char[] dChars = { '-', ' ' };
                    string[] versionParts = m_opensimVersion.Split(dChars, System.StringSplitOptions.RemoveEmptyEntries);
                    string flavour = versionParts[versionParts.Length - 1];     // TODO: do we every have to worry about this being length 0?
                    if (flavour == OpenSim.VersionInfo.Flavour.Release.ToString()) {
                        // 0.9.0 release
                        detectedOSVersion = "=0.9.0";
                        m_newLandPassFlow = true;
                    }
                }
                // TODO: Unclear if post-fixes is a necessary flavour check yet.
            } else {
                // If all else fails version is unknown
                detectedOSVersion = "unknown or earlier than 0.9.0 release";
                m_log.DebugFormat("[GLOEBITMONEYMODULE] Could not confirm recent OpenSim version.  Module may not function! Use config overrides if necessary.\n\tIf >= 0.9.0 release: set GLBNewLandPassFlow to True.\n\tIf >= 0.9.2: set GLBNewHTTPFlow to True");
            }

            // In case version is unknown or changed by user allow override via config
            if (m_forceNewHTTPFlow == true) {
                m_log.DebugFormat("[GLOEBITMONEYMODULE] Using new HTTP Flow, set by config");
                m_newHTTPFlow = true;
            }
            if (m_forceNewLandPassFlow == true) {
                m_log.DebugFormat("[GLOEBITMONEYMODULE] Using new LandPass Flow, set by config");
                m_newLandPassFlow = true;
            }			

            // Provide detailed feedback on which version is detected, for debugging and information
            m_log.DebugFormat("[GLOEBITMONEYMODULE] OpenSim version {0} present, detected: {1} Using New LandPass Flow: {2} Using New HTTP Flow: {3}", m_opensimVersionNumber.ToString(), detectedOSVersion.ToString(), m_newLandPassFlow.ToString(), m_newHTTPFlow.ToString());
        }
        
        #endregion // ISharedRegionModule Interface

        #region IMoneyModule Members

        /******
         * region handles
         * --- entrance points and logic for some commerce flows
         ******/
        
        // Dummy IMoneyModule interface which is not yet used.
        public void MoveMoney(UUID fromAgentID, UUID toAgentID, int amount, string text)
        {
            // Call the newer MoveMoney function with a transaction type and text string so that we report
            // errors on whatever this unimplemented transaction flow is.
            MoveMoney(fromAgentID, toAgentID, amount, (MoneyTransactionType)TransactionType.MOVE_MONEY_GENERAL, text);
        }
        
        // New IMoneyModule interface added in 0.9.1 dev to support ParcelBuyPass implementation
        // Prior to 0.9.1, see ParcelBuyPass event
        // MoveMoney(remote_client.AgentId, ldata.OwnerID, cost,MoneyTransactionType.LandPassSale, payDescription)
        public bool MoveMoney(UUID fromUser, UUID toUser, int amount, MoneyTransactionType type, string text)
        {
            m_log.InfoFormat("[GLOEBITMONEYMODULE] MoveMoney from {0}, to {1} amount {2} type {3} desc {4}", fromUser, toUser, amount, type, text);
            
            // Just approve if the amount is 0 or negative as no money needs to be moved.
            // Negative is undefined and may need to be re-assessed as other transaction types are added to this flow.
            if (amount <= 0) {
                return true;
            }
            
            // Grab info all vars we'll def need
            TransactionType txnType = TransactionType.MOVE_MONEY_GENERAL;
            //string txnTypeString;
            //Scene s = LocateSceneClientIn(agentID);
            //string regionID = s.RegionInfo.RegionID.ToString();
            Scene s = null;
            string regionName = String.Empty;
            UUID regionID = UUID.Zero;
            UUID partID = UUID.Zero;
            string partName = String.Empty;
            string partDescription = String.Empty;
            UUID categoryID = UUID.Zero;
            uint localID = 0;
            OSDMap descMap = null;
            string description = String.Empty;
            
            // TODO: switch on transaction type
            switch (type) {
                case MoneyTransactionType.LandPassSale:
                    // User buys time-limited pass to access parcel
                    // Comes through ParcelBuyPass event pre 0.9.1; MoveMoney 0.9.1 on.
                    //txnTypeString = "ParcelBuyPass";
                    txnType = TransactionType.USER_BUYS_LANDPASS;
                    
                    // Try to retrieve the info we want by parsing the text string
                    // text = String.Format("Parcel '{0}' at region '{1} {2:0.###} hours access pass", ldata.Name, regionName, ldata.PassHours);
                    string[] delimiterStrings = { "Parcel '", "' at region '", " hours access pass" };
                    string[] pieces = text.Split(delimiterStrings, 5, System.StringSplitOptions.None);
                    partName = pieces[1];
                    int finalSpaceIndex = pieces[2].LastIndexOf(' ');
                    string test = pieces[2].Substring(0,0);
                    regionName = pieces[2].Substring(0, finalSpaceIndex);
                    string passHours = pieces[2].Substring(finalSpaceIndex + 1);
                    m_log.DebugFormat("[GLOEBITMONEYMODULE] MoveMoney for ParcelBuyPass on Parcel: [{0}], Region: [{1}]; for [{2}] hours", partName, regionName, passHours);
                    // Set our own description for uniformity
                    description = String.Format("{0} hour LandPass purchased for parcel {1} on {2}, {3}", passHours, partName, regionName, m_gridnick);
                    
                    // Retrieve scene from name
                    s = GetSceneByName(regionName);
                    regionID = s.RegionInfo.RegionID;
                    m_log.DebugFormat("[GLOEBITMONEYMODULE] region uuid {0}", regionID);
                    
                    // Since we don't have a parcelID, use info we have to locate the parcel related to this transaction.
                    // ILandObject parcel = s.LandChannel.GetLandObject(ParcelLocalID);
                    List <ILandObject> pl = s.LandChannel.AllParcels();
                    int found = 0;
                    ILandObject parcel = null;
                    string phStr;
                    foreach (ILandObject lo in pl) {
                        phStr = String.Format("{0:0.###}", lo.LandData.PassHours);
                        if (lo.LandData.Name == partName &&
                        phStr == passHours &&
                        lo.LandData.PassPrice == amount &&
                        lo.LandData.OwnerID == toUser &&
                        (lo.LandData.Flags & (uint)ParcelFlags.UsePassList) != 0)
                        {
                            m_log.DebugFormat("[GLOEBITMONEYMODULE] FOUND PARCEL {0} {1} {2} {3} {4}", lo.LandData.Name, lo.LandData.LocalID, lo.LandData.GlobalID, lo.LandData.PassPrice, lo.LandData.PassHours);
                            found++;
                            parcel = lo;
                        }
                    }
                    m_log.DebugFormat("[GLOEBITMONEYMODULE] found {0} matching parcels", found);
                    
                    // Create the descMap extra details using the parcel we've found if we've found exactly one match.
                    //int parcelLocalID = 0;
                    if (found == 1) {
                        descMap = buildOpenSimTransactionDescMap(regionName, regionID.ToString(), "ParcelBuyPass", parcel.LandData);
                        partID = parcel.LandData.GlobalID;
                        partDescription = parcel.LandData.Description;
                        // NOTE: using category & localID to retrieve parcel on callback in pre 0.9.1 flow.
                        // Storing same data in txn for consistency.
                        // Should consider storing all LandData in assetMap to track PassHours too.
                        // Don't need these in new 0.9.1 flow for ParcelBuyPass.  Enact handled outside our control.
                        categoryID = regionID;
                        localID = (uint)parcel.LandData.LocalID;
                    } else {
                        descMap = buildOpenSimTransactionDescMap(regionName, regionID.ToString(), "ParcelBuyPass");
                    }
                    break;
                //case MoneyTransactionType.ClassifiedCharge: ---- Will likely implement group join fees here.
                //    // Classified Ad Fee
                //    description = String.Format("Classified Ad Fee on {0}, {1}", regionname, m_gridnick);
                //    txnTypeString = "ClassifiedAdFee";
                //    txnType = TransactionType.FEE_CLASSIFIED_AD;
                //    break;
                default:
                    // Other - not in core at time of writing.
                    //description = String.Format("Fee (type {0}) on {1}, {2}", type, regionname, m_gridnick);
                    //txnTypeString = "MoveMoneyGeneral";
                    txnType = TransactionType.MOVE_MONEY_GENERAL;
                    m_log.ErrorFormat("[GLOEBITMONEYMODULE] MoveMoney for MoneyTransactionType [{0}] with description [{1}] not implemented.  Please contact Gloebit and ask them to implement.", type, text);
                    alertUsersTransactionPreparationFailure(TransactionType.MOVE_MONEY_GENERAL, TransactionPrecheckFailure.SALE_TYPE_INVALID, LocateClientObject(fromUser));
                    return false;
                    //break;
            }
            
            // Submit transaction and return result
            GloebitTransaction txn = buildTransaction(transactionID: UUID.Zero, transactionType: txnType,
                                                          payerID: fromUser, payeeID: toUser, amount: amount, subscriptionID: UUID.Zero,
                                                          partID: partID, partName: partName, partDescription: partDescription,
                                                          categoryID: categoryID, localID: localID, saleType: 0);
            if (txn == null) {
                // build failed, likely due to a reused transactionID.  Shouldn't happen.
                alertUsersTransactionPreparationFailure(TransactionType.USER_BUYS_LANDPASS, TransactionPrecheckFailure.EXISTING_TRANSACTION_ID, LocateClientObject(fromUser));
                return false;
            }
            bool transaction_result = m_apiW.SubmitSyncTransaction(txn, description, descMap);
            if (transaction_result) {
                m_log.InfoFormat("[GLOEBITMONEYMODULE] ParcelBuyPass SubmitSyncTransaction successful {0}", txn.TransactionID.ToString());
            }
            return transaction_result;
        }
        
        // Old IMoneyModule interface designed for LLGiveMoney instead of LLTransferLindenDollars.  Deprecated.
        public bool ObjectGiveMoney(UUID objectID, UUID fromID, UUID toID, int amount)
        {
            string reason = String.Empty;
            UUID txnID = UUID.Zero;
            return ObjectGiveMoney(objectID, fromID, toID, amount, txnID, out reason);
        }
        
        // New IMoneyModule interface.
        // If called from LLGiveMoney, txnID is UUID.Zero and reason is thrown away.
        // If called from LLTransferLindenDollars, txnID is set and reason is returned to script if function returns false.
        public bool ObjectGiveMoney(UUID objectID, UUID fromID, UUID toID, int amount, UUID txnID, out string reason)
        {
            string description = String.Format("Object {0} pays {1}", resolveObjectName(objectID), resolveAgentName(toID));
            m_log.DebugFormat("[GLOEBITMONEYMODULE] ******************ObjectGiveMoney {0}", description);
            
            reason = String.Empty;
            
            SceneObjectPart part = null;
            string regionname = "";
            string regionID = "";
            
            // Try to get part from scene the payee is in first.
            // TODO: is this worth trying before we just look for the part?
            Scene s = LocateSceneClientIn(toID);
            if (s != null) {
                part = s.GetSceneObjectPart(objectID);
                regionname = s.RegionInfo.RegionName;
                regionID = s.RegionInfo.RegionID.ToString();
            }
            // If we haven't found the part, cycle through the scenes
            if (part == null) {
                lock (m_scenel)
                {
                    foreach (Scene _scene in m_scenel.Values)
                    {
                        part = _scene.GetSceneObjectPart(objectID);
                        if (part != null) {
                            // TODO: Do we need to verify that this is not a child part?
                            s = _scene;
                            regionname = s.RegionInfo.RegionName;
                            regionID = s.RegionInfo.RegionID.ToString();
                            break;
                        }
                    }
                }
            }
            
            // If we still haven't found the part, we have a problem
            if (part == null) {
                reason = String.Format("[GLOEBITMONEYMODULE] ObjectGiveMoney - Can not locate scripted object with id:{0} which triggered payment request.", objectID);
                m_log.Error(reason);
                return false;
            }
            
            // Check subscription table.  If not exists, send create call to Gloebit
            m_log.DebugFormat("[GLOEBITMONEYMODULE] ObjectGiveMoney - looking for local subscription");
            GloebitSubscription sub = GloebitSubscription.Get(objectID, m_key, m_apiUrl);
            if (sub == null || sub.SubscriptionID == UUID.Zero) {
                // null means we haven't attempted creation yet
                // SubscriptionID == UUID.Zero means we have a local sub, but haven't created this on Gloebit through API.  Likely a previous creation request failed
                // In either case, we need to create a subscription on Gloebit before proceeding

                // Message to user that we are creating the subscription.
                alertUsersSubscriptionTransactionFailedForSubscriptionCreation(fromID, toID, amount, part.Name, part.Description);

                // Don't create unless the object has a name and description
                // Make sure Name and Description are not null to avoid pgsql issue with storing null values
                // Make sure neither are empty as they are required by Gloebit to create a subscription
                if (String.IsNullOrEmpty(part.Name) || String.IsNullOrEmpty(part.Description)) {
                     m_log.InfoFormat("[GLOEBITMONEYMODULE] ObjectGiveMoney - Can not create local subscription because part name or description is blank - Name:{0} Description:{1}", part.Name, part.Description);
                     // Send message to the owner to let them know they must edit the object and add a name and description
                     String imMsg = String.Format("Object with auto-debit script is missing a name or description.  Name and description are required by Gloebit in order to create a subscription for this auto-debit object.  Please enter a name and description in the object.  Current values are Name:[{0}] and Description:[{1}].", part.Name, part.Description);
                     sendMessageToClient(LocateClientObject(fromID), imMsg, fromID);
                     reason = "Owner has not yet created a subscription and object name or description are blank.  Name and Description are required.";
                     return false;
                }

                // Add this user to map of waiting for sub to create auth.
                // TODO: signify that this was from a txn attempt rather than granting of auto-debit perms so that we send dialog instead of just creating auth.
                IClientAPI client = LocateClientObject(fromID);
                if (client != null) {
                    lock (m_authWaitingForSubMap) {
                        m_authWaitingForSubMap[objectID] = client;
                    }
                }

                // call api to submit creation request to Gloebit
                m_apiW.CreateSubscription(objectID, part.Name, part.Description);

                // return false so this the current transaction terminates and object is alerted to failure
                reason = "Owner has not yet created a subscription.";
                return false;   // Async creating sub.  when it returns, we'll continue flow in SubscriptionCreationCompleted
            }
            
            // Check that user has authed Gloebit and token is on file.
            GloebitUser payerUser = GloebitUser.Get(m_key, fromID);
            if (payerUser != null && String.IsNullOrEmpty(payerUser.GloebitToken)) {
                // send message asking to auth Gloebit.
                alertUsersSubscriptionTransactionFailedForGloebitAuthorization(fromID, toID, amount, sub);
                reason = "Owner has not authorized this app with Gloebit.";
                return false;
            }
            
            // Checks done.  Ready to build and submit transaction.
            
            OSDMap descMap = buildOpenSimTransactionDescMap(regionname, regionID, "ObjectGiveMoney", part);
            
            GloebitTransaction txn = buildTransaction(transactionID: txnID, transactionType: TransactionType.OBJECT_PAYS_USER,
                                                          payerID: fromID, payeeID: toID, amount: amount, subscriptionID: sub.SubscriptionID,
                                                          partID: objectID, partName: part.Name, partDescription: part.Description,
                                                          categoryID: UUID.Zero, localID: 0, saleType: 0);
            
            if (txn == null) {
                // build failed, likely due to a reused transactionID.  Shouldn't happen, but possible in ObjectGiveMoney.
                IClientAPI payerClient = LocateClientObject(fromID);
                alertUsersTransactionPreparationFailure(TransactionType.OBJECT_PAYS_USER, TransactionPrecheckFailure.EXISTING_TRANSACTION_ID, payerClient);
                reason = "Transaction submitted with ID for existing transaction.";
                return false;
            }
            
            // This needs to be a sync txn because the object recieves the bool response and uses it as txn success or failure.
            // Todo: remove callbacks from this transaction since we don't use them.
            bool give_result = m_apiW.SubmitSyncTransaction(txn, description, descMap);

            if (!give_result) {
                reason = "Transaction failed during processing.  See logs or text chat for more details.";
                // TODO: pass failure back through SubmitSyncTransaction and design system to pull error string from a failure.
            }
            return give_result;
        }

        public int GetBalance(UUID agentID)
        {
            m_log.DebugFormat("[GLOEBITMONEYMODULE] GetBalance for agent {0}", agentID);
            
            // forceAuthOnInvalidToken = false.  If another system is calling this frequently, it will prevent spamming of users with auth requests.
            // userName is empty string as it is only needed to request auth.
            return (int)m_apiW.GetUserBalance(agentID, false, String.Empty);
        }

        // Please do not refactor these to be just one method
        // Existing implementations need the distinction
        //
        
        // Checks a user's balance to ensure they can cover an upload fee.
        // NOTE: we need to force a balance update because this immediately uploads the asset if we return true.  It does not wait for the charge response.
        // For more details, see:
        // --- BunchOfCaps.NewAgentInventoryRequest
        // --- AssetTransactionModule.HandleUDPUploadRequest
        // --- BunchOfCaps.UploadCompleteHandler
        public bool UploadCovered(UUID agentID, int amount)
        {
            m_log.InfoFormat("[GLOEBITMONEYMODULE] UploadCovered for agent {0}, price {1}", agentID, amount);

            // price is 0.  just return true
            if (amount <= 0) {
                return true;
            }

            IClientAPI client = LocateClientObject(agentID);
            double balance = 0.0;
            
            // force a balance update, then check against amount.
            // Retrieve balance from Gloebit if authed.  Reqeust auth if not authed.  Send purchase url if authed but lacking funds to cover amount.
            balance = UpdateBalance(agentID, client, amount);
            
            if (balance < amount) {
                return false;
            }
            return true;
        }
        
        // Checks a user's balance to ensure they can cover a fee.
        // For more details, see:
        // --- GroupsModule.CreateGroup
        // --- UserProfileModule.ClassifiedInfoUpdate
        public bool AmountCovered(UUID agentID, int amount)
        {
            m_log.InfoFormat("[GLOEBITMONEYMODULE] AmountCovered for agent {0}, price {1}", agentID, amount);

            // price is 0.  just return true
            if (amount <= 0) {
                return true;
            }

            IClientAPI client = LocateClientObject(agentID);
            double balance = 0.0;
            
            // force a balance update, then check against amount.
            // Retrieve balance from Gloebit if authed.  Request auth if not authed.  Send purchase url if authed but lacking funds to cover amount.
            balance = UpdateBalance(agentID, client, amount);
            
            if (balance < amount) {
                return false;
            }
            return true;
        }

        // Please do not refactor these to be just one method
        // Existing implementations need the distinction
        //
        public void ApplyCharge(UUID agentID, int amount, MoneyTransactionType type, string extraData)
        {
            m_log.InfoFormat("[GLOEBITMONEYMODULE] ApplyCharge for agent {0} with extraData {1}", agentID, extraData);
            // As far as I can tell, this is not used in recent versions of OpenSim.
            // For backwards compatibility, call new ApplyCharge func
            ApplyCharge(agentID, amount, type);
        }

        // Charge user a fee
        // Group Creation
        // --- GroupsModule.CreateGroup
        // --- type = MoneyTransactionType.GroupCreate
        // --- Do not throw exception on error.  Group has already been created.  Response has not been sent to viewer.  Unclear what would fail.  Log error instead.
        // Classified Ad fee
        // --- UserProfileModule.ClassifiedInfoUpdate
        // --- type = MoneyTransactionType.ClassifiedCharge
        // --- Throw exception on failure.  Classified ad has not been created yet.
        public void ApplyCharge(UUID agentID, int amount, MoneyTransactionType type)
        {
            m_log.InfoFormat("[GLOEBITMONEYMODULE] ApplyCharge for agent {0}, MoneyTransactionType {1}", agentID, type);
            
            if (amount <= 0) {
                // TODO: Should we report this?  Should we ever get here?
                return;
            }
            
            Scene s = LocateSceneClientIn(agentID);
            string agentName = resolveAgentName(agentID);
            string regionname = s.RegionInfo.RegionName;
            string regionID = s.RegionInfo.RegionID.ToString();
            
            
            string description = String.Empty;
            string txnTypeString = "GeneralFee";
            TransactionType txnType = TransactionType.FEE_GENERAL;
            switch (type) {
                case MoneyTransactionType.GroupCreate:
                    // Group creation fee
                    description = String.Format("Group Creation Fee on {0}, {1}", regionname, m_gridnick);
                    txnTypeString = "GroupCreationFee";
                    txnType = TransactionType.FEE_GROUP_CREATION;
                    break;
                case MoneyTransactionType.ClassifiedCharge:
                    // Classified Ad Fee
                    description = String.Format("Classified Ad Fee on {0}, {1}", regionname, m_gridnick);
                    txnTypeString = "ClassifiedAdFee";
                    txnType = TransactionType.FEE_CLASSIFIED_AD;
                    break;
                default:
                    // Other - not in core at type of writing.
                    description = String.Format("Fee (type {0}) on {1}, {2}", type, regionname, m_gridnick);
                    txnTypeString = "GeneralFee";
                    txnType = TransactionType.FEE_GENERAL;
                    break;
            }
            
            OSDMap descMap = buildOpenSimTransactionDescMap(regionname, regionID.ToString(), txnTypeString);
            
            GloebitTransaction txn = buildTransaction(transactionID: UUID.Zero, transactionType: txnType,
                                                          payerID: agentID, payeeID: UUID.Zero, amount: amount, subscriptionID: UUID.Zero,
                                                          partID: UUID.Zero, partName: String.Empty, partDescription: String.Empty,
                                                          categoryID: UUID.Zero, localID: 0, saleType: 0);
            
            if (txn == null) {
                // build failed, likely due to a reused transactionID.  Shouldn't happen.
                IClientAPI payerClient = LocateClientObject(agentID);
                alertUsersTransactionPreparationFailure(txnType, TransactionPrecheckFailure.EXISTING_TRANSACTION_ID, payerClient);
                return;
            }
            
            bool transaction_result = m_apiW.SubmitTransaction(txn, description, descMap, false);
            
            if (!transaction_result) {
                m_log.ErrorFormat("[GLOEBITMONEYMODULE] ApplyCharge failed to create HTTP Request for [{0}] from agent: [{1}] -- txnID: [{2}] -- agent likely received benefit without being charged.", description, agentID, txn.TransactionID.ToString());
            } else {
                m_log.InfoFormat("[GLOEBITMONEYMODULE] ApplyCharge Transaction queued {0}", txn.TransactionID.ToString());
            }
        }

        // Process the upload charge
        // NOTE: Do not throw exception on failure.  Delivery is complete, but BunchOfCaps.m_FileAgentInventoryState has not been reset to idle.  Fire off an error log instead.
        // For more details, see:
        // --- Scene.AddUploadedInventoryItem "Asset upload"
        // --- BunchOfCaps.UploadCompleteHandler
        public void ApplyUploadCharge(UUID agentID, int amount, string text)
        {
            m_log.InfoFormat("[GLOEBITMONEYMODULE] ApplyUploadCharge for agent {0}, amount {1}, for {2}", agentID, amount, text);
            
            if (amount <= 0) {
                // TODO: Should we report this?  Should we ever get here?
                return;
            }
            
            Scene s = LocateSceneClientIn(agentID);
            string agentName = resolveAgentName(agentID);
            string regionname = s.RegionInfo.RegionName;
            string regionID = s.RegionInfo.RegionID.ToString();
            
            string description = String.Format("Asset Upload Fee on {0}, {1}", regionname, m_gridnick);
            string txnTypeString = "AssetUploadFee";
            TransactionType txnType = TransactionType.FEE_UPLOAD_ASSET;
            
            OSDMap descMap = buildOpenSimTransactionDescMap(regionname, regionID.ToString(), txnTypeString);
            
            GloebitTransaction txn = buildTransaction(transactionID: UUID.Zero, transactionType: txnType,
                                                          payerID: agentID, payeeID: UUID.Zero, amount: amount, subscriptionID: UUID.Zero,
                                                          partID: UUID.Zero, partName: String.Empty, partDescription: String.Empty,
                                                          categoryID: UUID.Zero, localID: 0, saleType: 0);
            
            if (txn == null) {
                // build failed, likely due to a reused transactionID.  Shouldn't happen.
                IClientAPI payerClient = LocateClientObject(agentID);
                alertUsersTransactionPreparationFailure(txnType, TransactionPrecheckFailure.EXISTING_TRANSACTION_ID, payerClient);
                return;
            }
            
            bool transaction_result = m_apiW.SubmitTransaction(txn, description, descMap, false);
            
            if (!transaction_result) {
                m_log.ErrorFormat("[GLOEBITMONEYMODULE] ApplyUploadCharge failed to create HTTP Request for [{0}] from agent: [{1}] -- txnID: [{2}] -- agent likely received benefit without being charged.", description, agentID, txn.TransactionID.ToString());
            } else {
                m_log.InfoFormat("[GLOEBITMONEYMODULE] ApplyUploadCharge Transaction queued {0}", txn.TransactionID.ToString());
            }
        }

        // property to store fee for uploading assets
        // NOTE: This is the prim BaseCost.  If mesh, this is calculated in BunchOfCaps
        // For more details, see:
        // --- BunchOfCaps.NewAgentInventoryRequest
        // --- AssetTransactionModule.HandleUDPUploadRequest
        // Returns the PriceUpload set by the economy section of the config
        public int UploadCharge
        {
            get { return PriceUpload; }
        }

        // property to store fee for creating a group
        // For more details, see:
        // --- GroupsModule.CreateGroup
        public int GroupCreationCharge
        {
            get { return PriceGroupCreate; }    // TODO: PriceGroupCreate is defaulted to -1, not 0.  Why is this?  How should we handle this?
        }

//#pragma warning disable 0067
        public event ObjectPaid OnObjectPaid;
//#pragma warning restore 0067

        #endregion // IMoneyModule members

        #region GMM API Setup

        /******************
         * Configuration parameters must be loaded before the API is initialized
         * API must be initialized with those parameters
         * DB connections must be initialized/established
         *******************/

        /// <summary>
        /// Configure the GloebitAPI details for connecting to the Gloebit service
        /// --- key & secret identifying the Gloebit app
        /// --- URL of environment to use -- TODO: make this simply an environment and set URL in GAPI
        /// Define Callback interfaces for async operations, enacting of delivery of assets, and various alerts
        /// Initialize DB connections
        /// </summary>
        private void InitGloebitAPI()
        {
            m_log.Info("[GLOEBITMONEYMODULE] Initializing Gloebit API");
            m_apiW = new GloebitAPIWrapper(m_key, m_keyAlias, m_secret, new Uri(m_apiUrl), m_dbProvider, m_dbConnectionString, this, this, this, this, this, this);
        }

        private void RegisterGloebitWebhooks(IHttpServer httpServer)
        {
            /********** Register endpoints the Gloebit Service will call back into **********/
            // Register callback for 2nd stage of OAuth2 Authorization_code_grant
            httpServer.AddHTTPHandler("/gloebit/auth_complete", m_apiW.authComplete_func);
            // Register callback for asset enact, consume, & cancel holds transaction parts
            httpServer.AddHTTPHandler("/gloebit/transaction", m_apiW.transactionState_func);
            // Used by the inform parameter to GloebitAPI.Purchase.  Called when a user has finished purchasing gloebits -- not yet implemented by Gloebit service
            httpServer.AddHTTPHandler("/gloebit/buy_complete", m_apiW.buyComplete_func);
        }

        #endregion // GMM API Setup

        #region GMM User Auth and Balance

        /***************
         * GloebitUser.Get() should be called to create or retrieve an AppUser
         * --- There is not one central GMM function for this as it is done throughout the
         * --- GMM, but it may later be centralized.
         * --- See SendNewSessionMessaging() function for closest thing to a StartUserSession().
         * GloebitUser.Cleanup() should be called to free up memory when a User is no longer active
         * calling UpdateBalance() or m_apiW.GetUserBalance() function with forceAuthoOnInvalidToken=true
         * --- is simplest way to ensure user is authorized and ready to proceed with transactions.
         ***************/

        /// <summary>
        /// Requests the user's balance from Gloebit if authorized.
        /// If not authorized, sends an auth request to the user.
        /// Sends the balance to the client (or sends 0 if failure due to lack of auth).
        /// If the balance is less than the purchaseIndicator, sends the purchase url to the user.
        /// NOTE: Does not provide any transaction details in the SendMoneyBalance call.  Do not use this helper for updates within a transaction.
        /// </summary>
        /// <param name="agentID">OpenSim AgentID for the user whose balance is being updated.</param>
        /// <param name="client">IClientAPI for agent.  Need to pass this in because locating returns null when called from OnNewClient.
        ///                      moved to OnCompleteMovementToRegion, but haven't tested if locating is still null.
        ///                      also have not yet compared efficiency to removing and calling resolveAgentName.</param>
        /// <param name ="purchaseIndicator">int indicating whether we should deliver the purchase url to the user when we have an authorized user.
        ///                 -1: always deliver
        ///                 0: never deliver
        ///                 positive number: deliver if user's balance is below this indicator
        /// </param>
        /// <returns>Gloebit balance for the Gloebit account linked to this OpenSim agent or 0.0.</returns>
        private double UpdateBalance(UUID agentID, IClientAPI client, int purchaseIndicator)
        {
            int returnfunds = 0;
            double realBal = 0.0;

            try
            {
                // Request balance from Gloebit.  Request Auth from Gloebit if necessary
                realBal = m_apiW.GetUserBalance(agentID, true, client.Name);
            }
            catch (WebException we)
            {
                string errorMessage = we.Message;
                if (we.Status == WebExceptionStatus.ProtocolError)
                {
                    using (HttpWebResponse webResponse = (HttpWebResponse)we.Response)
                        errorMessage = String.Format("[{0}] {1}",webResponse.StatusCode,webResponse.StatusDescription);
                }
                m_log.Error(errorMessage);
                m_log.Error(we.ToString());
                client.SendAlertMessage(we.Message + " ");
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[GLOEBITMONEYMODULE] UpdateBalance Failure, Exception:{0} \n\nE:{1}", e.Message, e);
                client.SendAlertMessage(e.Message + " ");
            }

            // Get balance rounded down (may not be int for merchants)
            returnfunds = (int)realBal;
            // NOTE: if updating as part of a transaction, call SendMoneyBalance directly with transaction information instead of using UpdateBalance
            client.SendMoneyBalance(UUID.Zero, true, new byte[0], returnfunds, 0, UUID.Zero, false, UUID.Zero, false, 0, String.Empty);

            if (purchaseIndicator == -1 || purchaseIndicator > returnfunds) {
                // Send purchase URL to make it easy to find out how to buy more gloebits.
                GloebitUser u = GloebitUser.Get(m_key, client.AgentId);
                if (u.IsAuthed()) {
                    // Deliver Purchase URI in case the helper-uri is not working
                    Uri url = m_apiW.BuildPurchaseURI(BaseURI, u);
                    SendUrlToClient(client, "Need more gloebits?", "Buy gloebits you can spend on this grid:", url);
                }
            }
            return realBal;
        }

        #endregion //GMM User Auth and Balance

        #region GMM Transaction

        /****************
         * To add a new commerce flow:
         * 1. Add a TransactionType enum.
         *    Define a new TransationType for the flow which should be supplied by triggering event.
         *    Add this TransactionType to the switch statement of a receiving event for processing this flow.
         * 2. Handle TransactionPrecheckFailures
         *    Define any newly necessary TransactionPrecheckFailure enums not already created.
         *    Call alertUsersTransactionPreparationFailure() as necessary from the interface function handling processing.
         *    Edit alertUsersTransactionPrepartionFailure() as necessary to provide specific messaging for this new TransactionType.
         * 3. Compile info to be supplied in user's transaction history on gloebit.com 
         *    Call buildOpenSimTransactionDescMap
         *    If more info is needed, either create a new override for buildOpenSimTransactionDescMap, or
         *    call AddDescMapEntry to add elements one at a time to the base map.
         *    Create a description string to be displayed as the primary transaction description
         * 4. Build the Transaction
         *    Supply the proper information to buildTransaction().
         *    If you will need additional information when processing this transaction which you can not store in the default 
         *    transaction parameters, then you will need to create a new dictionary to map the transaction UUID to the asset 
         *    information you'll require.
         *    If this is a subscription transaction, you'll also need to create or retrieve the subscription authorization.
         * 5. Submit the Transaction to Gloebit
         *    Call GloebitAPIWrapper SubmitTransaction() or SubmitSyncTransaction() and supply the transaction, description and descMap 
         *    for this transaction type.
         * 6. Implement delivery of asset related to payment (see region GMM IAssetCallback Interface)
         *    Add this TransactionType to processAssetEnactHold(), processAssetConsumeHold(), processAssetCancelHold()
         *    to handle the particulars of asset delivery for this TransactionType
         *    These will be triggered by Gloebit during processing of this transaction
         * 7. Implement any platform specific functional requirements of transaction process stages not handled in 
         *    asset enact/consume/cancel via the GloebitAPIWrapper ITransactionAlert interface AlertTransaction functions
         * 8. Provide messaging to user throughout transaction (see region GMM User Messaging)
         *    Add this TransactionType to alertUsersTransactionBegun()
         *    As necessary, add this TransactionType to alertUsersTransactionStageCompleted(), alertUsersTransactionFailed()
         *    and alertUsersTransactionSucceeded() to supply transaction specific messaging.
         ****************/

        // TODO: Consider if we could use classes for different transaction types to group the entire transaction flow and 
        // messaging into one area.

        #region GMM Transaction enums

        // TODO: consider replacing with libOpenMetaverse MoneyTransactionType
        // https://github.com/openmetaversefoundation/libopenmetaverse/blob/master/OpenMetaverse/AgentManager.cs#L342
        public enum TransactionType : int
        {
            /* Fees */
            FEE_GROUP_CREATION  = 1002,             // comes through ApplyCharge
            FEE_UPLOAD_ASSET    = 1101,             // comes through ApplyUploadCharge
            FEE_CLASSIFIED_AD   = 1103,             // comes through ApplyCharge
            FEE_GENERAL         = 1104,             // here for anything we're unaware of yet.

            /* Purchases */
            USER_BUYS_OBJECT    = 5000,             // comes through ObjectBuy
            USER_PAYS_USER      = 5001,             // comes through OnMoneyTransfer
            USER_BUYS_LAND      = 5002,             // comes through scene events OnValidateLandBuy and OnLandBuy
            REFUND              = 5005,             // not yet implemented
            USER_BUYS_LANDPASS  = 5006,             // comes through ParcelBuyPass pre 0.9.1; MoveMoney post 0.9.1
            USER_PAYS_OBJECT    = 5008,             // comes through OnMoneyTransfer

            /* Auto-Debit Subscription */
            OBJECT_PAYS_USER    = 5009,             // script auto debit owner - comes through ObjectGiveMoney

            /* Catch-all for unimplemented MoveMoney types */
            MOVE_MONEY_GENERAL  = 5011,             // Unimplemented MoveMoney - will fail.
        }

        public enum TransactionPrecheckFailure : int
        {
            BUYING_DISABLED,
            OBJECT_NOT_FOUND,
            AMOUNT_MISMATCH,
            SALE_TYPE_INVALID,
            SALE_TYPE_MISMATCH,
            BUY_SELL_MODULE_INACCESSIBLE,
            LAND_VALIDATION_FAILED,
            EXISTING_TRANSACTION_ID,
            GROUP_OWNED,
        }

        #endregion // GMM Transaction enums

        #region GMM Transaction submission
        
        /// <summary>
        /// Build a GloebitTransaction for a specific TransactionType.  This Transaction will be:
        /// --- persistently stored
        /// --- used for submitting to Gloebit via the TransactU2U endpoint via <see cref="SubmitTransaction"/> and <see cref="SubmitSyncTransaction"/> functions,
        /// --- used for processing transact enact/consume/cancel callbacks to handle any other OpenSim components of the transaction(such as object delivery),
        /// --- used for tracking/reporting/analysis
        /// </summary>
        /// <param name="transactionID">UUID to use for this transaction.  If UUID.Zero, a random UUID is chosen.</param>
        /// <param name="transactionType">enum from OpenSim defining the type of transaction (buy object, pay object, pay user, object pays user, etc).  This will not affect how Gloebit process the monetary component of a transaction, but is useful for easily varying how OpenSim should handle processing once funds are transferred.</param>
        /// <param name="payerID">OpenSim UUID of agent sending gloebits.</param>
        /// <param name="payeeID">OpenSim UUID of agent receiving gloebits.  UUID.Zero if this is a fee being paid to the app owner (not a u2u txn).</param>
        /// <param name="amount">Amount of gloebits being transferred.</param>
        /// <param name="subscriptionID">UUID of subscription for automated transactions (Object pays user).  Otherwise UUID.Zero.</param>
        /// <param name="partID">UUID of the object, when transaction involves an object.  UUID.Zero otherwise.</param>
        /// <param name="partName">string name of the object, when transaction involves an object.  null otherwise.</param>
        /// <param name="partDescription">string description of the object, when transaction involves an object.  String.Empty otherwise.</param>
        /// <param name="categoryID">UUID of folder in object used when transactionType is ObjectBuy and saleType is copy.  UUID.Zero otherwise.  Required by IBuySellModule.</param>
        /// <param name="localID">uint region specific id of object used when transactionType is ObjectBuy.  0 otherwise.  Required by IBuySellModule.</param>
        /// <param name="saleType">int differentiating between original, copy or contents for ObjectBuy.  Required by IBuySellModule to process delivery.</param>
        /// <returns>GloebitTransaction created. if successful.</returns>
        private GloebitTransaction buildTransaction(UUID transactionID, TransactionType transactionType, UUID payerID, UUID payeeID, int amount, UUID subscriptionID, UUID partID, string partName, string partDescription, UUID categoryID, uint localID, int saleType)
        {
            // TODO: we should store "transaction description" with the Transaction?
            
            bool isRandomID = false;
            if (transactionID == UUID.Zero) {
                // Create a transaction ID
                transactionID = UUID.Random();
                isRandomID = true;
            }
            
            // Get user names
            string payerName = resolveAgentName(payerID);
            string payeeName = resolveAgentName(payeeID);
            
            // set up defaults
            bool isSubscriptionDebit = false;
            string transactionTypeString = String.Empty;
            //subscriptionID = UUID.Zero;
            
            switch (transactionType) {
                case TransactionType.USER_BUYS_OBJECT:
                    // 5000 - ObjectBuy
                    transactionTypeString = "User Buys Object";
                    // This is the only type which requires categoryID, localID, and saleType
                    break;
                case TransactionType.USER_PAYS_USER:
                    // 5001 - OnMoneyTransfer - Pay User
                    transactionTypeString = "User Pays User";
                    // This is the only type which doesn't include a partID, partName or partDescription since no object is involved.
                    break;
                case TransactionType.USER_PAYS_OBJECT:
                    // 5008 - OnMoneyTransfer - Pay Object
                    transactionTypeString = "User Pays Object";
                    break;
                case TransactionType.OBJECT_PAYS_USER:
                    // 5009 - ObjectGiveMoney
                    transactionTypeString = "Object Pays User";
                    isSubscriptionDebit = true;
                    // TODO: should I get the subscription ID here instead of passing it in?
                    // TODO: what to do if subscriptionID is zero?
                    break;
                case TransactionType.USER_BUYS_LAND:
                    // 5002 - OnLandBuy
                    transactionTypeString = "User Buys Land";
                    break;
                case TransactionType.USER_BUYS_LANDPASS:
                    // 5006 - OnParcelBuyPass pre 0.9.1; MoveMoney after;
                    transactionTypeString = "User Buys Land Pass";
                    break;
                case TransactionType.FEE_GROUP_CREATION:
                    // 1002 - ApplyCharge
                    transactionTypeString = "Group Creation Fee";
                    if (payeeID == UUID.Zero) {
                        payeeName = "App Owner";
                    }
                    break;
                case TransactionType.FEE_UPLOAD_ASSET:
                    // 1101 - ApplyUploadCharge
                    transactionTypeString = "Asset Upload Fee";
                    if (payeeID == UUID.Zero) {
                        payeeName = "App Owner";
                    }
                    break;
                case TransactionType.FEE_CLASSIFIED_AD:
                    // 1103 - ApplyCharge
                    transactionTypeString = "Classified Ad Fee";
                    if (payeeID == UUID.Zero) {
                        payeeName = "App Owner";
                    }
                    break;
                case TransactionType.FEE_GENERAL:
                    // 1104 - ApplyCharge - catch all in case there are modules which enable fees which are not used in the core.
                    transactionTypeString = "General Fee";
                    if (payeeID == UUID.Zero) {
                        payeeName = "App Owner";
                    }
                    break;
                default:
                    m_log.ErrorFormat("[GLOEBITMONEYMODULE] buildTransaction failed --- unknown transaction type: {0}", transactionType);
                    // TODO: should we throw an exception?  return null?  just continue?
                    break;
            }
            
            // Storing a null field in pgsql fails, so ensure partName and partDescription are not null in case those are not properly set to String.Empty when blank
            if (partName == null) {
                partName = String.Empty;
            }
            if (partDescription == null) {
                partDescription = String.Empty;
            }
            
            GloebitTransaction txn = GloebitTransaction.Create(transactionID, payerID, payerName, payeeID, payeeName, amount, (int)transactionType, transactionTypeString, isSubscriptionDebit, subscriptionID, partID, partName, partDescription, categoryID, localID, saleType);
            
            if (txn == null && isRandomID) {
                // Try one more time in case the incredibly unlikely event of a UUID.Random overlap has occurred.
                transactionID = UUID.Random();
                txn = GloebitTransaction.Create(transactionID, payerID, payerName, payeeID, payeeName, amount, (int)transactionType, transactionTypeString, isSubscriptionDebit, subscriptionID, partID, partName, partDescription, categoryID, localID, saleType);
            }
            
            return txn;
        }
        

        #region GMM Transaciton Desc Map helpers

        /// <summary>
        /// Helper function to build the minimal OpenSim transaction description sent to the Gloebit transactU2U endpoint.
        /// Used for tracking as well as information provided in transaction histories.
        /// If transaction includes an object, use the version which takes a fourth parameter as a SceneObjectPart.
        /// </summary>
        /// <param name="regionname">Name of the OpenSim region where this transaction is taking place.</param>
        /// <param name="regionID">OpenSim UUID of the region where this transaction is taking place.</param>
        /// <param name="txnType">String describing the type of transaction.  e.g. ObjectBuy, PayObject, PayUser, etc.</param>
        /// <returns>OSDMap to be sent with the transaction request parameters.  Map contains six dictionary entries, each including an OSDArray.</returns>
        private OSDMap buildOpenSimTransactionDescMap(string regionname, string regionID, string txnType)
        {
            // Create descMap
            OSDMap descMap = m_apiW.BuildBaseTransactionDescMap(txnType);

            // Add base platform details
            m_apiW.AddDescMapEntry(descMap, "platform", "platform", "OpenSim");
            m_apiW.AddDescMapEntry(descMap, "platform", "version", m_opensimVersion);
            m_apiW.AddDescMapEntry(descMap, "platform", "version-number", m_opensimVersionNumber);
            // TODO: Should we add hosting-provider or more?

            // Add base location details
            m_apiW.AddDescMapEntry(descMap, "location", "grid-name", m_gridname);
            m_apiW.AddDescMapEntry(descMap, "location", "grid-nick", m_gridnick);
            m_apiW.AddDescMapEntry(descMap, "location", "region-name", regionname);
            m_apiW.AddDescMapEntry(descMap, "location", "region-id", regionID);

            return descMap;
        }

        /// <summary>
        /// Helper function to build the minimal OpenSim transaction description including a ScenObjectPart
        /// sent to the Gloebit transactU2U endpoint.
        /// Used for tracking as well as information provided in transaction histories.
        /// If transaction does not include an object, use the version which takes three parameters instead.
        /// </summary>
        /// <param name="regionname">Name of the OpenSim region where this transaction is taking place.</param>
        /// <param name="regionID">OpenSim UUID of the region where this transaction is taking place.</param>
        /// <param name="txnType">String describing the type of transaction.  eg. ObjectBuy, PayObject, PayUser, etc.</param>
        /// <param name="part">Object (as SceneObjectPart) which is involved in this transaction (being sold, being paid, paying user, etc.).</param>
        /// <returns>OSDMap to be sent with the transaction request parameters.  Map contains six dictionary entries, each including an OSDArray.</returns>
        private OSDMap buildOpenSimTransactionDescMap(string regionname, string regionID, string txnType, SceneObjectPart part)
        {
            // Build universal base OpenSim descMap
            OSDMap descMap = buildOpenSimTransactionDescMap(regionname, regionID, txnType);

            // Add base descMap details for transaction involving an object/part
            if (descMap != null && part != null) {
                m_apiW.AddDescMapEntry(descMap, "location", "object-group-position", part.GroupPosition.ToString());
                m_apiW.AddDescMapEntry(descMap, "location", "object-absolute-position", part.AbsolutePosition.ToString());
                m_apiW.AddDescMapEntry(descMap, "transaction", "object-name", part.Name);
                m_apiW.AddDescMapEntry(descMap, "transaction", "object-description", part.Description);
                m_apiW.AddDescMapEntry(descMap, "transaction", "object-id", part.UUID.ToString());
                m_apiW.AddDescMapEntry(descMap, "transaction", "creator-name", resolveAgentName(part.CreatorID));
                m_apiW.AddDescMapEntry(descMap, "transaction", "creator-id", part.CreatorID.ToString());
            }
            return descMap;
        }

        /// <summary>
        /// Helper function to build the minimal OpenSim transaction description including LandData
        /// sent to the Gloebit transactU2U endpoint.
        /// Used for tracking as well as information provided in transaction histories.
        /// If transaction does not include Parcel.LandData, use the version which takes three parameters instead.
        /// </summary>
        /// <param name="regionname">Name of the OpenSim region where this transaction is taking place.</param>
        /// <param name="regionID">OpenSim UUID of the region where this transaction is taking place.</param>
        /// <param name="txnType">String describing the type of transaction.  e.g. ObjectBuy, PayObject, PayUser, etc.</param>
        /// <param name="pld">Object (as Parcel.LandData) which is involved in this transaction (pass being purchased for it).</param>
        /// <returns>OSDMap to be sent with the transaction request parameters.  Map contains six dictionary entries, each including an OSDArray.</returns>
        private OSDMap buildOpenSimTransactionDescMap(string regionname, string regionID, string txnType, LandData pld)
        {
            // Build universal base descMap
            OSDMap descMap = buildOpenSimTransactionDescMap(regionname, regionID, txnType);

            // Add base descMap details for transaction involving an object/part
            if (descMap != null && pld != null) {
                m_apiW.AddDescMapEntry(descMap, "location", "parcel-upper-corner-position", pld.AABBMax.ToString());
                m_apiW.AddDescMapEntry(descMap, "location", "parcel-lower-corner-position", pld.AABBMin.ToString());
                m_apiW.AddDescMapEntry(descMap, "location", "parcel-area", pld.Area.ToString());
                m_apiW.AddDescMapEntry(descMap, "transaction", "parcel-name", pld.Name);
                m_apiW.AddDescMapEntry(descMap, "transaction", "parcel-description", pld.Description);
                m_apiW.AddDescMapEntry(descMap, "transaction", "parcel-global-id", pld.GlobalID.ToString());
                m_apiW.AddDescMapEntry(descMap, "transaction", "parcel-local-id", pld.LocalID.ToString());
                if (pld.IsGroupOwned) {
                    m_apiW.AddDescMapEntry(descMap, "transaction", "parcel-group-owner-id", pld.GroupID.ToString());
                } else {
                    m_apiW.AddDescMapEntry(descMap, "transaction", "parcel-owner-name", resolveAgentName(pld.OwnerID));
                }
                m_apiW.AddDescMapEntry(descMap, "transaction", "parcel-owner-id", pld.OwnerID.ToString());
                m_apiW.AddDescMapEntry(descMap, "transaction", "pass-hours", pld.PassHours.ToString());
            }
            return descMap;
        }

        #endregion // GMM Transaction Desc Map helpers

        #endregion // GMM Transaction Submission

        #region GMM IAssetCallback Interface

        /***************************************/
        /**** IAssetCallback Interface *********/
        /***************************************/

        /* Interface for handling state progression of assets.
         * Any transaction which includes a local asset will receive callbacks at each transaction stage.
         * ENACT -> Funds have been transferred.  Process local asset (generally deliver a product)
         * CONSUME -> Funds have been released to recipient.  Finalize anything necessary.
         * CANCEL -> Transaction has been canceled.  Undo anything necessary
         */

        public bool processAssetEnactHold(GloebitTransaction txn, out string returnMsg) {

            // If we've gotten this call, then the Gloebit components have enacted successfully
            // all funds have been transferred.
            // ITransactionAlert.AlertTransactionStageCompleted for ENACT_GLOEBIT just fired

            switch ((TransactionType)txn.TransactionType) {
                case TransactionType.USER_BUYS_OBJECT:
                    // 5000 - ObjectBuy
                    // Need to deliver the object/contents purchased.
                    bool delivered = deliverObject(txn, out returnMsg);
                    if (!delivered) {
                        // Local Asset Enact failed - set returnMsg
                        returnMsg = String.Format("Asset enact failed: {0}", returnMsg);
                        return false;
                    }
                    break;
                case TransactionType.USER_PAYS_USER:
                    // 5001 - OnMoneyTransfer - Pay User
                    // nothing to enact
                    break;
                case TransactionType.USER_PAYS_OBJECT:
                    // 5008 - OnMoneyTransfer - Pay Object
                    // need to alert the object that it has been paid.
                    ObjectPaid handleObjectPaid = OnObjectPaid;
                    if(handleObjectPaid != null) {
                        handleObjectPaid(txn.PartID, txn.PayerID, txn.Amount);
                        // This doesn't provide a return or ability to query state, so we assume success
                    } else {
                        // This really shouldn't happen, as it would mean that the OpenSim region is not properly set up
                        // However, we won't fail here as expectation is unclear
                        // We have received this when a sim has another active money module which didn't respect the config and tried to enable on
                        // this region as well and it received the objectPaid event registration instead of the GMM.
                        m_log.ErrorFormat("[GLOEBITMONEYMODULE].processAssetEnactHold - IMoneyModule OnObjectPaid event not properly subscribed.  Object payment may have failed.");
                    }
                    break;
                case TransactionType.OBJECT_PAYS_USER:
                    // 5009 - ObjectGiveMoney
                    // nothing to enact.
                    break;
                case TransactionType.USER_BUYS_LAND:
                    // 5002 - OnLandBuy
                    // Need to transfer land
                    bool transferred = transferLand(txn, out returnMsg);
                    if (!transferred) {
                        // Local Asset Enact failed - set returnMsg
                        returnMsg = String.Format("Asset enact failed: {0}", returnMsg);
                        // remove land asset from map since cancel will not get called
                        // TODO: should we do this here, or 
                        //       - adjust ProcessAssetCancelHold to always be called and check state to see if something needs to be undone?
                        //       - do this from AlertTransactionFailed()
                        lock(m_landAssetMap) {
                            m_landAssetMap.Remove(txn.TransactionID);
                        }
                        return false;
                    }
                    break;
                case TransactionType.USER_BUYS_LANDPASS:
                    // 5006 - OnParcelBuyPass prior to 0.9.1; MoveMoney after;
                    // NOTE: new flow: delivery is outside of our system.  Nothing to enact.
                    if (!m_newLandPassFlow) {
                        bool landPassDelivered = deliverLandPass(txn, out returnMsg);
                        if (!landPassDelivered) {
                            // Local Asset Enact failed - set returnMsg
                            returnMsg = String.Format("Asset enact failed: {0}", returnMsg);
                            return false;
                        }
                    }
                    break;
                case TransactionType.FEE_GROUP_CREATION:
                    // 1002 - ApplyCharge
                    // Nothing to do since the group was already created.  Ideally, this would create group or finalize creation.
                    break;
                case TransactionType.FEE_UPLOAD_ASSET:
                    // 1101 - ApplyUploadCharge
                    // Nothing to do since the asset was already uploaded.  Ideally, this would upload asset or finalize upload.
                    break;
                case TransactionType.FEE_CLASSIFIED_AD:
                    // 1103 - ApplyCharge
                    // Nothing to do since the ad was already placed.  Ideally, this would create ad finalize ad.
                    break;
                default:
                    m_log.ErrorFormat("[GLOEBITMONEYMODULE] processAssetEnactHold called on unknown transaction type: {0}", txn.TransactionType);
                    // TODO: should we throw an exception?  return null?  just continue?
                    // take no action.
                    break;
            }

            // Local Asset Enact completed
            // ITransactionAlert.AlertTransactionStageCompleted for ENACT_ASSET will be fired by calling function
            returnMsg = "Asset enact succeeded";
            return true;
        }
            
        public bool processAssetConsumeHold(GloebitTransaction txn, out string returnMsg) {

            m_log.InfoFormat("[GLOEBITMONEYMODULE].processAssetConsumeHold SUCCESS - transaction complete");

            // If we've gotten this call, then the Gloebit components have enacted successfully
            // all transferred funds have been committed.
            // ITransactionAlert.AlertTransactionStageCompleted for CONSUME_GLOEBIT just fired

            switch ((TransactionType)txn.TransactionType) {
                case TransactionType.USER_BUYS_OBJECT:
                    // 5000 - ObjectBuy
                    // nothing to finalize
                    break;
                case TransactionType.USER_PAYS_USER:
                    // 5001 - OnMoneyTransfer - Pay User
                    // nothing to finalize
                    break;
                case TransactionType.USER_PAYS_OBJECT:
                    // 5008 - OnMoneyTransfer - Pay Object
                    // nothing to finalize
                    break;
                case TransactionType.OBJECT_PAYS_USER:
                    // 5009 - ObjectGiveMoney
                    // nothing to finalize
                    break;
                case TransactionType.USER_BUYS_LAND:
                    // 5002 - OnLandBuy
                    // Remove land asset from map
                    lock(m_landAssetMap) {
                        m_landAssetMap.Remove(txn.TransactionID);
                    }
                    break;
                case TransactionType.USER_BUYS_LANDPASS:
                    // 5006 - OnParcelBuyPass pre 0.9.1; MoveMoney after;
                    // nothing to finalize
                    break;
                case TransactionType.FEE_GROUP_CREATION:
                    // 1002 - ApplyCharge
                    // Nothing to do since the group was already created.  Ideally, this would finalize creation.
                    break;
                case TransactionType.FEE_UPLOAD_ASSET:
                    // 1101 - ApplyUploadCharge
                    // Nothing to do since the asset was already uploaded.  Ideally, this would finalize upload.
                    break;
                case TransactionType.FEE_CLASSIFIED_AD:
                    // 1103 - ApplyCharge
                    // Nothing to do since the ad was already placed.  Ideally, this would finalize ad.
                    break;
                default:
                    m_log.ErrorFormat("[GLOEBITMONEYMODULE] processAssetConsumeHold called on unknown transaction type: {0}", txn.TransactionType);
                    // TODO: should we throw an exception?  return null?  just continue?
                    // take no action.
                    break;
            }

            // Local Asset Consume completed
            returnMsg = "Asset consume succeeded";
            return true;
        }

        // This is called even if the the local asset had not previously been successfully enacted so cleanup can occur.
        // But txn.enacted should be checked before attempting to roll back anything done in enactHold
        // It may not be possible for this to be called with txn.enacted=true since the local asset is the final transaction component
        // and the transaction should not be able to fail once it enacts successfully.
        public bool processAssetCancelHold(GloebitTransaction txn, out string returnMsg)
        {
            m_log.InfoFormat("[GLOEBITMONEYMODULE].processAssetCancelHold SUCCESS - transaction rolled back");
            // nothing to cancel - either enact of asset failed or was never called if we're here.
            switch ((TransactionType)txn.TransactionType) {
                case TransactionType.USER_BUYS_OBJECT:
                    // 5000 - ObjectBuy
                    // no mechanism for reversing delivery
                    break;
                case TransactionType.USER_PAYS_USER:
                    // 5001 - OnMoneyTransfer - Pay User
                    // nothing to cancel
                    break;
                case TransactionType.USER_PAYS_OBJECT:
                    // 5008 - OnMoneyTransfer - Pay Object
                    // no mechanism for notifying object
                    break;
                case TransactionType.OBJECT_PAYS_USER:
                    // 5009 - ObjectGiveMoney
                    // nothing to cancel
                    break;
                case TransactionType.USER_BUYS_LAND:
                    // 5002 - OnLandBuy
                    // nothing to cancel, if we're here, it is because land was not transferred successfully.
                    break;
                case TransactionType.USER_BUYS_LANDPASS:
                    // 5006 - OnParcelBuyPass pre 0.9.1; MoveMoney after;
                    // nothing to cancel, if we're here, it is because landpass was not granted successfully.
                    break;
                case TransactionType.FEE_GROUP_CREATION:
                    // 1002 - ApplyCharge
                    // TODO: can we delete the group?
                    break;
                case TransactionType.FEE_UPLOAD_ASSET:
                    // 1101 - ApplyUploadCharge
                    // TODO: can we delete the asset?
                    break;
                case TransactionType.FEE_CLASSIFIED_AD:
                    // 1103 - ApplyCharge
                    // TODO: can we delete the ad?
                    break;
                default:
                    m_log.ErrorFormat("[GLOEBITMONEYMODULE] processAssetCancelHold called on unknown transaction type: {0}", txn.TransactionType);
                    // TODO: should we throw an exception?  return null?  just continue?
                    // take no action.
                    break;
            }

            // Local Asset Cancel completed
            returnMsg = "Asset cancel succeeded";
            return true;
        }

        #endregion // GMM IAssetCallback Interface

        #region GMM Transaction Alerts

        public void AlertTransactionBegun(GloebitTransaction txn, string description)
        {
            alertUsersTransactionBegun(txn, description);
        }

        public void AlertTransactionStageCompleted(GloebitTransaction txn, GloebitAPI.TransactionStage stage, string additionalDetails)
        {
            alertUsersTransactionStageCompleted(txn, stage, additionalDetails);
        }
        public void AlertTransactionSucceeded(GloebitTransaction txn)
        {
            alertUsersTransactionSucceeded(txn);
        }

        public void AlertTransactionFailed(GloebitTransaction txn, GloebitAPI.TransactionStage stage, GloebitAPI.TransactionFailure failure, string additionalFailureDetails, OSDMap extraData)
        {
            // Notify affected users that the transaction failed
            alertUsersTransactionFailed(txn, stage, failure, additionalFailureDetails);

            // Handle any functional/flow requirements of any failures.
            // Since OpenSim uses subscriptions in an odd way and doesn't yet store subscription authorizations, those need special handling here.
            if (stage == GloebitAPI.TransactionStage.VALIDATE) {
                IClientAPI payerClient = LocateClientObject(txn.PayerID);
                switch(failure) {
                    case GloebitAPI.TransactionFailure.SUBSCRIPTION_AUTH_NOT_FOUND:           /* No sub_auth has been created for this user for this subscription */
                        m_log.InfoFormat("[GLOEBITMONEYMODULE].AlertTransactionFailed No subscription authorization in place.  Asking payer to auth.  transactionID:{0}, app-subscription-id:{1} PayerID:{2} PayerName:{3}", txn.TransactionID, txn.SubscriptionID, txn.PayerID, txn.PayerName);
                        // TODO: Should we store auths so we know if we need to create it or just to ask user to auth after failed transaction?
                        // We have a valid subscription, but no subscription auth for this user-id-on-app+token(gloebit_uid) combo

                        // Ask user if they would like to authorize
                        // Don't call CreateSubscriptionAuthorization unless they do.  If this is fraud, the user will not want to see a pending auth on Gloebit.
                        if (payerClient != null) {
                            Dialog.Send(new CreateSubscriptionAuthorizationDialog(payerClient, txn.PayerID, txn.PayerName, txn.PartID, txn.PartName, txn.PartDescription, txn.TransactionID, txn.PayeeID, txn.PayeeName, txn.Amount, txn.SubscriptionID, m_apiW, BaseURI));
                        } else {
                            // TODO: does the message eventually make it if the user is offline?  Is there a way to send a Dialog to a user the next time they log in?
                            // Should we just create the subscription_auth in this case?
                            // TODO: This is an issue with restoring of auto-debit scripted objects with new UUID.  If owner isn't online, they won't get asked to re-auth on failure.
                        }
                        break;
                    case GloebitAPI.TransactionFailure.SUBSCRIPTION_AUTH_PENDING:             /* User has not yet approved or declined the authorization for this subscription */
                        // User has been asked and chose to auth already.
                        // Subscription-authorization has already been created.
                        // User has not yet responded to that request, so send a dialog again to ask for auth and allow reporting of fraud.

                        m_log.InfoFormat("[GLOEBITMONEYMODULE].AlertTransactionFailed subscription authorization pending.  Asking payer to auth again.  transactionID:{0}, app-subscription-id:{1} PayerID:{2} PayerName:{3}", txn.TransactionID, txn.SubscriptionID, txn.PayerID, txn.PayerName);
                        // Send request to user again
                        if (payerClient != null) {
                            UUID subscriptionAuthID = UUID.Zero;
                            // pull from extraData because not in transaction
                            if (extraData.ContainsKey("subscription-authorization-id")) {
                                subscriptionAuthID = UUID.Parse(extraData["subscription-authorization-id"]);
                            } else {
                                m_log.Error("[GLOEBITMONEYMODULE].AlertTransactionFailed subscription-authorization-id expected, but missing from extraData");
                            }
                            Dialog.Send(new PendingSubscriptionAuthorizationDialog(payerClient, txn.PayerID, txn.PayerName, txn.PartID, txn.PartName, txn.PartDescription, txn.TransactionID, txn.PayeeID, txn.PayeeName, txn.Amount, txn.SubscriptionID, subscriptionAuthID, m_apiW, BaseURI));
                        } else {
                            // TODO: does the message eventually make it if the user is offline?  Is there a way to send a Dialog to a user the next time they log in?
                            // Should we just create the subscription_auth in this case?
                        }
                        break;
                    case GloebitAPI.TransactionFailure.SUBSCRIPTION_AUTH_DECLINED:            /* User has declined the authorization for this subscription */
                        m_log.InfoFormat("[GLOEBITMONEYMODULE].AlertTransactionFailed subscription authorization declined.  Asking payer to auth again.  transactionID:{0}, app-subscription-id:{1} PayerID:{2} PayerName:{3}", txn.TransactionID, txn.SubscriptionID, txn.PayerID, txn.PayerName);

                        // TODO: We should really send another dialog here like the PendingDialog instead of just a url here.
                        // Send dialog asking user to auth or report --- needs different message.
                        string subscriptionAuthIDStr = String.Empty;
                        // pull from extraData because not in transaction
                        if (extraData.ContainsKey("subscription-authorization-id")) {
                            subscriptionAuthIDStr = extraData["subscription-authorization-id"];
                        } else {
                            m_log.Error("[GLOEBITMONEYMODULE].AlertTransactionFailed subscription-authorization-id expected, but missing from extraData");
                        }
                        GloebitSubscription sub = GloebitSubscription.GetBySubscriptionID(txn.SubscriptionID.ToString(), m_apiW.m_url.ToString());
                        m_apiW.AuthorizeSubscription(txn.PayerID, subscriptionAuthIDStr, sub, true);
                        break;
                }
            }
        }

        #endregion // GMM Transaction Alerts

        #endregion // GMM Transaction

        #region GMM IUriLoader Interface

        /******************************************/
        /********** IUriLoader Interface **********/
        /******************************************/

        /* Functions called when the GAPI needs to load a URI for the user
        * These allow the app/platform to determine how this is done
        */

        public void LoadAuthorizeUriForUser(GloebitUser user, Uri authorizeUri)
        {
            // Since we can't launch a website in OpenSim, we have to send the URL via an IM
            IClientAPI client = LocateClientObject(UUID.Parse(user.PrincipalID));
            string title = "AUTHORIZE GLOEBIT";
            string body = "To use Gloebit currency, please authorize Gloebit to link to your avatar's account on this web page:";
            SendUrlToClient(client, title, body, authorizeUri);
        }

        /// <summary>
        /// Deliver the URI for authorizing a subscription to the user.
        /// This specifically sends a message with a clickable URL to the client.
        /// </summary>
        /// <param name="user">GloebitUser we are sending the URL to</param>
        /// <param name="subAuthID">ID of the authorization request the user will be asked to approve - provided by Gloebit.</param>
        /// <param name="sub">GloebitSubscription which contains necessary details for message to user.</param>
        /// <param name="isDeclined">Bool is true if this sub auth has already been declined by the user which should present different messaging.</param>
        public void LoadSubscriptionAuthorizationUriForUser(GloebitUser user, Uri subAuthUri, GloebitSubscription sub, bool isDeclined)
        {
            // Since we can't launch a website in OpenSim, we have to send the URL via an IM
            IClientAPI client = LocateClientObject(UUID.Parse(user.PrincipalID));
            // TODO: adjust our wording
            string title = "GLOEBIT Subscription Authorization Request (scripted object auto-debit):";
            string body;
            if (!isDeclined) {
                body = String.Format("To approve or decline the request to authorize this object:\n   {0}\n   {1}\n\nPlease visit this web page:", sub.ObjectName, sub.ObjectID);
            } else {
                body = String.Format("You've already declined the request to authorize this object:\n   {0}\n   {1}\n\nIf you would like to review the request, or alter your response, please visit this web page:", sub.ObjectName, sub.ObjectID);
            }
            SendUrlToClient(client, title, body, subAuthUri);
        }

        #endregion // GMM IUriLoader Interface

        #region GMM IPlatformAccessor Interface
        
        /*********************************************************/
        /************* IPlatformAccessor Interface ***************/
        /*********************************************************/

        // <summary>
        // Helper property for retrieving the base URI for HTTP callbacks from Gloebit Service back into GMM
        // Used throughout the GMM to provide the callback URI to Gloebit or provide URLs to the user.
        // The callbacks registered below should be available at this base URI.
        //</summary>
        private Uri BaseURI {
            get {
                if(m_overrideBaseURI != null) {
                    // Overriding default behavior to hardcode callback base uri
                    // Generally used for testing
                    return m_overrideBaseURI;
                } else {
                    return new Uri(GetAnyScene().RegionInfo.ServerURI);
                }
            }
        }

        public Uri GetBaseURI() {
            return BaseURI;
        }

        public string resolveAgentName(UUID agentID)
        {
            string avatarname = String.Empty;
            Scene scene = GetAnyScene();

            // Try using IUserManagement module which works for both local users and hypergrid visitors
            IUserManagement umModule = scene.RequestModuleInterface<IUserManagement>();
            if (umModule != null) {
                avatarname = umModule.GetUserName(agentID);
            }

            // If above didn't work, try old method which doesn't work for hypergrid visitors
            if (String.IsNullOrEmpty(avatarname)) {
                UserAccount account = scene.UserAccountService.GetUserAccount(scene.RegionInfo.ScopeID, agentID);
                if (account != null)
                {
                    avatarname = account.FirstName + " " + account.LastName;
                } else {
                    // both methods failed.  Log error.
                    m_log.ErrorFormat("[GLOEBITMONEYMODULE]: Could not resolve name for user {0}", agentID);
                }
            }

            return avatarname;
        }

        public string resolveAgentEmail(UUID agentID)
        {
            // try avatar username surname
            Scene scene = GetAnyScene();
            UserAccount account = scene.UserAccountService.GetUserAccount(scene.RegionInfo.ScopeID, agentID);
            if (account != null)
            {
                return account.Email;
            }
            else
            {
                m_log.ErrorFormat("[GLOEBITMONEYMODULE]: Could not resolve user {0}", agentID);
            }

            return String.Empty;
        }
        
        #endregion // GMM IPlatformAccessor Interface

        #region GMM IUserAlert Interface

        /******************************************/
        /********** IUserAlert Interface **********/
        /******************************************/

        // This is the point where a user is actually authorized/linked
        public void AlertUserAuthorized(GloebitUser user, UUID agentID, double balance, OSDMap extraData)
        {
            // If we have a logged in client for this user, send a balance update and purchase uri
            IClientAPI client = LocateClientObject(agentID);
            if (client != null) {   // If client is null, user is logged out or no longer on a Gloebit enabled region
                client.SendMoneyBalance (UUID.Zero, true, new byte[0], (int)balance, 0, UUID.Zero, false, UUID.Zero, false, 0, String.Empty);

                // Deliver Purchase URI in case the helper-uri is not working
                Uri url = m_apiW.BuildPurchaseURI (BaseURI, user);
                SendUrlToClient (client, "Gloebit Authorization Successful", "Buy gloebits you can spend on this grid:", url);
            }
        }

        #endregion // GMM IUserAlert Interface
        
        #region GMM ISubscriptionAlert Interface
        
        /******************************************/
        /****** ISubscriptionAlert Interfaces *****/
        /******************************************/
        
        public void AlertSubscriptionCreated(GloebitSubscription subscription)
        {
            m_log.InfoFormat ("[GLOEBITMONEYMODULE].AlertSubscriptionCreated appSubID:{0} GloebitSubID:{1}", subscription.ObjectID, subscription.SubscriptionID);
            // TODO: Do we need to message any client?
                
            // OpenSim needs to handle this specially because we are using subscriptions for auto-debit and therefore creating
            // on the fly and owner is waiting for auth request.  Triggered when auto-debit perms were granted or when auto-debit txn failed.
            // Ask user to auth.  Do not try to restart stalled txn.
                
            // Look at authWaitingForSubMap - if auth waiting, start that flow.
            IClientAPI client = null;
            bool foundClient = false;
            lock (m_authWaitingForSubMap) {
                foundClient = m_authWaitingForSubMap.TryGetValue (subscription.ObjectID, out client);
                if (foundClient) {
                    m_authWaitingForSubMap.Remove (subscription.ObjectID);
                }
            }
            // TODO: Not sending dialog.  Just creating auth.  Should we separate flow from transaction failure and deliver dialog?
            if (foundClient) {
                m_apiW.AuthorizeSubscription(client.AgentId, String.Empty, subscription, false);
            }
        }

        public void AlertSubscriptionCreationFailed(GloebitSubscription subscription)
        {
            m_log.InfoFormat ("[GLOEBITMONEYMODULE].AlertSubscriptionCreationFailed appSubID:{0}", subscription.ObjectID);
            // If we added to this map.  remove so we're not leaking memory in failure cases.
            lock(m_authWaitingForSubMap) {
                m_authWaitingForSubMap.Remove(subscription.ObjectID);
            }
        }
        
        #endregion // GMM ISubscriptionAlert Interface

        #region Utility Helpers

        /// <summary>
        /// Locates a IClientAPI for the client specified
        /// </summary>
        /// <param name="AgentID"></param>
        /// <returns></returns>
        private IClientAPI LocateClientObject(UUID AgentID)
        {
            ScenePresence tPresence = null;
            IClientAPI rclient = null;

            lock (m_scenel)
            {
                foreach (Scene _scene in m_scenel.Values)
                {
                    tPresence = _scene.GetScenePresence(AgentID);
                    if (tPresence != null)
                    {
                        if (!tPresence.IsChildAgent)
                        {
                            rclient = tPresence.ControllingClient;
                        }
                    }
                    if (rclient != null)
                    {
                        return rclient;
                    }
                }
            }
            return null;
        }

        private Scene LocateSceneClientIn(UUID AgentId)
        {
            lock (m_scenel)
            {
                foreach (Scene _scene in m_scenel.Values)
                {
                    ScenePresence tPresence = _scene.GetScenePresence(AgentId);
                    if (tPresence != null)
                    {
                        if (!tPresence.IsChildAgent)
                        {
                            return _scene;
                        }
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Utility function Gets an arbitrary scene in the instance.  For when which scene exactly you're doing something with doesn't matter
        /// </summary>
        /// <returns></returns>
        private Scene GetAnyScene()
        {
            lock (m_scenel)
            {
                foreach (Scene rs in m_scenel.Values)
                    return rs;
            }
            return null;
        }

        /// <summary>
        /// Utility function to get a Scene by RegionID in a module
        /// </summary>
        /// <param name="RegionID"></param>
        /// <returns></returns>
        private Scene GetSceneByUUID(UUID RegionID)
        {
            lock (m_scenel)
            {
                foreach (Scene rs in m_scenel.Values)
                {
                    if (rs.RegionInfo.originRegionID == RegionID)
                    {
                        return rs;
                    }
                }
            }
            return null;
        }
        
        /// <summary>
        /// Utility function to get a Scene by Region Name in a module
        /// Recommend retrieving by UUID if possible.  Not idea, but
        /// necessary because MoveMoney doesn't supply the Scene or RegionID
        /// </summary>
        /// <param name="RegionName"></param>
        /// <returns></returns>
        private Scene GetSceneByName(string regionName)
        {
            lock (m_scenel)
            {
                foreach (Scene rs in m_scenel.Values)
                {
                    if (rs.RegionInfo.RegionName == regionName)
                    {
                        return rs;
                    }
                }
            }
            return null;
        }

        private SceneObjectPart findPrim(UUID objectID)
        {
            lock (m_scenel)
            {
                foreach (Scene s in m_scenel.Values)
                {
                    SceneObjectPart part = s.GetSceneObjectPart(objectID);
                    if (part != null)
                    {
                        return part;
                    }
                }
            }
            return null;
        }

        private string resolveObjectName(UUID objectID)
        {
            SceneObjectPart part = findPrim(objectID);
            if (part != null)
            {
                return part.Name;
            }
            return String.Empty;
        }

        // Possible that this is automatic as firstname=FN.LN and lastname=@home_uri
        // If not set through account service, than may change resolveAgentName to use umModule.GetUserName(UUID) which should work.
        // Or, alternatively, for better tracking of unique avatars across all OpenSim Hypergrid, we could try to discover
        // the home_uri when user is on home grid, and turn name into same format as a foreign user.
        // TODO: consider adding user's homeURL to tracked data on Gloebit --- might help spot grids with user accounts we should blacklist.
/*      private string resolveAgentNameAtHome(UUID agentID)
        {
            // try avatar username surname
            Scene scene = GetAnyScene();
            UserAccount account = scene.UserAccountService.GetUserAccount(scene.RegionInfo.ScopeID, agentID);
            IUserManagement umModule = scene.RequestModuleInterface<IUserManagement>();
            
            m_log.InfoFormat("[GLOEBITMONEYMODULE]: resolveAgentNameAtHome\n GetUserName:{0} \nGetUserHomeURL:{1} \nGetUserUUI:{2}",
                              umModule.GetUserName(agentID), umModule.GetUserHomeURL(agentID), umModule.GetUserUUI(agentID));

            
            if (account != null && umModule != null)
            {
                string avatarname = account.FirstName + " " + account.LastName + " @" + umModule.GetUserHomeURL(agentID);
                return avatarname;
            }
            else
            {
                m_log.ErrorFormat(
                                  "[GLOEBITMONEYMODULE]: Could not resolve user {0}",
                                  agentID);
            }
            return String.Empty;
        }
*/

        #endregion

        #region Event Handlers

        #region User Management Event Handlers

        /// <summary>
        /// Scene.EventManager.OnClientLogin event handler
        /// </summary>
        /// <param name="client">Client.</param>
        private void OnClientLogin(IClientAPI client)
        {
            m_log.DebugFormat("[GLOEBITMONEYMODULE] OnClientLogin for {0}", client.AgentId);

            // Bit of a hack
            // OnCompleteMovementToRegion requests balance and asks for auth if not authed
            // -- This is required to cover teleports and crossing into new regions from non GMM region.
            // But, If this was due to a login, the viewer also requests the balance which triggers the same auth or purchase messaging.
            // -- Unfortunately, the event at login from the viewer is the same as when a user manually clicks on their balance.
            // Two auths look bad.
            // So, we tell our balance request to ignore the one right after login from the viewer.
            // We set a timestamp in case any viewers have removed this request, so that this ignore flag expires within a few seconds.
            LoginBalanceRequest lbr = LoginBalanceRequest.Get(client.AgentId);
            lbr.IgnoreNextBalanceRequest = true;
        }

        /// <summary>
        /// Scene.EventManager.OnNewClientNew event handler
        /// Registers necessary client events
        /// </summary>
        /// <param name="client"></param>
        private void OnNewClient(IClientAPI client)
        {
            m_log.DebugFormat("[GLOEBITMONEYMODULE] OnNewClient for {0}", client.AgentId);

            // Subscribe to money related messages
            client.OnEconomyDataRequest += EconomyDataRequestHandler;           // Handles sending OpenSim economy data
            client.OnMoneyBalanceRequest += MoneyBalanceRequest;                // Handles updating balance and sending to client
            client.OnRequestPayPrice += requestPayPrice;                        // Handles OpenSim price request
            client.OnObjectBuy += ObjectBuy;                                    // Handles 5000 (Object Buy) event
            client.OnScriptAnswer += handleScriptAnswer;                        // Handle response of granting auto-debit permissions

            if (!m_newLandPassFlow) {
                client.OnParcelBuyPass += ParcelBuyPass;                        // Handle purchase of timed access to parcel
            }

            // Subscribe to other events
            client.OnLogout += ClientLoggedOut;                                 // Handles cleanup
            client.OnCompleteMovementToRegion += OnCompleteMovementToRegion;    // Handles balance update and new session messaging
        }

        /// <summary>
        /// client.OnCompleteMovementToRegion event handler
        /// Event triggered when agent enters new region.
        /// Handles updating of information necessary when a user has arrived at a new region, sim, or grid.
        /// Requests balance from Gloebit if authed and delivers to viewer.
        /// If this is a new session, if not authed, requests auth.  If authed, sends purchase url.
        /// </summary>
        private void OnCompleteMovementToRegion(IClientAPI client, bool blah) {
            m_log.DebugFormat("[GLOEBITMONEYMODULE] OnCompleteMovementToRegion for {0} with bool {1}", client.AgentId, blah);

            System.Threading.ThreadPool.QueueUserWorkItem(delegate 
            {
                m_log.DebugFormat("[GLOEBITMONEYMODULE] OnCompleteMovementToRegion AgentID:{0} SessionId:{1} SecureSessionId:{2}", client.AgentId, client.SessionId, client.SecureSessionId);

                GloebitUser user = GloebitUser.Get(m_key, client.AgentId);
                // If authed, update balance immediately
                if (user.IsAuthed()) {
                    // TODO: may now be able to remove client from UpdateBalance as we moved this call here and out of OnNewClient
                    // Don't send Buy Gloebits messaging so that we don't spam --- last arg is 0
                    UpdateBalance(client.AgentId, client, 0);
                } else {
                    //update viewer balance to zero in case user came from alt money module region and has old viewer
                    //Note: New Firestorm viewer will request update and will get double send here.
                    int zeroBal = 0;
                    client.SendMoneyBalance(UUID.Zero, true, new byte[0], zeroBal, 0, UUID.Zero, false, UUID.Zero, false, 0, String.Empty);
                }
                if (user.IsNewSession(client.SessionId)) {
                    // Send welcome messaging
                    SendNewSessionMessaging(client, user);
                }
            }, null);
        }

        /// <summary>
        /// client.OnLogout event handler
        /// When the client logs out, we remove their info from
        /// any local memory stores to free up resources.
        /// </summary>
        /// <param name="client">The client logging out</param>
        private void ClientLoggedOut(IClientAPI client)
        {
            // TODO: Is cleanup ok in here, or should it be triggered in the ClientClosed event?

            // Deregister OnChatFromClient if we have one.
            Dialog.DeregisterAgent(client);

            // Remove from s_LoginBalanceRequestMap
            LoginBalanceRequest.Cleanup(client.AgentId);

            // Remove from s_userMap
            GloebitUser.Cleanup(client.AgentId);

            m_log.DebugFormat("[GLOEBITMONEYMODULE] ClientLoggedOut {0}", client.AgentId);
        }

        #endregion // User Management Event Handlers

        #region Commerce Event Handlers

        /// <summary>
        /// client.OnEconomyDataRequest event handler
        /// Legacy event which is still triggered when a new client connects and is expected
        /// to deliver some economy information about the grid
        /// </summary>
        /// <param name="client"></param>
        private void EconomyDataRequestHandler(IClientAPI client)
        {
            m_log.DebugFormat("[GLOEBITMONEYMODULE] EconomyDataRequestHandler {0}", client.AgentId);
            Scene s = (Scene)client.Scene;

            client.SendEconomyData(EnergyEfficiency, s.RegionInfo.ObjectCapacity, ObjectCount, PriceEnergyUnit, PriceGroupCreate,
                PriceObjectClaim, PriceObjectRent, PriceObjectScaleFactor, PriceParcelClaim, PriceParcelClaimFactor,
                PriceParcelRent, PricePublicObjectDecay, PricePublicObjectDelete, PriceRentLight, PriceUpload,
                TeleportMinPrice, TeleportPriceExponent);
        }

        /// <summary>
        /// client.OnMoneyBalanceRequest event handler
        /// Requests the agent's balance from Gloebit and sends it to the client
        /// NOTE:
        /// --- This is triggered by the OnMoneyBalanceRequest event
        /// ------ This appears to get called at login and when a user clicks on his/her balance.  The TransactionID is zero in both cases.
        /// ------ This may get called in other situations, but buying an object does not seem to trigger it.
        /// ------ It appears that The system which calls ApplyUploadCharge calls immediately after (still with TransactionID of Zero).
        /// </summary>
        /// <param name="client"></param>
        /// <param name="agentID"></param>
        /// <param name="SessionID"></param>
        /// <param name="TransactionID"></param>
        private void MoneyBalanceRequest(IClientAPI client, UUID agentID, UUID SessionID, UUID TransactionID)
        {
            m_log.DebugFormat("[GLOEBITMONEYMODULE] SendMoneyBalance request from {0} about {1} for transaction {2}", client.AgentId, agentID, TransactionID);

            if (client.AgentId == agentID && client.SessionId == SessionID)
            {
                // HACK to ignore request when this is just after we delivered the balance at login
                LoginBalanceRequest lbr = LoginBalanceRequest.Get(client.AgentId);
                if (lbr.IgnoreNextBalanceRequest) {
                    lbr.IgnoreNextBalanceRequest = false;
                    return;
                }

                // Request balance from Gloebit.  Request Auth if not authed.  If Authed, always deliver Gloebit purchase url.
                // NOTE: we are not passing the TransactionID to SendMoneyBalance as it appears to always be UUID.Zero.
                UpdateBalance(agentID, client, -1);
            }
            else
            {
                m_log.WarnFormat("[GLOEBITMONEYMODULE] SendMoneyBalance - Unable to send money balance");
                client.SendAlertMessage("Unable to send your money balance to you!");
            }
        }

        /// <summary>
        /// client.OnRequestPayPrice event handler
        /// </summary>
        /// <param name="client">Client.</param>
        /// <param name="objectID">Object I.</param>
        private void requestPayPrice(IClientAPI client, UUID objectID)
        {
            m_log.DebugFormat("[GLOEBITMONEYMODULE] requestPayPrice");
            Scene scene = LocateSceneClientIn(client.AgentId);
            if (scene == null)
                return;

            SceneObjectPart task = scene.GetSceneObjectPart(objectID);
            if (task == null)
                return;
            SceneObjectGroup group = task.ParentGroup;
            SceneObjectPart root = group.RootPart;

            client.SendPayPrice(objectID, root.PayPrice);
        }

        /// <summary>
        /// Scene.EventManager.OnMoneyTransfer event handler
        /// This method gets called when someone chooses to pay another user or an object directly.
        /// </summary>
        /// <param name="osender">Scene that triggered this event</param>
        /// <param name="e">EventManager.MoneyTransferArgs defining the payment</param>
        private void OnMoneyTransfer(Object osender, EventManager.MoneyTransferArgs e)
        {
            m_log.DebugFormat("[GLOEBITMONEYMODULE] OnMoneyTransfer sender {0} receiver {1} amount {2} transactiontype {3} description '{4}'", e.sender, e.receiver, e.amount, e.transactiontype, e.description);

            Scene s = (Scene) osender;
            string regionname = s.RegionInfo.RegionName;
            string regionID = s.RegionInfo.RegionID.ToString();

            // Decalare variables to be assigned in switch below
            UUID fromID = UUID.Zero;
            UUID toID = UUID.Zero;
            UUID partID = UUID.Zero;
            string partName = String.Empty;
            string partDescription = String.Empty;
            OSDMap descMap = null;
            SceneObjectPart part = null;
            string description;

            // TODO: figure out how to get agent locations and add them to descMaps below

            /****** Fill in fields dependent upon transaction type ******/
            switch((TransactionType)e.transactiontype) {
            case TransactionType.USER_PAYS_USER:
                // 5001 - OnMoneyTransfer - Pay User
                fromID = e.sender;
                toID = e.receiver;
                descMap = buildOpenSimTransactionDescMap(regionname, regionID, "PayUser");
                if (String.IsNullOrEmpty(e.description)) {
                    description = "PayUser: <no description provided>";
                } else {
                    description = String.Format("PayUser: {0}", e.description);
                }
                break;
            case TransactionType.USER_PAYS_OBJECT:
                // 5008 - OnMoneyTransfer - Pay Object
                partID = e.receiver;
                part = s.GetSceneObjectPart(partID);
                // TODO: Do we need to verify that part is not null?  can it ever by here?
                partName = part.Name;
                partDescription = part.Description;
                fromID = e.sender;
                toID = part.OwnerID;
                descMap = buildOpenSimTransactionDescMap(regionname, regionID, "PayObject", part);
                description = e.description;
                break;
            case TransactionType.OBJECT_PAYS_USER:
                // 5009 - ObjectGiveMoney
                m_log.ErrorFormat("******* OBJECT_PAYS_USER received in OnMoneyTransfer - Unimplemented transactiontype: {0}", e.transactiontype);

                // TransactionType 5009 is handled by ObjectGiveMoney and should never trigger a call to OnMoneyTransfer
                /*
                    partID = e.sender;
                    part = s.GetSceneObjectPart(partID);
                    partName = part.Name;
                    partDescription = part.Description;
                    fromID = part.OwnerID;
                    toID = e.receiver;
                    descMap = buildOpenSimTransactionDescMap(regionname, regionID, "ObjectPaysUser", part);
                    description = e.description;
                    */
                return;
            default:
                m_log.ErrorFormat("UNKNOWN Unimplemented transactiontype received in OnMoneyTransfer: {0}", e.transactiontype);
                return;
            }

            /******** Set up necessary parts for Gloebit transact-u2u **********/

            GloebitTransaction txn = buildTransaction(transactionID: UUID.Zero, transactionType: (TransactionType)e.transactiontype,
                payerID: fromID, payeeID: toID, amount: e.amount, subscriptionID: UUID.Zero,
                partID: partID, partName: partName, partDescription: partDescription,
                categoryID: UUID.Zero, localID: 0, saleType: 0);

            if (txn == null) {
                // build failed, likely due to a reused transactionID.  Shouldn't happen.
                IClientAPI payerClient = LocateClientObject(fromID);
                alertUsersTransactionPreparationFailure((TransactionType)e.transactiontype, TransactionPrecheckFailure.EXISTING_TRANSACTION_ID, payerClient);
                return;
            }

            bool transaction_result = m_apiW.SubmitTransaction(txn, description, descMap, true);

            // TODO - do we need to send any error message to the user if things failed above?`
        }

        /// <summary>
        /// Client.OnObjectBuy event handler
        /// event triggered when user clicks on buy for an object which is for sale
        /// </summary>
        private void ObjectBuy(IClientAPI remoteClient, UUID agentID,
            UUID sessionID, UUID groupID, UUID categoryID,
            uint localID, byte saleType, int salePrice)
        {
            m_log.DebugFormat("[GLOEBITMONEYMODULE] ObjectBuy client:{0}, agentID: {1}", remoteClient.AgentId, agentID);

            if (!m_sellEnabled)
            {
                alertUsersTransactionPreparationFailure(TransactionType.USER_BUYS_OBJECT, TransactionPrecheckFailure.BUYING_DISABLED, remoteClient);
                return;
            }

            Scene s = LocateSceneClientIn(remoteClient.AgentId);

            // The sale information in this event comes from the client, not the server, so we must validate that the
            // data the client sent matches the server.  If not, the data could be out of sync since a recent change
            // or it could be a malicious client, or something was corrupted.  The cause doesn't matter, but we should
            // not proceed if the data doesn't match and we should alert the user.

            // Validate that the object exists in the scene the user is in
            SceneObjectPart part = s.GetSceneObjectPart(localID);
            if (part == null)
            {
                alertUsersTransactionPreparationFailure(TransactionType.USER_BUYS_OBJECT, TransactionPrecheckFailure.OBJECT_NOT_FOUND, remoteClient);
                return;
            }

            // Validate that the client sent the price that the object is being sold for 
            if (part.SalePrice != salePrice)
            {
                alertUsersTransactionPreparationFailure(TransactionType.USER_BUYS_OBJECT, TransactionPrecheckFailure.AMOUNT_MISMATCH, remoteClient);
                return;
            }

            // Validate that is the client sent the proper sale type the object has set
            if (saleType < 1 || saleType > 3) {
                // Should not get here unless an object purchase is submitted with a bad or new (but unimplemented) saleType.
                m_log.ErrorFormat("[GLOEBITMONEYMODULE] ObjectBuy Unrecognized saleType:{0} --- expected 1,2 or 3 for original, copy, or contents", saleType);
                alertUsersTransactionPreparationFailure(TransactionType.USER_BUYS_OBJECT, TransactionPrecheckFailure.SALE_TYPE_INVALID, remoteClient);
                return;
            }
            if (part.ObjectSaleType != saleType) {
                alertUsersTransactionPreparationFailure(TransactionType.USER_BUYS_OBJECT, TransactionPrecheckFailure.SALE_TYPE_MISMATCH, remoteClient);
                return;
            }

            // Check that the IBuySellModule is accessible before submitting the transaction to Gloebit
            IBuySellModule module = s.RequestModuleInterface<IBuySellModule>();
            if (module == null) {
                m_log.ErrorFormat("[GLOEBITMONEYMODULE] ObjectBuy FAILED to access to IBuySellModule");
                alertUsersTransactionPreparationFailure(TransactionType.USER_BUYS_OBJECT, TransactionPrecheckFailure.BUY_SELL_MODULE_INACCESSIBLE, remoteClient);
                return;
            }

            // If 0G$ txn, don't build and submit txn
            if (salePrice == 0) {
                // Nothing to submit to Gloebit.  Just deliver the object
                bool delivered = module.BuyObject(remoteClient, categoryID, localID, saleType, salePrice);
                // Inform the user of success or failure.
                if (!delivered) {
                    m_log.ErrorFormat("[GLOEBITMONEYMODULE] ObjectBuy delivery of free object failed.");
                    // returnMsg = "IBuySellModule.BuyObject failed delivery attempt.";
                    sendMessageToClient(remoteClient, String.Format("Delivery of free object failed\nObject Name: {0}", part.Name), agentID);
                } else {
                    m_log.DebugFormat("[GLOEBITMONEYMODULE] ObjectBuy delivery of free object succeeded.");
                    // returnMsg = "object delivery succeeded";
                    sendMessageToClient(remoteClient, String.Format("Delivery of free object succeeded\nObject Name: {0}", part.Name), agentID);
                }
                return;
            }

            string agentName = resolveAgentName(agentID);
            string regionname = s.RegionInfo.RegionName;
            string regionID = s.RegionInfo.RegionID.ToString();

            // string description = String.Format("{0} bought object {1}({2}) on {3}({4})@{5}", agentName, part.Name, part.UUID, regionname, regionID, m_gridnick);
            string description = String.Format("{0} object purchased on {1}, {2}", part.Name, regionname, m_gridnick);

            OSDMap descMap = buildOpenSimTransactionDescMap(regionname, regionID.ToString(), "ObjectBuy", part);

            GloebitTransaction txn = buildTransaction(transactionID: UUID.Zero, transactionType: TransactionType.USER_BUYS_OBJECT,
                payerID: agentID, payeeID: part.OwnerID, amount: salePrice, subscriptionID: UUID.Zero,
                partID: part.UUID, partName: part.Name, partDescription: part.Description,
                categoryID: categoryID, localID: localID, saleType: saleType);

            if (txn == null) {
                // build failed, likely due to a reused transactionID.  Shouldn't happen.
                alertUsersTransactionPreparationFailure(TransactionType.USER_BUYS_OBJECT, TransactionPrecheckFailure.EXISTING_TRANSACTION_ID, remoteClient);
                return;
            }

            bool transaction_result = m_apiW.SubmitTransaction(txn, description, descMap, true);

            m_log.InfoFormat("[GLOEBITMONEYMODULE] ObjectBuy Transaction queued {0}", txn.TransactionID.ToString());
        }

        private bool deliverObject(GloebitTransaction txn, out string returnMsg) {
            // TODO: this could fail if user logs off right after submission.  Is this what we want?
            // TODO: This basically always fails when you crash opensim and recover during a transaction.  Is this what we want?
            IClientAPI buyerClient = LocateClientObject(txn.PayerID);
            if (buyerClient == null) {
                m_log.ErrorFormat("[GLOEBITMONEYMODULE].deliverObject FAILED to locate buyer agent.  Agent may have logged out prior to delivery.");
                returnMsg = "Can't locate buyer.";
                return false;
            }

            // Retrieve BuySellModule used for delivering this asset
            Scene s = LocateSceneClientIn(buyerClient.AgentId);
            // TODO: we should be locating the scene the part is in instead of the agent in case the agent moved (to a non Gloebit region) -- maybe store scene ID in asset -- see processLandBuy?
            IBuySellModule module = s.RequestModuleInterface<IBuySellModule>();
            if (module == null) {
                m_log.ErrorFormat("[GLOEBITMONEYMODULE].deliverObject FAILED to access to IBuySellModule");
                returnMsg = "Can't access IBuySellModule.";
                return false;
            }

            // Rebuild delivery params from Asset and attempt delivery of object
            uint localID;
            if (!txn.TryGetLocalID(out localID)) {
                SceneObjectPart part;
                if (s.TryGetSceneObjectPart(txn.PartID, out part)) {
                    localID = part.LocalId;
                } else {
                    m_log.ErrorFormat("[GLOEBITMONEYMODULE].deliverObject FAILED to deliver asset - could not retrieve SceneObjectPart from ID");
                    returnMsg = "Failed to deliver asset.  Could not retrieve SceneObjectPart from ID.";
                    return false;
                }
            }
            bool success = module.BuyObject(buyerClient, txn.CategoryID, localID, (byte)txn.SaleType, txn.Amount);
            if (!success) {
                m_log.ErrorFormat("[GLOEBITMONEYMODULE].deliverObject FAILED to deliver asset");
                returnMsg = "IBuySellModule.BuyObject failed delivery attempt.";
            } else {
                m_log.InfoFormat("[GLOEBITMONEYMODULE].deliverObject SUCCESS - delivered asset");
                returnMsg = "object delivery succeeded";
            }
            return success;
        }

        #region ParcelBuyPass pre-0.9 Flow
        // This code is only used if m_newLandPassFlow is false.
        // When true, see MoveMoney() for TransactionType::USER_BUYS_LANDPASS

        /// <summary>
        /// client.OnParcelBuyPass event handler
        /// Event is triggered when a user tries to buy a time limited access pass to a parcel.
        /// Should only be handled directly before v 0.9.0
        /// v 0.9.0+ of OpenSim consumes this event in core and calls MoveMoney
        /// </summary>
        /// <param name="client">Client.</param>
        /// <param name="agentID">UUID of the agent purchasing the pass</param>
        /// <param name="ParcelLocalID">int local ID of the parcel for which the agent is purchasing a pass</param>
        private void ParcelBuyPass(IClientAPI client, UUID agentID, int ParcelLocalID) {
            // This function is only registered if we are in the old LandPassFlow.  See m_newLandPassFlow

            m_log.DebugFormat("[GLOEBITMONEYMODULE] ParcelBuyPass event {0} {1}", agentID, ParcelLocalID);

            if (client == null) {
                m_log.Warn("[GLOEBITMONEYMODULE] ParcelBuyPass event with null client.  Returning.");
            }
            Scene s = (Scene)client.Scene;
            ILandObject parcel = s.LandChannel.GetLandObject(ParcelLocalID);

            // Some basic checks
            if (parcel == null) {
                m_log.Warn("[GLOEBITMONEYMODULE] ParcelBuyPass event with null parcel.  Returning.");
                alertUsersTransactionPreparationFailure(TransactionType.USER_BUYS_LANDPASS, TransactionPrecheckFailure.OBJECT_NOT_FOUND, client);
                return;
            }
            // Make sure parcel is set to sell passes
            if ((parcel.LandData.Flags & (uint)ParcelFlags.UsePassList) == 0) {
                m_log.WarnFormat("[GLOEBITMONEYMODULE] AgentID:{0} attempted to buy a pass on parcel:{1} which is not set to sell passes", agentID, parcel.LandData.GlobalID);
                alertUsersTransactionPreparationFailure(TransactionType.USER_BUYS_LANDPASS, TransactionPrecheckFailure.SALE_TYPE_INVALID, client);
                // TODO: Message user
                return;
            }
            // Holding off on this check as the parcel owner can functionally be added to the access list.  Maybe there is a reason an owner would want to do this
            // If owner, don't charge
            //if ((parcel.LandData.OwnerID == agentID)) {
            // TODO: Message user
            //    return;
            //}
            // We can't handle group transactions, so fail that
            if(parcel.LandData.IsGroupOwned)
            {
                m_log.WarnFormat("[GLOEBITMONEYMODULE] AgentID:{0} attempted to buy a pass on parcel:{1} which is group owned.  Group Transactions are not defined.", agentID, parcel.LandData.GlobalID);
                alertUsersTransactionPreparationFailure(TransactionType.USER_BUYS_LANDPASS, TransactionPrecheckFailure.GROUP_OWNED, client);
                return;
            }

            int price = parcel.LandData.PassPrice;
            float hours = parcel.LandData.PassHours;
            string parcelName = parcel.LandData.Name;

            // If 0G$ txn, don't build and submit txn
            if (price == 0) {
                // Nothing to submit to Gloebit.  Just deliver the object
                string returnMsg = "";
                bool timeAdded = addPassTimeToParcelAccessList(s, parcel, agentID, out returnMsg);
                // Inform the user of success or failure.
                if (!timeAdded) {
                    m_log.ErrorFormat("[GLOEBITMONEYMODULE] ParcelBuyPass delivery of free land pass failed; reason:{0}.", returnMsg);
                    sendMessageToClient(client, String.Format("Delivery of free land pass failed\nParcel Name: {0}\nReason: {1}", parcelName, returnMsg), agentID);
                } else {
                    m_log.DebugFormat("[GLOEBITMONEYMODULE] ParcelBuyPass delivery of free land pass succeeded.");
                    sendMessageToClient(client, String.Format("Delivery of free land pass succeeded\nParcel Name: {0}", parcelName), agentID);
                }
                return;
            }

            // Build Transaction
            string agentName = resolveAgentName(agentID);
            string regionname = s.RegionInfo.RegionName;
            UUID regionID = s.RegionInfo.RegionID;

            string description = String.Format("{0} hour LandPass purchased for parcel {1} on {2}, {3}", hours, parcelName, regionname, m_gridnick);

            OSDMap descMap = buildOpenSimTransactionDescMap(regionname, regionID.ToString(), "LandPassBuy", parcel.LandData);

            GloebitTransaction txn = buildTransaction(transactionID: UUID.Zero, transactionType: TransactionType.USER_BUYS_LANDPASS,
                payerID: agentID, payeeID: parcel.LandData.OwnerID, amount: price, subscriptionID: UUID.Zero,
                partID: parcel.LandData.GlobalID, partName: parcel.LandData.Name, partDescription: parcel.LandData.Description,
                categoryID: regionID, localID: (uint)parcel.LandData.LocalID, saleType: 0);
            // NOTE: using category & localID to retrieve parcel on callback in case GlobalID doesn't work.
            // Should consider storing all LandData in assetMap to ensure PassHours hasn't changed.

            if (txn == null) {
                // build failed, likely due to a reused transactionID.  Shouldn't happen.
                alertUsersTransactionPreparationFailure(TransactionType.USER_BUYS_LANDPASS, TransactionPrecheckFailure.EXISTING_TRANSACTION_ID, client);
                return;
            }

            bool transaction_result = m_apiW.SubmitTransaction(txn, description, descMap, true);

            m_log.InfoFormat("[GLOEBITMONEYMODULE] ParcelBuyPass Transaction queued {0}", txn.TransactionID.ToString());
        }

        // Called for flow pre-0.9.1 where we deliver asset.  0.9.1 and after, flow does delivery, so this is not called
        // NOTE: have not moved to asset enact region of file because it is not used in current versions.
        private bool deliverLandPass(GloebitTransaction txn, out string returnMsg) {
            m_log.DebugFormat("[GLOEBITMONEYMODULE] deliverLandPass to:{0}", txn.PayerID);
            // Get Parcel back
            uint localID;
            if (!txn.TryGetLocalID(out localID)) {
                m_log.ErrorFormat("[GLOEBITMONEYMODULE].deliverLandPass FAILED to deliver asset - could not retrieve parcel LocalID");
                returnMsg = "Failed to deliver land pass.  Could not retrieve parcel LocalID.";
                return false;
            }
            int parcelLocalID = (int)localID;
            UUID agentID = txn.PayerID;
            UUID regionID = txn.CategoryID;
            Scene s = GetSceneByUUID(regionID);
            if (s == null) {
                // Should probably never happen
                m_log.WarnFormat("[GLOEBITMONEYMODULE].deliverLandPass FAILED because we couldn't retrieve the scene with the parcel.");
                returnMsg = "Could not locate scene with parcel selling land pass.";
                return false;
            }
            ILandObject parcel = s.LandChannel.GetLandObject(parcelLocalID);
            if (parcel == null) {
                // Parcel was deleted perhaps
                m_log.WarnFormat("[GLOEBITMONEYMODULE].deliverLandPass FAILED because we couldn't retrieve the parcel selling the land pass.");
                returnMsg = "Could not locate parcel selling land pass.";
                return false;
            }

            // Make sure nothing vital about the parcel changed
            // Make sure parcel is still set to sell passes
            if ((parcel.LandData.Flags & (uint)ParcelFlags.UsePassList) == 0) {
                m_log.WarnFormat("[GLOEBITMONEYMODULE] AgentID:{0} attempted to buy a pass on parcel:{1} which turned off pass sales before transaciton completed.", agentID, parcel.LandData.GlobalID);
                returnMsg = "Parcel is no longer selling passes.";
                return false;
            }
            // Make sure owner hasn't changed
            if ((parcel.LandData.OwnerID != txn.PayeeID)) {
                m_log.WarnFormat("[GLOEBITMONEYMODULE] AgentID:{0} attempted to buy a pass on parcel:{1} which changed ownership before transaction completed.", agentID, parcel.LandData.GlobalID);
                returnMsg = "Parcel ownership has changed.  Please retry purchase.";
                return false;
            }
            // Make sure price hasn't changed
            if(parcel.LandData.PassPrice != txn.Amount)
            {
                m_log.WarnFormat("[GLOEBITMONEYMODULE] AgentID:{0} attempted to buy a pass on parcel:{1} which changed price before transaction completed.", agentID, parcel.LandData.GlobalID);
                returnMsg = "Price of land pass has changed.  Please retry purchase.";
                return false;
            }
            // TODO: Should really double check PassHours, but we have no float in txn and haven't crated an asset map just for this.
            // TODO: Could put this in salesType since OpenSim pre 0.9.1 had a bug making this an int.

            // Do the real work
            bool timeAdded = addPassTimeToParcelAccessList(s, parcel, agentID, out returnMsg);
            if (!timeAdded) {
                m_log.WarnFormat("[GLOEBITMONEYMODULE] AgentID:{0} attempted to buy a pass on parcel:{1} but adding access time failed with reason:{2}.", agentID, parcel.LandData.GlobalID, returnMsg);
                //returnMsg is set from out parameter of addPassTimeToParcelAccessList
                return false;
            }
            // We're Done.  Success.
            returnMsg = "land pass delivery succeeded";
            return true;
        }

        private bool addPassTimeToParcelAccessList(Scene s, ILandObject parcel, UUID agentID, out string returnMsg) {
            m_log.DebugFormat("[GLOEBITMONEYMODULE] addPassToParcelAccessList agent:{0} for parcel:{1}", agentID, parcel.LandData.GlobalID);

            float hours = parcel.LandData.PassHours;
            int parcelLocalID = parcel.LandData.LocalID;
            string parcelName = parcel.LandData.Name;

            // COPIED from llAddToLandPassList and then modified/improved
            int expires = 0;
            int hoursInSeconds = (int)(3600.0 * hours);
            int nowInSeconds = Util.UnixTimeSinceEpoch();
            if (hours != 0) {
                expires = Util.UnixTimeSinceEpoch() + (int)(3600.0 * hours);
            }
            int idx = parcel.LandData.ParcelAccessList.FindIndex(
                delegate(LandAccessEntry e)
                {
                    if (e.AgentID == agentID && e.Flags == AccessList.Access) {
                        return true;
                    }
                    return false;
                });
            // If there is already a parcel access entry for agent, we may have to change the expiration
            if (idx != -1) {
                int prevExp = parcel.LandData.ParcelAccessList[idx].Expires;
                if (prevExp == 0) {
                    // This user has permanent access without a time limit.  Let them know and don't charge them
                    m_log.WarnFormat("[GLOEBITMONEYMODULE].deliverLandPass FAILED because agent already has permanent access.");
                    returnMsg = "Payer already has a land pass with no expiration for this parcel.";
                    return false;
                } else if (hours == 0) {
                    // User is getting perm access.  Doesn't matter what their current expiration is.  Set to 0
                    expires = 0;
                } else if (nowInSeconds < prevExp) {
                    // User is buying time and has some left on last pass.  Add together
                    // TODO: message user that they had Expires-now and are adding hours for new exp
                    expires = prevExp + hoursInSeconds;
                }
            }

            // Remove existing entry
            if (idx != -1) {
                // TODO: is there a reason we remove this rather than edit it?
                // TODO: Do we need to lock this?  How do these work?  Does TriggerLandObjectUpdated fix this for us?
                parcel.LandData.ParcelAccessList.RemoveAt(idx);
            }

            // Add agent to parcel access list with expiration
            LandAccessEntry entry = new LandAccessEntry();
            entry.AgentID = agentID;
            entry.Flags = AccessList.Access;
            entry.Expires = expires;
            parcel.LandData.ParcelAccessList.Add(entry);
            //s.EventManager.TriggerLandObjectUpdated((uint)ParcelLocalID, parcel);
            s.EventManager.TriggerLandObjectUpdated((uint)parcelLocalID, parcel);

            returnMsg = "Time successfully added to parcel access list entry for agent";
            return true;
        }

        #endregion //ParcelBuyPass pre-0.9 Flow

        #region LandBuy Flow

        /*********************************/
        /*** Land Purchasing Functions ***/
        /*********************************/
        
        // NOTE:
        // This system first calls the preflightBuyLandPrep XMLRPC function to run some checks and produce some info for the buyer.  If this is not implemented, land purchasing will not proceed.
        // When a user click's buy, this sends an event to the server which triggers the IClientAPI's HandleParcelBuyRequest function.
        // --- validates the agentID and sessionID
        // --- sets agentID, groupID, final, groupOwned, removeContribution, parcelLocalID, parcelArea, and parcelPrice to the packet data and authenticated to false.
        // ------ authenticated should probably be true since this is what the IClientAPI does, but it is set to false and ignored.
        // --- Calls all registered OnParcelBuy events, one (and maybe the only) of which is the Scene's ProcessParcelBuy function.
        // Scene's ProcessParcelBuy function in Scene.PacketHandlers.cs
        // This function creates the LandBuyArgs and then calls two EventManager functions in succession:
        // --- TriggerValidateLandBuy: Calls all registered OnValidateLandBuy functions.  These are expected to set some variables, run some checks, and set the landValidated and economyValidated bools.
        // --- TriggerLandBuy: Calls all registered OnLandBuy functions which check the land/economyValidated bools.  If both are true, they proceed and process the land purchase.
        // This system is problematic because the order of validations and process landBuys is unknown, and they lack a middle step to place holds/enact.  Because of this, we need to do a complex integration here.
        // --- In validate, set economyValidated to false to ensure that the LandManager won't process the LandBuy on the first run.
        // --- In process, if landValidated = true, create and send a u2u transaction for the purchase to Gloebit.
        // --- In the asset enact response for the Gloebit Transaction, callTriggerValidateLandBuy.  This time, the GMM can set economyValidated to true.
        // ------ If landValidated is false, return false to enact to cancel transaction.
        // ------ If landValidated is true, call TriggerLandBuy.  GMM shouldn't have to do anything during ProcessLandBuy
        // --------- Ideally, we can verify that the land transferred.  If not, return false to cancel txn.  If true, return true to signal enacted so that txn will be consumed.
        
        /// <summary>
        /// Scene.EventManager.OnValidateLandBuy event handler
        /// Event triggered when a client chooses to purchase land.
        /// Called to validate that the monetary portion of a land sale is possible before attempting to process that land sale.
        /// Should set LandBuyArgs.economyValidated to true if/when land sale should proceed.
        /// After all validation functions are called, all process functions are called.
        /// see also ProcessLandBuy, ProcessAssetEnactHold, and transferLand
        /// </summary>
        /// <param name="osender">Object Scene which sent the request.</param>
        /// <param name="LandBuyArgs">EventManager.LandBuyArgs passed through the event chain
        /// --- agentId: UUID of buyer
        /// --- groupId: UUID of group if being purchased for a group (see LandObject.cs UpdateLandSold)
        /// --- parcelOwnerID: UUID of seller - Set by land validator, so cannot be relied upon in validation.
        /// ------ ***NOTE*** if land is group owned (see LandObject.cs DeedToGroup & UpdateLandSold), this is a GroupID.
        /// ------ ********** If bought for group, may still be buyers agentID.
        /// ------ ********** We don't know how to handle sales to or by a group yet.
        /// --- final: bool
        /// --- groupOwned: bool - whether this is being purchased for a group (see LandObject.cs UpdateLandSold)
        /// --- removeContribution: bool - if true, removes tier contribution if purchase is successful
        /// --- parcelLocalID: int ID of parcel in region
        /// --- parcelArea: int meters square size of parcel
        /// --- parcelPrice: int price buyer will pay
        /// --- authenticated: bool - set to false by IClientAPI and ignored.
        /// --- landValidated: bool set by the LandMangementModule during validation
        /// --- economyValidated: bool this validate function should set to true or false
        /// --- transactionID: int - Not used.  Commented out.  Was intended to store auction ID if land was purchased at auction. (see LandObject.cs UpdateLandSold)
        /// --- amountDebited: int - should be set by GMM
        /// </param>
        private void ValidateLandBuy(Object osender, EventManager.LandBuyArgs e)
        {
            // m_log.InfoFormat("[GLOEBITMONEYMODULE] ValidateLandBuy osender: {0}\nLandBuyArgs: \n   agentId:{1}\n   groupId:{2}\n   parcelOwnerID:{3}\n   final:{4}\n   groupOwned:{5}\n   removeContribution:{6}\n   parcelLocalID:{7}\n   parcelArea:{8}\n   parcelPrice:{9}\n   authenticated:{10}\n   landValidated:{11}\n   economyValidated:{12}\n   transactionID:{13}\n   amountDebited:{14}", osender, e.agentId, e.groupId, e.parcelOwnerID, e.final, e.groupOwned, e.removeContribution, e.parcelLocalID, e.parcelArea, e.parcelPrice, e.authenticated, e.landValidated, e.economyValidated, e.transactionID, e.amountDebited);
            
            if (e.economyValidated == false) {  /* Don't reValidate if something has said it's ready to go. */
                if (e.parcelPrice == 0) {
                    // No monetary component, so we can just approve this.
                    e.economyValidated = true;
                    // Should be redundant, but we'll set them anyway.
                    e.amountDebited = 0;
                    e.transactionID = 0;
                } else {
                    // We have a new request that requires a monetary transaction.
                    // Do nothing for now.
                    //// consider: we could create the asset here.
                }
            }
        }

        /// <summary>
        /// Scene.EventManager.OnLandBuy event handler
        /// Event triggered when a client chooses to purchase land.
        /// Called after all validation functions have been called.
        /// Called to process the monetary portion of a land sale.
        /// Should only proceed if LandBuyArgs.economyValidated and LandBuyArgs.landValidated are both true.
        /// Should set LandBuyArgs.amountDebited
        /// Also see ValidateLandBuy, ProcessAssetEnactHold and transferLand
        /// </summary>
        /// <param name="osender">Object Scene which sent the request.</param>
        /// <param name="LandBuyArgs">EventManager.LandBuyArgs passed through the event chain
        /// --- agentId: UUID of buyer
        /// --- groupId: UUID of group if being purchased for a group (see LandObject.cs UpdateLandSold)
        /// --- parcelOwnerID: UUID of seller - Set by land validator, so cannot be relied upon in validation.
        /// ------ ***NOTE*** if land is group owned (see LandObject.cs DeedToGroup & UpdateLandSold), this is a GroupID.
        /// ------ ********** If bought for group, may still be buyers agentID.
        /// ------ ********** We don't know how to handle sales to or by a group yet.
        /// --- final: bool
        /// --- groupOwned: bool - whether this is being purchased for a group (see LandObject.cs UpdateLandSold)
        /// --- removeContribution: bool - if true, removes tier contribution if purchase is successful
        /// --- parcelLocalID: int ID of parcel in region
        /// --- parcelArea: int meters square size of parcel
        /// --- parcelPrice: int price buyer will pay
        /// --- authenticated: bool - set to false by IClientAPI and ignored.
        /// --- landValidated: bool set by the LandMangementModule during validation
        /// --- economyValidated: bool this validate function should set to true or false
        /// --- transactionID: int - Not used.  Commented out.  Was intended to store auction ID if land was purchased at auction. (see LandObject.cs UpdateLandSold)
        /// --- amountDebited: int - should be set by GMM
        /// </param>
        private void ProcessLandBuy(Object osender, EventManager.LandBuyArgs e)
        {
            // m_log.InfoFormat("[GLOEBITMONEYMODULE] ProcessLandBuy osender: {0}\nLandBuyArgs: \n   agentId:{1}\n   groupId:{2}\n   parcelOwnerID:{3}\n   final:{4}\n   groupOwned:{5}\n   removeContribution:{6}\n   parcelLocalID:{7}\n   parcelArea:{8}\n   parcelPrice:{9}\n   authenticated:{10}\n   landValidated:{11}\n   economyValidated:{12}\n   transactionID:{13}\n   amountDebited:{14}", osender, e.agentId, e.groupId, e.parcelOwnerID, e.final, e.groupOwned, e.removeContribution, e.parcelLocalID, e.parcelArea, e.parcelPrice, e.authenticated, e.landValidated, e.economyValidated, e.transactionID, e.amountDebited);
            
            if (e.economyValidated == false) {  /* first time through */
                if (!e.landValidated) {
                    // Something's wrong with the land, can't continue
                    // Ideally, the land system would message this error, but they don't, so we will.
                    IClientAPI payerClient = LocateClientObject(e.agentId);
                    alertUsersTransactionPreparationFailure(TransactionType.USER_BUYS_LAND, TransactionPrecheckFailure.LAND_VALIDATION_FAILED, payerClient);
                    return;
                } else {
                    // Land is good to go.  Let's submit a transaction
                    //// TODO: verify that e.parcelPrice > 0;
                    //// TODO: what if parcelOwnerID is a groupID?
                    //// TODO: what if isGroupOwned is true and GroupID is not zero?
                    //// We'll have to test this and see if/how it fails when groups are involved.
                    string agentName = resolveAgentName(e.agentId);
                    string ownerName = resolveAgentName(e.parcelOwnerID);
                    Scene s = (Scene) osender;
                    string regionname = s.RegionInfo.RegionName;
                    string regionID = s.RegionInfo.RegionID.ToString();
                    
                    string description = String.Format("{0} sq. meters of land with parcel id {1} on {2}, {3}, purchased by {4} from {5}", e.parcelArea, e.parcelLocalID, regionname, m_gridnick, agentName,  ownerName);
                    
                    OSDMap descMap = buildOpenSimTransactionDescMap(regionname, regionID.ToString(), "LandBuy");
                    
                    GloebitTransaction txn = buildTransaction(transactionID: UUID.Zero, transactionType: TransactionType.USER_BUYS_LAND,
                                                                  payerID: e.agentId, payeeID: e.parcelOwnerID, amount: e.parcelPrice, subscriptionID: UUID.Zero,
                                                                  partID: UUID.Zero, partName: String.Empty, partDescription: String.Empty,
                                                                  categoryID: UUID.Zero, localID: 0, saleType: 0);
                    
                    if (txn == null) {
                        // build failed, likely due to a reused transactionID.  Shouldn't happen.
                        IClientAPI payerClient = LocateClientObject(e.agentId);
                        alertUsersTransactionPreparationFailure(TransactionType.USER_BUYS_LAND, TransactionPrecheckFailure.EXISTING_TRANSACTION_ID, payerClient);
                        return;
                    }
                    
                    // Add region UUID and LandBuyArgs to dictionary accessible for callback and wait for callback
                    // Needs to happen before we submit because C# can delay wakeup for this synchronous call and
                    // the enact could be received before we know if the submission succeeded.
                    lock(m_landAssetMap) {
                        m_landAssetMap[txn.TransactionID] = new Object[2]{s.RegionInfo.originRegionID, e};
                    }
                    bool submission_result = m_apiW.SubmitTransaction(txn, description, descMap, true);
                    // See GloebitAPIWrapper.TransactU2UCompleted and helper messaging functions for error messaging on failure - no action required.
                    // See ProcessAssetEnactHold for proceeding with txn on success.
                    
                    if (!submission_result) {
                        // payment failed.  message user and halt attempt to transfer land
                        //// TODO: message error
                        lock(m_landAssetMap) {
                            m_landAssetMap.Remove(txn.TransactionID);
                        }
                        return;
                    }
                }
            } else {                            /* economy is validated.  Second time through or 0G txn */
                if (e.parcelPrice == 0) {
                    // Free land.  No economic part.
                    e.amountDebited = 0;
                } else {
                    // Second time through.  Completing a transaction we launched the first time through.
                    // if e.landValidated, land has or will transfer.
                    // We can't verify here because the land process may happen after economy, so do nothing here.
                    // See processAssetEnactHold and transferLand for resolution.
                }
            }
        }

        private bool transferLand(GloebitTransaction txn, out string returnMsg) {
            //// retrieve LandBuyArgs from assetMap
            bool foundArgs = m_landAssetMap.ContainsKey(txn.TransactionID);
            if (!foundArgs) {
                returnMsg = "Could not locate land asset for transaction.";
                return false;
            }
            Object[] landBuyAsset = m_landAssetMap[txn.TransactionID];
            UUID regionID = (UUID)landBuyAsset[0];
            EventManager.LandBuyArgs e = (EventManager.LandBuyArgs)landBuyAsset[1];
            // Set land buy args that need setting
            // TODO: should we be creating a new LandBuyArgs and copying the data instead in case anything else subscribes to the LandBuy events and mucked with these?
            e.economyValidated = true;
            e.amountDebited = txn.Amount;
            e.landValidated = false;

            //// retrieve client
            IClientAPI sender = LocateClientObject(txn.PayerID);
            if (sender == null) {
                // TODO: Does it matter if we can't locate the client?  Does this break if sender is null?
                returnMsg = "Could not locate buyer.";
                return false;
            }
            //// retrieve scene
            Scene s = GetSceneByUUID(regionID);
            if (s == null) {
                returnMsg = "Could not locate scene.";
                return false;
            }

            //// Trigger validate
            s.EventManager.TriggerValidateLandBuy(sender, e);
            // Check land validation
            if (!e.landValidated) {
                returnMsg = "Land validation failed.";
                return false;
            }
            if (e.parcelOwnerID != txn.PayeeID) {
                returnMsg = "Parcel owner changed.";
                return false;
            }

            //// Trigger process
            s.EventManager.TriggerLandBuy(sender, e);
            // Verify that land transferred successfully - sad that we have to check this.
            ILandObject parcel = s.LandChannel.GetLandObject(e.parcelLocalID);
            UUID newOwnerID = parcel.LandData.OwnerID;
            if (newOwnerID != txn.PayerID) {
                // This should only happen if due to race condition.  Unclear if possible or result.
                returnMsg = "Land transfer failed.  Owner is not buyer.";
                return false;
            }
            returnMsg = "Transfer of land succeeded.";
            return true;
        }

        #endregion // LandBuy Flow

        /// <summary>
        /// client.OnScriptAnswer event handler
        /// Event triggered when a client responds yes to a script question (for permissions).
        /// GMM uses this to determine when auto-debit permissions are granted to a scripted object
        /// which intends to make payments to users on the object owners behalf.
        /// These transactions are handled by the IMoneyModule.ObjectGiveMoney function, not via an event.
        /// </summary>
        /// <param name="client">Client which responded.</param>
        /// <param name="objectID">SceneObjectPart UUID of the item the client is granting permissions on</param>
        /// <param name="itemID">UUID of the TaskInventoryItem associated with this SceneObjectPart which handles permissions</param>
        /// <param name="answer">Bitmap of the permissions which are being granted</param>
        private void handleScriptAnswer(IClientAPI client, UUID objectID, UUID itemID, int answer) {
            // m_log.InfoFormat("[GLOEBITMONEYMODULE] handleScriptAnswer for client:{0} with objectID:{1}, itemID:{2}, answer:{3}", client.AgentId, objectID, itemID, answer);

            if ((answer & ScriptBaseClass.PERMISSION_DEBIT) == 0)
            {
                // This is not PERMISSION_DEBIT
                // m_log.InfoFormat("[GLOEBITMONEYMODULE] handleScriptAnswer This is not a debit request");
                return;
            }
            // User granted permission debit.  Let's create a sub and sub-auth and provide link to user.
            m_log.DebugFormat("[GLOEBITMONEYMODULE] handleScriptAnswer for a grant of debit permissions");

            ////// Check if we have an auth for this objectID.  If not, request it. //////

            // Check subscription table.  If not exists, send create call to Gloebit.
            m_log.DebugFormat("[GLOEBITMONEYMODULE] handleScriptAnswer - looking for local subscription");
            GloebitSubscription sub = GloebitSubscription.Get(objectID, m_key, m_apiUrl);
            if (sub == null || sub.SubscriptionID == UUID.Zero) {
                // Don't create unless the object has a name and description
                // Make sure Name and Description are not null to avoid pgsql issue with storing null values
                // Make sure neither are empty as they are required by Gloebit to create a subscription
                SceneObjectPart part = findPrim(objectID);
                if (part == null) {
                    m_log.ErrorFormat("[GLOEBITMONEYMODULE] handleScriptAnswer - Could not find object - ID:{0}", objectID);
                    return;
                }
                if (String.IsNullOrEmpty(part.Name) || String.IsNullOrEmpty(part.Description)) {
                    m_log.WarnFormat("[GLOEBITMONEYMODULE] handleScriptAnswer - Can not create local subscription because part name or description is blank - Name:{0} Description:{1}", part.Name, part.Description);
                    // Send message to the owner to let them know they must edit the object and add a name and description
                    String imMsg = String.Format("Object with auto-debit script is missing a name or description.  Name and description are required by Gloebit in order to create a subscription for this auto-debit object.  Please enter a name and description in the object.  Current values are Name:[{0}] and Description:[{1}].", part.Name, part.Description);
                    sendMessageToClient(client, imMsg, client.AgentId);
                    return;
                }

                // Add this user to map of waiting for sub to create auth.
                lock(m_authWaitingForSubMap) {
                    m_authWaitingForSubMap[objectID] = client;
                }

                // call api to submit creation request to Gloebit
                m_log.DebugFormat("[GLOEBITMONEYMODULE] handleScriptAnswer - creating subscription for {0}", part.Name);
                m_apiW.CreateSubscription(objectID, part.Name, part.Description);
                return;     // Async creating sub.  when it returns, we'll continue flow in AlertSubscriptionCreated or AlertSubscriptionCreationFailed
            }
           
            // We have a Subscription.  Ask user to authorize it.
            m_apiW.AuthorizeSubscription(client.AgentId, String.Empty, sub, false);
            return;     // Async creating auth.  When returns, will send link to user.
        }

        #endregion // Commerce Event Handlers

        #endregion // Event Handlers

        #region XML RPC Handlers

        /*****************************************************************************************************
         * Buy-land, buy-currency and insufficient-funds flow handlers
         * - These functions can handle the calls to the currency helper-uri if it is configured to point
         *   at the sim.  The GMM provides this helper-uri and the currency symbol via the OpenSim Extras.
         *   Some viewers (Firestorm & Alchemy at time of writing) consume these so this requires no
         *   configuration to work for a user on a Gloebit enabled region.  For users with other or older viewers,
         *   the helper-uri will have to be configured properly, and if not pointed at a Gloebit enabled sim,
         *   the grid will have to handle these calls, which it has traditionally done with an XMLRPC server and
         *   currency.php and landtool.php helper scripts.  That is rather complex, so we recommend that all 
         *   viewers adopt this patch and that grids request that their users update to a viewer with this patch.
         *   --- Patch Info: http://dev.gloebit.com/blog/Upgrade-Viewer/
         *   --- Patch Info: https://medium.com/@colosi/multi-currency-support-coming-to-opensim-viewers-cd20e75f7990
         *   --- Patch Download: http://dev.gloebit.com/opensim/downloads/ColosiOpenSimMultiCurrencySupport.patch
         *   --- Firestorm Jira: https://jira.phoenixviewer.com/browse/FIRE-21587
         * - These functions handle some pre-flight checks which enable a land sales and provide some useful
         *   messaging for the buy-currency and insufficient-funds flows.  Unfortunately, we can not handle
         *   purchasing of currency directly through this flow.
         *****************************************************************************************************/

        private XmlRpcResponse quote_func(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            Hashtable requestData = (Hashtable) request.Params[0];

            string agentIdStr = requestData["agentId"] as string;
            UUID agentId = UUID.Parse(agentIdStr);
            UUID sessionId = UUID.Parse(requestData["secureSessionId"] as string);
            int amount = (int) requestData["currencyBuy"];

            m_log.InfoFormat("[GLOEBITMONEYMODULE] quote_func agentId: {0} sessionId: {1} currencyBuy: {2}", agentId, sessionId, amount);
            // foreach(DictionaryEntry e in requestData) { m_log.InfoFormat("{0}: {1}", e.Key, e.Value); }

            XmlRpcResponse returnval = new XmlRpcResponse();
            Hashtable quoteResponse = new Hashtable();
            Hashtable currencyResponse = new Hashtable();

            currencyResponse.Add("estimatedCost", amount / 2);
            currencyResponse.Add("currencyBuy", amount);

            quoteResponse.Add("success", true);
            quoteResponse.Add("currency", currencyResponse);

            // TODO - generate a unique confirmation token
            quoteResponse.Add("confirm", "asdfad9fj39ma9fj");

            GloebitUser user = GloebitUser.Get(m_key, agentId);
            if (!user.IsAuthed()) {
                IClientAPI client = LocateClientObject(agentId);
                m_apiW.Authorize(user, client.Name);
            }

            returnval.Value = quoteResponse;
            return returnval;
        }

        private XmlRpcResponse buy_func(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            Hashtable requestData = (Hashtable) request.Params[0];
            UUID agentId = UUID.Parse(requestData["agentId"] as string);
            string confirm = requestData["confirm"] as string;
            int currencyBuy = (int) requestData["currencyBuy"];
            int estimatedCost = (int) requestData["estimatedCost"];
            string secureSessionId = requestData["secureSessionId"] as string;

            // currencyBuy:viewerMinorVersion:secureSessionId:viewerBuildVersion:estimatedCost:confirm:agentId:viewerPatchVersion:viewerMajorVersion:viewerChannel:language
            // m_log.InfoFormat("[GLOEBITMONEYMODULE] buy_func params {0}", String.Join(":", requestData.Keys.Cast<String>()));
            m_log.InfoFormat("[GLOEBITMONEYMODULE] buy_func agentId {0} confirm {1} currencyBuy {2} estimatedCost {3} secureSessionId {4}",
                agentId, confirm, currencyBuy, estimatedCost, secureSessionId);

            GloebitUser u = GloebitUser.Get(m_key, agentId);
            Uri url = m_apiW.BuildPurchaseURI(BaseURI, u);
            string message = String.Format("Unfortunately we cannot yet sell Gloebits directly in the viewer.  Please visit {0} to buy Gloebits.", url);

            XmlRpcResponse returnval = new XmlRpcResponse();
            Hashtable returnresp = new Hashtable();
            returnresp.Add("success", false);
            returnresp.Add("errorMessage", message);
            returnresp.Add("errorUrl", url);
            returnval.Value = returnresp;
            return returnval;
        }

        private XmlRpcResponse preflightBuyLandPrep_func(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            m_log.InfoFormat("[GLOEBITMONEYMODULE] preflightBuyLandPrep_func");
            XmlRpcResponse ret = new XmlRpcResponse();
            Hashtable retparam = new Hashtable();
            Hashtable membershiplevels = new Hashtable();
            ArrayList levels = new ArrayList();
            Hashtable level = new Hashtable();
            level.Add("id", "00000000-0000-0000-0000-000000000000");
            level.Add("description", "some level");
            levels.Add(level);
            //membershiplevels.Add("levels",levels);

            Hashtable landuse = new Hashtable();
            landuse.Add("upgrade", false);
            landuse.Add("action", "http://invaliddomaininvalid.com/");

            Hashtable currency = new Hashtable();
            currency.Add("estimatedCost", 0);

            Hashtable membership = new Hashtable();
            membershiplevels.Add("upgrade", false);
            membershiplevels.Add("action", "http://invaliddomaininvalid.com/");
            membershiplevels.Add("levels", membershiplevels);

            retparam.Add("success", true);
            retparam.Add("currency", currency);
            retparam.Add("membership", membership);
            retparam.Add("landuse", landuse);
            retparam.Add("confirm", "asdfajsdkfjasdkfjalsdfjasdf");

            ret.Value = retparam;

            return ret;
        }

        private XmlRpcResponse landBuy_func(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            m_log.InfoFormat("[GLOEBITMONEYMODULE] landBuy_func");
            XmlRpcResponse ret = new XmlRpcResponse();
            Hashtable retparam = new Hashtable();
            // Hashtable requestData = (Hashtable) request.Params[0];

            // UUID agentId = UUID.Zero;
            // int amount = 0;

            retparam.Add("success", true);
            ret.Value = retparam;

            return ret;
        }

        #endregion // XML RPC Handlers

        #region GMM User Messaging
        
        /******************************************/
        /********* User Messaging Section *********/
        /******************************************/
        
        //**** Notes on OpenSim client messages ***/
        // AlertMessage: top right.  fades away. also appears in nearby chat (only to intended user). character limit of about 254
        // BlueBoxMessage: top right. fades away.  slightly darker than AlertMessage in Firestorm.  Also appears in nearby chat (only to intended user). no character limit
        // AgentAlertMessage w False: top right.  has "OK" button.  Fades away but stays in messages. character limit about 253
        // AgentAlertMessage w True: center. has "OK" button.  Does not fade away.  Requires clicking ok before interacting with anything else. character limit about 250
        
        /// <summary>
        /// Sends a message with url to user.
        /// </summary>
        /// <param name="client">IClientAPI of client we are sending the URL to</param>
        /// <param name="title">string title of message we are sending with the url</param>
        /// <param name="body">string body of message we are sending with the url</param>
        /// <param name="uri">full url we are sending to the client</param>
        private static void SendUrlToClient(IClientAPI client, string title, string body, Uri uri)
        {
            // Since we are trying to make the GloebitAPI strictly C# and not specific to OpenSim, we have removed IClientAPI where possible.
            // This means that in some flows where we start with a client but make an async call to the API, we only send the AgentId and
            // later have to re-retrieve the client from the AgentId.  Sometimes, this fails, and returns null, perhaps because a user has
            // logged out during the flow.  This causes a crash here because we dereference the client.  Instead, we will log a warning
            // (in case we start missing messages for users who are logged in) and return instead of attempting to message the client.
            if (client == null) {
                m_log.WarnFormat("[GLOEBITMONEYMODULE] SendUrlToClient called with null client.  Intended message was title:{0}; body:{1}; Uri:{2}", title, body, uri);
                return;
            }
            
            // Prep and send message
            string imMessage = String.Format("{0}\n\n{1}", title, body);
            UUID fromID = UUID.Zero;
            string fromName = String.Empty;
            UUID toID = client.AgentId;
            bool isFromGroup = false;
            UUID imSessionID = toID;     // Don't know what this is used for.  Saw it hacked to agent id in friendship module
            bool isOffline = true;       // I believe when true, if user is logged out, saves message and delivers it next time the user logs in.
            bool addTimestamp = false;
            GridInstantMessage im = new GridInstantMessage(client.Scene, fromID, fromName, toID, (byte)InstantMessageDialog.GotoUrl, isFromGroup, imMessage, imSessionID, isOffline, Vector3.Zero, Encoding.UTF8.GetBytes(uri.ToString() + "\0"), addTimestamp);
            client.SendInstantMessage(im);
        }
        
        /// <summary>
        /// Sends a message to a client.  If user is logged out and OfflineMessageModule is enabled, tries to save message to deliver at next login.
        /// </summary>
        /// <param name="client">IClientAPI of user we are messaging.</param>
        /// <param name="message">String message we are sending to client.</param>
        /// <param name="agentID">UUID of client we are messaging - only used if user is offline, to attempt saving of message.</param>
        private void sendMessageToClient(IClientAPI client, string message, UUID agentID)
        {
            //payerClient.SendBlueBoxMessage(UUID.Zero, "What is this?", String.Format("BlueBoxMessage: {0}", message));
            //payerClient.SendAgentAlertMessage(String.Format("AgentAlertMessage: {0}", message), false);
            //payerClient.SendAgentAlertMessage(String.Format("AgentAlertMessage True: {0}", message), true);
            //payerClient.SendAlertMessage(String.Format("AlertMessage: {0}", message));

            if (client != null) {
                //string imMessage = String.Format("{0}\n\n{1}", "Gloebit:", message);
                string imMessage = message;
                UUID fromID = UUID.Zero;
                string fromName = String.Empty; // Left blank as this is not used for the MessageBox message type
                UUID toID = client.AgentId;
                bool isFromGroup = false;
                UUID imSessionID = toID;        // Don't know what this is used for.  Saw it hacked to agent id in friendship module
                bool isOffline = true;          // Don't know what this is for.  Should probably try both.
                bool addTimestamp = false;
                    
                // TODO: add alternate MessageFromAgent which includes an ok button and doesn't show up in chat, rather goes to notifications
                GridInstantMessage im = new GridInstantMessage(client.Scene, fromID, fromName, toID, (byte)InstantMessageDialog.MessageBox, isFromGroup, imMessage, imSessionID, isOffline, Vector3.Zero, new byte[0], addTimestamp);
                client.SendInstantMessage(im);
            } else {
                // TODO: do we want to send an email or do anything else?
                
                // Attempt to save a message for the offline user.
                if (agentID != UUID.Zero) {     // Necessary because some txnPrecheckFailures don't currently pass the agentID
                    // If an OfflineMessageModule is set up and a service is registered at the following, this might work for offline messaging.
                    // SynchronousRestObjectRequester.MakeRequest<GridInstantMessage, bool>("POST", m_RestURL+"/SaveMessage/", im, 10000)
                    Scene s = GetAnyScene();
                    IMessageTransferModule tr = s.RequestModuleInterface<IMessageTransferModule>();
                    if (tr != null) {
                        GridInstantMessage im2 = new GridInstantMessage(null, UUID.Zero, "Gloebit", agentID, (byte)InstantMessageDialog.MessageFromAgent, false, message, agentID, true, Vector3.Zero, new byte[0], true);
                        tr.SendInstantMessage(im2, delegate(bool success) {});
                    }
                }
            }
        }

        /// <summary>
        /// Deliver intro messaging for user in new session or new environment.
        /// --- "Welcome to area running Gloebit in Sandbox for app MYAPP"
        /// Also sends auth message since we can't yet reliably tie into insufficient funds flow.
        /// </summary>
        private void SendNewSessionMessaging(IClientAPI client, GloebitUser user) {
            // TODO: Add in AppName to messages if we have it -- may need a new endpoint.
            string msg;
            if (m_environment == GLBEnv.Sandbox) {
                msg = String.Format("Welcome {0}.  This area is using the Gloebit Money Module in Sandbox Mode for testing.  All payments and transactions are fake.  Try it out.", client.Name);
            } else if (m_environment == GLBEnv.Production) {
                msg = String.Format("Welcome {0}.  This area is using the Gloebit Money Module.  You can transact with gloebits.", client.Name);
            } else {
                msg = String.Format("Welcome {0}.  This area is using the Gloebit Money Module in a Custom Devloper Mode.", client.Name);
            }
            // Add instructions for clicking balance to see auth or purchase url
            // TODO: Should this be a separate message?
            if (user.IsAuthed()) {
                msg = String.Format("{0}\nClick on your balance in the top right to purchase more gloebits.", msg);
            } else {
                msg = String.Format("{0}\nClick on your balance in the top right to link this avatar on this app to your Gloebit account.", msg);

            }
            // Delay messaging for a cleaner experience
            int delay = 1; // Delay 1 seconds on crossing or teleport where viewer is already loaded
            if (LoginBalanceRequest.ExistsAndJustLoggedIn(client.AgentId)) {
                delay = 10; // Delay 10 seconds if viewer isn't fully loaded, shows up as offline while away
            }
            Thread welcomeMessageThread = new Thread(delegate() {
                Thread.Sleep(delay * 1000);  // Delay milliseconds
                // Deliver welcome message if we have welcome popup enabled
                if (m_showWelcomeMessage) {
                    sendMessageToClient(client, msg, client.AgentId);
                }

                // If authed, delivery url where user can purchase Gloebits
                if (user.IsAuthed()) {
                    if (m_showNewSessionPurchaseIM) {
                        Uri url = m_apiW.BuildPurchaseURI(BaseURI, user);
                        SendUrlToClient(client, "How to purchase gloebits:", "Buy gloebits you can spend in this area:", url);
                    }
                } else {
                    if (m_showNewSessionAuthIM) {
                        // If not Authed, request auth.
                        m_apiW.Authorize(user, client.Name);
                    }
                }
            });
            welcomeMessageThread.Start();
        }
        
        /// <summary>
        /// See version with additional agentID argument.  Most status messages go to payer and we should have a valid client and in the case
        /// the payer is offline, can grab the agentID we need.  This version removes the need to supply that argument every time.
        /// In the case we are notifying the payee or someone else, the correct agentID can be supplied directly.
        /// </summary>
        private void sendTxnStatusToClient(GloebitTransaction txn, IClientAPI client, string baseStatus, bool showTxnDetails, bool showTxnID)
        {
            sendTxnStatusToClient(txn, client, baseStatus, showTxnDetails, showTxnID, txn.PayerID);
        }
        
        /// <summary>
        /// Builds a status string and sends it to the client
        /// Always includes an intro with shortened txn id and a base message.
        /// May include additional transaction details and txn id based upon bool arguments and bool overrides.
        /// </summary>
        /// <param name="txn">GloebitTransaction this status is in regards to.</param>
        /// <param name="client">Client we are messaging.  If null, our sendMessage func will handle properly.</param>
        /// <param name="baseStatus">String Status message to deliver.</param>
        /// <param name="showTxnDetails">If true, include txn details in status (can be overridden by global overrides).</param>
        /// <param name="showTxnID">If true, include full transaction id in status (can be overridden by global overrides).</param>
        /// <param name="agentID">UUID of user we are messaging.  Only used if user is offline and client is null.</param>
        private void sendTxnStatusToClient(GloebitTransaction txn, IClientAPI client, string baseStatus, bool showTxnDetails, bool showTxnID, UUID agentID)
        {
            // Determine if we're including Details and ID based on args and overrides
            bool alwaysShowTxnDetailsOverride = false;
            bool alwaysShowTxnIDOverride = false;
            bool neverShowTxnDetailsOverride = false;
            bool neverShowTxnIDOverride = false;
            bool includeDetails = (alwaysShowTxnDetailsOverride || (showTxnDetails && !neverShowTxnDetailsOverride));
            bool includeID = (alwaysShowTxnIDOverride || (showTxnID && !neverShowTxnIDOverride));
            
            // Get shortened txn id
            //int shortenedID = (int)(txn.TransactionID.GetULong() % 10000);
            string sid = txn.TransactionID.ToString().Substring(0,4).ToUpper();
            
            // Build status string
            string status = String.Format("Gloebit Transaction [{0}]:\n{1}"/*\n"*/, sid, baseStatus);
            if (includeDetails) {
                // build txn details string
                string paymentFrom = String.Format("Payment from: {0}", txn.PayerName);
                string paymentTo = String.Format("Payment to: {0}", txn.PayeeName);
                string amountStr = String.Format("Amount: {0:n0} gloebits", txn.Amount);
                // TODO: add description back in once txn includes it.
                // string descStr = String.Format("Description: {0}", description);
                string txnDetails = String.Format("Details:\n   {0}\n   {1}\n   {2}", paymentFrom, paymentTo, amountStr/*, descStr*/);
                
                status = String.Format("{0}\n{1}", status, txnDetails);
            }
            if (includeID) {
                // build txn id string
                string idStr = String.Format("Transaction ID: {0}", txn.TransactionID);
                
                status = String.Format("{0}\n{1}", status, idStr);
            }
            
            // Send status string to client
            sendMessageToClient(client, status, agentID);
        }
        
        /**** Functions to handle messaging users upon precheck failures in GMM before txn is created ****/
        
        /// <summary>
        /// Inform users of txn precheck failure due to subscription requiring creation.
        /// Triggered when an auto-debit transaction comes from an object for which no subscription has been created.
        /// </summary>
        /// <param name="payerID">UUID of payer from transaction that triggered this alert.</param>
        /// <param name="payeeID">UUID of payee from transaction that triggered this alert.</param>
        /// <param name="amount">Int amount of gloebits from transaction that triggered this alert.</param>
        /// <param name="subName">String name of subscription being sent to Gloebit for creation.</param>
        /// <param name="subDesc">String description of subscription being sent to Gloebit for creation.</param> 
        private void alertUsersSubscriptionTransactionFailedForSubscriptionCreation(UUID payerID, UUID payeeID, int amount, string subName, string subDesc)
        {
            IClientAPI payerClient = LocateClientObject(payerID);
            IClientAPI payeeClient = LocateClientObject(payeeID);
            
            string failedTxnDetails = String.Format("Failed Transaction Details:\n   Object Name: {0}\n   Object Description: {1}\n   Payment From: {2}\n   Payment To: {3}\n   Amount: {4}", subName, subDesc, resolveAgentName(payerID), resolveAgentName(payeeID), amount);
            
            // TODO: Need to alert payer whether online or not as action is required.
            sendMessageToClient(payerClient, String.Format("Gloebit: Scripted object attempted payment from you, but failed because no subscription exists for this recurring, automated payment.  Creating subscription now.  Once created, the next time this script attempts to debit your account, you will be asked to authorize that subscription for future auto-debits from your account.\n\n{0}", failedTxnDetails), payerID);
            
            // TODO: is this message bad if fraudster?
            // Should alert payee if online as might be expecting feedback
            sendMessageToClient(payeeClient, String.Format("Gloebit: Scripted object attempted payment to you, but failed because no subscription exists for this recurring, automated payment.  Creating subscription now.  If you triggered this transaction with an action, you can retry in a minute.\n\n{0}", failedTxnDetails), payeeID);
        }
        
        /// <summary>
        /// Inform users of txn precheck failure due to subscription transaction where payee never authorized with Gloebit, or revoked authorization.
        /// </summary>
        /// <param name="payerID">UUID of payer from transaction that triggered this alert.</param>
        /// <param name="payeeID">UUID of payee from transaction that triggered this alert.</param>
        /// <param name="amount">Int amount of gloebits from transaction that triggered this alert.</param>
        /// <param name="sub">GloebitSubscription that triggered this alert.</param>
        private void alertUsersSubscriptionTransactionFailedForGloebitAuthorization(UUID payerID, UUID payeeID, int amount, GloebitSubscription sub)
        {
            IClientAPI payerClient = LocateClientObject(payerID);
            IClientAPI payeeClient = LocateClientObject(payeeID);
            
            string failedTxnDetails = String.Format("Failed Transaction Details:\n   Object Name: {0}\n   Object Description: {1}\n   Payment From: {2}\n   Payment To: {3}\n   Amount: {4}", sub.ObjectName, sub.Description, resolveAgentName(payerID), resolveAgentName(payeeID), amount);
            
            // TODO: Need to alert payer whether online or not as action is required.
            sendMessageToClient(payerClient, String.Format("Gloebit: Scripted object attempted payment from you, but failed because you have not authorized this application from Gloebit.  Once you authorize this application, the next time this script attempts to debit your account, you will be asked to authorize that subscription for future auto-debits from your account.\n\n{0}", failedTxnDetails), payerID);
            if (payerClient != null) {
                m_apiW.Authorize(payerID, payerClient.Name);
            }
            
            // TODO: is this message bad if fraudster?
            // Should alert payee if online as might be expecting feedback
            sendMessageToClient(payeeClient, String.Format("Gloebit: Scripted object attempted payment to you, but failed because the object owner has not yet authorized this subscription to make recurring, automated payments.  Requesting authorization now.\n\n{0}", failedTxnDetails), payeeID);
        }
        
        /// <summary>
        /// Called when application preparation of a transaction fails before submission to Gloebit is attempted.
        /// Use to inform users or log issues
        /// At a minimum, this should inform the user who triggered the transaction of failure so they have feedback.
        /// This is separated from alertUsersTransactionBegun because there may not be a transaction yet and therefore
        /// different arguments are needed.
        /// </summary>
        /// <param name="typeID">TransactionType that was being prepared.</param>
        /// <param name="failure">TransactionPrecheckFailure that occurred.</param>
        /// <param name="payerClient">IClientAPI of payer or null.</param>
        private void alertUsersTransactionPreparationFailure(TransactionType typeID, TransactionPrecheckFailure failure, IClientAPI payerClient)
        {
            // TODO: move these to a string resource at some point.
            // Set up instruction strings which are used multiple times
            string tryAgainRelog = "Please retry your purchase.  If you continue to get this error, relog.";
            string tryAgainContactOwner = String.Format("Please try again.  If problem persists, contact {0}.", m_contactOwner);
            string tryAgainContactGloebit = String.Format("Please try again.  If problem persists, contact {0}.", m_contactGloebit);
            
            // Set up temp strings to hold failure messages based on transaction type and failure
            string txnTypeFailure = String.Empty;
            string precheckFailure = String.Empty;
            string instruction = String.Empty;
            
            // Retrieve failure strings into temp variables based on transaction type and failure
            switch (typeID) {
                case TransactionType.USER_BUYS_OBJECT:
                    // Alert payer only
                    txnTypeFailure = "Attempt to buy object failed prechecks.";
                    switch (failure) {
                        case TransactionPrecheckFailure.BUYING_DISABLED:
                            precheckFailure = "Buying is not enabled in economy settings.";
                            instruction = String.Format("If you believe this should be enabled on this region, please contact {0}.", m_contactOwner);
                            break;
                        case TransactionPrecheckFailure.OBJECT_NOT_FOUND:
                            precheckFailure = "Unable to buy now. The object was not found.";
                            instruction = tryAgainRelog;
                            break;
                        case TransactionPrecheckFailure.AMOUNT_MISMATCH:
                            precheckFailure = "Cannot buy at this price.  Price may have changed.";
                            instruction = tryAgainRelog;
                            break;
                        case TransactionPrecheckFailure.SALE_TYPE_INVALID:
                            precheckFailure = "Invalid saleType.";
                            instruction = tryAgainContactOwner;
                            break;
                        case TransactionPrecheckFailure.SALE_TYPE_MISMATCH:
                            precheckFailure = "Sale type mismatch.  Cannot buy this way.  Sale type may have changed.";
                            instruction = tryAgainRelog;
                            break;
                        case TransactionPrecheckFailure.BUY_SELL_MODULE_INACCESSIBLE:
                            precheckFailure = "Unable to access IBuySellModule necessary for transferring inventory.";
                            instruction = tryAgainContactOwner;
                            break;
                        default:
                            m_log.ErrorFormat("[GLOEBITMONEYMODULE] alertUsersTransactionPreparationFailure: Unimplemented failure TransactionPrecheckFailure [{0}] TransactionType.", failure, typeID);
                            break;
                    }
                    break;
                case TransactionType.USER_BUYS_LAND:
                    // Alert payer only as payer triggered txn
                    txnTypeFailure = "Attempt to buy land failed prechecks.";
                    precheckFailure = "Validation of parcel ownership and sale parameters failed.";
                    instruction = tryAgainRelog;
                    break;
                case TransactionType.USER_BUYS_LANDPASS:
                    // Alert Payer only
                    txnTypeFailure = "Attempt to buy land pass failed prechecks.";
                    switch (failure) {
                        case TransactionPrecheckFailure.OBJECT_NOT_FOUND:
                            precheckFailure = "Unable to buy now. The parcel was not found.";
                            instruction = tryAgainRelog;
                            break;
                        case TransactionPrecheckFailure.SALE_TYPE_INVALID:
                            precheckFailure = "Parcel is not set to sell passes.";
                            instruction = tryAgainRelog;
                            break;
                        case TransactionPrecheckFailure.GROUP_OWNED:
                            precheckFailure = "Parcel is group owned.  Transactions involving groups are undefined in OpenSim and thus have not been implemented yet by Gloebit.";
                            instruction = String.Format("If you would like Gloebit to prioritize this functionality, please contact {0}.", m_contactGloebit);
                            break;
                        default:
                            m_log.ErrorFormat("[GLOEBITMONEYMODULE] alertUsersTransactionPreparationFailure: Unimplemented failure TransactionPrecheckFailure [{0}] TransactionType.", failure, typeID);
                            break;
                    }
                    break;
                case TransactionType.MOVE_MONEY_GENERAL:
                    // MoveMoney unimplemented transaction type.
                    // Alert payer only
                    txnTypeFailure = "Unimplemented transaction type.";
                    instruction = String.Format("Please contact {0}, tell them what you were doing that requires a payment, and ask them to implement this transaction type.", m_contactGloebit);
                    break;
                case TransactionType.OBJECT_PAYS_USER:
                    // Alert payer and payee
                    // never happens currently
                    // txnTypeFailure = "Attempt by scripted object to pay user failed prechecks.";
                case TransactionType.USER_PAYS_USER:
                    // Alert payer only
                    // never happens currently
                    // txnTypeFailure = "Attempt to pay user failed prechecks.";
                case TransactionType.USER_PAYS_OBJECT:
                    // Alert payer only
                    // never happens currently
                    // txnTypeFailure = "Attempt to pay object failed prechecks.";
                default:
                    // Alert payer and payee
                    txnTypeFailure = "Transaction attempt failed prechecks.";
                    m_log.ErrorFormat("[GLOEBITMONEYMODULE] alertUsersTransactionPreparationFailure: Unimplemented failure TransactionType [{0}] Failure [{1}].", typeID, failure);
                    break;
            }
            
            // Handle some failures that could happen from any transaction type
            switch (failure) {
                case TransactionPrecheckFailure.EXISTING_TRANSACTION_ID:
                    precheckFailure = "Transaction conflicted with existing transaction record with identical ID.";
                    instruction = tryAgainRelog;
                    break;
            }
            
            // build failure message from temp strings
            string failureDetails = String.Format("Details:\n   {0}", txnTypeFailure);
            if (!String.IsNullOrEmpty(precheckFailure)) {
                failureDetails = String.Format("{0}\n   {1}", failureDetails, precheckFailure);
            }
            string failureMsg = String.Format("Transaction precheck FAILURE.\n{0}\n\n{1}\n", failureDetails, instruction);
            
            // send failure message to client
            // For now, only alert payer for simplicity and since We should only ever get here from an ObjectBuy
            // TODO: replace UUID.Zero with the agentID of the payer once we add it to function args. -- not vital that these make it to offline user.
            sendMessageToClient(payerClient, failureMsg, UUID.Zero);

        }
        
        /**** Functions to handle messaging transaction status to users (after GloebitTransaction has been built) ****/
        
        /// <summary>
        /// Called just prior to the application submitting a transaction to Gloebit.
        /// This function should be used to provide immediate feedback to a user that their request/interaction was received.
        /// It is assumed that this is almost instantaneous and should be the source of immediate feedback that the user's action
        /// has resulted in a transaction.  If something added to the application's preparation is likely to delay this, then
        /// the application may wish to lower the priority of this message in favor of messaging the start of preparation.
        /// Once this is called, an alert for 1 or more stage status will be received and a transaction completion alert.
        /// </summary>
        /// <param name="txn">Transaction that failed.</param>
        /// <param name="description">String containing txn description since this is not in the Transaction class yet.</param>
        private void alertUsersTransactionBegun(GloebitTransaction txn, string description)
        {
            // TODO: make user configurable
            bool showDetailsWithTxnBegun = true;
            bool showIDWithTxnBegun = false;
            
            // TODO: consider using Txn.TransactionTypeString
            String actionStr = String.Empty;
            String payeeActionStr = String.Empty;
            bool messagePayee = false;
            switch ((TransactionType)txn.TransactionType) {
                case TransactionType.USER_BUYS_OBJECT:
                    // Alert payer only; payee will be null
                    switch (txn.SaleType) {
                        case 1: // Sell as original (in-place sale)
                            actionStr = String.Format("Purchase Original: {0}", txn.PartName);
                            break;
                        case 2: // Sell a copy
                            actionStr = String.Format("Purchase Copy: {0}", txn.PartName);
                            break;
                        case 3: // Sell contents
                            actionStr = String.Format("Purchase Contents: {0}", txn.PartName);
                            break;
                        default:
                            // Should not get here as this should fail before transaction is built.
                            m_log.ErrorFormat("[GLOEBITMONEYMODULE] Transaction Begun With Unrecognized saleType:{0} --- expected 1,2 or 3 for original, copy, or contents", txn.SaleType);
                            // TODO: Assert this.
                            //assert(txn.TransactionType >= 1 && txn.TransactionType <= 3);
                            break;
                    }
                    break;
                case TransactionType.OBJECT_PAYS_USER:
                    // Alert payer and payee, as we don't know who triggered it.
                    // This looks like a message for payee, but is sent to payer
                    actionStr = String.Format("Auto-debit created by object: {0}", txn.PartName);
                    payeeActionStr = String.Format("Payment to you from object: {0}", txn.PartName);
                    messagePayee = true;
                    break;
                case TransactionType.USER_PAYS_USER:
                    // Alert payer only
                    actionStr = String.Format("Paying User: {0}", txn.PayeeName);
                    break;
                case TransactionType.USER_PAYS_OBJECT:
                    // Alert payer only
                    actionStr = String.Format("Paying Object: {0}", txn.PartName);
                    break;
                case TransactionType.USER_BUYS_LAND:
                    // Alert payer only
                    actionStr = "Purchase Land.";
                    break;
                case TransactionType.USER_BUYS_LANDPASS:
                    // Alert Payer only
                    actionStr = "Purcahase Land Pass.";
                    break;
                case TransactionType.FEE_GROUP_CREATION:
                    // Alert payer only.  Payee is App.
                    actionStr = "Paying Grid to create a group";
                    break;
                case TransactionType.FEE_UPLOAD_ASSET:
                    // Alert payer only.  Payee is App.
                    actionStr = "Paying Grid to upload an asset";
                    break;
                case TransactionType.FEE_CLASSIFIED_AD:
                    // Alert payer only.  Payee is App.
                    actionStr = "Paying Grid to place a classified ad";
                    break;
                default:
                    // Alert payer and payee
                    m_log.ErrorFormat("[GLOEBITMONEYMODULE] alertUsersTransactionPreparationFailure: Unimplemented TransactionBegun TransactionType [{0}] with description [{1}].", txn.TransactionType, description);
                    actionStr = "";
                    break;
            }
            
            // Alert payer
            IClientAPI payerClient = LocateClientObject(txn.PayerID);
            // TODO: remove description once in txn and managed in sendTxnStatusToClient
            //string baseStatus = String.Format("Submitting transaction request...\n   {0}", actionStr);
            string baseStatus = String.Format("Submitting transaction request...\n   {0}\nDescription: {1}", actionStr, description);
            sendTxnStatusToClient(txn, payerClient, baseStatus, showDetailsWithTxnBegun, showIDWithTxnBegun);
            
            // If necessary, alert Payee
            if (messagePayee && (txn.PayerID != txn.PayeeID)) {
                IClientAPI payeeClient = LocateClientObject(txn.PayeeID);
                // TODO: remove description once in txn and managed in sendTxnStatusToClient
                // string payeeBaseStatus = String.Format("Submitting transaction request...\n   {0}", payeeActionStr);
                string payeeBaseStatus = String.Format("Submitting transaction request...\n   {0}\nDescription: {1}", payeeActionStr, description);
                sendTxnStatusToClient(txn, payeeClient, payeeBaseStatus, showDetailsWithTxnBegun, showIDWithTxnBegun, txn.PayeeID);
            }
        }
        
        /// <summary>
        /// Called when various stages of the transaction succeed.
        /// These will never be the final message received, but in failure cases, may provide information that will not be
        /// contained in the final message, so it is recommended that failure messages make it to at least the user who
        /// triggered the transaction.
        /// If desired, the completion of the enaction (asset enacted/delivered) can be used to short circuit to a final success message
        /// more quickly as the transaction should always eventually succeed after this.
        /// Stages:
        /// --- Submitted: TransactU2U call succeeded
        /// --- Queued: TransactU2U async callback succeeded
        ///             This will also trigger final success or failure if the transaction did not include an asset with callback urls.
        ///             --- currently, this is txn == null which doesn't happen in OpenSim and will eventually switch to using an "asset".
        /// --- Enacted:
        /// ------ Funds transferred: AssetEnact started (all Gloebit components of transaction enacted successfully)
        /// ------ Asset enacted: In Opensim, object delivery or notification of payment completing (local components of transaction enacted successfully)
        /// --- Consumed: Finalized (notified of successful enact across all components.  commit enacts.  Eventually, all enacts will be consumed/finalized/committed).
        /// --- Canceled: Probably shouldn't ever get called.  Worth logging.
        /// </summary>
        /// <param name="txn">Transaction that failed.</param>
        /// <param name="stage">TransactionStage which the transaction successfully completed to drive this alert.</param>
        /// <param name="additionalDetails">String containing additional details to be appended to the alert message.</param>
        private void alertUsersTransactionStageCompleted(GloebitTransaction txn, GloebitAPI.TransactionStage stage, string additionalDetails)
        {
            // TODO: make user configurable
            bool reportAllTxnStagesOverride = false;
            bool reportNoTxnStagesOverride = false;
            Dictionary<GloebitAPI.TransactionStage, bool> reportTxnStageMap = new Dictionary<GloebitAPI.TransactionStage, bool>();
            reportTxnStageMap[GloebitAPI.TransactionStage.ENACT_ASSET] = true;
            
            // Determine if we are going to report this stage
            bool reportThisStage = false;
            if (reportTxnStageMap.ContainsKey(stage)) {
                reportThisStage = reportTxnStageMap[stage];
            }
            if (!(reportAllTxnStagesOverride || (reportThisStage && !reportNoTxnStagesOverride))) {
                return;
            }
            
            // TODO: make user configurable
            bool showDetailsWithTxnStage = false;
            bool showIDWithTxnStage = false;
            
            string status = String.Empty;
            
            switch (stage) {
                case GloebitAPI.TransactionStage.SUBMIT:
                    status = "Successfully submitted to Gloebit service.";
                    break;
                case GloebitAPI.TransactionStage.QUEUE:
                    // a) queued and gloebits transferred.
                    // b) resubmitted
                    // c) queued, but early enact failure
                    status = "Successfully received by Gloebit and queued for processing.";
                    break;
                case GloebitAPI.TransactionStage.ENACT_GLOEBIT:
                    status = "Successfully transferred gloebits.";
                    break;
                case GloebitAPI.TransactionStage.ENACT_ASSET:
                    switch ((TransactionType)txn.TransactionType) {
                        case TransactionType.USER_BUYS_OBJECT:
                            // 5000 - ObjectBuy
                            // delivered the object/contents purchased.
                            switch (txn.SaleType) {
                                case 1: // Sell as original (in-place sale)
                                    status = "Successfully delivered object.";
                                    break;
                                case 2: // Sell a copy
                                    status = "Successfully delivered copy of object to inventory.";
                                    break;
                                case 3: // Sell contents
                                    status = "Successfully delivered object contents to inventory.";
                                    break;
                                default:
                                    m_log.ErrorFormat("[GLOEBITMONEYMODULE] alertUsersTransactionStageCompleted called on unknown sale type: {0}", txn.SaleType);
                                    ////status = "Successfully enacted local components of transaction.";
                                    break;
                            }
                            break;
                        case TransactionType.USER_PAYS_USER:
                            // 5001 - OnMoneyTransfer - Pay User
                            // nothing local enacted
                            ////status = "Successfully enacted local components of transaction.";
                            break;
                        case TransactionType.USER_PAYS_OBJECT:
                            // 5008 - OnMoneyTransfer - Pay Object
                            // alerted the object that it has been paid.
                            status = "Successfully notified object of payment.";
                            break;
                        case TransactionType.OBJECT_PAYS_USER:
                            // 5009 - ObjectGiveMoney
                            // nothing local enacted
                            ////status = "Successfully enacted local components of transaction.";
                            break;
                        case TransactionType.USER_BUYS_LAND:
                            // 5002 - OnLandBuy
                            // land transferred
                            status = "Successfully transferred parcel to new owner.";
                            break;
                        case TransactionType.USER_BUYS_LANDPASS:
                            // 5006 - OnParcelBuyPass pre 0.9.1; MoveMoney post;
                            // avatar added to parcel access list with expiration
                            if (!m_newLandPassFlow) {
                                status = "Successfully added you to parcel access list.";
                            } else {
                                status = "Land Management System should be adding you to the parcel access list now.";
                            }
                            break;
                        case TransactionType.FEE_GROUP_CREATION:
                            // 1002 - ApplyCharge
                            // Nothing local enacted.
                            break;
                        case TransactionType.FEE_UPLOAD_ASSET:
                            // 1101 - ApplyUploadCharge
                            // Nothing local enacted
                            break;
                        case TransactionType.FEE_CLASSIFIED_AD:
                            // 1103 - ApplyCharge
                            // Nothing local enacted
                            break;
                        default:
                            m_log.ErrorFormat("[GLOEBITMONEYMODULE] alertUsersTransactionStageCompleted called on unknown transaction type: {0}", txn.TransactionType);
                            // TODO: should we throw an exception?  return null?  just continue?
                            // take no action.
                            ////status = "Successfully enacted local components of transaction.";
                            break;
                    }
                    break;
                case GloebitAPI.TransactionStage.CONSUME_GLOEBIT:
                    status = "Successfully finalized transfer of gloebits.";
                    break;
                case GloebitAPI.TransactionStage.CONSUME_ASSET:
                    status = "Successfully finalized local components of transaction.";
                    break;
                case GloebitAPI.TransactionStage.CANCEL_GLOEBIT:
                    status = "Successfully canceled and rolled back transfer of gloebits.";
                    break;
                case GloebitAPI.TransactionStage.CANCEL_ASSET:
                    status = "Successfully canceled and rolled back local components of transaction.";
                    break;
                default:
                    m_log.ErrorFormat("[GLOEBITMONEYMODULE] alertUsersTransactionStageCompleted called on unhandled transaction stage : {0}", stage);
                    // TODO: should we throw an exception?  return null?  just continue?
                    status = "Successfully completed undefined transaction stage";
                    break;
            }
            
            // If this is a stage we have not stored a status for, then don't send a message
            if (String.IsNullOrEmpty(status)) {
                return;
            }
            
            if (!String.IsNullOrEmpty(additionalDetails)) {
                status = String.Format("{0}\n{1}", status, additionalDetails);
            }
            
            // for now, we're only going to send these to the payer.
            IClientAPI payerClient = LocateClientObject(txn.PayerID);
            sendTxnStatusToClient(txn, payerClient, status, showDetailsWithTxnStage, showIDWithTxnStage);
        }
        
        /// <summary>
        /// Called when transaction completes with failure.
        /// At a minimum, this should always be messaged to the user who triggered the transaction.
        /// </summary>
        /// <param name="txn">Transaction that failed.</param>
        /// <param name="stage">TransactionStage in which the transaction failed to drive this alert.</param>
        /// <param name="failure">TransactionFailure code providing necessary differentiation on specific failure within a stage.</param>
        /// <param name="additionalFailureDetails">String containing additional details to be appended to the alert message.</param>
        private void alertUsersTransactionFailed(GloebitTransaction txn, GloebitAPI.TransactionStage stage, GloebitAPI.TransactionFailure failure, string additionalFailureDetails)
        {
            // TODO: make user configurable
            bool showDetailsWithTxnFailed = false;
            bool showIDWithTxnFailed = true;
            // TODO: how does this work with instructions?
            
            // TODO: move these to a string resource at some point.
            // Set up instruction strings which are used multiple times
            string tryAgainContactOwner = String.Format("Please try again.  If problem persists, contact {0}.", m_contactOwner);
            string tryAgainContactGloebit = String.Format("Please try again.  If problem persists, contact {0}.", m_contactGloebit);
            string subAuthDialogComing = "You will be presented with an additional message instructing you how to approve or deny authorization for future automated transactions for this subscription.";
            string contactPayee = "Please alert seller/payee to this issue if possible and have seller/payee contact Gloebit.";
            string contactPayer = "Please alert buyer/payer to this issue.";
            
            // Set up temp strings to hold failure messages based on transaction type and failure
            string error = String.Empty;
            string instruction = String.Empty;
            string payeeInstruction = String.Empty;
            //string payeeAlert = String.Empty;
            
            // Separate message for when payee needs an alert whether or not payee knew about transaciton start.
            bool messagePayee = false;
            string payeeMessage = String.Empty;
            
            // Retrieve failure strings into temp variables based on transaction type and failure
            switch (stage) {
                case GloebitAPI.TransactionStage.SUBMIT:
                    error = "Region failed to properly create and send request to Gloebit.";
                    instruction = payeeInstruction = tryAgainContactOwner;
                    break;
                case GloebitAPI.TransactionStage.AUTHENTICATE:
                    // Only thing that should cause this right now is an invalid token, so we'll ignore the failure variable.
                    error = "Payer's authorization of this app has been revoked or expired.";
                    instruction = "Please re-authenticate with Gloebit.";
                    // TODO: write a better message.  Also, should we trigger auth message with link?
                    break;
                case GloebitAPI.TransactionStage.VALIDATE:
                    switch (failure) {
                        // Validate Form
                        case GloebitAPI.TransactionFailure.FORM_GENERIC_ERROR:                    /* One of many form errors.  something needs fixing.  See reason */
                            error = "Application provided malformed transaction to Gloebit.";
                            instruction = payeeInstruction = tryAgainContactOwner;
                            break;
                        case GloebitAPI.TransactionFailure.FORM_MISSING_SUBSCRIPTION_ID:          /* marked as subscription, but did not include any subscription id */
                            error = "Missing subscription-id from transaction marked as subscription payment.";
                            instruction = payeeInstruction = tryAgainContactOwner;
                            break;
                        // Validate Subscription
                        case GloebitAPI.TransactionFailure.SUBSCRIPTION_NOT_FOUND:              /* No sub found under app + identifiers provided */
                            error = "Gloebit did not find a subscription with the id provided for this subscription payment.";
                            instruction = payeeInstruction = tryAgainContactOwner;
                            break;
                        // Validate Subscription Authorization
                        case GloebitAPI.TransactionFailure.SUBSCRIPTION_AUTH_NOT_FOUND:         /* No sub_auth has been created to request authorizaiton yet */
                            error = "Payer has not authorized payments for this subscription.";
                            instruction = subAuthDialogComing;
                            payeeInstruction = contactPayer;
                            break;
                        case GloebitAPI.TransactionFailure.SUBSCRIPTION_AUTH_PENDING:
                            error = "Payer has a pending authorization for this subscription.";
                            instruction = subAuthDialogComing;
                            payeeInstruction = contactPayer;
                            break;
                        case GloebitAPI.TransactionFailure.SUBSCRIPTION_AUTH_DECLINED:
                            error = "Payer has declined authorization for this subscription.";
                            instruction = "You can review and alter your response subscription authorization requests from the Subscriptions section of the Gloebit website.";
                            payeeInstruction = contactPayer;
                            break;
                        // Validate Payer
                        case GloebitAPI.TransactionFailure.PAYER_ACCOUNT_LOCKED:
                            // TODO: should this message be BUYER ONLY?  Is this a privacy issue?
                            error = "Payer's Gloebit account is locked.";
                            instruction = "Please contact Gloebit to resolve any account status issues.";
                            payeeInstruction = contactPayer;
                            break;
                        // Validate Payee
                        case GloebitAPI.TransactionFailure.PAYEE_CANNOT_BE_IDENTIFIED:
                            // message payer and payee
                            error = "Gloebit can not identify payee from OpenSim account.";
                            instruction = "Please alert seller/payee to this issue.  They should run through the authorization flow from this grid to link their OpenSim agent to a Gloebit account.";
                            payeeInstruction = "Please ensure your OpenSim account has an email address, and that you have verified this email address in your Gloebit account.  If you are a hypergrid user with a foreign home grid, then your email address is not provided, so you will need to authorize this Grid in order to create a link from this agent to your Gloebit account.  You can immediately revoke your authorization if you don't want this Grid to be able to charge your account.  We will continue to send received funds to the last Gloebit account linked to this avatar.";
                            messagePayee = true;
                            payeeMessage = String.Format("Gloebit:\nAttempt to pay you failed because we cannot identify your Gloebit account from your OpenSim account.\n\n{0}", payeeInstruction);
                            break;
                        case GloebitAPI.TransactionFailure.PAYEE_CANNOT_RECEIVE:
                            // message payer and payee
                            // TODO: Is it a privacy issue to alert buyer here?
                            // TODO: research if/when account is in this state.  Only by admin?  All accounts until merchants?
                            error = "Payee's Gloebit account is unable to receive gloebits.";
                            instruction = contactPayee;
                            payeeInstruction = String.Format("Please contact {0} to address this issue", m_contactGloebit);
                            messagePayee = true;
                            payeeMessage = String.Format("Gloebit:\nAttempt to pay you failed because your Gloebit account cannot receive gloebits.\n\n{0}", payeeInstruction);
                            break;
                        default:
                            m_log.ErrorFormat("[GLOEBITMONEYMODULE] alertUsersTransactionFailed called on unhandled validation failure : {0}", failure);
                            error = "Validation error.";
                            instruction = payeeInstruction = tryAgainContactOwner;
                            break;
                    }
                    break;
                case GloebitAPI.TransactionStage.QUEUE:
                    // only one error which should make it here right now, and it's generic.
                    error = "Queuing Error.";
                    instruction = payeeInstruction = tryAgainContactGloebit;
                    break;
                case GloebitAPI.TransactionStage.ENACT_GLOEBIT:
                    // We get these through early-enact failures via GloebitAPIWrapper.transactU2UCompleted call -> AlertTransactionFailed
                    error = "Transfer of gloebits failed.";
                    switch (failure) {
                        case GloebitAPI.TransactionFailure.INSUFFICIENT_FUNDS:
                            error = String.Format("{0}  Insufficient funds.", error);
                            instruction = "Go to https://www.gloebit.com/purchase to get more gloebits.";
                            payeeInstruction = contactPayer;    // not considering privacy issue since caused by auto-debit and payer would want to know of failure.
                            break;
                        default:
                            error = String.Format("{0}  Failure during processing.", error);
                            instruction = payeeInstruction = tryAgainContactOwner;
                            break;
                    }
                    break;
                case GloebitAPI.TransactionStage.ENACT_ASSET:
                    switch ((TransactionType)txn.TransactionType) {
                        case TransactionType.USER_BUYS_OBJECT:
                            // 5000 - ObjectBuy
                            // delivered the object/contents purchased.
                            //// additional_details will include one of the following
                            ////"Can't locate buyer."
                            ////"Can't access IBuySellModule."
                            ////"IBuySellModule.BuyObject failed delivery attempt."
                            switch (txn.SaleType) {
                                case 1: // Sell as original (in-place sale)
                                    error = "Delivery of object failed.";
                                    break;
                                case 2: // Sell a copy
                                    error = "Delivery of object copy failed.";
                                    break;
                                case 3: // Sell contents
                                    error = "Delivery of object contents failed.";
                                    break;
                                default:
                                    error = "Enacting of local transaction components failed.";
                                    break;
                            }
                            break;
                        case TransactionType.USER_PAYS_USER:
                            // 5001 - OnMoneyTransfer - Pay User
                            // nothing local enacted
                            // Currently, shouldn't ever get here.
                            error = "Enacting of local transaction components failed.";
                            break;
                        case TransactionType.USER_PAYS_OBJECT:
                            // 5008 - OnMoneyTransfer - Pay Object
                            // alerted the object that it has been paid.
                            error = "Object payment notification failed.";
                            break;
                        case TransactionType.OBJECT_PAYS_USER:
                            // 5009 - ObjectGiveMoney
                            // TODO: who to alert payee, or payer.
                            // Currently, shouldn't ever get here.
                            error = "Enacting of local transaction components failed.";
                            break;
                        case TransactionType.USER_BUYS_LAND:
                            // 5002 - OnLandBuy
                            // land transfer failed
                            error = "Transfer of parcel to new owner failed.";
                            instruction = tryAgainContactOwner;
                            break;
                        default:
                            m_log.ErrorFormat("[GLOEBITMONEYMODULE] alertUsersTransactionFailed called on unknown transaction type: {0}", txn.TransactionType);
                            // TODO: should we throw an exception?  return null?  just continue?
                            error = "Enacting of local transaction components failed.";
                            break;
                    }
                    break;
                default:
                    m_log.ErrorFormat("[GLOEBITMONEYMODULE] alertUsersTransactionFailed called on unhandled transaction stage : {0}", stage);
                    // TODO: should we throw an exception?  return null?  just continue?
                    error = "Unhandled transaction failure.";
                    break;
            }
            
            // build failure alert from temp strings
            string status = String.Format("Transaction FAILED.\n   {0}", error);
            if (!String.IsNullOrEmpty(additionalFailureDetails)) {
                status = String.Format("{0}\n{1}", status, additionalFailureDetails);
            }
            string statusAndInstruction = status;
            if (!String.IsNullOrEmpty(instruction)) {
                statusAndInstruction = String.Format("{0}\n{1}", status, instruction);
            }
            
            // send failure alert to payer
            IClientAPI payerClient = LocateClientObject(txn.PayerID);
            sendTxnStatusToClient(txn, payerClient, statusAndInstruction, showDetailsWithTxnFailed, showIDWithTxnFailed);
            
            // Determine if alert needs to be sent to payee and send
            IClientAPI payeeClient = null;
            if (txn.TransactionType == (int)TransactionType.OBJECT_PAYS_USER || messagePayee) {
                // locate payee since we'll need to message
                payeeClient = LocateClientObject(txn.PayeeID);
            }
            // If this is a transaction type where we notified the payer the txn started, we should alert to failure as payer may have triggered the txn
            if (txn.TransactionType == (int)TransactionType.OBJECT_PAYS_USER) {
                // build failure alert from temp strings
                statusAndInstruction = status;
                if (!String.IsNullOrEmpty(payeeInstruction)) {
                    statusAndInstruction = String.Format("{0}\n{1}", status, payeeInstruction);
                }
                sendTxnStatusToClient(txn, payeeClient, statusAndInstruction, showDetailsWithTxnFailed, showIDWithTxnFailed, txn.PayeeID);
            }
            
            // If necessary, send separate message to Payee
            if (messagePayee) {
                sendMessageToClient(payeeClient, payeeMessage, txn.PayeeID);
                // TODO: this message should be delivered to email if client is not online and didn't trigger this message.
                
                // Since unidentified seller can now be fixed by auth, send the auth link if they are online
                if (payeeClient != null && failure == GloebitAPI.TransactionFailure.PAYEE_CANNOT_BE_IDENTIFIED) {
                    m_apiW.Authorize(payeeClient.AgentId, payeeClient.Name);
                }
            }
            
        }
        
        /// <summary>
        /// Called when a transaction has successfully completed so that necessary notification can be triggered.
        /// At a minimum, this should notify the user who triggered the transaction.
        /// </summary>
        /// <param name="txn">Transaction that succeeded.</param>
        private void alertUsersTransactionSucceeded(GloebitTransaction txn)
        {
            // TODO: make user configurable
            bool showDetailsWithTxnSucceeded = false;
            bool showIDWithTxnSucceeded = false;
            
            IClientAPI payerClient = LocateClientObject(txn.PayerID);
            IClientAPI payeeClient = LocateClientObject(txn.PayeeID);   // get this regardless of messaging since we'll try to update balance
            
            // send success message to payer
            sendTxnStatusToClient(txn, payerClient, "Transaction SUCCEEDED.", showDetailsWithTxnSucceeded, showIDWithTxnSucceeded);
            
            // If this is a transaction type where we notified the payee the txn started, we should alert to successful completion
            if ((txn.TransactionType == (int)TransactionType.OBJECT_PAYS_USER) && (txn.PayerID != txn.PayeeID)) {
                sendTxnStatusToClient(txn, payeeClient, "Transaction SUCCEEDED.", showDetailsWithTxnSucceeded, showIDWithTxnSucceeded, txn.PayeeID);
            }
            // If this transaction was one user paying another, if the user is online, we should let them know they received a payment
            if (txn.TransactionType == (int)TransactionType.USER_PAYS_USER) {
                string message = String.Format("You've received Gloebits from {0}.", resolveAgentName(payerClient.AgentId));
                sendTxnStatusToClient(txn, payeeClient, message, true, showIDWithTxnSucceeded, txn.PayeeID);
            }
            // TODO: consider if we want to send an alert that payee earned money with transaction details for other transaction types
            
            // TODO: should consider updating API to return payee ending balance as well.  Potential privacy issue here if not approved to see balance.
            
            // TODO: Once we store description in txn, change 3rd arg in SMB below to Utils.StringToBytes(description)
            
            // Update Payer & Payee balances if still logged in.
            if (payerClient != null) {
                if (txn.PayerEndingBalance >= 0) {  /* if -1, got an invalid balance in response.  possible this shouldn't ever happen */
                    payerClient.SendMoneyBalance(txn.TransactionID, true, new byte[0], txn.PayerEndingBalance, txn.TransactionType, txn.PayerID, false, txn.PayeeID, false, txn.Amount, txn.PartDescription);
                } else {
                    // TODO: consider what this delays while it makes non async call GetBalance from GetUserBalance call get balance
                    int payerBalance = (int)m_apiW.GetUserBalance(txn.PayerID, true, payerClient.Name);
                    payerClient.SendMoneyBalance(txn.TransactionID, true, new byte[0], payerBalance, txn.TransactionType, txn.PayerID, false, txn.PayeeID, false, txn.Amount, txn.PartDescription);
                }
            }
            if ((payeeClient != null) && (txn.PayerID != txn.PayeeID)) {
                // TODO: consider what this delays while it makes non async call GetBalance from GetUserBalance call get balance
                int payeeBalance = (int)m_apiW.GetUserBalance(txn.PayeeID, false, payeeClient.Name);
                payeeClient.SendMoneyBalance(txn.TransactionID, true, new byte[0], payeeBalance, txn.TransactionType, txn.PayerID, false, txn.PayeeID, false, txn.Amount, txn.PartDescription);
            }
        }
        
        #endregion // GMM User Messaging
        
    }
}
