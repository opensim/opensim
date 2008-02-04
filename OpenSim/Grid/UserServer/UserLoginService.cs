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
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS AS IS AND ANY
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
    public delegate void UserLoggedInAtLocation(LLUUID agentID, LLUUID sessionID, LLUUID RegionID, ulong regionhandle, LLVector3 Position);


    public class UserLoginService : LoginService
    {
        public event UserLoggedInAtLocation OnUserLoggedInAtLocation;
       
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
        public override void CustomiseResponse(LoginResponse response, UserProfileData theUser)
        {
            bool tryDefault = false;
            //CFK: Since the try is always "tried", the "Home Location" message should always appear, so comment this one.
            //CFK: MainLog.Instance.Verbose("LOGIN", "Load information from the gridserver");
            RegionProfileData SimInfo = new RegionProfileData();
            try
            {
                SimInfo =
                    SimInfo.RequestSimProfileData(theUser.currentAgent.currentHandle, m_config.GridServerURL,
                                                  m_config.GridSendKey, m_config.GridRecvKey);

                // Customise the response
                //CFK: This is redundant and the next message should always appear.
                //CFK: MainLog.Instance.Verbose("LOGIN", "Home Location");
                response.Home = "{'region_handle':[r" + (SimInfo.regionLocX*256).ToString() + ",r" +
                                (SimInfo.regionLocY*256).ToString() + "], " +
                                "'position':[r" + theUser.homeLocation.X.ToString() + ",r" +
                                theUser.homeLocation.Y.ToString() + ",r" + theUser.homeLocation.Z.ToString() + "], " +
                                "'look_at':[r" + theUser.homeLocation.X.ToString() + ",r" +
                                theUser.homeLocation.Y.ToString() + ",r" + theUser.homeLocation.Z.ToString() + "]}";

                // Destination
                //CFK: The "Notifying" message always seems to appear, so subsume the data from this message into 
                //CFK: the next one for X & Y and comment this one.
                //CFK: MainLog.Instance.Verbose("LOGIN", "CUSTOMISERESPONSE: Region X: " + SimInfo.regionLocX + 
                //CFK: "; Region Y: " + SimInfo.regionLocY);
                response.SimAddress = Util.GetHostFromDNS(SimInfo.serverIP).ToString();
                response.SimPort = (uint) SimInfo.serverPort;
                response.RegionX = SimInfo.regionLocX;
                response.RegionY = SimInfo.regionLocY;

                //Not sure if the + "/CAPS/" should in fact be +"CAPS/" depending if there is already a / as part of httpServerURI
                string capsPath = Util.GetRandomCapsPath();
                response.SeedCapability = SimInfo.httpServerURI + "CAPS/" + capsPath + "0000/";

                // Notify the target of an incoming user
                //CFK: The "Notifying" message always seems to appear, so subsume the data from this message into 
                //CFK: the next one for X & Y and comment this one.
                //CFK: MainLog.Instance.Verbose("LOGIN", SimInfo.regionName + " (" + SimInfo.serverURI + ")  " + 
                //CFK:    SimInfo.regionLocX + "," + SimInfo.regionLocY);

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
                theUser.currentAgent.currentRegion = SimInfo.UUID;
                theUser.currentAgent.currentHandle = SimInfo.regionHandle;

                MainLog.Instance.Verbose("LOGIN", SimInfo.regionName + " @ " + SimInfo.httpServerURI + "  " +
                                                  SimInfo.regionLocX + "," + SimInfo.regionLocY);

                XmlRpcRequest GridReq = new XmlRpcRequest("expect_user", SendParams);
                XmlRpcResponse GridResp = GridReq.Send(SimInfo.httpServerURI, 6000);
            }
            catch (Exception)
            {
                tryDefault = true;
            }
            if (tryDefault)
            {
                // Send him to default region instead
                // Load information from the gridserver

                ulong defaultHandle = (((ulong) m_config.DefaultX*256) << 32) | ((ulong) m_config.DefaultY*256);

                MainLog.Instance.Warn(
                    "LOGIN",
                    "Home region not available: sending to default " + defaultHandle.ToString());

                SimInfo = new RegionProfileData();
                try
                {
                    SimInfo =
                        SimInfo.RequestSimProfileData(defaultHandle, m_config.GridServerURL,
                                                      m_config.GridSendKey, m_config.GridRecvKey);

                    // Customise the response
                    MainLog.Instance.Verbose("LOGIN", "Home Location");
                    response.Home = "{'region_handle':[r" + (SimInfo.regionLocX*256).ToString() + ",r" +
                                    (SimInfo.regionLocY*256).ToString() + "], " +
                                    "'position':[r" + theUser.homeLocation.X.ToString() + ",r" +
                                    theUser.homeLocation.Y.ToString() + ",r" + theUser.homeLocation.Z.ToString() + "], " +
                                    "'look_at':[r" + theUser.homeLocation.X.ToString() + ",r" +
                                    theUser.homeLocation.Y.ToString() + ",r" + theUser.homeLocation.Z.ToString() + "]}";

                    // Destination
                    MainLog.Instance.Verbose("LOGIN",
                                             "CUSTOMISERESPONSE: Region X: " + SimInfo.regionLocX + "; Region Y: " +
                                             SimInfo.regionLocY);
                    response.SimAddress = Util.GetHostFromDNS(SimInfo.serverIP).ToString();
                    response.SimPort = (uint) SimInfo.serverPort;
                    response.RegionX = SimInfo.regionLocX;
                    response.RegionY = SimInfo.regionLocY;

                    //Not sure if the + "/CAPS/" should in fact be +"CAPS/" depending if there is already a / as part of httpServerURI
                    string capsPath = Util.GetRandomCapsPath();
                    response.SeedCapability = SimInfo.httpServerURI + "CAPS/" + capsPath + "0000/";

                    // Notify the target of an incoming user
                    MainLog.Instance.Verbose("LOGIN", "Notifying " + SimInfo.regionName + " (" + SimInfo.serverURI + ")");

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

                    MainLog.Instance.Verbose("LOGIN", "Informing region at " + SimInfo.httpServerURI);
                    // Send
                    XmlRpcRequest GridReq = new XmlRpcRequest("expect_user", SendParams);
                    XmlRpcResponse GridResp = GridReq.Send(SimInfo.httpServerURI, 6000);
                    if (OnUserLoggedInAtLocation != null)
                    {
                        OnUserLoggedInAtLocation(theUser.UUID, theUser.currentAgent.sessionID, theUser.currentAgent.currentRegion, theUser.currentAgent.currentHandle, theUser.currentAgent.currentPos);
                    }
                }

                catch (Exception e)
                {
                    MainLog.Instance.Warn("LOGIN", "Default region also not available");
                    MainLog.Instance.Warn("LOGIN", e.ToString());
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
                MainLog.Instance.Warn(
                    "LOGIN",
                    "A root inventory folder for user ID " + userID + " was not found.  A new set"
                    + " of empty inventory folders is being created.");

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
                MainLog.Instance.Warn("LOGIN", "The root inventory folder could still not be retrieved" +
                                               " for user ID " + userID);

                AgentInventory userInventory = new AgentInventory();
                userInventory.CreateRootFolder(userID, false);

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
