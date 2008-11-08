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
using System.Reflection;
using System.Net;
using System.Threading;
using OpenMetaverse;
using log4net;
using Nini.Config;
using Nwc.XmlRpc;
using OpenSim.Framework;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Scenes;

namespace OpenSim.Region.Environment.Modules.Avatar.InstantMessage
{
    public class InstantMessageModule : IRegionModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private readonly List<Scene> m_scenes = new List<Scene>();
        private Dictionary<UUID, ulong> m_userRegionMap = new Dictionary<UUID, ulong>();

        #region IRegionModule Members

        private bool gridmode = false;


        public void Initialise(Scene scene, IConfigSource config)
        {
            if (config.Configs["Messaging"] != null)
            {
                if (config.Configs["Messaging"].GetString(
                        "InstantMessageModule", "InstantMessageModule") !=
                        "InstantMessageModule")
                    return;
            }

            lock (m_scenes)
            {
                if (m_scenes.Count == 0)
                {
                    //scene.AddXmlRPCHandler("avatar_location_update", processPresenceUpdate);
                    scene.AddXmlRPCHandler("grid_instant_message", processXMLRPCGridInstantMessage);
                    ReadConfig(config);
                }

                if (!m_scenes.Contains(scene))
                {
                    m_scenes.Add(scene);
                    scene.EventManager.OnNewClient += OnNewClient;
                    scene.EventManager.OnGridInstantMessage += OnGridInstantMessage;
                }
            }
        }

        private void ReadConfig(IConfigSource config)
        {
            IConfig cnf = config.Configs["Startup"];
            if (cnf != null)
            {
                gridmode = cnf.GetBoolean("gridmode", false);
            }
        }

        public void PostInitialise()
        {
        }

        public void Close()
        {
        }

        public string Name
        {
            get { return "InstantMessageModule"; }
        }

        public bool IsSharedModule
        {
            get { return true; }
        }

        #endregion

        private void OnNewClient(IClientAPI client)
        {
            client.OnInstantMessage += OnInstantMessage;
        }

        private void OnInstantMessage(IClientAPI client, UUID fromAgentID,
                                      UUID fromAgentSession, UUID toAgentID,
                                      UUID imSessionID, uint timestamp, string fromAgentName,
                                      string message, byte dialog, bool fromGroup, byte offline,
                                      uint ParentEstateID, Vector3 Position, UUID RegionID,
                                      byte[] binaryBucket)
        {
            bool dialogHandledElsewhere
                = (   dialog == (byte) InstantMessageDialog.FriendshipOffered
                   || dialog == (byte) InstantMessageDialog.FriendshipAccepted
                   || dialog == (byte) InstantMessageDialog.FriendshipDeclined
                   || dialog == (byte) InstantMessageDialog.InventoryOffered
                   || dialog == (byte) InstantMessageDialog.InventoryAccepted
                   || dialog == (byte) InstantMessageDialog.InventoryDeclined
                   || dialog == (byte) InstantMessageDialog.GroupNoticeInventoryAccepted
                   || dialog == (byte) InstantMessageDialog.GroupNoticeInventoryDeclined
                   || dialog == (byte) InstantMessageDialog.GroupInvitationAccept
                   || dialog == (byte) InstantMessageDialog.GroupInvitationDecline
                   || dialog == (byte) InstantMessageDialog.GroupNotice);

            // IM dialogs need to be pre-processed and have their sessionID filled by the server
            // so the sim can match the transaction on the return packet.

            // Don't process IMs that are handled elsewhere (e.g. friend dialog
            // IMs) with a non-UUID.Zero agent session, as those have been send
            // by a client (either directly or from another region via
            // inter-region communication) and will be processed in another
            // module (e.g. the friends-module).
            // IMs with fromAgentSession == UUID.Zero come from the server, and
            // have to be passed to the matching viewer 
            if (!dialogHandledElsewhere || fromAgentSession == UUID.Zero)
            {
                // Try root avatar only first
                foreach (Scene scene in m_scenes)
                {
                    if (scene.Entities.ContainsKey(toAgentID) && scene.Entities[toAgentID] is ScenePresence)
                    {
                        // Local message
                        ScenePresence user = (ScenePresence) scene.Entities[toAgentID];
                        if (!user.IsChildAgent)
                        {
                            user.ControllingClient.SendInstantMessage(fromAgentID, message,
                                                                      toAgentID, fromAgentName, dialog,
                                                                      timestamp, fromGroup, binaryBucket);
                            // Message sent
                            return;
                        }
                    }
                }

                // try child avatar second
                foreach (Scene scene in m_scenes)
                {
                    if (scene.Entities.ContainsKey(toAgentID) && scene.Entities[toAgentID] is ScenePresence)
                    {
                        // Local message
                        ScenePresence user = (ScenePresence) scene.Entities[toAgentID];

                        user.ControllingClient.SendInstantMessage(fromAgentID, message,
                                                                  toAgentID, fromAgentName, dialog,
                                                                  timestamp, fromGroup, binaryBucket);
                        // Message sent
                        return;
                    }
                }
                if (gridmode)
                {
                    // Still here, try send via Grid

                    // don't send session drop yet, as it's not reliable somehow.
                    if (dialog != (byte)InstantMessageDialog.SessionDrop)
                    {
                        SendGridInstantMessageViaXMLRPC(client, fromAgentID,
                                         fromAgentSession, toAgentID,
                                         imSessionID, timestamp, fromAgentName,
                                         message, dialog, fromGroup, offline,
                                         ParentEstateID, Position, RegionID,
                                         binaryBucket, getLocalRegionHandleFromUUID(RegionID), 0);
                    }
                }
                else
                {
                    if (client != null)
                    {
                        if (dialog != (byte)InstantMessageDialog.StartTyping && dialog != (byte)InstantMessageDialog.StopTyping && dialog != (byte)InstantMessageDialog.SessionDrop)
                            client.SendInstantMessage(toAgentID, "Unable to send instant message.  User is not logged in.", fromAgentID, "System", (byte)InstantMessageDialog.BusyAutoResponse, (uint)Util.UnixTimeSinceEpoch());// SendAlertMessage("Unable to send instant message");
                    }
                }
            }


        }

        // Trusty OSG1 called method.  This method also gets called from the FriendsModule
        // Turns out the sim has to send an instant message to the user to get it to show an accepted friend.
        /// <summary>
        ///
        /// </summary>
        /// <param name="msg"></param>
        private void OnGridInstantMessage(GridInstantMessage msg, InstantMessageReceiver which)
        {
            if ((which & InstantMessageReceiver.IMModule) == 0)
                return;

            // Trigger the above event handler
            OnInstantMessage(null, new UUID(msg.fromAgentID), new UUID(msg.fromAgentSession),
                             new UUID(msg.toAgentID), new UUID(msg.imSessionID), msg.timestamp, msg.fromAgentName,
                             msg.message, msg.dialog, msg.fromGroup, msg.offline, msg.ParentEstateID,
                             new Vector3(msg.Position.X, msg.Position.Y, msg.Position.Z), new UUID(msg.RegionID),
                             msg.binaryBucket);
        }


        /// <summary>
        /// Process a XMLRPC Grid Instant Message
        /// </summary>
        /// <param name="request">XMLRPC parameters from_agent_id from_agent_session to_agent_id im_session_id timestamp
        /// from_agent_name message dialog from_group offline parent_estate_id  position_x position_y  position_z region_id
        /// binary_bucket region_handle</param>
        /// <returns>Nothing much</returns>
        protected virtual XmlRpcResponse processXMLRPCGridInstantMessage(XmlRpcRequest request)
        {
            bool successful = false;
            // various rational defaults
            UUID fromAgentID = UUID.Zero;
            UUID fromAgentSession = UUID.Zero;
            UUID toAgentID = UUID.Zero;
            UUID imSessionID = UUID.Zero;
            uint timestamp = 0;
            string fromAgentName = "";
            string message = "";
            byte dialog = (byte)0;
            bool fromGroup = false;
            byte offline = (byte)0;
            uint ParentEstateID=0;
            Vector3 Position = Vector3.Zero;
            UUID RegionID = UUID.Zero ;
            byte[] binaryBucket = new byte[0];

            float pos_x = 0;
            float pos_y = 0;
            float pos_z = 0;
            //m_log.Info("Processing IM");


            Hashtable requestData = (Hashtable)request.Params[0];
            // Check if it's got all the data
            if (requestData.ContainsKey("from_agent_id") && requestData.ContainsKey("from_agent_session")
                    && requestData.ContainsKey("to_agent_id") && requestData.ContainsKey("im_session_id")
                    && requestData.ContainsKey("timestamp") && requestData.ContainsKey("from_agent_name")
                    && requestData.ContainsKey("message") && requestData.ContainsKey("dialog")
                    && requestData.ContainsKey("from_group")
                    && requestData.ContainsKey("offline") && requestData.ContainsKey("parent_estate_id")
                    && requestData.ContainsKey("position_x") && requestData.ContainsKey("position_y")
                    && requestData.ContainsKey("position_z") && requestData.ContainsKey("region_id")
                    && requestData.ContainsKey("binary_bucket") &&  requestData.ContainsKey("region_handle"))
            {
                // Do the easy way of validating the UUIDs
                UUID.TryParse((string)requestData["from_agent_id"], out fromAgentID);
                UUID.TryParse((string)requestData["from_agent_session"], out fromAgentSession);
                UUID.TryParse((string)requestData["to_agent_id"], out toAgentID);
                UUID.TryParse((string)requestData["im_session_id"], out imSessionID);
                UUID.TryParse((string)requestData["region_id"], out RegionID);

                # region timestamp
                try
                {
                    timestamp = (uint)Convert.ToInt32((string)requestData["timestamp"]);
                }
                catch (ArgumentException)
                {
                }
                catch (FormatException)
                {
                }
                catch (OverflowException)
                {
                }
                # endregion

                fromAgentName = (string)requestData["from_agent_name"];
                message = (string)requestData["message"];

                // Bytes don't transfer well over XMLRPC, so, we Base64 Encode them.
                string requestData1 = (string)requestData["dialog"];
                if (string.IsNullOrEmpty(requestData1))
                {
                    dialog = 0;
                }
                else
                {
                    byte[] dialogdata = Convert.FromBase64String(requestData1);
                    dialog = dialogdata[0];
                }

                if ((string)requestData["from_group"] == "TRUE")
                    fromGroup = true;

                string requestData2 = (string)requestData["offline"];
                if (String.IsNullOrEmpty(requestData2))
                {
                    offline = 0;
                }
                else
                {
                    byte[] offlinedata = Convert.FromBase64String(requestData2);
                    offline = offlinedata[0];
                }

                # region ParentEstateID
                try
                {
                    ParentEstateID = (uint)Convert.ToInt32((string)requestData["parent_estate_id"]);
                }
                catch (ArgumentException)
                {
                }
                catch (FormatException)
                {
                }
                catch (OverflowException)
                {
                }
                # endregion

                # region pos_x
                try
                {
                    pos_x = (uint)Convert.ToInt32((string)requestData["position_x"]);
                }
                catch (ArgumentException)
                {
                }
                catch (FormatException)
                {
                }
                catch (OverflowException)
                {
                }
                # endregion
                # region pos_y
                try
                {
                    pos_y = (uint)Convert.ToInt32((string)requestData["position_y"]);
                }
                catch (ArgumentException)
                {
                }
                catch (FormatException)
                {
                }
                catch (OverflowException)
                {
                }
                # endregion
                # region pos_z
                try
                {
                    pos_z = (uint)Convert.ToInt32((string)requestData["position_z"]);
                }
                catch (ArgumentException)
                {
                }
                catch (FormatException)
                {
                }
                catch (OverflowException)
                {
                }
                # endregion

                Position = new Vector3(pos_x, pos_y, pos_z);

                string requestData3 = (string)requestData["binary_bucket"];
                if (string.IsNullOrEmpty(requestData3))
                {
                    binaryBucket = new byte[0];
                }
                else
                {
                    binaryBucket = Convert.FromBase64String(requestData3);
                }

                // Create a New GridInstantMessageObject the the data
                GridInstantMessage gim = new GridInstantMessage();
                gim.fromAgentID = fromAgentID.Guid;
                gim.fromAgentName = fromAgentName;
                gim.fromAgentSession = fromAgentSession.Guid;
                gim.fromGroup = fromGroup;
                gim.imSessionID = imSessionID.Guid;
                gim.RegionID = RegionID.Guid;
                gim.timestamp = timestamp;
                gim.toAgentID = toAgentID.Guid;
                gim.message = message;
                gim.dialog = dialog;
                gim.offline = offline;
                gim.ParentEstateID = ParentEstateID;
                gim.Position = Position;
                gim.binaryBucket = binaryBucket;


                // Trigger the Instant message in the scene.
                foreach (Scene scene in m_scenes)
                {
                    if (scene.Entities.ContainsKey(toAgentID) && scene.Entities[toAgentID] is ScenePresence)
                    {
                        // Local message
                        ScenePresence user = (ScenePresence)scene.Entities[toAgentID];
                        if (!user.IsChildAgent)
                        {
                            scene.EventManager.TriggerGridInstantMessage(gim, InstantMessageReceiver.FriendsModule | InstantMessageReceiver.GroupsModule | InstantMessageReceiver.IMModule);
                            successful = true;
                        }
                    }
                }
                //OnGridInstantMessage(gim);

            }

            //Send response back to region calling if it was successful
            // calling region uses this to know when to look up a user's location again.
            XmlRpcResponse resp = new XmlRpcResponse();
            Hashtable respdata = new Hashtable();
            if (successful)
                respdata["success"] = "TRUE";
            else
                respdata["success"] = "FALSE";
            resp.Value = respdata;

            return resp;
        }

        #region Asynchronous setup
        /// <summary>
        /// delegate for sending a grid instant message asynchronously
        /// </summary>
        /// <param name="client"></param>
        /// <param name="fromAgentID"></param>
        /// <param name="fromAgentSession"></param>
        /// <param name="toAgentID"></param>
        /// <param name="imSessionID"></param>
        /// <param name="timestamp"></param>
        /// <param name="fromAgentName"></param>
        /// <param name="message"></param>
        /// <param name="dialog"></param>
        /// <param name="fromGroup"></param>
        /// <param name="offline"></param>
        /// <param name="ParentEstateID"></param>
        /// <param name="Position"></param>
        /// <param name="RegionID"></param>
        /// <param name="binaryBucket"></param>
        /// <param name="regionhandle"></param>
        /// <param name="prevRegionHandle"></param>
        public delegate void GridInstantMessageDelegate(IClientAPI client, UUID fromAgentID,
                                      UUID fromAgentSession, UUID toAgentID,
                                      UUID imSessionID, uint timestamp, string fromAgentName,
                                      string message, byte dialog, bool fromGroup, byte offline,
                                      uint ParentEstateID, Vector3 Position, UUID RegionID,
                                      byte[] binaryBucket, ulong regionhandle, ulong prevRegionHandle);

        private void GridInstantMessageCompleted(IAsyncResult iar)
        {
            GridInstantMessageDelegate icon = (GridInstantMessageDelegate)iar.AsyncState;
            icon.EndInvoke(iar);
        }


        protected virtual void SendGridInstantMessageViaXMLRPC(IClientAPI client, UUID fromAgentID,
                                      UUID fromAgentSession, UUID toAgentID,
                                      UUID imSessionID, uint timestamp, string fromAgentName,
                                      string message, byte dialog, bool fromGroup, byte offline,
                                      uint ParentEstateID, Vector3 Position, UUID RegionID,
                                      byte[] binaryBucket, ulong regionhandle, ulong prevRegionHandle)
        {
                    GridInstantMessageDelegate d = SendGridInstantMessageViaXMLRPCAsync;

                    d.BeginInvoke(client,fromAgentID,
                                      fromAgentSession,toAgentID,
                                      imSessionID,timestamp, fromAgentName,
                                      message, dialog, fromGroup, offline,
                                      ParentEstateID, Position, RegionID,
                                     binaryBucket, regionhandle, prevRegionHandle,
                                  GridInstantMessageCompleted,
                                  d);
                }

        #endregion


        /// <summary>
        /// Recursive SendGridInstantMessage over XMLRPC method.  The prevRegionHandle contains the last regionhandle tried
        /// if it's the same as the user's looked up region handle, then we end the recursive loop
        /// </summary>
        /// <param name="prevRegionHandle"></param>
        protected virtual void SendGridInstantMessageViaXMLRPCAsync(IClientAPI client, UUID fromAgentID,
                              UUID fromAgentSession, UUID toAgentID,
                              UUID imSessionID, uint timestamp, string fromAgentName,
                              string message, byte dialog, bool fromGroup, byte offline,
                              uint ParentEstateID, Vector3 Position, UUID RegionID,
                              byte[] binaryBucket, ulong regionhandle, ulong prevRegionHandle)
        {
            UserAgentData  upd = null;

            bool lookupAgent = false;

            lock (m_userRegionMap)
            {
                if (m_userRegionMap.ContainsKey(toAgentID) && prevRegionHandle == 0)
                {
                    upd = new UserAgentData();
                    upd.AgentOnline = true;
                    upd.Handle = m_userRegionMap[toAgentID];

                }
                else
                {
                    lookupAgent = true;


                }
            }

            // Are we needing to look-up an agent?
            if (lookupAgent)
            {
                // Non-cached user agent lookup.
                upd = m_scenes[0].CommsManager.UserService.GetAgentByUUID(toAgentID);

                if (upd != null)
                {
                    // check if we've tried this before..     This is one way to end the recursive loop
                    if (upd.Handle == prevRegionHandle)
                    {
                        m_log.Error("[GRID INSTANT MESSAGE]: Unable to deliver an instant message");
                        if (client != null)
                        {
                            if (dialog != (byte)InstantMessageDialog.StartTyping && dialog != (byte)InstantMessageDialog.StopTyping && dialog != (byte)InstantMessageDialog.SessionDrop)
                                client.SendInstantMessage(toAgentID, "Unable to send instant message", fromAgentID, "System", (byte)InstantMessageDialog.BusyAutoResponse, (uint)Util.UnixTimeSinceEpoch());
                        }
                        return;
                    }
                }
                else
                {
                    m_log.Error("[GRID INSTANT MESSAGE]: Unable to deliver an instant message");
                    if (client != null)
                    {
                        if (dialog != (byte)InstantMessageDialog.StartTyping && dialog != (byte)InstantMessageDialog.StopTyping && dialog != (byte)InstantMessageDialog.SessionDrop)
                            client.SendInstantMessage(toAgentID, "Unable to send instant message", fromAgentID, "System", (byte)InstantMessageDialog.BusyAutoResponse, (uint)Util.UnixTimeSinceEpoch());
                    }
                    return;
                }
            }

            if (upd != null)
            {
                if (upd.AgentOnline)
                {
                    RegionInfo reginfo = m_scenes[0].SceneGridService.RequestNeighbouringRegionInfo(upd.Handle);
                    if (reginfo != null)
                    {
                        GridInstantMessage msg = new GridInstantMessage();
                        msg.fromAgentID = fromAgentID.Guid;
                        msg.fromAgentSession = fromAgentSession.Guid;
                        msg.toAgentID = toAgentID.Guid;
                        msg.imSessionID = imSessionID.Guid;
                        msg.timestamp = timestamp;
                        msg.fromAgentName = fromAgentName;
                        msg.message = message;
                        msg.dialog = dialog;
                        msg.fromGroup = fromGroup;
                        msg.offline = offline;
                        msg.ParentEstateID = ParentEstateID;
                        msg.Position = Position;
                        msg.RegionID = RegionID.Guid;
                        msg.binaryBucket = binaryBucket;

                        Hashtable msgdata = ConvertGridInstantMessageToXMLRPC(msg);
                        msgdata["region_handle"] = getLocalRegionHandleFromUUID(RegionID);
                        bool imresult = doIMSending(reginfo, msgdata);
                        if (imresult)
                        {
                            // IM delivery successful, so store the Agent's location in our local cache.
                            lock (m_userRegionMap)
                            {
                                if (m_userRegionMap.ContainsKey(toAgentID))
                                {
                                    m_userRegionMap[toAgentID] = upd.Handle;
                                }
                                else
                                {
                                    m_userRegionMap.Add(toAgentID, upd.Handle);
                                }
                            }
                            //m_log.Info("[GRID INSTANT MESSAGE]: Successfully sent a message");
                        }
                        else
                        {
                            // try again, but lookup user this time.
                            // Warning, this must call the Async version
                            // of this method or we'll be making thousands of threads
                            // The version within the spawned thread is SendGridInstantMessageViaXMLRPCAsync
                            // The version that spawns the thread is SendGridInstantMessageViaXMLRPC

                            // This is recursive!!!!!
                            SendGridInstantMessageViaXMLRPCAsync(client, fromAgentID,
                                      fromAgentSession, toAgentID,
                                      imSessionID, timestamp, fromAgentName,
                                      message, dialog, fromGroup, offline,
                                      ParentEstateID, Position, RegionID,
                                      binaryBucket, regionhandle, upd.Handle);
                        }

                    }
                }
                else
                {
                    // send Agent Offline message
                    if (client != null)
                    {
                        if (dialog != (byte)InstantMessageDialog.StartTyping && dialog != (byte)InstantMessageDialog.StopTyping && dialog != (byte)InstantMessageDialog.SessionDrop)
                            client.SendInstantMessage(toAgentID, "Unable to send instant message: Agent Offline", fromAgentID, "System", (byte)InstantMessageDialog.BusyAutoResponse, (uint)Util.UnixTimeSinceEpoch());// SendAlertMessage("Unable to send instant message");
                    }
                }
            }
            else
            {
                // send Agent doesn't exist message
                if (client != null)
                    client.SendInstantMessage(toAgentID, "Unable to send instant message: Are you sure this agent exists anymore?", fromAgentID, "System", (byte)InstantMessageDialog.MessageFromObject, (uint)Util.UnixTimeSinceEpoch());// SendAlertMessage("Unable to send instant message");
            }

        }

        /// <summary>
        /// This actually does the XMLRPC Request
        /// </summary>
        /// <param name="reginfo">RegionInfo we pull the data out of to send the request to</param>
        /// <param name="xmlrpcdata">The Instant Message data Hashtable</param>
        /// <returns>Bool if the message was successfully delivered at the other side.</returns>
        private bool doIMSending(RegionInfo reginfo, Hashtable xmlrpcdata)
        {

            ArrayList SendParams = new ArrayList();
            SendParams.Add(xmlrpcdata);
            XmlRpcRequest GridReq = new XmlRpcRequest("grid_instant_message", SendParams);
            try
            {

                XmlRpcResponse GridResp = GridReq.Send("http://" + reginfo.ExternalHostName + ":" + reginfo.HttpPort, 3000);

                Hashtable responseData = (Hashtable)GridResp.Value;

                if (responseData.ContainsKey("success"))
                {
                    if ((string)responseData["success"] == "TRUE")
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
                else
                {
                    return false;
                }
            }
            catch (WebException e)
            {
                m_log.ErrorFormat("[GRID INSTANT MESSAGE]: Error sending message to http://{0}:{1} the host didn't respond ({2})", 
                                  reginfo.ExternalHostName, reginfo.HttpPort, e.Message);
            }

            return false;
        }

        /// <summary>
        /// Get ulong region handle for region by it's Region UUID.
        /// We use region handles over grid comms because there's all sorts of free and cool caching.
        /// </summary>
        /// <param name="regionID">UUID of region to get the region handle for</param>
        /// <returns></returns>
        private ulong getLocalRegionHandleFromUUID(UUID regionID)
        {
            ulong returnhandle = 0;

            lock (m_scenes)
            {
                foreach (Scene sn in m_scenes)
                {
                    if (sn.RegionInfo.RegionID == regionID)
                    {
                        returnhandle = sn.RegionInfo.RegionHandle;
                        break;
                    }
                }
            }
            return returnhandle;
        }

        /// <summary>
        /// Takes a GridInstantMessage and converts it into a Hashtable for XMLRPC
        /// </summary>
        /// <param name="msg">The GridInstantMessage object</param>
        /// <returns>Hashtable containing the XMLRPC request</returns>
        private Hashtable ConvertGridInstantMessageToXMLRPC(GridInstantMessage msg)
        {
            Hashtable gim = new Hashtable();
            gim["from_agent_id"] = msg.fromAgentID.ToString();
            gim["from_agent_session"] = msg.fromAgentSession.ToString();
            gim["to_agent_id"] = msg.toAgentID.ToString();
            gim["im_session_id"] = msg.imSessionID.ToString();
            gim["timestamp"] = msg.timestamp.ToString();
            gim["from_agent_name"] = msg.fromAgentName;
            gim["message"] = msg.message;
            byte[] dialogdata = new byte[1];dialogdata[0] = msg.dialog;
            gim["dialog"] = Convert.ToBase64String(dialogdata,Base64FormattingOptions.None);

            if (msg.fromGroup)
                gim["from_group"] = "TRUE";
            else
                gim["from_group"] = "FALSE";
            byte[] offlinedata = new byte[1]; offlinedata[0] = msg.offline;
            gim["offline"] = Convert.ToBase64String(offlinedata, Base64FormattingOptions.None);
            gim["parent_estate_id"] = msg.ParentEstateID.ToString();
            gim["position_x"] = msg.Position.X.ToString();
            gim["position_y"] = msg.Position.Y.ToString();
            gim["position_z"] = msg.Position.Z.ToString();
            gim["region_id"] = msg.RegionID.ToString();
            gim["binary_bucket"] = Convert.ToBase64String(msg.binaryBucket,Base64FormattingOptions.None);
            return gim;
        }

    }
}
