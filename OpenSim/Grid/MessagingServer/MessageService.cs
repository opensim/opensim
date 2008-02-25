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
* 
*/
using System;
using System.Net;
using System.Net.Sockets;
using System.Collections;
using System.Collections.Generic;
//using System.Xml;
using libsecondlife;
using Nwc.XmlRpc;
using OpenSim.Framework;
using OpenSim.Framework.Console;
using OpenSim.Framework.Data;
using OpenSim.Framework.Servers;
using FriendRights = libsecondlife.FriendRights;

namespace OpenSim.Grid.MessagingServer
{
    public class MessageService
    {
        private static readonly log4net.ILog m_log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private MessageServerConfig m_cfg;

        //A hashtable of all current presences this server knows about
        private Hashtable m_presences = new Hashtable();

        //a hashtable of all current regions this server knows about
        private Hashtable m_regionInfoCache = new Hashtable();

        //A hashtable containing lists of UUIDs keyed by UUID for fast backreferencing
        private Hashtable m_presence_BackReferences = new Hashtable();

        // Hashtable containing work units that need to be processed
        private Hashtable m_unProcessedWorkUnits = new Hashtable();

        public MessageService(MessageServerConfig cfg)
        {
            m_cfg = cfg;
        }
        
        #region RegionComms Methods

        /// <summary>
        /// Informs a region about an Agent
        /// </summary>
        /// <param name="TalkingAbout">User to talk about</param>
        /// <param name="UserToUpdate">User we're sending this too (contains the region)</param>
        public void SendRegionPresenceUpdate(UserPresenceData TalkingAbout, UserPresenceData UserToUpdate)
        {
            // TODO: Fill in pertenant Presence Data from 'TalkingAbout'

            RegionProfileData whichRegion = UserToUpdate.regionData;
            //whichRegion.httpServerURI

            Hashtable PresenceParams = new Hashtable();
            ArrayList SendParams = new ArrayList();
            SendParams.Add(PresenceParams);

            m_log.Info("[PRESENCE]: Informing " + whichRegion.regionName + " at " + whichRegion.httpServerURI);
            // Send
            XmlRpcRequest RegionReq = new XmlRpcRequest("presence_update", SendParams);
            XmlRpcResponse RegionResp = RegionReq.Send(whichRegion.httpServerURI, 6000);
        }


        #endregion

        #region FriendList Methods

        
        /// <summary>
        /// Process Friendlist subscriptions for a user
        /// The login method calls this for a User
        /// </summary>
        /// <param name="userpresence">The Agent we're processing the friendlist subscriptions</param>
        public void ProcessFriendListSubscriptions(UserPresenceData userpresence)
        {
            List<FriendListItem> uFriendList = userpresence.friendData;
            for (int i = 0; i < uFriendList.Count; i++)
            {
                m_presence_BackReferences.Add(userpresence.agentData.AgentID, uFriendList[i].Friend);
                m_presence_BackReferences.Add(uFriendList[i].Friend, userpresence.agentData.AgentID);

                if (m_presences.Contains(uFriendList[i].Friend))
                {
                    UserPresenceData friendup = (UserPresenceData)m_presences[uFriendList[i]];
                    // Add backreference
                    
                    SubscribeToPresenceUpdates(userpresence, friendup, uFriendList[i],i);
                }
            }

            m_presences.Add(userpresence.agentData.AgentID, userpresence);
        }

        /// <summary>
        /// Does the necessary work to subscribe one agent to another's presence notifications
        /// Gets called by ProcessFriendListSubscriptions.  You shouldn't call this directly 
        /// unless you know what you're doing
        /// </summary>
        /// <param name="userpresence">P1</param>
        /// <param name="friendpresence">P2</param>
        /// <param name="uFriendListItem"></param>
        /// <param name="uFriendListIndex"></param>
        public void SubscribeToPresenceUpdates(UserPresenceData userpresence, UserPresenceData friendpresence, 
                                                FriendListItem uFriendListItem, int uFriendListIndex)
        {
            
            if ((uFriendListItem.FriendListOwnerPerms & (uint)FriendRights.CanSeeOnline) != 0)
            {
                // Subscribe and Send Out updates
                if (!friendpresence.subscriptionData.Contains(friendpresence.agentData.AgentID))
                {
                    userpresence.subscriptionData.Add(friendpresence.agentData.AgentID);
                    //Send Region Notice....   
                    
                }
                else
                {
                    // we need to send out online status update, but the user is already subscribed
                    
                }
                SendRegionPresenceUpdate(friendpresence, userpresence);
            }
            if ((uFriendListItem.FriendPerms & (uint)FriendRights.CanSeeOnline) != 0)
            {
                if (!friendpresence.subscriptionData.Contains(userpresence.agentData.AgentID))
                {
                    friendpresence.subscriptionData.Add(userpresence.agentData.AgentID);
                    //Send Region Notice....
                }
                else
                {
                    // we need to send out online status update, but the user is already subscribed

                }
                SendRegionPresenceUpdate(userpresence, friendpresence);
            }

        }


        /// <summary>
        /// Adds a backreference so presence specific data doesn't have to be 
        /// enumerated for each logged in user every time someone logs on or off.
        /// </summary>
        /// <param name="agentID"></param>
        /// <param name="friendID"></param>
        public void addBackReference(LLUUID agentID, LLUUID friendID)
        {
            if (m_presence_BackReferences.Contains(friendID))
            {
                List<LLUUID> presenseBackReferences = (List<LLUUID>)m_presence_BackReferences[friendID];
                if (!presenseBackReferences.Contains(agentID))
                {
                    presenseBackReferences.Add(agentID);
                }
                m_presence_BackReferences[friendID] = presenseBackReferences;
            }
            else
            {
                List<LLUUID> presenceBackReferences = new List<LLUUID>();
                presenceBackReferences.Add(agentID);
                m_presence_BackReferences[friendID] = presenceBackReferences;
            }
        }

        /// <summary>
        /// Removes a backreference to free up some memory
        /// </summary>
        /// <param name="agentID"></param>
        /// <param name="friendID"></param>
        public void removeBackReference(LLUUID agentID, LLUUID friendID)
        {
            if (m_presence_BackReferences.Contains(friendID))
            {
                List<LLUUID> presenseBackReferences = (List<LLUUID>)m_presence_BackReferences[friendID];
                if (presenseBackReferences.Contains(agentID))
                {
                    presenseBackReferences.Remove(agentID);
                }

                // If there are no more backreferences for this agent, 
                // remove it to free up memory.
                if (presenseBackReferences.Count == 0)
                {
                    m_presence_BackReferences.Remove(agentID);
                }
            }
        }
        /// <summary>
        /// Logoff Processor.  Call this to clean up agent presence data and send logoff presence notifications
        /// </summary>
        /// <param name="AgentID"></param>
        private void ProcessLogOff(LLUUID AgentID)
        {
            if (m_presences.Contains(AgentID))
            {
                UserPresenceData AgentData = (UserPresenceData)m_presences[AgentID];

                if (m_presence_BackReferences.Contains(AgentID))
                {
                    List<LLUUID> AgentsNeedingNotification = (List<LLUUID>)m_presence_BackReferences[AgentID];
                    for (int i = 0; i < AgentsNeedingNotification.Count; i++)
                    {
                        // TODO: Do Region Notifications
                        if (m_presences.Contains(AgentsNeedingNotification[i]))
                        {
                            UserPresenceData friendd = (UserPresenceData)m_presences[AgentsNeedingNotification[i]];
                            
                            // This might need to be enumerated and checked before we try to remove it.
                            friendd.subscriptionData.Remove(AgentID);
                            
                            List<FriendListItem> fl = friendd.friendData;
                            for (int j = 0; j < fl.Count; j++)
                            {
                                if (fl[j].Friend == AgentID)
                                {
                                    fl[j].onlinestatus = false;
                                }

                            }
                            friendd.friendData = fl;

                            SendRegionPresenceUpdate(AgentData, friendd);

                        }
                        removeBackReference(AgentID, AgentsNeedingNotification[i]);

                    }
                }
            }
        }
        

        #endregion

        #region UserServer Comms

        /// <summary>
        /// Returns a list of FriendsListItems that describe the friends and permissions in the friend relationship for LLUUID friendslistowner
        /// </summary>
        /// <param name="friendlistowner">The agent that we're retreiving the friends Data.</param>
        public List<FriendListItem> GetUserFriendList(LLUUID friendlistowner)
        {
            List<FriendListItem> buddylist = new List<FriendListItem>();

            try
            {
                Hashtable param = new Hashtable();
                param["ownerID"] = friendlistowner.UUID.ToString();

                IList parameters = new ArrayList();
                parameters.Add(param);
                XmlRpcRequest req = new XmlRpcRequest("get_user_friend_list", parameters);
                XmlRpcResponse resp = req.Send(m_cfg.UserServerURL, 3000);
                Hashtable respData = (Hashtable)resp.Value;

                if (respData.Contains("avcount"))
                {
                    buddylist = ConvertXMLRPCDataToFriendListItemList(respData);
                }

            }
            catch (WebException e)
            {
                m_log.Warn("Error when trying to fetch Avatar's friends list: " +
                                      e.Message);
                // Return Empty list (no friends)
            }
            return buddylist;

        }

        /// <summary>
        /// Converts XMLRPC Friend List to FriendListItem Object
        /// </summary>
        /// <param name="data">XMLRPC response data Hashtable</param>
        /// <returns></returns>
        public List<FriendListItem> ConvertXMLRPCDataToFriendListItemList(Hashtable data)
        {
            List<FriendListItem> buddylist = new List<FriendListItem>();
            int buddycount = Convert.ToInt32((string)data["avcount"]);


            for (int i = 0; i < buddycount; i++)
            {
                FriendListItem buddylistitem = new FriendListItem();

                buddylistitem.FriendListOwner = new LLUUID((string)data["ownerID" + i.ToString()]);
                buddylistitem.Friend = new LLUUID((string)data["friendID" + i.ToString()]);
                buddylistitem.FriendListOwnerPerms = (uint)Convert.ToInt32((string)data["ownerPerms" + i.ToString()]);
                buddylistitem.FriendPerms = (uint)Convert.ToInt32((string)data["friendPerms" + i.ToString()]);

                buddylist.Add(buddylistitem);
            }


            return buddylist;
        }
        /// <summary>
        /// UserServer sends an expect_user method
        /// this handles the method and provisions the 
        /// necessary info for presence to work
        /// </summary>
        /// <param name="request">UserServer Data</param>
        /// <returns></returns>
        public XmlRpcResponse UserLoggedOn(XmlRpcRequest request)
        {
            m_log.Info("[LOGON]: User logged on, building indexes for user");
            Hashtable requestData = (Hashtable)request.Params[0];
            AgentCircuitData agentData = new AgentCircuitData();
            agentData.SessionID = new LLUUID((string)requestData["session_id"]);
            agentData.SecureSessionID = new LLUUID((string)requestData["secure_session_id"]);
            agentData.firstname = (string)requestData["firstname"];
            agentData.lastname = (string)requestData["lastname"];
            agentData.AgentID = new LLUUID((string)requestData["agent_id"]);
            agentData.circuitcode = Convert.ToUInt32(requestData["circuit_code"]);
            agentData.CapsPath = (string)requestData["caps_path"];

            if (requestData.ContainsKey("child_agent") && requestData["child_agent"].Equals("1"))
            {
                agentData.child = true;
            }
            else
            {
                agentData.startpos =
                    new LLVector3(Convert.ToUInt32(requestData["startpos_x"]),
                                  Convert.ToUInt32(requestData["startpos_y"]),
                                  Convert.ToUInt32(requestData["startpos_z"]));
                agentData.child = false;
            }

            ulong regionHandle = Convert.ToUInt64((string)requestData["regionhandle"]);

            UserPresenceData up = new UserPresenceData();
            up.agentData = agentData;
            List<FriendListItem> flData = GetUserFriendList(agentData.AgentID);
            up.friendData = flData;
            RegionProfileData riData = GetRegionInfo(regionHandle);
            up.regionData = riData;

            ProcessFriendListSubscriptions(up);


            return new XmlRpcResponse();
        }
        
        /// <summary>
        /// The UserServer got a Logoff message 
        /// Cleanup time for that user.  Send out presence notifications
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public XmlRpcResponse UserLoggedOff(XmlRpcRequest request)
        {

            Hashtable requestData = (Hashtable)request.Params[0];
            
            LLUUID AgentID = new LLUUID((string)requestData["agent_id"]);


            //ProcessLogOff(AgentID);


            return new XmlRpcResponse();
        }

        #endregion
        #region regioninfo gathering

        /// <summary>
        /// Gets and caches a RegionInfo object from the gridserver based on regionhandle
        /// if the regionhandle is already cached, use the cached values
        /// </summary>
        /// <param name="regionhandle">handle to the XY of the region we're looking for</param>
        /// <returns>A RegionInfo object to stick in the presence info</returns>
        public RegionProfileData GetRegionInfo(ulong regionhandle)
        {
            RegionProfileData regionInfo = null;
            if (m_regionInfoCache.Contains(regionhandle))
            {
                regionInfo = (RegionProfileData)m_regionInfoCache[regionhandle];
            }
            else 
            {
                regionInfo = RequestRegionInfo(regionhandle);
            }
            return regionInfo;
        }
        /// <summary>
        /// Get RegionProfileData from the GridServer
        /// We'll Cache this information and use it for presence updates
        /// </summary>
        /// <param name="regionHandle"></param>
        /// <returns></returns>
        public RegionProfileData RequestRegionInfo(ulong regionHandle)
        {   RegionProfileData regionProfile = null;
            try
            {
                
                Hashtable requestData = new Hashtable();
                requestData["region_handle"] = regionHandle.ToString();
                requestData["authkey"] = m_cfg.GridSendKey;
                ArrayList SendParams = new ArrayList();
                SendParams.Add(requestData);
                XmlRpcRequest GridReq = new XmlRpcRequest("simulator_data_request", SendParams);
                XmlRpcResponse GridResp = GridReq.Send(m_cfg.GridServerURL, 3000);
                
                Hashtable responseData = (Hashtable)GridResp.Value;

                if (responseData.ContainsKey("error"))
                {
                    m_log.Error("[GRID]: error received from grid server" + responseData["error"]);
                    return null;
                }

                uint regX = Convert.ToUInt32((string)responseData["region_locx"]);
                uint regY = Convert.ToUInt32((string)responseData["region_locy"]);
                string internalIpStr = (string)responseData["sim_ip"];
                uint port = Convert.ToUInt32(responseData["sim_port"]);
                string externalUri = (string)responseData["sim_uri"];
                string neighbourExternalUri = externalUri;

                regionProfile = new RegionProfileData();
                regionProfile.httpPort = (uint)Convert.ToInt32((string)responseData["http_port"]);
                regionProfile.httpServerURI = "http://" + internalIpStr + ":" + regionProfile.httpPort + "/";
                regionProfile.regionHandle = Helpers.UIntsToLong((regX * Constants.RegionSize), (regY * Constants.RegionSize));
                regionProfile.regionLocX = regX;
                regionProfile.regionLocY = regY;
               
                regionProfile.remotingPort = Convert.ToUInt32((string)responseData["remoting_port"]);
                regionProfile.UUID = new LLUUID((string)responseData["region_UUID"]);
                regionProfile.regionName = (string)responseData["region_name"];

                m_regionInfoCache.Add(regionHandle, regionProfile);
            }
            catch (WebException)
            {
                m_log.Error("[GRID]: " +
                                       "Region lookup failed for: " + regionHandle.ToString() +
                                       " - Is the GridServer down?");
                return null;
            }
           

            return regionProfile;
        }
        public bool registerWithUserServer ()
        {
            
            Hashtable UserParams = new Hashtable();
            // Login / Authentication
            
            if (m_cfg.HttpSSL)
            {
                UserParams["uri"] = "https://" + m_cfg.MessageServerIP + ":" + m_cfg.HttpPort;
            }
            else 
            {
                UserParams["uri"] = "http://" + m_cfg.MessageServerIP + ":" + m_cfg.HttpPort;
            }

            UserParams["recvkey"] = m_cfg.UserRecvKey;
            UserParams["sendkey"] = m_cfg.UserRecvKey;
            

            

            // Package into an XMLRPC Request
            ArrayList SendParams = new ArrayList();
            SendParams.Add(UserParams);

            // Send Request
            XmlRpcRequest UserReq;
            XmlRpcResponse UserResp;
            try
            {
                UserReq = new XmlRpcRequest("register_messageserver", SendParams);
                UserResp = UserReq.Send(m_cfg.UserServerURL, 16000);
            } catch (Exception ex)
            {
                m_log.Error("Unable to connect to grid. Grid server not running?");
                throw(ex);
            }
            Hashtable GridRespData = (Hashtable)UserResp.Value;
            Hashtable griddatahash = GridRespData;

            // Process Response
            if (GridRespData.ContainsKey("responsestring"))
            {
                return true;
            }
            else
            {
                return false;
            }
            
       
        }
        public bool deregisterWithUserServer()
        {

            Hashtable UserParams = new Hashtable();
            // Login / Authentication

            if (m_cfg.HttpSSL)
            {
                UserParams["uri"] = "https://" + m_cfg.MessageServerIP + ":" + m_cfg.HttpPort;
            }
            else
            {
                UserParams["uri"] = "http://" + m_cfg.MessageServerIP + ":" + m_cfg.HttpPort;
            }

            UserParams["recvkey"] = m_cfg.UserRecvKey;
            UserParams["sendkey"] = m_cfg.UserRecvKey;

            // Package into an XMLRPC Request
            ArrayList SendParams = new ArrayList();
            SendParams.Add(UserParams);

            // Send Request
            XmlRpcRequest UserReq;
            XmlRpcResponse UserResp;
            try
            {
                UserReq = new XmlRpcRequest("deregister_messageserver", SendParams);
                UserResp = UserReq.Send(m_cfg.UserServerURL, 16000);
            }
            catch (Exception ex)
            {
                m_log.Error("Unable to connect to grid. Grid server not running?");
                throw (ex);
            }
            Hashtable UserRespData = (Hashtable)UserResp.Value;
            Hashtable userdatahash = UserRespData;

            // Process Response
            if (UserRespData.ContainsKey("responsestring"))
            {
                return true;
            }
            else
            {
                return false;
            }


        }
        #endregion
    }

}
