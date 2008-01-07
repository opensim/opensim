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
using System.Xml;
using libsecondlife;
using Nwc.XmlRpc;
using OpenSim.Framework;
using OpenSim.Framework.Console;
using OpenSim.Framework.Data;
using OpenSim.Framework.Servers;

namespace OpenSim.Grid.MessagingServer
{
    public class MessageService
    {
        private LogBase m_log;
        private MessageServerConfig m_cfg;
        private Hashtable m_presences = new Hashtable();
        private Hashtable m_regionInfoCache = new Hashtable();

        public MessageService(LogBase log, MessageServerConfig cfg)
        {
            m_log = log;
            m_cfg = cfg;
        }

        public XmlRpcResponse UserLoggedOn(XmlRpcRequest request)
        {
            
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

            m_presences.Add(regionHandle, agentData);


            return new XmlRpcResponse();
        }


        #region FriendList Gathering

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
                MainLog.Instance.Warn("Error when trying to fetch Avatar's friends list: " +
                                      e.Message);
                // Return Empty list (no friends)
            }
            return buddylist;

        }
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
        #endregion

        #region regioninfo gathering

        /// <summary>
        /// Gets and caches a RegionInfo object from the gridserver based on regionhandle
        /// if the regionhandle is already cached, use the cached values
        /// </summary>
        /// <param name="regionhandle">handle to the XY of the region we're looking for</param>
        /// <returns>A RegionInfo object to stick in the presence info</returns>
        public RegionInfo GetRegionInfo(ulong regionhandle)
        {
            RegionInfo regionInfo = null;
            if (m_regionInfoCache.Contains(regionhandle))
            {
                regionInfo = (RegionInfo)m_regionInfoCache[regionhandle];
            }
            else 
            {
                regionInfo = RequestRegionInfo(regionhandle);
            }
            return regionInfo;
        }
        public RegionInfo RequestRegionInfo(ulong regionHandle)
        {   RegionInfo regionInfo = null;
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
                    m_log.Error("GRID","error received from grid server" + responseData["error"]);
                    return null;
                }

                uint regX = Convert.ToUInt32((string)responseData["region_locx"]);
                uint regY = Convert.ToUInt32((string)responseData["region_locy"]);
                string internalIpStr = (string)responseData["sim_ip"];
                uint port = Convert.ToUInt32(responseData["sim_port"]);
                string externalUri = (string)responseData["sim_uri"];

                IPEndPoint neighbourInternalEndPoint = new IPEndPoint(IPAddress.Parse(internalIpStr), (int)port);
                string neighbourExternalUri = externalUri;
                regionInfo = new RegionInfo(regX, regY, neighbourInternalEndPoint, internalIpStr);

                regionInfo.RemotingPort = Convert.ToUInt32((string)responseData["remoting_port"]);
                regionInfo.RemotingAddress = internalIpStr;

                regionInfo.RegionID = new LLUUID((string)responseData["region_UUID"]);
                regionInfo.RegionName = (string)responseData["region_name"];

                m_regionInfoCache.Add(regionHandle, regionInfo);
            }
            catch (WebException)
            {
                MainLog.Instance.Error("GRID",
                                       "Region lookup failed for: " + regionHandle.ToString() +
                                       " - Is the GridServer down?");
                return null;
            }
           

            return regionInfo;
        }
        #endregion
    }

}
