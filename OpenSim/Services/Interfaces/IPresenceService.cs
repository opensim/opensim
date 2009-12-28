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
using OpenSim.Framework;
using System.Collections.Generic;
using OpenMetaverse;

namespace OpenSim.Services.Interfaces
{
    public class PresenceInfo
    {
        public string UserID;
        public UUID RegionID;
        public bool Online;
        public DateTime Login;
        public DateTime Logout;
        public Vector3 Position;
        public Vector3 LookAt;
        public UUID HomeRegionID;
        public Vector3 HomePosition;
        public Vector3 HomeLookAt;

        public PresenceInfo()
        {
        }

        public PresenceInfo(Dictionary<string, object> kvp)
        {
            if (kvp.ContainsKey("UserID"))
                UserID = kvp["UserID"].ToString();
            if (kvp.ContainsKey("RegionID"))
                UUID.TryParse(kvp["RegionID"].ToString(), out RegionID);
            if (kvp.ContainsKey("login"))
                DateTime.TryParse(kvp["login"].ToString(), out Login);
            if (kvp.ContainsKey("logout"))
                DateTime.TryParse(kvp["logout"].ToString(), out Logout);
            if (kvp.ContainsKey("lookAt"))
                Vector3.TryParse(kvp["lookAt"].ToString(), out LookAt);
            if (kvp.ContainsKey("online"))
                Boolean.TryParse(kvp["online"].ToString(), out Online);
            if (kvp.ContainsKey("position"))
                Vector3.TryParse(kvp["position"].ToString(), out Position);

        }

        public Dictionary<string, object> ToKeyValuePairs()
        {
            Dictionary<string, object> result = new Dictionary<string, object>();
            result["UserID"] = UserID;
            result["RegionID"] = RegionID.ToString();
            result["online"] = Online.ToString();
            result["login"] = Login.ToString();
            result["logout"] = Logout.ToString();
            result["position"] = Position.ToString();
            result["lookAt"] = LookAt.ToString();

            return result;
        }
    }

    public interface IPresenceService
    {
        bool LoginAgent(string userID, UUID sessionID, UUID secureSessionID);
        bool LogoutAgent(UUID sessionID);
        bool LogoutRegionAgents(UUID regionID);

        bool ReportAgent(UUID sessionID, UUID regionID, Vector3 position, Vector3 lookAt);
        bool SetHomeLocation(string userID, UUID regionID, Vector3 position, Vector3 lookAt);

        PresenceInfo GetAgent(UUID sessionID);
        PresenceInfo[] GetAgents(string[] userIDs);
    }
}
