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
using System.Text.RegularExpressions;
using System.Xml.Serialization;
using log4net;
using Nwc.XmlRpc;
using OpenMetaverse;
using OpenSim.Data;
using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Communications.Clients;

namespace OpenSim.Region.Communications.OGS1
{
    public class OGS1UserServices : UserManagerBase
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        
        public OGS1UserServices(CommunicationsManager commsManager)
            : base(commsManager)
        {
        }
        
        public override void ClearUserAgent(UUID avatarID)
        {
            // TODO: implement
            // It may be possible to use the UserManagerBase implementation.
        }
        
        protected virtual string GetUserServerURL(UUID userID)
        {
            return m_commsManager.NetworkServersInfo.UserURL;
        }
        
        /// <summary>
        /// Logs off a user on the user server
        /// </summary>
        /// <param name="UserID">UUID of the user</param>
        /// <param name="regionID">UUID of the Region</param>
        /// <param name="regionhandle">regionhandle</param>
        /// <param name="position">final position</param>
        /// <param name="lookat">final lookat</param>
        public override void LogOffUser(UUID userid, UUID regionid, ulong regionhandle, Vector3 position, Vector3 lookat)
        {
            Hashtable param = new Hashtable();
            param["avatar_uuid"] = userid.Guid.ToString();
            param["region_uuid"] = regionid.Guid.ToString();
            param["region_handle"] = regionhandle.ToString();
            param["region_pos_x"] = position.X.ToString();
            param["region_pos_y"] = position.Y.ToString();
            param["region_pos_z"] = position.Z.ToString();
            param["lookat_x"] = lookat.X.ToString();
            param["lookat_y"] = lookat.Y.ToString();
            param["lookat_z"] = lookat.Z.ToString();

            IList parameters = new ArrayList();
            parameters.Add(param);
            XmlRpcRequest req = new XmlRpcRequest("logout_of_simulator", parameters);

            try
            {
                req.Send(GetUserServerURL(userid), 3000);
            }
            catch (WebException)
            {
                m_log.Warn("[LOGOFF]: Unable to notify grid server of user logoff");
            }
        }
            
        /// <summary>
        /// Retrieve the user information for the given master uuid.
        /// </summary>
        /// <param name="uuid"></param>
        /// <returns></returns>
        public override UserProfileData SetupMasterUser(string firstName, string lastName)
        {
            return SetupMasterUser(firstName, lastName, String.Empty);
        }

        /// <summary>
        /// Retrieve the user information for the given master uuid.
        /// </summary>
        /// <param name="uuid"></param>
        /// <returns></returns>
        public override UserProfileData SetupMasterUser(string firstName, string lastName, string password)
        {
            UserProfileData profile = GetUserProfile(firstName, lastName);
            return profile;
        }

        /// <summary>
        /// Retrieve the user information for the given master uuid.
        /// </summary>
        /// <param name="uuid"></param>
        /// <returns></returns>
        public override UserProfileData SetupMasterUser(UUID uuid)
        {
            UserProfileData data = GetUserProfile(uuid);

            if (data == null)
            {
                throw new Exception(
                    "Could not retrieve profile for master user " + uuid + ".  User server did not respond to the request.");
            }

            return data;
        }
        
        public override bool VerifySession(UUID userID, UUID sessionID)
        {
            m_log.DebugFormat("[OGS1 USER SERVICES]: Verifying user session for " + userID);
            return AuthClient.VerifySession(GetUserServerURL(userID), userID, sessionID);
        }

        public override bool AuthenticateUserByPassword(UUID userID, string password)
        {
            Hashtable param = new Hashtable();
            param["user_uuid"] = userID.ToString();
            param["password"] = password;
            IList parameters = new ArrayList();
            parameters.Add(param);
            XmlRpcRequest req = new XmlRpcRequest("authenticate_user_by_password", parameters);
            XmlRpcResponse resp = req.Send(m_commsManager.NetworkServersInfo.UserURL, 30000);

            // Temporary measure to deal with older services
            if (resp.IsFault && resp.FaultCode == XmlRpcErrorCodes.SERVER_ERROR_METHOD)
            {
                throw new Exception(
                    String.Format(
                        "XMLRPC method 'authenticate_user_by_password' not yet implemented by user service at {0}",
                        m_commsManager.NetworkServersInfo.UserURL));
            }

            Hashtable respData = (Hashtable)resp.Value;

            if ((string)respData["auth_user"] == "TRUE")
            {
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}