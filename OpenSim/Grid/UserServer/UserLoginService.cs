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
using System.Threading;
using libsecondlife;
using Nwc.XmlRpc;

using OpenSim.Framework;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Framework.Console;
using OpenSim.Framework.Data;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Statistics;
using OpenSim.Framework.UserManagement;
using InventoryFolder=OpenSim.Framework.InventoryFolder;

namespace OpenSim.Grid.UserServer
{
    public delegate void UserLoggedInAtLocation(LLUUID agentID, LLUUID sessionID, LLUUID RegionID,
    ulong regionhandle, float positionX, float positionY, float positionZ, string firstname, string lastname);

    public class UserLoginService : LoginService
    {
        private static readonly log4net.ILog m_log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public event UserLoggedInAtLocation OnUserLoggedInAtLocation;

        private UserLoggedInAtLocation handlerUserLoggedInAtLocation = null;
       
        public UserConfig m_config;

        public UserLoginService(
            UserManagerBase userManager, LibraryRootFolder libraryRootFolder, 
            UserConfig config, string welcomeMess)
            : base(userManager, libraryRootFolder, welcomeMess)
        {
            m_config = config;
        }

        /// <summary>
        /// Customises the login response and fills in missing values.
        /// </summary>
        /// <param name="response">The existing response</param>
        /// <param name="theUser">The user profile</param>
        public override void CustomiseResponse(LoginResponse response, UserProfileData theUser, string startLocationRequest)
        {
            bool tryDefault = false;
            //CFK: Since the try is always "tried", the "Home Location" message should always appear, so comment this one.
            //CFK: m_log.Info("[LOGIN]: Load information from the gridserver");

            try
            {
                RegionProfileData SimInfo = null;
                if (startLocationRequest == "last")
                {
                    SimInfo =
                        RegionProfileData.RequestSimProfileData(
                            theUser.currentAgent.currentHandle, m_config.GridServerURL,
                            m_config.GridSendKey, m_config.GridRecvKey);
                }
                else if (startLocationRequest == "home")
                {
                    SimInfo =
                        RegionProfileData.RequestSimProfileData(
                            theUser.homeRegion, m_config.GridServerURL,
                            m_config.GridSendKey, m_config.GridRecvKey);
                }
                else
                {
                    string[] startLocationRequestParsed = Util.ParseStartLocationRequest(startLocationRequest);
                    m_log.Info("[DEBUGLOGINPARSE]: 1:" + startLocationRequestParsed[0] + ", 2:" + startLocationRequestParsed[1] + ", 3:" + startLocationRequestParsed[2] + ", 4:" + startLocationRequestParsed[3]);
                    if (startLocationRequestParsed[0] == "last")
                    {
                        // TODO: Parse out startlocationrequest string in the format; 'uri:RegionName&X&Y&Z'
                        SimInfo =
                            RegionProfileData.RequestSimProfileData(
                                theUser.currentAgent.currentHandle, m_config.GridServerURL,
                                m_config.GridSendKey, m_config.GridRecvKey);
                    }
                    else
                    {
                        m_log.Info("[LOGIN]: Looking up Sim: " + startLocationRequestParsed[0]);
                        SimInfo =
                        RegionProfileData.RequestSimProfileData(
                            startLocationRequestParsed[0], m_config.GridServerURL,
                            m_config.GridSendKey, m_config.GridRecvKey);

                        if (SimInfo == null)
                        {
                            m_log.Info("[LOGIN]: Didn't find region with a close name match sending to home location");
                            SimInfo =
                                RegionProfileData.RequestSimProfileData(
                                    theUser.homeRegion, m_config.GridServerURL,
                                    m_config.GridSendKey, m_config.GridRecvKey);
                        }
                    }
                }

                // Customise the response
                //CFK: This is redundant and the next message should always appear.
                //CFK: m_log.Info("[LOGIN]: Home Location");
                response.Home = "{'region_handle':[r" + (SimInfo.regionLocX * Constants.RegionSize).ToString() + ",r" +
                                (SimInfo.regionLocY * Constants.RegionSize).ToString() + "], " +
                                "'position':[r" + theUser.homeLocation.X.ToString() + ",r" +
                                theUser.homeLocation.Y.ToString() + ",r" + theUser.homeLocation.Z.ToString() + "], " +
                                "'look_at':[r" + theUser.homeLocation.X.ToString() + ",r" +
                                theUser.homeLocation.Y.ToString() + ",r" + theUser.homeLocation.Z.ToString() + "]}";

                // Destination
                //CFK: The "Notifying" message always seems to appear, so subsume the data from this message into 
                //CFK: the next one for X & Y and comment this one.
                //CFK: m_log.Info("[LOGIN]: CUSTOMISERESPONSE: Region X: " + SimInfo.regionLocX + 
                //CFK: "; Region Y: " + SimInfo.regionLocY);
                response.SimAddress = Util.GetHostFromDNS(SimInfo.serverURI.Split(new char[] { '/', ':' })[3]).ToString();
                response.SimPort = uint.Parse(SimInfo.serverURI.Split(new char[] { '/', ':' })[4]);
                response.RegionX = SimInfo.regionLocX;
                response.RegionY = SimInfo.regionLocY;

                //Not sure if the + "/CAPS/" should in fact be +"CAPS/" depending if there is already a / as part of httpServerURI
                string capsPath = Util.GetRandomCapsPath();
                response.SeedCapability = SimInfo.httpServerURI + "CAPS/" + capsPath + "0000/";
                
                m_log.DebugFormat(
                    "[CAPS]: Sending new CAPS seed url {0} to client {1}", 
                    response.SeedCapability, response.AgentID);                 

                // Notify the target of an incoming user
                //CFK: The "Notifying" message always seems to appear, so subsume the data from this message into 
                //CFK: the next one for X & Y and comment this one.
                //CFK: m_log.Info("[LOGIN]: " + SimInfo.regionName + " (" + SimInfo.serverURI + ")  " + 
                //CFK:    SimInfo.regionLocX + "," + SimInfo.regionLocY);

                theUser.currentAgent.currentRegion = SimInfo.UUID;
                theUser.currentAgent.currentHandle = SimInfo.regionHandle;
                
                // Prepare notification
                Hashtable SimParams = new Hashtable();
                SimParams["session_id"] = theUser.currentAgent.sessionID.ToString();
                SimParams["secure_session_id"] = theUser.currentAgent.secureSessionID.ToString();
                SimParams["firstname"] = theUser.username;
                SimParams["lastname"] = theUser.surname;
                SimParams["agent_id"] = theUser.UUID.ToString();
                SimParams["circuit_code"] = (Int32) Convert.ToUInt32(response.CircuitCode);
                SimParams["startpos_x"] = theUser.currentAgent.currentPos.X.ToString();
                SimParams["startpos_y"] = theUser.currentAgent.currentPos.Y.ToString();
                SimParams["startpos_z"] = theUser.currentAgent.currentPos.Z.ToString();
                SimParams["regionhandle"] = theUser.currentAgent.currentHandle.ToString();
                SimParams["caps_path"] = capsPath;
                ArrayList SendParams = new ArrayList();
                SendParams.Add(SimParams);

                // Update agent with target sim

                m_log.InfoFormat(
                    "[LOGIN]: Telling region {0} @ {1},{2} ({3}) to expect user connection", 
                    SimInfo.regionName, response.RegionX, response.RegionY, SimInfo.httpServerURI); 

                XmlRpcRequest GridReq = new XmlRpcRequest("expect_user", SendParams);                
                XmlRpcResponse GridResp = GridReq.Send(SimInfo.httpServerURI, 6000);
                
                if (GridResp.IsFault)
                {
                    m_log.ErrorFormat(
                        "[LOGIN]: XMLRPC request for {0} failed, fault code: {1}, reason: {2}", 
                        SimInfo.httpServerURI, GridResp.FaultCode, GridResp.FaultString);
                }
                handlerUserLoggedInAtLocation = OnUserLoggedInAtLocation;
                if (handlerUserLoggedInAtLocation != null)
                {
                    m_log.Info("[LOGIN]: Letting other objects know about login");
                    handlerUserLoggedInAtLocation(theUser.UUID, theUser.currentAgent.sessionID, theUser.currentAgent.currentRegion, 
                        theUser.currentAgent.currentHandle, theUser.currentAgent.currentPos.X,theUser.currentAgent.currentPos.Y,theUser.currentAgent.currentPos.Z,
                        theUser.username,theUser.surname);
                }
            }
            catch (Exception)
            //catch (System.AccessViolationException)
            {
                tryDefault = true;
            }
            
            if (tryDefault)
            {
                // Send him to default region instead
                // Load information from the gridserver

                ulong defaultHandle = (((ulong)m_config.DefaultX * Constants.RegionSize) << 32) | ((ulong)m_config.DefaultY * Constants.RegionSize);

                m_log.Warn(
                    "[LOGIN]: Home region not available: sending to default " + defaultHandle.ToString());

                try
                {
                    RegionProfileData SimInfo = RegionProfileData.RequestSimProfileData(
                        defaultHandle, m_config.GridServerURL,
                        m_config.GridSendKey, m_config.GridRecvKey);

                    // Customise the response
                    m_log.Info("[LOGIN]: Home Location");
                    response.Home = "{'region_handle':[r" + (SimInfo.regionLocX * Constants.RegionSize).ToString() + ",r" +
                                    (SimInfo.regionLocY * Constants.RegionSize).ToString() + "], " +
                                    "'position':[r" + theUser.homeLocation.X.ToString() + ",r" +
                                    theUser.homeLocation.Y.ToString() + ",r" + theUser.homeLocation.Z.ToString() + "], " +
                                    "'look_at':[r" + theUser.homeLocation.X.ToString() + ",r" +
                                    theUser.homeLocation.Y.ToString() + ",r" + theUser.homeLocation.Z.ToString() + "]}";

                    // Destination
                    m_log.Info("[LOGIN]: " +
                               "CUSTOMISERESPONSE: Region X: " + SimInfo.regionLocX + "; Region Y: " +
                               SimInfo.regionLocY);
                    response.SimAddress = Util.GetHostFromDNS(SimInfo.serverURI.Split(new char[] { '/', ':' })[3]).ToString();
                    response.SimPort = uint.Parse(SimInfo.serverURI.Split(new char[] { '/', ':' })[4]);
                    response.RegionX = SimInfo.regionLocX;
                    response.RegionY = SimInfo.regionLocY;

                    //Not sure if the + "/CAPS/" should in fact be +"CAPS/" depending if there is already a / as part of httpServerURI
                    string capsPath = Util.GetRandomCapsPath();
                    response.SeedCapability = SimInfo.httpServerURI + "CAPS/" + capsPath + "0000/";

                    // Notify the target of an incoming user
                    m_log.Info("[LOGIN]: Notifying " + SimInfo.regionName + " (" + SimInfo.serverURI + ")");

                    // Update agent with target sim
                    theUser.currentAgent.currentRegion = SimInfo.UUID;
                    theUser.currentAgent.currentHandle = SimInfo.regionHandle;

                    // Prepare notification
                    Hashtable SimParams = new Hashtable();
                    SimParams["session_id"] = theUser.currentAgent.sessionID.ToString();
                    SimParams["secure_session_id"] = theUser.currentAgent.secureSessionID.ToString();
                    SimParams["firstname"] = theUser.username;
                    SimParams["lastname"] = theUser.surname;
                    SimParams["agent_id"] = theUser.UUID.ToString();
                    SimParams["circuit_code"] = (Int32) Convert.ToUInt32(response.CircuitCode);
                    SimParams["startpos_x"] = theUser.currentAgent.currentPos.X.ToString();
                    SimParams["startpos_y"] = theUser.currentAgent.currentPos.Y.ToString();
                    SimParams["startpos_z"] = theUser.currentAgent.currentPos.Z.ToString();
                    SimParams["regionhandle"] = theUser.currentAgent.currentHandle.ToString();
                    SimParams["caps_path"] = capsPath;
                    ArrayList SendParams = new ArrayList();
                    SendParams.Add(SimParams);

                    m_log.Info("[LOGIN]: Informing region at " + SimInfo.httpServerURI);
                    // Send
                    XmlRpcRequest GridReq = new XmlRpcRequest("expect_user", SendParams);
                    XmlRpcResponse GridResp = GridReq.Send(SimInfo.httpServerURI, 6000);
                    handlerUserLoggedInAtLocation = OnUserLoggedInAtLocation;
                    if (handlerUserLoggedInAtLocation != null)
                    {
                        m_log.Info("[LOGIN]: Letting other objects know about login");
                        handlerUserLoggedInAtLocation(theUser.UUID, theUser.currentAgent.sessionID, theUser.currentAgent.currentRegion,
                        theUser.currentAgent.currentHandle, theUser.currentAgent.currentPos.X, theUser.currentAgent.currentPos.Y, theUser.currentAgent.currentPos.Z,
                        theUser.username, theUser.surname);
                    }
                }

                catch (Exception e)
                {
                    m_log.Warn("[LOGIN]: Default region also not available");
                    m_log.Warn("[LOGIN]: " + e.ToString());
                }
            }
        }

        protected override InventoryData CreateInventoryData(LLUUID userID)
        {
            List<InventoryFolderBase> folders
                = SynchronousRestObjectPoster.BeginPostObject<Guid, List<InventoryFolderBase>>(
                    "POST", m_config.InventoryUrl + "RootFolders/", userID.UUID);

            // In theory, the user will only ever be missing a root folder in situations where a grid
            // which didn't previously run a grid wide inventory server is being transitioned to one 
            // which does.
            if (null == folders | folders.Count == 0)
            {
                m_log.Warn(
                    "[LOGIN]: " +
                    "root inventory folder user " + userID + " not found. Creating.");

                RestObjectPoster.BeginPostObject<Guid>(
                    m_config.InventoryUrl + "CreateInventory/", userID.UUID);

                // A big delay should be okay here since the recreation of the user's root folders should
                // only ever happen once.  We need to sleep to let the inventory server do its work - 
                // previously 1000ms has been found to be too short.
                Thread.Sleep(10000);
                folders = SynchronousRestObjectPoster.BeginPostObject<Guid, List<InventoryFolderBase>>(
                    "POST", m_config.InventoryUrl + "RootFolders/", userID.UUID);
            }

            if (folders.Count > 0)
            {
                LLUUID rootID = LLUUID.Zero;
                ArrayList AgentInventoryArray = new ArrayList();
                Hashtable TempHash;
                foreach (InventoryFolderBase InvFolder in folders)
                {
                    if (InvFolder.parentID == LLUUID.Zero)
                    {
                        rootID = InvFolder.folderID;
                    }
                    TempHash = new Hashtable();
                    TempHash["name"] = InvFolder.name;
                    TempHash["parent_id"] = InvFolder.parentID.ToString();
                    TempHash["version"] = (Int32) InvFolder.version;
                    TempHash["type_default"] = (Int32) InvFolder.type;
                    TempHash["folder_id"] = InvFolder.folderID.ToString();
                    AgentInventoryArray.Add(TempHash);
                }
                return new InventoryData(AgentInventoryArray, rootID);
            }
            else
            {
                m_log.Warn("[LOGIN]: The root inventory folder could still not be retrieved" +
                           " for user ID " + userID);

                AgentInventory userInventory = new AgentInventory();
                userInventory.CreateRootFolder(userID);

                ArrayList AgentInventoryArray = new ArrayList();
                Hashtable TempHash;
                foreach (InventoryFolder InvFolder in userInventory.InventoryFolders.Values)
                {
                    TempHash = new Hashtable();
                    TempHash["name"] = InvFolder.FolderName;
                    TempHash["parent_id"] = InvFolder.ParentID.ToString();
                    TempHash["version"] = (Int32) InvFolder.Version;
                    TempHash["type_default"] = (Int32) InvFolder.DefaultType;
                    TempHash["folder_id"] = InvFolder.FolderID.ToString();
                    AgentInventoryArray.Add(TempHash);
                }

                return new InventoryData(AgentInventoryArray, userInventory.InventoryRoot.FolderID);
            }
        }
    }
}
