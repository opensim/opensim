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
using libsecondlife;
using Nini.Config;
using Nwc.XmlRpc;
using OpenSim.Framework;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Scenes;

namespace OpenSim.Region.Environment.Modules.Avatar.InstantMessage
{
    public class InstantMessageModule : IRegionModule
    {
        private readonly List<Scene> m_scenes = new List<Scene>();

        #region IRegionModule Members

        private bool gridmode = false;

        public void Initialise(Scene scene, IConfigSource config)
        {
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
                    scene.EventManager.OnGridInstantMessageToIMModule += OnGridInstantMessage;
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

        private void OnInstantMessage(IClientAPI client, LLUUID fromAgentID,
                                      LLUUID fromAgentSession, LLUUID toAgentID,
                                      LLUUID imSessionID, uint timestamp, string fromAgentName,
                                      string message, byte dialog, bool fromGroup, byte offline,
                                      uint ParentEstateID, LLVector3 Position, LLUUID RegionID,
                                      byte[] binaryBucket)
        {
            bool dialogHandledElsewhere
                = ((dialog == 38) || (dialog == 39) || (dialog == 40)
                   || dialog == (byte) InstantMessageDialog.InventoryOffered
                   || dialog == (byte) InstantMessageDialog.InventoryAccepted
                   || dialog == (byte) InstantMessageDialog.InventoryDeclined);

            // IM dialogs need to be pre-processed and have their sessionID filled by the server
            // so the sim can match the transaction on the return packet.

            // Don't send a Friend Dialog IM with a LLUUID.Zero session.
            if (!(dialogHandledElsewhere && imSessionID == LLUUID.Zero))
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
                            user.ControllingClient.SendInstantMessage(fromAgentID, fromAgentSession, message,
                                                                      toAgentID, imSessionID, fromAgentName, dialog,
                                                                      timestamp);
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

                        user.ControllingClient.SendInstantMessage(fromAgentID, fromAgentSession, message,
                                                                  toAgentID, imSessionID, fromAgentName, dialog,
                                                                  timestamp);
                        // Message sent
                        return;
                    }
                }
            }

            if (gridmode)
            {
                // Still here, try send via Grid
                // TODO

            }
        }

        // Trusty OSG1 called method.  This method also gets called from the FriendsModule
        // Turns out the sim has to send an instant message to the user to get it to show an accepted friend.

        private void OnGridInstantMessage(GridInstantMessage msg)
        {
            // Trigger the above event handler
            OnInstantMessage(null, new LLUUID(msg.fromAgentID), new LLUUID(msg.fromAgentSession),
                             new LLUUID(msg.toAgentID), new LLUUID(msg.imSessionID), msg.timestamp, msg.fromAgentName,
                             msg.message, msg.dialog, msg.fromGroup, msg.offline, msg.ParentEstateID,
                             new LLVector3(msg.Position.x, msg.Position.y, msg.Position.z), new LLUUID(msg.RegionID),
                             msg.binaryBucket);
        }
        protected virtual XmlRpcResponse processXMLRPCGridInstantMessage(XmlRpcRequest request)
        {
            // various rational defaults
            LLUUID fromAgentID = LLUUID.Zero;
            LLUUID fromAgentSession = LLUUID.Zero;
            LLUUID toAgentID = LLUUID.Zero;
            LLUUID imSessionID = LLUUID.Zero;
            uint timestamp = 0;
            string fromAgentName = "";
            string message = "";
            byte dialog = (byte)0; 
            bool fromGroup = false;
            byte offline = (byte)0;
            uint ParentEstateID;
            LLVector3 Position = LLVector3.Zero;
            LLUUID RegionID = LLUUID.Zero ;
            byte[] binaryBucket = new byte[0];

            float pos_x = 0;
            float pos_y = 0;
            float pos_z = 0;



            Hashtable requestData = (Hashtable)request.Params[0];

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
                Helpers.TryParse((string)requestData["from_agent_id"], out fromAgentID);
                Helpers.TryParse((string)requestData["from_agent_session"], out fromAgentSession);
                Helpers.TryParse((string)requestData["to_agent_id"], out toAgentID);
                Helpers.TryParse((string)requestData["im_session_id"], out imSessionID);
                Helpers.TryParse((string)requestData["region_id"], out RegionID);

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
                dialog = (byte)requestData["dialog"];
                
                if ((string)requestData["from_group"] == "TRUE")
                    fromGroup = true;

                offline = (byte)requestData["offline"];

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

                Position = new LLVector3(pos_x, pos_y, pos_z);
                binaryBucket = (byte[])requestData["binary_bucket"];
            }

            return new XmlRpcResponse();
            //(string)
            //(string)requestData["message"];

        }

        protected virtual void SendGridInstantMessageViaXMLRPC(IClientAPI client, LLUUID fromAgentID,
                                      LLUUID fromAgentSession, LLUUID toAgentID,
                                      LLUUID imSessionID, uint timestamp, string fromAgentName,
                                      string message, byte dialog, bool fromGroup, byte offline,
                                      uint ParentEstateID, LLVector3 Position, LLUUID RegionID,
                                      byte[] binaryBucket)
        {

        }
    }
}