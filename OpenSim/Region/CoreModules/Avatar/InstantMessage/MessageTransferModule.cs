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
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Reflection;
using log4net;
using Nini.Config;
using Nwc.XmlRpc;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using GridRegion = OpenSim.Services.Interfaces.GridRegion;

namespace OpenSim.Region.CoreModules.Avatar.InstantMessage
{
    public class MessageTransferModule : IRegionModule, IMessageTransferModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        // private bool m_Enabled = false;
        protected bool m_Gridmode = false;
        protected List<Scene> m_Scenes = new List<Scene>();
        protected Dictionary<UUID, ulong> m_UserRegionMap = new Dictionary<UUID, ulong>();

        public event UndeliveredMessage OnUndeliveredMessage;

        public virtual void Initialise(Scene scene, IConfigSource config)
        {
            IConfig cnf = config.Configs["Messaging"];
            if (cnf != null && cnf.GetString(
                    "MessageTransferModule", "MessageTransferModule") !=
                    "MessageTransferModule")
            {
                m_log.Debug("[MESSAGE TRANSFER]: Disabled by configuration");
                return;
            }

            cnf = config.Configs["Startup"];
            if (cnf != null)
                m_Gridmode = cnf.GetBoolean("gridmode", false);

            // m_Enabled = true;

            lock (m_Scenes)
            {
                if (m_Scenes.Count == 0)
                {
                    MainServer.Instance.AddXmlRPCHandler(
                        "grid_instant_message", processXMLRPCGridInstantMessage);
                }

                m_log.Debug("[MESSAGE TRANSFER]: Message transfer module active");
                scene.RegisterModuleInterface<IMessageTransferModule>(this);
                m_Scenes.Add(scene);
            }
        }

        public virtual void PostInitialise()
        {
        }

        public virtual void Close()
        {
        }

        public virtual string Name
        {
            get { return "MessageTransferModule"; }
        }

        public virtual bool IsSharedModule
        {
            get { return true; }
        }

        public virtual void SendInstantMessage(GridInstantMessage im, MessageResultNotification result)
        {
            UUID toAgentID = new UUID(im.toAgentID);

            m_log.DebugFormat("[INSTANT MESSAGE]: Attempting delivery of IM from {0} to {1}", im.fromAgentName, toAgentID.ToString());

            // Try root avatar only first
            foreach (Scene scene in m_Scenes)
            {
                if (scene.Entities.ContainsKey(toAgentID) &&
                        scene.Entities[toAgentID] is ScenePresence)
                {
                    m_log.DebugFormat("[INSTANT MESSAGE]: Looking for {0} in {1}", toAgentID.ToString(), scene.RegionInfo.RegionName);
                    // Local message
                    ScenePresence user = (ScenePresence) scene.Entities[toAgentID];
                    if (!user.IsChildAgent)
                    {
                        m_log.DebugFormat("[INSTANT MESSAGE]: Delivering to client");
                        user.ControllingClient.SendInstantMessage(im);

                        // Message sent
                        result(true);
                        return;
                    }
                }
            }

            // try child avatar second
            foreach (Scene scene in m_Scenes)
            {
//                m_log.DebugFormat(
//                    "[INSTANT MESSAGE]: Looking for child of {0} in {1}", toAgentID, scene.RegionInfo.RegionName);

                if (scene.Entities.ContainsKey(toAgentID) &&
                        scene.Entities[toAgentID] is ScenePresence)
                {
                    // Local message
                    ScenePresence user = (ScenePresence) scene.Entities[toAgentID];

                    m_log.DebugFormat("[INSTANT MESSAGE]: Delivering to client");
                    user.ControllingClient.SendInstantMessage(im);

                    // Message sent
                    result(true);
                    return;
                }
            }

            if (m_Gridmode)
            {
                //m_log.DebugFormat("[INSTANT MESSAGE]: Delivering via grid");
                // Still here, try send via Grid
                SendGridInstantMessageViaXMLRPC(im, result);
                return;
            }

            HandleUndeliveredMessage(im, result);

            return;
        }

        private void HandleUndeliveredMessage(GridInstantMessage im, MessageResultNotification result)
        {
            UndeliveredMessage handlerUndeliveredMessage = OnUndeliveredMessage;

            // If this event has handlers, then the IM will be considered
            // delivered. This will suppress the error message.
            //
            if (handlerUndeliveredMessage != null)
            {
                handlerUndeliveredMessage(im);
                result(true);
                return;
            }

            //m_log.DebugFormat("[INSTANT MESSAGE]: Undeliverable");
            result(false);
        }

        /// <summary>
        /// Process a XMLRPC Grid Instant Message
        /// </summary>
        /// <param name="request">XMLRPC parameters
        /// </param>
        /// <returns>Nothing much</returns>
        protected virtual XmlRpcResponse processXMLRPCGridInstantMessage(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            bool successful = false;
            
            // TODO: For now, as IMs seem to be a bit unreliable on OSGrid, catch all exception that
            // happen here and aren't caught and log them.
            try 
            {
                // various rational defaults
                UUID fromAgentID = UUID.Zero;
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
                if (requestData.ContainsKey("from_agent_id")
                        && requestData.ContainsKey("to_agent_id") && requestData.ContainsKey("im_session_id")
                        && requestData.ContainsKey("timestamp") && requestData.ContainsKey("from_agent_name")
                        && requestData.ContainsKey("message") && requestData.ContainsKey("dialog")
                        && requestData.ContainsKey("from_group")
                        && requestData.ContainsKey("offline") && requestData.ContainsKey("parent_estate_id")
                        && requestData.ContainsKey("position_x") && requestData.ContainsKey("position_y")
                        && requestData.ContainsKey("position_z") && requestData.ContainsKey("region_id")
                        && requestData.ContainsKey("binary_bucket"))
                {
                    // Do the easy way of validating the UUIDs
                    UUID.TryParse((string)requestData["from_agent_id"], out fromAgentID);
                    UUID.TryParse((string)requestData["to_agent_id"], out toAgentID);
                    UUID.TryParse((string)requestData["im_session_id"], out imSessionID);
                    UUID.TryParse((string)requestData["region_id"], out RegionID);

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
                    foreach (Scene scene in m_Scenes)
                    {
                        if (scene.Entities.ContainsKey(toAgentID) &&
                                scene.Entities[toAgentID] is ScenePresence)
                        {
                            ScenePresence user =
                                    (ScenePresence)scene.Entities[toAgentID];

                            if (!user.IsChildAgent)
                            {
                                scene.EventManager.TriggerIncomingInstantMessage(gim);
                                successful = true;
                            }
                        }
                    }
                    if (!successful)
                    {
                        // If the message can't be delivered to an agent, it
                        // is likely to be a group IM. On a group IM, the
                        // imSessionID = toAgentID = group id. Raise the
                        // unhandled IM event to give the groups module
                        // a chance to pick it up. We raise that in a random
                        // scene, since the groups module is shared.
                        //
                        m_Scenes[0].EventManager.TriggerUnhandledInstantMessage(gim);
                    }
                }
            }
            catch (Exception e)
            {
                m_log.Error("[INSTANT MESSAGE]: Caught unexpected exception:", e);
                successful = false;
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

        /// <summary>
        /// delegate for sending a grid instant message asynchronously
        /// </summary>
        public delegate void GridInstantMessageDelegate(GridInstantMessage im, MessageResultNotification result, ulong prevRegionHandle);

        protected virtual void GridInstantMessageCompleted(IAsyncResult iar)
        {
            GridInstantMessageDelegate icon =
                    (GridInstantMessageDelegate)iar.AsyncState;
            icon.EndInvoke(iar);
        }


        protected virtual void SendGridInstantMessageViaXMLRPC(GridInstantMessage im, MessageResultNotification result)
        {
            GridInstantMessageDelegate d = SendGridInstantMessageViaXMLRPCAsync;

            d.BeginInvoke(im, result, 0, GridInstantMessageCompleted, d);
        }

        /// <summary>
        /// Recursive SendGridInstantMessage over XMLRPC method.
        /// This is called from within a dedicated thread.
        /// The first time this is called, prevRegionHandle will be 0 Subsequent times this is called from 
        /// itself, prevRegionHandle will be the last region handle that we tried to send.
        /// If the handles are the same, we look up the user's location using the grid.
        /// If the handles are still the same, we end.  The send failed.
        /// </summary>
        /// <param name="prevRegionHandle">
        /// Pass in 0 the first time this method is called.  It will be called recursively with the last 
        /// regionhandle tried
        /// </param>
        protected virtual void SendGridInstantMessageViaXMLRPCAsync(GridInstantMessage im, MessageResultNotification result, ulong prevRegionHandle)
        {
            UUID toAgentID = new UUID(im.toAgentID);

            UserAgentData  upd = null;

            bool lookupAgent = false;

            lock (m_UserRegionMap)
            {
                if (m_UserRegionMap.ContainsKey(toAgentID))
                {
                    upd = new UserAgentData();
                    upd.AgentOnline = true;
                    upd.Handle = m_UserRegionMap[toAgentID];

                    // We need to compare the current regionhandle with the previous region handle
                    // or the recursive loop will never end because it will never try to lookup the agent again
                    if (prevRegionHandle == upd.Handle)
                    {
                        lookupAgent = true;
                    }
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
                upd = m_Scenes[0].CommsManager.UserService.GetAgentByUUID(toAgentID);

                if (upd != null)
                {
                    // check if we've tried this before..
                    // This is one way to end the recursive loop
                    //
                    if (upd.Handle == prevRegionHandle)
                    {
                        m_log.Error("[GRID INSTANT MESSAGE]: Unable to deliver an instant message");
                        HandleUndeliveredMessage(im, result);
                        return;
                    }
                }
                else
                {
                    m_log.Error("[GRID INSTANT MESSAGE]: Unable to deliver an instant message");
                    HandleUndeliveredMessage(im, result);
                    return;
                }
            }

            if (upd != null)
            {
                if (upd.AgentOnline)
                {
                    uint x = 0, y = 0;
                    Utils.LongToUInts(upd.Handle, out x, out y);
                    GridRegion reginfo = m_Scenes[0].GridService.GetRegionByPosition(m_Scenes[0].RegionInfo.ScopeID,
                        (int)x, (int)y);
                    if (reginfo != null)
                    {
                        Hashtable msgdata = ConvertGridInstantMessageToXMLRPC(im);
                        // Not actually used anymore, left in for compatibility
                        // Remove at next interface change
                        //
                        msgdata["region_handle"] = 0;
                        bool imresult = doIMSending(reginfo, msgdata);
                        if (imresult)
                        {
                            // IM delivery successful, so store the Agent's location in our local cache.
                            lock (m_UserRegionMap)
                            {
                                if (m_UserRegionMap.ContainsKey(toAgentID))
                                {
                                    m_UserRegionMap[toAgentID] = upd.Handle;
                                }
                                else
                                {
                                    m_UserRegionMap.Add(toAgentID, upd.Handle);
                                }
                            }
                            result(true);
                        }
                        else
                        {
                            // try again, but lookup user this time.
                            // Warning, this must call the Async version
                            // of this method or we'll be making thousands of threads
                            // The version within the spawned thread is SendGridInstantMessageViaXMLRPCAsync
                            // The version that spawns the thread is SendGridInstantMessageViaXMLRPC

                            // This is recursive!!!!!
                            SendGridInstantMessageViaXMLRPCAsync(im, result,
                                    upd.Handle);
                        }
                    }
                    else
                    {
                        m_log.WarnFormat("[GRID INSTANT MESSAGE]: Unable to find region {0}", upd.Handle);
                        HandleUndeliveredMessage(im, result);
                    }
                }
                else
                {
                    HandleUndeliveredMessage(im, result);
                }
            }
            else
            {
                m_log.WarnFormat("[GRID INSTANT MESSAGE]: Unable to find user {0}", toAgentID);
                HandleUndeliveredMessage(im, result);
            }
        }

        /// <summary>
        /// This actually does the XMLRPC Request
        /// </summary>
        /// <param name="reginfo">RegionInfo we pull the data out of to send the request to</param>
        /// <param name="xmlrpcdata">The Instant Message data Hashtable</param>
        /// <returns>Bool if the message was successfully delivered at the other side.</returns>
        protected virtual bool doIMSending(GridRegion reginfo, Hashtable xmlrpcdata)
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
//        private virtual ulong getLocalRegionHandleFromUUID(UUID regionID)
//        {
//            ulong returnhandle = 0;
//
//            lock (m_Scenes)
//            {
//                foreach (Scene sn in m_Scenes)
//                {
//                    if (sn.RegionInfo.RegionID == regionID)
//                    {
//                        returnhandle = sn.RegionInfo.RegionHandle;
//                        break;
//                    }
//                }
//            }
//            return returnhandle;
//        }

        /// <summary>
        /// Takes a GridInstantMessage and converts it into a Hashtable for XMLRPC
        /// </summary>
        /// <param name="msg">The GridInstantMessage object</param>
        /// <returns>Hashtable containing the XMLRPC request</returns>
        protected virtual Hashtable ConvertGridInstantMessageToXMLRPC(GridInstantMessage msg)
        {
            Hashtable gim = new Hashtable();
            gim["from_agent_id"] = msg.fromAgentID.ToString();
            // Kept for compatibility
            gim["from_agent_session"] = UUID.Zero.ToString();
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
