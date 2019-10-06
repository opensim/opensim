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

using OpenMetaverse;
using Nwc.XmlRpc;
using log4net;

using OpenSim.Framework;

namespace OpenSim.Services.Connectors.InstantMessage
{
    public class InstantMessageServiceConnector
    {
        private static readonly ILog m_log =
            LogManager.GetLogger(
            MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// This actually does the XMLRPC Request
        /// </summary>
        /// <param name="url">URL we pull the data out of to send the request to</param>
        /// <param name="im">The Instant Message </param>
        /// <returns>Bool if the message was successfully delivered at the other side.</returns>
        public static bool SendInstantMessage(string url, GridInstantMessage im, string messageKey)
        {
            Hashtable xmlrpcdata = ConvertGridInstantMessageToXMLRPC(im, messageKey);
            xmlrpcdata["region_handle"] = 0;

            ArrayList SendParams = new ArrayList();
            SendParams.Add(xmlrpcdata);
            XmlRpcRequest GridReq = new XmlRpcRequest("grid_instant_message", SendParams);
            try
            {

                XmlRpcResponse GridResp = GridReq.Send(url, 10000);

                Hashtable responseData = (Hashtable)GridResp.Value;

                if (responseData.ContainsKey("success"))
                {
                    if ((string)responseData["success"] == "TRUE")
                    {
                        //m_log.DebugFormat("[XXX] Success");
                        return true;
                    }
                    else
                    {
                        //m_log.DebugFormat("[XXX] Fail");
                        return false;
                    }
                }
                else
                {
                    m_log.DebugFormat("[GRID INSTANT MESSAGE]: No response from {0}", url);
                    return false;
                }
            }
            catch (WebException e)
            {
                m_log.ErrorFormat("[GRID INSTANT MESSAGE]: Error sending message to {0} the host didn't respond " + e.ToString(), url);
            }

            return false;
        }

        /// <summary>
        /// Takes a GridInstantMessage and converts it into a Hashtable for XMLRPC
        /// </summary>
        /// <param name="msg">The GridInstantMessage object</param>
        /// <returns>Hashtable containing the XMLRPC request</returns>
        protected static Hashtable ConvertGridInstantMessageToXMLRPC(GridInstantMessage msg, string messageKey)
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
            byte[] dialogdata = new byte[1]; dialogdata[0] = msg.dialog;
            gim["dialog"] = Convert.ToBase64String(dialogdata, Base64FormattingOptions.None);

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
            gim["binary_bucket"] = Convert.ToBase64String(msg.binaryBucket, Base64FormattingOptions.None);
            gim["region_id"] = new UUID(msg.RegionID).ToString();

            if (messageKey != String.Empty)
                gim["message_key"] = messageKey;

            return gim;
        }

    }
}
