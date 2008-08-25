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
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;

using libsecondlife;
using libsecondlife.StructuredData;

using log4net;
using Nini.Config;
using Nwc.XmlRpc;

using OpenSim.Framework;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Scenes;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Framework.Communications.Capabilities;
using OpenSim.Framework.Statistics;
using LLSD = libsecondlife.StructuredData.LLSD;
using LLSDMap = libsecondlife.StructuredData.LLSDMap;
using LLSDArray = libsecondlife.StructuredData.LLSDArray;

namespace OpenSim.Region.Environment.Modules.InterGrid
{
    public struct OGPState
    {
        public string first_name;
        public string last_name;
        public LLUUID agent_id;
        public LLUUID local_agent_id;
        public LLUUID region_id;
        public uint circuit_code;
        public LLUUID secure_session_id;
        public LLUUID session_id;
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
    }


    public class OpenGridProtocolModule : IRegionModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private List<Scene> m_scene = new List<Scene>();
        
        private Dictionary<string, AgentCircuitData> CapsLoginID = new Dictionary<string, AgentCircuitData>();
        private Dictionary<LLUUID, OGPState> m_OGPState = new Dictionary<LLUUID, OGPState>();
        
        

        #region IRegionModule Members

        public void Initialise(Scene scene, IConfigSource config)
        {
            bool enabled = false;
            IConfig cfg = null;
            try
            {
                cfg = config.Configs["OpenGridProtocol"];
            } catch (NullReferenceException)
            {
                enabled = false;
            }
            if (cfg != null)
            {
                enabled = cfg.GetBoolean("ogp_enabled", false);
                if (enabled)
                {
                    m_log.Warn("[OGP]: Open Grid Protocol is on, Listening for Clients on /agent/");
                    lock (m_scene)
                    {
                        if (m_scene.Count == 0)
                        {
                            scene.AddLLSDHandler("/agent/", ProcessAgentDomainMessage);
                        }

                        if (!m_scene.Contains(scene))
                            m_scene.Add(scene);
                    }
                }
            }
            // Of interest to this module potentially
            //scene.EventManager.OnNewClient += OnNewClient;
            //scene.EventManager.OnGridInstantMessageToFriendsModule += OnGridInstantMessage;
            //scene.EventManager.OnAvatarEnteringNewParcel += AvatarEnteringParcel;
            //scene.EventManager.OnMakeChildAgent += MakeChildAgent;
            //scene.EventManager.OnClientClosed += ClientLoggedOut;
        }

        public void PostInitialise()
        {
        }

        public void Close()
        {
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

        public LLSD ProcessAgentDomainMessage(string path, LLSD request, string endpoint)
        {
            // /agent/*

            string[] pathSegments = path.Split('/');
            if (pathSegments.Length < 1)
            {
                return GenerateNoHandlerMessage();
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
                    //break;
                default:
                    return GenerateNoHandlerMessage();
            }
            //return null;
        }

        public LLSD RequestRezAvatarMethod(string path, LLSD request)
        {
            m_log.WarnFormat("[REQUESTREZAVATAR]: {0}", request.ToString());

            LLSDMap requestMap = (LLSDMap)request;
            
            Scene homeScene = GetRootScene();
            
           

            if (homeScene == null)
                return GenerateNoHandlerMessage();


            RegionInfo reg = homeScene.RegionInfo;
            ulong regionhandle = GetOSCompatibleRegionHandle(reg);
            string RegionURI = reg.ServerURI;
            int RegionPort = (int)reg.HttpPort;

            LLUUID RemoteAgentID = requestMap["agent_id"].AsUUID();

            // will be used in the future.  The client always connects with the aditi agentid currently
            LLUUID LocalAgentID = RemoteAgentID; 

            string FirstName = requestMap["first_name"].AsString();
            string LastName = requestMap["last_name"].AsString();


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

            UpdateOGPState(LocalAgentID, userState);

            LLSDMap responseMap = new LLSDMap();
            responseMap["sim_host"] = LLSD.FromString(Util.GetHostFromDNS(reg.ExternalHostName).ToString());
            responseMap["sim_ip"] = LLSD.FromString(Util.GetHostFromDNS(reg.ExternalHostName).ToString());
            responseMap["connect"] = LLSD.FromBoolean(true);
            responseMap["sim_port"] = LLSD.FromInteger(reg.InternalEndPoint.Port);
            responseMap["region_x"] = LLSD.FromInteger(reg.RegionLocX * (uint)Constants.RegionSize); // LLX
            responseMap["region_Y"] = LLSD.FromInteger(reg.RegionLocY * (uint)Constants.RegionSize); // LLY
            responseMap["region_id"] = LLSD.FromUUID(reg.originRegionID);
            responseMap["sim_access"] = LLSD.FromString((reg.RegionSettings.Maturity == 1) ? "Mature" : "PG");
            
            // Generate a dummy agent for the user so we can get back a CAPS path
            AgentCircuitData agentData = new AgentCircuitData();
            agentData.AgentID = LocalAgentID;
            agentData.BaseFolder=LLUUID.Zero;
            agentData.CapsPath=Util.GetRandomCapsPath();
            agentData.child = false;
            agentData.circuitcode = (uint)(Util.RandomClass.Next());
            agentData.firstname = FirstName;
            agentData.lastname = LastName;
            agentData.SecureSessionID=LLUUID.Random();
            agentData.SessionID=LLUUID.Random();
            agentData.startpos=LLVector3.Zero;

            // Pre-Fill our region cache with information on the agent.
            UserAgentData useragent = new UserAgentData();
            useragent.AgentIP="unknown";
            useragent.AgentOnline=true;
            useragent.AgentPort = (uint)0;
            useragent.Handle = regionhandle;
            useragent.InitialRegion = reg.originRegionID;
            useragent.LoginTime=Util.UnixTimeSinceEpoch();
            useragent.LogoutTime = 0;
            useragent.Position=agentData.startpos;
            useragent.PositionX=agentData.startpos.X;
            useragent.PositionY=agentData.startpos.Y;
            useragent.PositionZ=agentData.startpos.Z;
            useragent.Region=reg.originRegionID;
            useragent.SecureSessionID=agentData.SecureSessionID;
            useragent.SessionID = agentData.SessionID;

            
            UserProfileData userProfile = new UserProfileData();
            userProfile.AboutText = "OGP User";
            userProfile.CanDoMask = (uint)0;
            userProfile.Created = Util.UnixTimeSinceEpoch();
            userProfile.CurrentAgent = useragent;
            userProfile.CustomType = "OGP";
            userProfile.FirstLifeAboutText = "I'm testing OpenGrid Protocol";
            userProfile.FirstLifeImage = LLUUID.Zero;
            userProfile.FirstName = agentData.firstname;
            userProfile.GodLevel = 0;
            userProfile.HomeLocation = agentData.startpos;
            userProfile.HomeLocationX = agentData.startpos.X;
            userProfile.HomeLocationY = agentData.startpos.Y;
            userProfile.HomeLocationZ = agentData.startpos.Z;
            userProfile.HomeLookAt = LLVector3.Zero;
            userProfile.HomeLookAtX = userProfile.HomeLookAt.X;
            userProfile.HomeLookAtY = userProfile.HomeLookAt.Y;
            userProfile.HomeLookAtZ = userProfile.HomeLookAt.Z;
            userProfile.HomeRegion = reg.RegionHandle;
            userProfile.HomeRegionID = reg.originRegionID;
            userProfile.HomeRegionX = reg.RegionLocX;
            userProfile.HomeRegionY = reg.RegionLocY;
            userProfile.ID = agentData.AgentID;
            userProfile.Image = LLUUID.Zero;
            userProfile.LastLogin = Util.UnixTimeSinceEpoch();
            userProfile.Partner = LLUUID.Zero;
            userProfile.PasswordHash = "$1$";
            userProfile.PasswordSalt = "";
            userProfile.RootInventoryFolderID = LLUUID.Zero;
            userProfile.SurName = agentData.lastname;
            userProfile.UserAssetURI = homeScene.CommsManager.NetworkServersInfo.AssetURL;
            userProfile.UserFlags = 0;
            userProfile.UserInventoryURI = homeScene.CommsManager.NetworkServersInfo.InventoryURL;
            userProfile.WantDoMask = 0;
            userProfile.WebLoginKey = LLUUID.Random();

            // Do caps registration
            // get seed cap
            
            
            // Stick our data in the cache so the region will know something about us
            homeScene.CommsManager.UserProfileCacheService.PreloadUserCache(agentData.AgentID, userProfile);

            // Call 'new user' event handler
            homeScene.NewUserConnection(reg.RegionHandle, agentData);


            string raCap = string.Empty;

            LLUUID AvatarRezCapUUID = LLUUID.Random();
            string rezAvatarPath = "/agent/" + AvatarRezCapUUID + "/rez_avatar";

            // Get a reference to the user's cap so we can pull out the Caps Object Path
            OpenSim.Framework.Communications.Capabilities.Caps userCap = homeScene.GetCapsHandlerForUser(agentData.AgentID);

            responseMap["seed_capability"] = LLSD.FromString("http://" + reg.ExternalHostName + ":" + reg.HttpPort + "/CAPS/" + userCap.CapsObjectPath + "0000/");

            responseMap["rez_avatar/rez"] = LLSD.FromString("http://" + reg.ExternalHostName + ":" + reg.HttpPort + rezAvatarPath);

            // Add the user to the list of CAPS that are outstanding.
            // well allow the caps hosts in this dictionary
            lock (CapsLoginID)
            {
                if (CapsLoginID.ContainsKey(rezAvatarPath))
                {
                    m_log.Error("[OGP]: Holy anomoly batman! Caps path already existed!  All the UUID Duplication worries were founded!");
                }
                else
                {
                    CapsLoginID.Add(rezAvatarPath, agentData);
                }
            }

            return responseMap;
        }

        public LLSD RezAvatarMethod(string path, LLSD request)
        {
            m_log.WarnFormat("[REZAVATAR]: {0}", request.ToString());
            
            LLSDMap responseMap = new LLSDMap();
            
            AgentCircuitData userData = null;
            
            // Only people we've issued a cap can go further
            if (TryGetAgentCircuitData(path,out userData))
            {
                LLSDMap requestMap = (LLSDMap)request;
                
                // take these values to start.  There's a few more
                LLUUID SecureSessionID=requestMap["secure_session_id"].AsUUID();
                LLUUID SessionID = requestMap["session_id"].AsUUID();
                int circuitcode = requestMap["circuit_code"].AsInteger();

                //Update our Circuit data with the real values
                userData.SecureSessionID = SecureSessionID;
                userData.SessionID = SessionID;

                // Locate a home scene suitable for the user.
                Scene homeScene = GetRootScene();
                
                if (homeScene != null)
                {
                    // Get a reference to their Cap object so we can pull out the capobjectroot
                    OpenSim.Framework.Communications.Capabilities.Caps userCap = 
                                                homeScene.GetCapsHandlerForUser(userData.AgentID);

                    //Update the circuit data in the region so this user is authorized
                    homeScene.UpdateCircuitData(userData);
                    homeScene.ChangeCircuitCode(userData.circuitcode,(uint)circuitcode);

                    // Load state
                    OGPState userState = GetOGPState(userData.AgentID);

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
                    
                    // Save state changes
                    UpdateOGPState(userData.AgentID, userState);

                    // Get the region information for the home region.
                    RegionInfo reg = homeScene.RegionInfo;

                    // Dummy positional and look at info..  we don't have it.
                    LLSDArray PositionArray = new LLSDArray();
                    PositionArray.Add(LLSD.FromInteger(128));
                    PositionArray.Add(LLSD.FromInteger(128));
                    PositionArray.Add(LLSD.FromInteger(40)); 
 
                    LLSDArray LookAtArray = new LLSDArray();
                    LookAtArray.Add(LLSD.FromInteger(1));
                    LookAtArray.Add(LLSD.FromInteger(1));
                    LookAtArray.Add(LLSD.FromInteger(1));

                    // Our region's X and Y position in OpenSimulator space.
                    uint fooX = reg.RegionLocX;
                    uint fooY = reg.RegionLocY;
                    m_log.InfoFormat("[OGP]: region x({0}) region y({1})", fooX, fooY);
                    m_log.InfoFormat("[OGP]: region http {0} {1}", reg.ServerURI, reg.HttpPort);
                    m_log.InfoFormat("[OGO]: region UUID {0} ", reg.RegionID);

                    // Convert the X and Y position to LL space
                    responseMap["region_x"] = LLSD.FromInteger(fooX * (uint)Constants.RegionSize); // convert it to LL X
                    responseMap["region_y"] = LLSD.FromInteger(fooY * (uint)Constants.RegionSize); // convert it to LL Y

                    // Give em a new seed capability
                    responseMap["seed_capability"] = LLSD.FromString("http://" + reg.ExternalHostName + ":" + reg.HttpPort + "/CAPS/" + userCap.CapsObjectPath + "0000/");
                    responseMap["region"] = LLSD.FromUUID(reg.originRegionID);
                    responseMap["look_at"] = LookAtArray;
                    
                    responseMap["sim_port"] = LLSD.FromInteger(reg.InternalEndPoint.Port);
                    responseMap["sim_host"] = LLSD.FromString(Util.GetHostFromDNS(reg.ExternalHostName).ToString());// + ":" + reg.InternalEndPoint.Port.ToString());
                    responseMap["sim_ip"] = LLSD.FromString(Util.GetHostFromDNS(reg.ExternalHostName).ToString());
                    
                    responseMap["session_id"] = LLSD.FromUUID(SessionID);
                    responseMap["secure_session_id"] = LLSD.FromUUID(SecureSessionID);
                    responseMap["circuit_code"] = LLSD.FromInteger(circuitcode);
                    
                    responseMap["position"] = PositionArray;
                    
                    responseMap["region_id"] = LLSD.FromUUID(reg.originRegionID);
                    
                    responseMap["sim_access"] = LLSD.FromString("Mature");
                    
                    responseMap["connect"] = LLSD.FromBoolean(true);

                    m_log.InfoFormat("[OGP]: host: {0}, IP {1}", responseMap["sim_host"].ToString(), responseMap["sim_ip"].ToString());

                }

            }

            return responseMap; 
        }

   

        public LLSD DerezAvatarMethod(string path, LLSD request)
        {


            m_log.ErrorFormat("DerezPath: {0}, Request: {1}", path, request.ToString());
            
            LLSD llsdResponse = null;
            LLSDMap responseMap = new LLSDMap();
            


            string[] PathArray = path.Split('/');
            m_log.InfoFormat("[OGP]: prefix {0}, uuid {1}, suffix {2}", PathArray[1], PathArray[2], PathArray[3]);
            string uuidString = PathArray[2];
            m_log.InfoFormat("[OGP]: Request to Derez avatar with UUID {0}", uuidString);
            LLUUID userUUID = LLUUID.Zero;
            if (Helpers.TryParse(uuidString, out userUUID))
            {

                LLUUID RemoteID = uuidString;
                LLUUID LocalID = RemoteID;
                // FIXME: TODO: Routine to map RemoteUUIDs to LocalUUIds
                //         would be done already..  but the client connects with the Aditi UUID 
                //         regardless over the UDP stack

                OGPState userState = GetOGPState(LocalID);
                if (userState.agent_id != LLUUID.Zero)
                {

                    LLSDMap outboundRequestMap = new LLSDMap();
                    LLSDMap inboundRequestMap = (LLSDMap)request;
                    string rezAvatarString = inboundRequestMap["rez_avatar"].AsString();

                    LLSDArray LookAtArray = new LLSDArray();
                    LookAtArray.Add(LLSD.FromInteger(1));
                    LookAtArray.Add(LLSD.FromInteger(1));
                    LookAtArray.Add(LLSD.FromInteger(1));


                    LLSDArray PositionArray = new LLSDArray();
                    PositionArray.Add(LLSD.FromInteger(128));
                    PositionArray.Add(LLSD.FromInteger(128));
                    PositionArray.Add(LLSD.FromInteger(40));
                    
                    LLSDArray lookArray = new LLSDArray();
                    lookArray.Add(LLSD.FromInteger(128));
                    lookArray.Add(LLSD.FromInteger(128));
                    lookArray.Add(LLSD.FromInteger(40));

                    responseMap["connect"] = LLSD.FromBoolean(true);// it's okay to give this user up
                    responseMap["look_at"] = LookAtArray;
                    
                    m_log.WarnFormat("[OGP]: Invoking rez_avatar on host:{0} for avatar: {1} {2}", rezAvatarString, userState.first_name, userState.last_name);

                    LLSDMap rezResponseMap = invokeRezAvatarCap(responseMap, rezAvatarString,userState);
                    
                    // If invoking it returned an error, parse and end
                    if (rezResponseMap.ContainsKey("connect"))
                    {
                        if (rezResponseMap["connect"].AsBoolean() == false)
                        {
                            return responseMap;
                        }
                    }

                    string rezRespSeedCap = rezResponseMap["seed_capability"].AsString();
                    string rezRespSim_ip = rezResponseMap["sim_ip"].AsString();
                    string rezRespSim_host = rezResponseMap["sim_host"].AsString();
                    
                    int rrPort = rezResponseMap["sim_port"].AsInteger();
                    int rrX = rezResponseMap["region_x"].AsInteger();
                    int rrY = rezResponseMap["region_y"].AsInteger();
                    m_log.ErrorFormat("X:{0}, Y:{1}", rrX, rrY);
                    LLUUID rrRID = rezResponseMap["region_id"].AsUUID();
                    
                    string rrAccess = rezResponseMap["sim_access"].AsString();
                    
                    LLSDArray RezResponsePositionArray = (LLSDArray)rezResponseMap["position"];

                    responseMap["seed_capability"] = LLSD.FromString(rezRespSeedCap);
                    responseMap["sim_ip"] = LLSD.FromString(Util.GetHostFromDNS(rezRespSim_ip).ToString());
                    responseMap["sim_host"] = LLSD.FromString(Util.GetHostFromDNS(rezRespSim_host).ToString());
                    responseMap["sim_port"] = LLSD.FromInteger(rrPort);
                    responseMap["region_x"] = LLSD.FromInteger(rrX );
                    responseMap["region_y"] = LLSD.FromInteger(rrY );
                    responseMap["region_id"] = LLSD.FromUUID(rrRID);
                    responseMap["sim_access"] = LLSD.FromString(rrAccess);

                   

                    responseMap["position"] = RezResponsePositionArray;
                    responseMap["look_at"] = lookArray;
                    responseMap["connect"] = LLSD.FromBoolean(true);

                    ShutdownConnection(LocalID,this);


                    m_log.Warn("RESPONSEDEREZ: " + responseMap.ToString());
                    return responseMap;

                }
                else 
                {

                    return GenerateNoHandlerMessage();
                }
            }
            else
            {
                return GenerateNoHandlerMessage();
            }

            return responseMap;
        }

        private LLSDMap invokeRezAvatarCap(LLSDMap responseMap, string CapAddress, OGPState userState)
        {

            WebRequest DeRezRequest = WebRequest.Create(CapAddress);
            DeRezRequest.Method = "POST";
            DeRezRequest.ContentType = "application/xml+llsd";

            LLSDMap RAMap = new LLSDMap();
            LLSDMap AgentParms = new LLSDMap();
            LLSDMap RegionParms = new LLSDMap();


            AgentParms["first_name"] = LLSD.FromString(userState.first_name);
            AgentParms["last_name"] = LLSD.FromString(userState.last_name);
            AgentParms["agent_id"] = LLSD.FromUUID(userState.agent_id);
            RegionParms["region_id"] = LLSD.FromUUID(userState.region_id);
            AgentParms["circuit_code"] = LLSD.FromInteger(userState.circuit_code);
            AgentParms["secure_session_id"] = LLSD.FromUUID(userState.secure_session_id);
            AgentParms["session_id"] = LLSD.FromUUID(userState.session_id);
            AgentParms["agent_access"] = LLSD.FromBoolean(userState.agent_access);
            AgentParms["god_level"] = LLSD.FromInteger(userState.god_level);
            AgentParms["god_overide"] = LLSD.FromBoolean(userState.god_overide);
            AgentParms["identified"] = LLSD.FromBoolean(userState.identified);
            AgentParms["transacted"] = LLSD.FromBoolean(userState.transacted);
            AgentParms["age_verified"] = LLSD.FromBoolean(userState.age_verified);
            AgentParms["limited_to_estate"] = LLSD.FromInteger(userState.limited_to_estate);
            AgentParms["inventory_host"] = LLSD.FromString(userState.inventory_host);
            RAMap["agent_params"] = AgentParms;
            RAMap["region_params"] = RegionParms;

            string RAMapString = AgentParms.ToString();
            m_log.InfoFormat("[OGP] RAMap string {0}", RAMapString);
            LLSD LLSDofRAMap = AgentParms; // RENAME if this works

            m_log.InfoFormat("[OGP]: LLSD of map as string  was {0}", LLSDofRAMap.ToString());
            byte[] buffer = LLSDParser.SerializeXmlBytes(LLSDofRAMap);

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
                responseMap["connect"] = LLSD.FromBoolean(false);

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
                    responseMap["connect"] = LLSD.FromBoolean(false);

                    return responseMap;
                }
                LLSD rezResponse = null;
                try
                {
                    rezResponse = LLSDParser.DeserializeXml(rez_avatar_reply);

                    responseMap = (LLSDMap)rezResponse;
                }
                catch (Exception ex)
                {
                    m_log.InfoFormat("[OGP]: exception on parse of rez reply {0}", ex.Message);
                    responseMap["connect"] = LLSD.FromBoolean(false);

                    return responseMap;
                }
            }
            return responseMap;
        }



        public LLSD GenerateNoHandlerMessage()
        {
            LLSDMap map = new LLSDMap();
            map["reason"] = LLSD.FromString("LLSDRequest");
            map["message"] = LLSD.FromString("No handler registered for LLSD Requests");
            map["login"] = LLSD.FromString("false");
            
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
        
        private ulong GetOSCompatibleRegionHandle(RegionInfo reg)
        {
            return Util.UIntsToLong(reg.RegionLocX, reg.RegionLocY);
        }

        private ulong GetOSCompatibleRegionHandle(uint x, uint y)
        {
            return Util.UIntsToLong(x, y);
        }

        private ulong GetOSCompatibleRegionHandle(ulong regionhandle)
        {
            uint x,y;
            Helpers.LongToUInts(regionhandle,out x, out y);
            return GetOSCompatibleRegionHandle(x,y);
        }


        private ulong GetOGPCompatibleRegionHandle(RegionInfo reg)
        {
            return Util.UIntsToLong((reg.RegionLocX * (uint)Constants.RegionSize), (reg.RegionLocY * (uint)Constants.RegionSize));
        }

        private ulong GetOGPCompatibleRegionHandle(uint x, uint y)
        {
            return Util.UIntsToLong((x * (uint)Constants.RegionSize), (y * (uint)Constants.RegionSize));
        }

        private ulong GetOGPCompatibleRegionHandle(ulong regionhandle)
        {
            uint x, y;
            Helpers.LongToUInts(regionhandle, out x, out y);
            return GetOGPCompatibleRegionHandle(x, y);
        }

        private OGPState InitializeNewState()
        {
            OGPState returnState = new OGPState();
            returnState.first_name = "";
            returnState.last_name = "";
            returnState.agent_id = LLUUID.Zero;
            returnState.local_agent_id = LLUUID.Zero;
            returnState.region_id = LLUUID.Zero;
            returnState.circuit_code = 0;
            returnState.secure_session_id = LLUUID.Zero;
            returnState.session_id = LLUUID.Zero;
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
            
            return returnState;
        }

        private OGPState GetOGPState(LLUUID agentId)
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

        public void DeleteOGPState(LLUUID agentId)
        {
            lock (m_OGPState)
                if (m_OGPState.ContainsKey(agentId))
                    m_OGPState.Remove(agentId);

        }

        private void UpdateOGPState(LLUUID agentId, OGPState state)
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
        public void ShutdownConnection(LLUUID avatarId, OpenGridProtocolModule mod)
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
            LLUUID avUUID = LLUUID.Zero;

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
}
