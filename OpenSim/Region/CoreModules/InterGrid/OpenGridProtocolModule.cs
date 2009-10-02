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
using System.Net;
using System.Net.Security;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Web;
using log4net;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using OpenSim.Framework;
using OpenSim.Framework.Capabilities;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using Caps=OpenSim.Framework.Capabilities.Caps;
using OSDArray=OpenMetaverse.StructuredData.OSDArray;
using OSDMap=OpenMetaverse.StructuredData.OSDMap;

namespace OpenSim.Region.CoreModules.InterGrid
{
    public struct OGPState
    {
        public string first_name;
        public string last_name;
        public UUID agent_id;
        public UUID local_agent_id;
        public UUID region_id;
        public uint circuit_code;
        public UUID secure_session_id;
        public UUID session_id;
        public bool agent_access;
        public string sim_access;
        public uint god_level;
        public bool god_overide;
        public bool identified;
        public bool transacted;
        public bool age_verified;
        public bool allow_redirect;
        public int limited_to_estate;
        public string inventory_host;
        public bool src_can_see_mainland;
        public int src_estate_id;
        public int src_version;
        public int src_parent_estate_id;
        public bool visible_to_parent;
        public string teleported_into_region;
    }
    
    public class OpenGridProtocolModule : IRegionModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private List<Scene> m_scene = new List<Scene>();

        private Dictionary<string, AgentCircuitData> CapsLoginID = new Dictionary<string, AgentCircuitData>();
        private Dictionary<UUID, OGPState> m_OGPState = new Dictionary<UUID, OGPState>();
        private Dictionary<string, string> m_loginToRegionState = new Dictionary<string, string>();
        

        private string LastNameSuffix = "_EXTERNAL";
        private string FirstNamePrefix = "";
        private string httpsCN = "";
        private bool httpSSL = false;
        private uint httpsslport = 0;
        private bool GridMode = false;

        #region IRegionModule Members

        public void Initialise(Scene scene, IConfigSource config)
        {
            bool enabled = false;
            IConfig cfg = null;
            IConfig httpcfg = null;
            IConfig startupcfg = null;
            try
            {
                cfg = config.Configs["OpenGridProtocol"];
            } catch (NullReferenceException)
            {
                enabled = false;
            }

            try
            {
                httpcfg = config.Configs["Network"];
            }
            catch (NullReferenceException)
            {
               
            }
            try
            {
                startupcfg = config.Configs["Startup"];
            }
            catch (NullReferenceException)
            {

            }

            if (startupcfg != null)
            {
                GridMode = enabled = startupcfg.GetBoolean("gridmode", false);
            }

            if (cfg != null)
            {
                enabled = cfg.GetBoolean("ogp_enabled", false);
                LastNameSuffix = cfg.GetString("ogp_lastname_suffix", "_EXTERNAL");
                FirstNamePrefix = cfg.GetString("ogp_firstname_prefix", "");
                if (enabled)
                {
                    m_log.Warn("[OGP]: Open Grid Protocol is on, Listening for Clients on /agent/");
                    lock (m_scene)
                    {
                        if (m_scene.Count == 0)
                        {
                            MainServer.Instance.AddLLSDHandler("/agent/", ProcessAgentDomainMessage);
                            MainServer.Instance.AddLLSDHandler("/", ProcessRegionDomainSeed);
                            try
                            {
                                ServicePointManager.ServerCertificateValidationCallback += customXertificateValidation;
                            }
                            catch (NotImplementedException)
                            {
                                try
                                {
#pragma warning disable 0612, 0618
                                    // Mono does not implement the ServicePointManager.ServerCertificateValidationCallback yet!  Don't remove this!
                                    ServicePointManager.CertificatePolicy = new MonoCert();
#pragma warning restore 0612, 0618
                                }
                                catch (Exception)
                                {
                                    m_log.Error("[OGP]: Certificate validation handler change not supported.  You may get ssl certificate validation errors teleporting from your region to some SSL regions.");
                                }
                            }

                        }
                        // can't pick the region 'agent' because it would conflict with our agent domain handler
                        // a zero length region name would conflict with are base region seed cap
                        if (!SceneListDuplicateCheck(scene.RegionInfo.RegionName) && scene.RegionInfo.RegionName.ToLower() != "agent" && scene.RegionInfo.RegionName.Length > 0)
                        {
                            MainServer.Instance.AddLLSDHandler(
                                "/" + HttpUtility.UrlPathEncode(scene.RegionInfo.RegionName.ToLower()),
                                ProcessRegionDomainSeed);
                        }

                        if (!m_scene.Contains(scene))
                            m_scene.Add(scene);
                    }
                }
            }
            lock (m_scene)
            {
                if (m_scene.Count == 1)
                {
                    if (httpcfg != null)
                    {
                        httpSSL = httpcfg.GetBoolean("http_listener_ssl", false);
                        httpsCN = httpcfg.GetString("http_listener_cn", scene.RegionInfo.ExternalHostName);
                        if (httpsCN.Length == 0)
                            httpsCN = scene.RegionInfo.ExternalHostName;
                        httpsslport = (uint)httpcfg.GetInt("http_listener_sslport",((int)scene.RegionInfo.HttpPort + 1));
                    }
                }
            }
        }
        
        public void PostInitialise()
        {
        }

        public void Close()
        {
            //scene.EventManager.OnAvatarEnteringNewParcel -= AvatarEnteringParcel;
        }

        public string Name
        {
            get { return "OpenGridProtocolModule"; }
        }

        public bool IsSharedModule
        {
            get { return true; }
        }

        #endregion

        public OSD ProcessRegionDomainSeed(string path, OSD request, string endpoint)
        {
            string[] pathSegments = path.Split('/');
            
            if (pathSegments.Length <= 1)
            {
                return GenerateNoHandlerMessage();

            }
            
            return GenerateRezAvatarRequestMessage(pathSegments[1]);
            
            

            //m_log.InfoFormat("[OGP]: path {0}, segments {1} segment[1] {2} Last segment {3}",
            //                 path, pathSegments.Length, pathSegments[1], pathSegments[pathSegments.Length - 1]);
            //return new OSDMap();

        }

        public OSD ProcessAgentDomainMessage(string path, OSD request, string endpoint)
        {
            // /agent/*

            string[] pathSegments = path.Split('/');
            if (pathSegments.Length <= 1)
            {
                return GenerateNoHandlerMessage();
                
            }
            if (pathSegments[0].Length == 0 && pathSegments[1].Length == 0)
            {
                return GenerateRezAvatarRequestMessage("");
            }
            m_log.InfoFormat("[OGP]: path {0}, segments {1} segment[1] {2} Last segment {3}",
                             path, pathSegments.Length, pathSegments[1], pathSegments[pathSegments.Length - 1]);

            switch (pathSegments[pathSegments.Length - 1])
            {
                case "rez_avatar":
                    return RezAvatarMethod(path, request);
                    //break;
                case "derez_avatar":
                    return DerezAvatarMethod(path, request);
                    //break;

            }
            if (path.Length < 2)
            {
                return GenerateNoHandlerMessage();
            }

            switch (pathSegments[pathSegments.Length - 2] + "/" + pathSegments[pathSegments.Length - 1])
            {
                case "rez_avatar/rez":
                    return RezAvatarMethod(path, request);
                    //break;
                case "rez_avatar/request":
                    return RequestRezAvatarMethod(path, request);
                case "rez_avatar/place":
                    return RequestRezAvatarMethod(path, request);
                case "rez_avatar/derez":
                    return DerezAvatarMethod(path, request);
                    //break;
                default:
                    return GenerateNoHandlerMessage();
            }
            //return null;
        }

        private OSD GenerateRezAvatarRequestMessage(string regionname)
        {
            Scene region = null;
            bool usedroot = false;

            if (regionname.Length == 0)
            {
                region = GetRootScene();
                usedroot = true;
            }
            else
            {
                region = GetScene(HttpUtility.UrlDecode(regionname).ToLower());
            }

            // this shouldn't happen since we don't listen for a region that is down..   but 
            // it might if the region was taken down or is in the middle of restarting

            if (region == null)
            {
                region = GetRootScene();
                usedroot = true;
            }
            
            UUID statekeeper = UUID.Random();

            
            

            RegionInfo reg = region.RegionInfo;

            OSDMap responseMap = new OSDMap();
            string rezHttpProtocol = "http://";
            //string regionCapsHttpProtocol = "http://";
            string httpaddr = reg.ExternalHostName;
            string urlport = reg.HttpPort.ToString();
            string requestpath = "/agent/" + statekeeper + "/rez_avatar/request";

            if (!usedroot)
            {
                lock (m_loginToRegionState)
                {
                    if (!m_loginToRegionState.ContainsKey(requestpath))
                    {
                        m_loginToRegionState.Add(requestpath, region.RegionInfo.RegionName.ToLower());
                    }
                }
            }

            if (httpSSL)
            {
                rezHttpProtocol = "https://";
                //regionCapsHttpProtocol = "https://";
                urlport = httpsslport.ToString();

                if (httpsCN.Length > 0)
                    httpaddr = httpsCN;
            }

            responseMap["connect"] = OSD.FromBoolean(true);
            OSDMap capabilitiesMap = new OSDMap();
            capabilitiesMap["rez_avatar/request"] = OSD.FromString(rezHttpProtocol + httpaddr + ":" + urlport + requestpath);
            responseMap["capabilities"] = capabilitiesMap;
            
            return responseMap;
        }

        // Using OpenSim.Framework.Capabilities.Caps here one time..
        // so the long name is probably better then a using statement
        public void OnRegisterCaps(UUID agentID, Caps caps)
        {
            /* If we ever want to register our own caps here....
             * 
            string capsBase = "/CAPS/" + caps.CapsObjectPath;
            caps.RegisterHandler("CAPNAME",
                                 new RestStreamHandler("POST", capsBase + CAPSPOSTFIX!,
                                                       delegate(string request, string path, string param,
                                                                OSHttpRequest httpRequest, OSHttpResponse httpResponse)
                                                       {
                                                           return METHODHANDLER(request, path, param,
                                                                                         agentID, caps);
                                         }));
            
             *
             */
        }

        public OSD RequestRezAvatarMethod(string path, OSD request)
        {
            //m_log.Debug("[REQUESTREZAVATAR]: " + request.ToString());

            OSDMap requestMap = (OSDMap)request;


            Scene homeScene = null;

            lock (m_loginToRegionState)
            {
                if (m_loginToRegionState.ContainsKey(path))
                {
                    homeScene = GetScene(m_loginToRegionState[path]);
                    m_loginToRegionState.Remove(path);

                    if (homeScene == null)
                        homeScene = GetRootScene();
                }
                else
                {
                    homeScene = GetRootScene();
                }
            }

            // Homescene is still null, we must have no regions that are up
            if (homeScene == null)
                return GenerateNoHandlerMessage();

            RegionInfo reg = homeScene.RegionInfo;
            ulong regionhandle = GetOSCompatibleRegionHandle(reg);
            //string RegionURI = reg.ServerURI;
            //int RegionPort = (int)reg.HttpPort;

            UUID RemoteAgentID = requestMap["agent_id"].AsUUID();
            
            // will be used in the future.  The client always connects with the aditi agentid currently
            UUID LocalAgentID = RemoteAgentID;

            string FirstName = requestMap["first_name"].AsString();
            string LastName = requestMap["last_name"].AsString();

            FirstName = FirstNamePrefix + FirstName;
            LastName = LastName + LastNameSuffix;

            OGPState userState = GetOGPState(LocalAgentID);

            userState.first_name = requestMap["first_name"].AsString();
            userState.last_name = requestMap["last_name"].AsString();
            userState.age_verified = requestMap["age_verified"].AsBoolean();
            userState.transacted = requestMap["transacted"].AsBoolean();
            userState.agent_access = requestMap["agent_access"].AsBoolean();
            userState.allow_redirect = requestMap["allow_redirect"].AsBoolean();
            userState.identified = requestMap["identified"].AsBoolean();
            userState.god_level = (uint)requestMap["god_level"].AsInteger();
            userState.sim_access = requestMap["sim_access"].AsString();
            userState.agent_id = RemoteAgentID;
            userState.limited_to_estate = requestMap["limited_to_estate"].AsInteger();
            userState.src_can_see_mainland = requestMap["src_can_see_mainland"].AsBoolean();
            userState.src_estate_id = requestMap["src_estate_id"].AsInteger();
            userState.local_agent_id = LocalAgentID;
            userState.teleported_into_region = reg.RegionName.ToLower();

            UpdateOGPState(LocalAgentID, userState);

            OSDMap responseMap = new OSDMap();

            if (RemoteAgentID == UUID.Zero)
            {
                responseMap["connect"] = OSD.FromBoolean(false);
                responseMap["message"] = OSD.FromString("No agent ID was specified in rez_avatar/request");
                m_log.Error("[OGP]: rez_avatar/request failed because no avatar UUID was provided in the request body");
                return responseMap;
            }

            responseMap["sim_host"] = OSD.FromString(reg.ExternalHostName);
            
            // DEPRECATED
            responseMap["sim_ip"] = OSD.FromString(Util.GetHostFromDNS(reg.ExternalHostName).ToString());
            
            responseMap["connect"] = OSD.FromBoolean(true);
            responseMap["sim_port"] = OSD.FromInteger(reg.InternalEndPoint.Port);
            responseMap["region_x"] = OSD.FromInteger(reg.RegionLocX * (uint)Constants.RegionSize); // LLX
            responseMap["region_y"] = OSD.FromInteger(reg.RegionLocY * (uint)Constants.RegionSize); // LLY
            responseMap["region_id"] = OSD.FromUUID(reg.originRegionID);

            if (reg.RegionSettings.Maturity == 1)
            {
                responseMap["sim_access"] = OSD.FromString("Mature");
            }
            else if (reg.RegionSettings.Maturity == 2)
            {
                responseMap["sim_access"] = OSD.FromString("Adult");
            }
            else
            {
                responseMap["sim_access"] = OSD.FromString("PG");
            }

            // Generate a dummy agent for the user so we can get back a CAPS path
            AgentCircuitData agentData = new AgentCircuitData();
            agentData.AgentID = LocalAgentID;
            agentData.BaseFolder = UUID.Zero;
            agentData.CapsPath = CapsUtil.GetRandomCapsObjectPath();
            agentData.child = false;
            agentData.circuitcode = (uint)(Util.RandomClass.Next());
            agentData.firstname = FirstName;
            agentData.lastname = LastName;
            agentData.SecureSessionID = UUID.Random();
            agentData.SessionID = UUID.Random();
            agentData.startpos = new Vector3(128f, 128f, 100f);

            // Pre-Fill our region cache with information on the agent.
            UserAgentData useragent = new UserAgentData();
            useragent.AgentIP = "unknown";
            useragent.AgentOnline = true;
            useragent.AgentPort = (uint)0;
            useragent.Handle = regionhandle;
            useragent.InitialRegion = reg.originRegionID;
            useragent.LoginTime = Util.UnixTimeSinceEpoch();
            useragent.LogoutTime = 0;
            useragent.Position = agentData.startpos;
            useragent.Region = reg.originRegionID;
            useragent.SecureSessionID = agentData.SecureSessionID;
            useragent.SessionID = agentData.SessionID;

            UserProfileData userProfile = new UserProfileData();
            userProfile.AboutText = "OGP User";
            userProfile.CanDoMask = (uint)0;
            userProfile.Created = Util.UnixTimeSinceEpoch();
            userProfile.CurrentAgent = useragent;
            userProfile.CustomType = "OGP";
            userProfile.FirstLifeAboutText = "I'm testing OpenGrid Protocol";
            userProfile.FirstLifeImage = UUID.Zero;
            userProfile.FirstName = agentData.firstname;
            userProfile.GodLevel = 0;
            userProfile.HomeLocation = agentData.startpos;
            userProfile.HomeLocationX = agentData.startpos.X;
            userProfile.HomeLocationY = agentData.startpos.Y;
            userProfile.HomeLocationZ = agentData.startpos.Z;
            userProfile.HomeLookAt = Vector3.Zero;
            userProfile.HomeLookAtX = userProfile.HomeLookAt.X;
            userProfile.HomeLookAtY = userProfile.HomeLookAt.Y;
            userProfile.HomeLookAtZ = userProfile.HomeLookAt.Z;
            userProfile.HomeRegion = reg.RegionHandle;
            userProfile.HomeRegionID = reg.originRegionID;
            userProfile.HomeRegionX = reg.RegionLocX;
            userProfile.HomeRegionY = reg.RegionLocY;
            userProfile.ID = agentData.AgentID;
            userProfile.Image = UUID.Zero;
            userProfile.LastLogin = Util.UnixTimeSinceEpoch();
            userProfile.Partner = UUID.Zero;
            userProfile.PasswordHash = "$1$";
            userProfile.PasswordSalt = "";
            userProfile.RootInventoryFolderID = UUID.Zero;
            userProfile.SurName = agentData.lastname;
            userProfile.UserAssetURI = homeScene.CommsManager.NetworkServersInfo.AssetURL;
            userProfile.UserFlags = 0;
            userProfile.UserInventoryURI = homeScene.CommsManager.NetworkServersInfo.InventoryURL;
            userProfile.WantDoMask = 0;
            userProfile.WebLoginKey = UUID.Random();

            // Do caps registration
            // get seed capagentData.firstname = FirstName;agentData.lastname = LastName;
            if (homeScene.CommsManager.UserService.GetUserProfile(agentData.AgentID) == null && !GridMode)
            {
                homeScene.CommsManager.UserAdminService.AddUser(
                    agentData.firstname, agentData.lastname, CreateRandomStr(7), "", 
                    homeScene.RegionInfo.RegionLocX, homeScene.RegionInfo.RegionLocY, agentData.AgentID);
                
                UserProfileData userProfile2 = homeScene.CommsManager.UserService.GetUserProfile(agentData.AgentID);
                if (userProfile2 != null)
                {
                    userProfile = userProfile2;
                    userProfile.AboutText = "OGP USER";
                    userProfile.FirstLifeAboutText = "OGP USER";
                    homeScene.CommsManager.UserService.UpdateUserProfile(userProfile);
                }
            }
            
            // Stick our data in the cache so the region will know something about us
            homeScene.CommsManager.UserProfileCacheService.PreloadUserCache(userProfile);

            // Call 'new user' event handler
            string reason;
            if (!homeScene.NewUserConnection(agentData, out reason))
            {
                responseMap["connect"] = OSD.FromBoolean(false);
                responseMap["message"] = OSD.FromString(String.Format("Connection refused: {0}", reason));
                m_log.ErrorFormat("[OGP]: rez_avatar/request failed: {0}", reason);
                return responseMap;
            }


            //string raCap = string.Empty;

            UUID AvatarRezCapUUID = LocalAgentID;
            string rezAvatarPath = "/agent/" + AvatarRezCapUUID + "/rez_avatar/rez";
            string derezAvatarPath = "/agent/" + AvatarRezCapUUID + "/rez_avatar/derez";
            // Get a reference to the user's cap so we can pull out the Caps Object Path
            Caps userCap 
                = homeScene.CapsModule.GetCapsHandlerForUser(agentData.AgentID);

            string rezHttpProtocol = "http://";
            string regionCapsHttpProtocol = "http://";
            string httpaddr = reg.ExternalHostName;
            string urlport = reg.HttpPort.ToString();

            if (httpSSL)
            {
                rezHttpProtocol = "https://";
                regionCapsHttpProtocol = "https://";
                urlport = httpsslport.ToString();

                if (httpsCN.Length > 0)
                    httpaddr = httpsCN;
            }
            
            // DEPRECATED
            responseMap["seed_capability"] 
                = OSD.FromString(
                    regionCapsHttpProtocol + httpaddr + ":" + reg.HttpPort + CapsUtil.GetCapsSeedPath(userCap.CapsObjectPath));
            
            // REPLACEMENT
            responseMap["region_seed_capability"] 
                = OSD.FromString(
                    regionCapsHttpProtocol + httpaddr + ":" + reg.HttpPort + CapsUtil.GetCapsSeedPath(userCap.CapsObjectPath));

            responseMap["rez_avatar"] = OSD.FromString(rezHttpProtocol + httpaddr + ":" + urlport + rezAvatarPath);
            responseMap["rez_avatar/rez"] = OSD.FromString(rezHttpProtocol + httpaddr + ":" + urlport + rezAvatarPath);
            responseMap["rez_avatar/derez"] = OSD.FromString(rezHttpProtocol + httpaddr + ":" + urlport + derezAvatarPath);

            // Add the user to the list of CAPS that are outstanding.
            // well allow the caps hosts in this dictionary
            lock (CapsLoginID)
            {
                if (CapsLoginID.ContainsKey(rezAvatarPath))
                {
                    CapsLoginID[rezAvatarPath] = agentData;
                    
                    // This is a joke, if you didn't notice...  It's so unlikely to happen, that I'll print this message if it does occur!
                    m_log.Error("[OGP]: Holy anomoly batman! Caps path already existed!  All the UUID Duplication worries were founded!");
                }
                else
                {
                    CapsLoginID.Add(rezAvatarPath, agentData);
                }
            }
            
            //m_log.Debug("Response:" + responseMap.ToString());
            return responseMap;
        }

        public OSD RezAvatarMethod(string path, OSD request)
        {
            m_log.WarnFormat("[REZAVATAR]: {0}", request.ToString());

            OSDMap responseMap = new OSDMap();

            AgentCircuitData userData = null;

            // Only people we've issued a cap can go further
            if (TryGetAgentCircuitData(path,out userData))
            {
                OSDMap requestMap = (OSDMap)request;

                // take these values to start.  There's a few more
                UUID SecureSessionID=requestMap["secure_session_id"].AsUUID();
                UUID SessionID = requestMap["session_id"].AsUUID();
                int circuitcode = requestMap["circuit_code"].AsInteger();
                OSDArray Parameter = new OSDArray();
                if (requestMap.ContainsKey("parameter"))
                {
                   Parameter = (OSDArray)requestMap["parameter"];
                }

                //int version = 1;
                int estateID = 1;
                int parentEstateID = 1;
                UUID regionID = UUID.Zero;
                bool visibleToParent = true;

                for (int i = 0; i < Parameter.Count; i++)
                {
                    OSDMap item = (OSDMap)Parameter[i];
//                    if (item.ContainsKey("version"))
//                    {
//                        version = item["version"].AsInteger();
//                    }
                    if (item.ContainsKey("estate_id"))
                    {
                        estateID = item["estate_id"].AsInteger();
                    }
                    if (item.ContainsKey("parent_estate_id"))
                    {
                        parentEstateID = item["parent_estate_id"].AsInteger();

                    }
                    if (item.ContainsKey("region_id"))
                    {
                        regionID = item["region_id"].AsUUID();

                    }
                    if (item.ContainsKey("visible_to_parent"))
                    {
                        visibleToParent = item["visible_to_parent"].AsBoolean();
                    }
                }
                //Update our Circuit data with the real values
                userData.SecureSessionID = SecureSessionID;
                userData.SessionID = SessionID;

                OGPState userState = GetOGPState(userData.AgentID);

                // Locate a home scene suitable for the user.
                Scene homeScene = null;

                homeScene = GetScene(userState.teleported_into_region);
                
                if (homeScene == null)
                    homeScene = GetRootScene();

                if (homeScene != null)
                {
                    // Get a referenceokay -  to their Cap object so we can pull out the capobjectroot
                    Caps userCap 
                        = homeScene.CapsModule.GetCapsHandlerForUser(userData.AgentID);

                    //Update the circuit data in the region so this user is authorized
                    homeScene.UpdateCircuitData(userData);
                    homeScene.ChangeCircuitCode(userData.circuitcode,(uint)circuitcode);

                    // Load state
                    

                    // Keep state changes
                    userState.first_name = requestMap["first_name"].AsString();
                    userState.secure_session_id = requestMap["secure_session_id"].AsUUID();
                    userState.age_verified = requestMap["age_verified"].AsBoolean();
                    userState.region_id = homeScene.RegionInfo.originRegionID; // replace 0000000 with our regionid
                    userState.transacted = requestMap["transacted"].AsBoolean();
                    userState.agent_access = requestMap["agent_access"].AsBoolean();
                    userState.inventory_host = requestMap["inventory_host"].AsString();
                    userState.identified = requestMap["identified"].AsBoolean();
                    userState.session_id = requestMap["session_id"].AsUUID();
                    userState.god_level = (uint)requestMap["god_level"].AsInteger();
                    userState.last_name = requestMap["last_name"].AsString();
                    userState.god_overide = requestMap["god_override"].AsBoolean();
                    userState.circuit_code = (uint)requestMap["circuit_code"].AsInteger();
                    userState.limited_to_estate = requestMap["limited_to_estate"].AsInteger();
                    userState.src_estate_id = estateID;
                    userState.region_id = regionID;
                    userState.src_parent_estate_id = parentEstateID;
                    userState.visible_to_parent = visibleToParent;

                    // Save state changes
                    UpdateOGPState(userData.AgentID, userState);

                    // Get the region information for the home region.
                    RegionInfo reg = homeScene.RegionInfo;

                    // Dummy positional and look at info..  we don't have it.
                    OSDArray PositionArray = new OSDArray();
                    PositionArray.Add(OSD.FromInteger(128));
                    PositionArray.Add(OSD.FromInteger(128));
                    PositionArray.Add(OSD.FromInteger(40));

                    OSDArray LookAtArray = new OSDArray();
                    LookAtArray.Add(OSD.FromInteger(1));
                    LookAtArray.Add(OSD.FromInteger(1));
                    LookAtArray.Add(OSD.FromInteger(1));

                    // Our region's X and Y position in OpenSimulator space.
                    uint fooX = reg.RegionLocX;
                    uint fooY = reg.RegionLocY;
                    m_log.InfoFormat("[OGP]: region x({0}) region y({1})", fooX, fooY);
                    m_log.InfoFormat("[OGP]: region http {0} {1}", reg.ServerURI, reg.HttpPort);
                    m_log.InfoFormat("[OGO]: region UUID {0} ", reg.RegionID);

                    // Convert the X and Y position to LL space
                    responseMap["region_x"] = OSD.FromInteger(fooX * (uint)Constants.RegionSize); // convert it to LL X
                    responseMap["region_y"] = OSD.FromInteger(fooY * (uint)Constants.RegionSize); // convert it to LL Y

                    // Give em a new seed capability
                    responseMap["seed_capability"] = OSD.FromString("http://" + reg.ExternalHostName + ":" + reg.HttpPort + "/CAPS/" + userCap.CapsObjectPath + "0000/");
                    responseMap["region"] = OSD.FromUUID(reg.originRegionID);
                    responseMap["look_at"] = LookAtArray;

                    responseMap["sim_port"] = OSD.FromInteger(reg.InternalEndPoint.Port);
                    responseMap["sim_host"] = OSD.FromString(reg.ExternalHostName);// + ":" + reg.InternalEndPoint.Port.ToString());
                    
                    // DEPRECATED
                    responseMap["sim_ip"] = OSD.FromString(Util.GetHostFromDNS(reg.ExternalHostName).ToString());

                    responseMap["session_id"] = OSD.FromUUID(SessionID);
                    responseMap["secure_session_id"] = OSD.FromUUID(SecureSessionID);
                    responseMap["circuit_code"] = OSD.FromInteger(circuitcode);

                    responseMap["position"] = PositionArray;

                    responseMap["region_id"] = OSD.FromUUID(reg.originRegionID);

                    responseMap["sim_access"] = OSD.FromString("Mature");

                    responseMap["connect"] = OSD.FromBoolean(true);

                   

                    m_log.InfoFormat("[OGP]: host: {0}, IP {1}", responseMap["sim_host"].ToString(), responseMap["sim_ip"].ToString());
                }
            }

            return responseMap;
        }

        public OSD DerezAvatarMethod(string path, OSD request)
        {
            m_log.ErrorFormat("DerezPath: {0}, Request: {1}", path, request.ToString());

            //LLSD llsdResponse = null;
            OSDMap responseMap = new OSDMap();

            string[] PathArray = path.Split('/');
            m_log.InfoFormat("[OGP]: prefix {0}, uuid {1}, suffix {2}", PathArray[1], PathArray[2], PathArray[3]);
            string uuidString = PathArray[2];
            m_log.InfoFormat("[OGP]: Request to Derez avatar with UUID {0}", uuidString);
            UUID userUUID = UUID.Zero;
            if (UUID.TryParse(uuidString, out userUUID))
            {
                UUID RemoteID = (UUID)uuidString;
                UUID LocalID = RemoteID;
                // FIXME: TODO: Routine to map RemoteUUIDs to LocalUUIds
                //         would be done already..  but the client connects with the Aditi UUID
                //         regardless over the UDP stack

                OGPState userState = GetOGPState(LocalID);
                if (userState.agent_id != UUID.Zero)
                {
                    //OSDMap outboundRequestMap = new OSDMap();
                    OSDMap inboundRequestMap = (OSDMap)request;
                    string rezAvatarString = inboundRequestMap["rez_avatar"].AsString();
                    if (rezAvatarString.Length == 0)
                    {
                        rezAvatarString = inboundRequestMap["rez_avatar/rez"].AsString();
                    }
                    OSDArray LookAtArray = new OSDArray();
                    LookAtArray.Add(OSD.FromInteger(1));
                    LookAtArray.Add(OSD.FromInteger(1));
                    LookAtArray.Add(OSD.FromInteger(1));

                    OSDArray PositionArray = new OSDArray();
                    PositionArray.Add(OSD.FromInteger(128));
                    PositionArray.Add(OSD.FromInteger(128));
                    PositionArray.Add(OSD.FromInteger(40));

                    OSDArray lookArray = new OSDArray();
                    lookArray.Add(OSD.FromInteger(128));
                    lookArray.Add(OSD.FromInteger(128));
                    lookArray.Add(OSD.FromInteger(40));

                    responseMap["connect"] = OSD.FromBoolean(true);// it's okay to give this user up
                    responseMap["look_at"] = LookAtArray;

                    m_log.WarnFormat("[OGP]: Invoking rez_avatar on host:{0} for avatar: {1} {2}", rezAvatarString, userState.first_name, userState.last_name);

                    OSDMap rezResponseMap = invokeRezAvatarCap(responseMap, rezAvatarString,userState);

                    // If invoking it returned an error, parse and end
                    if (rezResponseMap.ContainsKey("connect"))
                    {
                        if (rezResponseMap["connect"].AsBoolean() == false)
                        {
                            return responseMap;
                        }
                    }

                    string rezRespSeedCap = "";

                    // DEPRECATED
                    if (rezResponseMap.ContainsKey("seed_capability"))
                        rezRespSeedCap = rezResponseMap["seed_capability"].AsString();
                    
                    // REPLACEMENT
                    if (rezResponseMap.ContainsKey("region_seed_capability"))
                        rezRespSeedCap = rezResponseMap["region_seed_capability"].AsString();

                    // REPLACEMENT
                    if (rezResponseMap.ContainsKey("rez_avatar/rez"))
                        rezRespSeedCap = rezResponseMap["rez_avatar/rez"].AsString();

                    // DEPRECATED
                    string rezRespSim_ip = rezResponseMap["sim_ip"].AsString();
                    
                    string rezRespSim_host = rezResponseMap["sim_host"].AsString();

                    int rrPort = rezResponseMap["sim_port"].AsInteger();
                    int rrX = rezResponseMap["region_x"].AsInteger();
                    int rrY = rezResponseMap["region_y"].AsInteger();
                    m_log.ErrorFormat("X:{0}, Y:{1}", rrX, rrY);
                    UUID rrRID = rezResponseMap["region_id"].AsUUID();
                    OSDArray RezResponsePositionArray = null;
                    string rrAccess = rezResponseMap["sim_access"].AsString();
                    if (rezResponseMap.ContainsKey("position"))
                    {
                        RezResponsePositionArray = (OSDArray)rezResponseMap["position"];
                    }
                    // DEPRECATED
                    responseMap["seed_capability"] = OSD.FromString(rezRespSeedCap);
                    
                    // REPLACEMENT r3
                    responseMap["region_seed_capability"] = OSD.FromString(rezRespSeedCap);

                    // DEPRECATED
                    responseMap["sim_ip"] = OSD.FromString(Util.GetHostFromDNS(rezRespSim_ip).ToString());
                    
                    responseMap["sim_host"] = OSD.FromString(rezRespSim_host);
                    responseMap["sim_port"] = OSD.FromInteger(rrPort);
                    responseMap["region_x"] = OSD.FromInteger(rrX);
                    responseMap["region_y"] = OSD.FromInteger(rrY);
                    responseMap["region_id"] = OSD.FromUUID(rrRID);
                    responseMap["sim_access"] = OSD.FromString(rrAccess);

                    if (RezResponsePositionArray != null)
                    {
                        responseMap["position"] = RezResponsePositionArray;
                    }
                    responseMap["look_at"] = lookArray;
                    responseMap["connect"] = OSD.FromBoolean(true);

                    ShutdownConnection(LocalID,this);
                    // PLEASE STOP CHANGING THIS TO an M_LOG, M_LOG DOESN'T WORK ON MULTILINE .TOSTRINGS
                    Console.WriteLine("RESPONSEDEREZ: " + responseMap.ToString());
                    return responseMap;
                }
                else
                {
                    return GenerateNoStateMessage(LocalID);
                }
            }
            else
            {
                return GenerateNoHandlerMessage();
            }

            //return responseMap;
        }

        private OSDMap invokeRezAvatarCap(OSDMap responseMap, string CapAddress, OGPState userState)
        {
            Scene reg = GetRootScene();

            WebRequest DeRezRequest = WebRequest.Create(CapAddress);
            DeRezRequest.Method = "POST";
            DeRezRequest.ContentType = "application/xml+llsd";

            OSDMap RAMap = new OSDMap();
            OSDMap AgentParms = new OSDMap();
            OSDMap RegionParms = new OSDMap();

            OSDArray Parameter = new OSDArray(2);

            OSDMap version = new OSDMap();
            version["version"] = OSD.FromInteger(userState.src_version);
            Parameter.Add(version);

            OSDMap SrcData = new OSDMap();
            SrcData["estate_id"] = OSD.FromInteger(reg.RegionInfo.EstateSettings.EstateID);
            SrcData["parent_estate_id"] = OSD.FromInteger((reg.RegionInfo.EstateSettings.ParentEstateID == 100 ? 1 : reg.RegionInfo.EstateSettings.ParentEstateID));
            SrcData["region_id"] = OSD.FromUUID(reg.RegionInfo.originRegionID);
            SrcData["visible_to_parent"] = OSD.FromBoolean(userState.visible_to_parent);
            Parameter.Add(SrcData);

            AgentParms["first_name"] = OSD.FromString(userState.first_name);
            AgentParms["last_name"] = OSD.FromString(userState.last_name);
            AgentParms["agent_id"] = OSD.FromUUID(userState.agent_id);
            RegionParms["region_id"] = OSD.FromUUID(userState.region_id);
            AgentParms["circuit_code"] = OSD.FromInteger(userState.circuit_code);
            AgentParms["secure_session_id"] = OSD.FromUUID(userState.secure_session_id);
            AgentParms["session_id"] = OSD.FromUUID(userState.session_id);
            AgentParms["agent_access"] = OSD.FromBoolean(userState.agent_access);
            AgentParms["god_level"] = OSD.FromInteger(userState.god_level);
            AgentParms["god_overide"] = OSD.FromBoolean(userState.god_overide);
            AgentParms["identified"] = OSD.FromBoolean(userState.identified);
            AgentParms["transacted"] = OSD.FromBoolean(userState.transacted);
            AgentParms["age_verified"] = OSD.FromBoolean(userState.age_verified);
            AgentParms["limited_to_estate"] = OSD.FromInteger(userState.limited_to_estate);
            AgentParms["inventory_host"] = OSD.FromString(userState.inventory_host);

            // version 1
            RAMap = AgentParms;

            // Planned for version 2
            // RAMap["agent_params"] = AgentParms;

            RAMap["region_params"] = RegionParms;
            RAMap["parameter"] = Parameter;

            string RAMapString = RAMap.ToString();
            m_log.InfoFormat("[OGP] RAMap string {0}", RAMapString);
            OSD LLSDofRAMap = RAMap; // RENAME if this works

            m_log.InfoFormat("[OGP]: LLSD of map as string  was {0}", LLSDofRAMap.ToString());
            //m_log.InfoFormat("[OGP]: LLSD+XML: {0}", LLSDParser.SerializeXmlString(LLSDofRAMap));
            byte[] buffer = OSDParser.SerializeLLSDXmlBytes(LLSDofRAMap);

            //string bufferDump = System.Text.Encoding.ASCII.GetString(buffer);
            //m_log.InfoFormat("[OGP]: buffer form is {0}",bufferDump);
            //m_log.InfoFormat("[OGP]: LLSD of map was {0}",buffer.Length);

            Stream os = null;
            try
            { // send the Post
                DeRezRequest.ContentLength = buffer.Length;   //Count bytes to send
                os = DeRezRequest.GetRequestStream();
                os.Write(buffer, 0, buffer.Length);         //Send it
                os.Close();
                m_log.InfoFormat("[OGP]: Derez Avatar Posted Rez Avatar request to remote sim {0}", CapAddress);
            }
            catch (WebException ex)
            {
                m_log.InfoFormat("[OGP] Bad send on de_rez_avatar {0}", ex.Message);
                responseMap["connect"] = OSD.FromBoolean(false);

                return responseMap;
            }

            m_log.Info("[OGP] waiting for a reply after rez avatar send");
            string rez_avatar_reply = null;
            { // get the response
                try
                {
                    WebResponse webResponse = DeRezRequest.GetResponse();
                    if (webResponse == null)
                    {
                        m_log.Info("[OGP:] Null reply on rez_avatar post");
                    }

                    StreamReader sr = new StreamReader(webResponse.GetResponseStream());
                    rez_avatar_reply = sr.ReadToEnd().Trim();
                    m_log.InfoFormat("[OGP]: rez_avatar reply was {0} ", rez_avatar_reply);

                }
                catch (WebException ex)
                {
                    m_log.InfoFormat("[OGP]: exception on read after send of rez avatar {0}", ex.Message);
                    responseMap["connect"] = OSD.FromBoolean(false);

                    return responseMap;
                }
                OSD rezResponse = null;
                try
                {
                    rezResponse = OSDParser.DeserializeLLSDXml(rez_avatar_reply);

                    responseMap = (OSDMap)rezResponse;
                }
                catch (Exception ex)
                {
                    m_log.InfoFormat("[OGP]: exception on parse of rez reply {0}", ex.Message);
                    responseMap["connect"] = OSD.FromBoolean(false);

                    return responseMap;
                }
            }
            return responseMap;
        }

        public OSD GenerateNoHandlerMessage()
        {
            OSDMap map = new OSDMap();
            map["reason"] = OSD.FromString("LLSDRequest");
            map["message"] = OSD.FromString("No handler registered for LLSD Requests");
            map["login"] = OSD.FromString("false");
            map["connect"] = OSD.FromString("false");
            return map;
        }
        public OSD GenerateNoStateMessage(UUID passedAvatar)
        {
            OSDMap map = new OSDMap();
            map["reason"] = OSD.FromString("derez failed");
            map["message"] = OSD.FromString("Unable to locate OGP state for avatar " + passedAvatar.ToString());
            map["login"] = OSD.FromString("false");
            map["connect"] = OSD.FromString("false");
            return map;
        }
        private bool TryGetAgentCircuitData(string path, out AgentCircuitData userdata)
        {
            userdata = null;
            lock (CapsLoginID)
            {
                if (CapsLoginID.ContainsKey(path))
                {
                    userdata = CapsLoginID[path];
                    DiscardUsedCap(path);
                    return true;
                }
            }
            return false;
        }

        private void DiscardUsedCap(string path)
        {
            CapsLoginID.Remove(path);
        }

        private Scene GetRootScene()
        {
            Scene ReturnScene = null;
            lock (m_scene)
            {
                if (m_scene.Count > 0)
                {
                    ReturnScene = m_scene[0];
                }
            }

            return ReturnScene;
        }

        private Scene GetScene(string scenename)
        {
            Scene ReturnScene = null;
            lock (m_scene)
            {
                foreach (Scene s in m_scene)
                {
                    if (s.RegionInfo.RegionName.ToLower() == scenename)
                    {
                        ReturnScene = s;
                        break;
                    }
                }
            }

            return ReturnScene;
        }

        private ulong GetOSCompatibleRegionHandle(RegionInfo reg)
        {
            return Util.UIntsToLong(reg.RegionLocX, reg.RegionLocY);
        }

        private OGPState InitializeNewState()
        {
            OGPState returnState = new OGPState();
            returnState.first_name = "";
            returnState.last_name = "";
            returnState.agent_id = UUID.Zero;
            returnState.local_agent_id = UUID.Zero;
            returnState.region_id = UUID.Zero;
            returnState.circuit_code = 0;
            returnState.secure_session_id = UUID.Zero;
            returnState.session_id = UUID.Zero;
            returnState.agent_access = true;
            returnState.god_level = 0;
            returnState.god_overide = false;
            returnState.identified = false;
            returnState.transacted = false;
            returnState.age_verified = false;
            returnState.limited_to_estate = 1;
            returnState.inventory_host = "http://inv4.mysql.aditi.lindenlab.com";
            returnState.allow_redirect = true;
            returnState.sim_access = "";
            returnState.src_can_see_mainland = true;
            returnState.src_estate_id = 1;
            returnState.src_version = 1;
            returnState.src_parent_estate_id = 1;
            returnState.visible_to_parent = true;
            returnState.teleported_into_region = "";

            return returnState;
        }

        private OGPState GetOGPState(UUID agentId)
        {
            lock (m_OGPState)
            {
                if (m_OGPState.ContainsKey(agentId))
                {
                    return m_OGPState[agentId];
                }
                else
                {
                    return InitializeNewState();
                }
            }
        }

        public void DeleteOGPState(UUID agentId)
        {
            lock (m_OGPState)
            {
                if (m_OGPState.ContainsKey(agentId))
                    m_OGPState.Remove(agentId);
            }
        }

        private void UpdateOGPState(UUID agentId, OGPState state)
        {
            lock (m_OGPState)
            {
                if (m_OGPState.ContainsKey(agentId))
                {
                    m_OGPState[agentId] = state;
                }
                else
                {
                    m_OGPState.Add(agentId,state);
                }
            }
        }
        private bool SceneListDuplicateCheck(string str)
        {
            // no lock, called from locked space!
            bool found = false;
            
            foreach (Scene s in m_scene)
            {
                if (s.RegionInfo.RegionName == str)
                {
                    found = true;
                    break;
                }
            }

            return found;
        }

        public void ShutdownConnection(UUID avatarId, OpenGridProtocolModule mod)
        {
            Scene homeScene = GetRootScene();
            ScenePresence avatar = null;
            if (homeScene.TryGetAvatar(avatarId,out avatar))
            {
                KillAUser ku = new KillAUser(avatar,mod);
                Thread ta = new Thread(ku.ShutdownNoLogout);
                ta.IsBackground = true;
                ta.Name = "ShutdownThread";
                ta.Start();
            }
        }

        private string CreateRandomStr(int len)
        {
            Random rnd = new Random(Environment.TickCount);
            string returnstring = "";
            string chars = "abcdefghijklmnopqrstuvwxyz0123456789";

            for (int i = 0; i < len; i++)
            {
                returnstring += chars.Substring(rnd.Next(chars.Length), 1);
            }
            return returnstring;
        }
        // Temporary hack to allow teleporting to and from Vaak
        private static bool customXertificateValidation(object sender, X509Certificate cert, X509Chain chain, SslPolicyErrors error)
        {
            //if (cert.Subject == "E=root@lindenlab.com, CN=*.vaak.lindenlab.com, O=\"Linden Lab, Inc.\", L=San Francisco, S=California, C=US")
            //{
                return true;
            //}

            //return false;
        }
    }

    public class KillAUser
    {
        private ScenePresence avToBeKilled = null;
        private OpenGridProtocolModule m_mod = null;

        public KillAUser(ScenePresence avatar, OpenGridProtocolModule mod)
        {
            avToBeKilled = avatar;
            m_mod = mod;
        }

        public void ShutdownNoLogout()
        {
            UUID avUUID = UUID.Zero;

            if (avToBeKilled != null)
            {
                avUUID = avToBeKilled.UUID;
                avToBeKilled.MakeChildAgent();

                avToBeKilled.ControllingClient.SendLogoutPacketWhenClosing = false;

                Thread.Sleep(30000);

                // test for child agent because they might have come back
                if (avToBeKilled.IsChildAgent)
                {
                    m_mod.DeleteOGPState(avUUID);
                    avToBeKilled.ControllingClient.Close(true);
                }
            }
        }
        
    }
    
    public class MonoCert : ICertificatePolicy
    {
        #region ICertificatePolicy Members

        public bool CheckValidationResult(ServicePoint srvPoint, X509Certificate certificate, WebRequest request, int certificateProblem)
        {
            return true;
        }

        #endregion
    }
}
