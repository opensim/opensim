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
using Mono.Addins;
using Nini.Config;
using Nwc.XmlRpc;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Servers;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using GridRegion = OpenSim.Services.Interfaces.GridRegion;
using PresenceInfo = OpenSim.Services.Interfaces.PresenceInfo;
using OpenSim.Services.Interfaces;

namespace OpenSim.Region.CoreModules.Avatar.InstantMessage
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "MessageTransferModule")]
    public class MessageTransferModule : ISharedRegionModule, IMessageTransferModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private bool m_Enabled = false;
        protected List<Scene> m_Scenes = new List<Scene>();
        protected Dictionary<UUID, UUID> m_UserRegionMap = new Dictionary<UUID, UUID>();

        public event UndeliveredMessage OnUndeliveredMessage;

        private IPresenceService m_PresenceService;
        protected IPresenceService PresenceService
        {
            get
            {
                if (m_PresenceService == null)
                    m_PresenceService = m_Scenes[0].RequestModuleInterface<IPresenceService>();
                return m_PresenceService;
            }
        }

        public virtual void Initialise(IConfigSource config)
        {
            IConfig cnf = config.Configs["Messaging"];
            if (cnf != null && cnf.GetString(
                    "MessageTransferModule", "MessageTransferModule") !=
                    "MessageTransferModule")
            {
                m_log.Debug("[MESSAGE TRANSFER]: Disabled by configuration");
                return;
            }

            m_Enabled = true;
        }

        public virtual void AddRegion(Scene scene)
        {
            if (!m_Enabled)
                return;

            lock (m_Scenes)
            {
                m_log.Debug("[MESSAGE TRANSFER]: Message transfer module active");
                scene.RegisterModuleInterface<IMessageTransferModule>(this);
                m_Scenes.Add(scene);
            }
        }

        public virtual void PostInitialise()
        {
            if (!m_Enabled)
                return;

            MainServer.Instance.AddXmlRPCHandler(
                "grid_instant_message", processXMLRPCGridInstantMessage);
        }

        public virtual void RegionLoaded(Scene scene)
        {
        }

        public virtual void RemoveRegion(Scene scene)
        {
            if (!m_Enabled)
                return;

            lock (m_Scenes)
            {
                m_Scenes.Remove(scene);
            }
        }

        public virtual void Close()
        {
        }

        public virtual string Name
        {
            get { return "MessageTransferModule"; }
        }

        public virtual Type ReplaceableInterface
        {
            get { return null; }
        }

        public virtual void SendInstantMessage(GridInstantMessage im, MessageResultNotification result)
        {
            UUID toAgentID = new UUID(im.toAgentID);

            // Try root avatar only first
            foreach (Scene scene in m_Scenes)
            {
//                m_log.DebugFormat(
//                    "[INSTANT MESSAGE]: Looking for root agent {0} in {1}",
//                    toAgentID.ToString(), scene.RegionInfo.RegionName);

                ScenePresence sp = scene.GetScenePresence(toAgentID);
                if (sp != null && !sp.IsChildAgent)
                {
                    // Local message
//                    m_log.DebugFormat("[INSTANT MESSAGE]: Delivering IM to root agent {0} {1}", sp.Name, toAgentID);

                    sp.ControllingClient.SendInstantMessage(im);

                    // Message sent
                    result(true);
                    return;
                }
            }

            // try child avatar second
            foreach (Scene scene in m_Scenes)
            {
//                m_log.DebugFormat(
//                    "[INSTANT MESSAGE]: Looking for child of {0} in {1}", toAgentID, scene.RegionInfo.RegionName);

                ScenePresence sp = scene.GetScenePresence(toAgentID);
                if (sp != null)
                {
                    // Local message
//                    m_log.DebugFormat("[INSTANT MESSAGE]: Delivering IM to child agent {0} {1}", sp.Name, toAgentID);

                    sp.ControllingClient.SendInstantMessage(im);

                    // Message sent
                    result(true);
                    return;
                }
            }

//            m_log.DebugFormat("[INSTANT MESSAGE]: Delivering IM to {0} via XMLRPC", im.toAgentID);

            SendGridInstantMessageViaXMLRPC(im, result);
        }

        public void HandleUndeliverableMessage(GridInstantMessage im, MessageResultNotification result)
        {
            UndeliveredMessage handlerUndeliveredMessage = OnUndeliveredMessage;

            // If this event has handlers, then an IM from an agent will be
            // considered delivered. This will suppress the error message.
            //
            if (handlerUndeliveredMessage != null)
            {
                handlerUndeliveredMessage(im);
                if (im.dialog == (byte)InstantMessageDialog.MessageFromAgent)
                    result(true);
                else
                    result(false);
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
                    if (message == null)
                        message = string.Empty;

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
                        ScenePresence sp = scene.GetScenePresence(toAgentID);
                        if (sp != null && !sp.IsChildAgent)
                        {
                            scene.EventManager.TriggerIncomingInstantMessage(gim);
                            successful = true;
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
        public delegate void GridInstantMessageDelegate(GridInstantMessage im, MessageResultNotification result);

        protected virtual void GridInstantMessageCompleted(IAsyncResult iar)
        {
            GridInstantMessageDelegate icon =
                    (GridInstantMessageDelegate)iar.AsyncState;
            icon.EndInvoke(iar);
        }


        protected virtual void SendGridInstantMessageViaXMLRPC(GridInstantMessage im, MessageResultNotification result)
        {
            GridInstantMessageDelegate d = SendGridInstantMessageViaXMLRPCAsync;

            d.BeginInvoke(im, result, GridInstantMessageCompleted, d);
        }

        /// <summary>
        /// Internal SendGridInstantMessage over XMLRPC method.
        /// </summary>
        /// <remarks>
        /// This is called from within a dedicated thread.
        /// </remarks>
        private void SendGridInstantMessageViaXMLRPCAsync(GridInstantMessage im, MessageResultNotification result)
        {
            UUID toAgentID = new UUID(im.toAgentID);
            UUID regionID;
            bool needToLookupAgent;

            lock (m_UserRegionMap)
                needToLookupAgent = !m_UserRegionMap.TryGetValue(toAgentID, out regionID);

            while (true)
            {
                if (needToLookupAgent)
                {
                    PresenceInfo[] presences = PresenceService.GetAgents(new string[] { toAgentID.ToString() }); 

                    UUID foundRegionID = UUID.Zero;

                    if (presences != null)
                    {
                        foreach (PresenceInfo p in presences)
                        {
                            if (p.RegionID != UUID.Zero)
                            {
                                foundRegionID = p.RegionID;
                                break;
                            }
                        }
                    }

                    // If not found or the found region is the same as the last lookup, then message is undeliverable
                    if (foundRegionID == UUID.Zero || foundRegionID == regionID)
                        break;
                    else
                        regionID = foundRegionID;
                }

                GridRegion reginfo = m_Scenes[0].GridService.GetRegionByUUID(m_Scenes[0].RegionInfo.ScopeID, regionID);
                if (reginfo == null)
                {
                    m_log.WarnFormat("[GRID INSTANT MESSAGE]: Unable to find region {0}", regionID);
                    break;
                }

                // Try to send the message to the agent via the retrieved region.
                Hashtable msgdata = ConvertGridInstantMessageToXMLRPC(im);
                msgdata["region_handle"] = 0;
                bool imresult = doIMSending(reginfo, msgdata);

                // If the message delivery was successful, then cache the entry.
                if (imresult)
                {
                    lock (m_UserRegionMap)
                    {
                        m_UserRegionMap[toAgentID] = regionID;
                    }
                    result(true);
                    return;
                }

                // If we reach this point in the first iteration of the while, then we may have unsuccessfully tried
                // to use a locally cached region ID.  All subsequent attempts need to lookup agent details from
                // the presence service.
                needToLookupAgent = true;
            }

            // If we reached this point then the message was not deliverable.  Remove the bad cache entry and 
            // signal the delivery failure.
            lock (m_UserRegionMap)
                m_UserRegionMap.Remove(toAgentID);

            // m_log.Error("[GRID INSTANT MESSAGE]: Unable to deliver an instant message");
            HandleUndeliverableMessage(im, result);
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

                XmlRpcResponse GridResp = GridReq.Send(reginfo.ServerURI, 3000);

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
                m_log.ErrorFormat("[GRID INSTANT MESSAGE]: Error sending message to {0} the host didn't respond " + e.ToString(), reginfo.ServerURI.ToString());
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
            gim["region_id"] = new UUID(msg.RegionID).ToString();
            gim["binary_bucket"] = Convert.ToBase64String(msg.binaryBucket,Base64FormattingOptions.None);
            return gim;
        }

    }
}
