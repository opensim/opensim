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
using Nwc.XmlRpc;
using OpenMetaverse;

namespace OpenSim.Framework.Communications.Clients
{
    public class AuthClient
    {
        public static string GetNewKey(string authurl, UUID userID, UUID authToken)
        {
            //Hashtable keyParams = new Hashtable();
            //keyParams["user_id"] = userID;
            //keyParams["auth_token"] = authKey;

            List<string> SendParams = new List<string>();
            SendParams.Add(userID.ToString());
            SendParams.Add(authToken.ToString());

            XmlRpcRequest request = new XmlRpcRequest("hg_new_auth_key", SendParams);
            XmlRpcResponse reply;
            try
            {
                reply = request.Send(authurl, 6000);
            }
            catch (Exception e)
            {
                System.Console.WriteLine("[HGrid]: Failed to get new key. Reason: " + e.Message);
                return string.Empty;
            }

            if (!reply.IsFault)
            {
                string newKey = string.Empty;
                if (reply.Value != null)
                    newKey = (string)reply.Value;

                return newKey;
            }
            else
            {
                System.Console.WriteLine("[HGrid]: XmlRpc request to get auth key failed with message {0}" + reply.FaultString + ", code " + reply.FaultCode);
                return string.Empty;
            }

        }

        public static bool VerifyKey(string authurl, UUID userID, string authKey)
        {
            List<string> SendParams = new List<string>();
            SendParams.Add(userID.ToString());
            SendParams.Add(authKey);

            System.Console.WriteLine("[HGrid]: Verifying user key with authority " + authurl);

            XmlRpcRequest request = new XmlRpcRequest("hg_verify_auth_key", SendParams);
            XmlRpcResponse reply;
            try
            {
                reply = request.Send(authurl, 10000);
            }
            catch (Exception e)
            {
                System.Console.WriteLine("[HGrid]: Failed to verify key. Reason: " + e.Message);
                return false;
            }

            if (reply != null)
            {
                if (!reply.IsFault)
                {
                    bool success = false;
                    if (reply.Value != null)
                        success = (bool)reply.Value;

                    return success;
                }
                else
                {
                    System.Console.WriteLine("[HGrid]: XmlRpc request to verify key failed with message {0}" + reply.FaultString + ", code " + reply.FaultCode);
                    return false;
                }
            }
            else
            {
                System.Console.WriteLine("[HGrid]: XmlRpc request to verify key returned null reply");
                return false;
            }
        }

        public static bool VerifySession(string authurl, UUID userID, UUID sessionID)
        {
            Hashtable requestData = new Hashtable();
            requestData["avatar_uuid"] = userID.ToString();
            requestData["session_id"] = sessionID.ToString();
            ArrayList SendParams = new ArrayList();
            SendParams.Add(requestData);
            XmlRpcRequest UserReq = new XmlRpcRequest("check_auth_session", SendParams);
            XmlRpcResponse UserResp = null;
            try
            {
                UserResp = UserReq.Send(authurl, 3000);
            }
            catch (Exception e)
            {
                System.Console.WriteLine("[Session Auth]: VerifySession XmlRpc: " + e.Message); 
                return false;
            }

            Hashtable responseData = (Hashtable)UserResp.Value;
            if (responseData.ContainsKey("auth_session") && responseData["auth_session"].ToString() == "TRUE")
            {
                //System.Console.WriteLine("[Authorization]: userserver reported authorized session for user " + userID);
                return true;
            }
            else
            {
                //System.Console.WriteLine("[Authorization]: userserver reported unauthorized session for user " + userID);
                return false;
            }
        }
    }
}
