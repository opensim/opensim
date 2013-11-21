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

        public PresenceInfo()
        {
        }

        public PresenceInfo(Dictionary<string, object> kvp)
        {
            if (kvp.ContainsKey("UserID"))
                UserID = kvp["UserID"].ToString();
            if (kvp.ContainsKey("RegionID"))
                UUID.TryParse(kvp["RegionID"].ToString(), out RegionID);
        }

        public Dictionary<string, object> ToKeyValuePairs()
        {
            Dictionary<string, object> result = new Dictionary<string, object>();
            result["UserID"] = UserID;
            result["RegionID"] = RegionID.ToString();

            return result;
        }
    }

    public interface IPresenceService
    {
        /// <summary>
        /// Store session information.
        /// </summary>
        /// <returns>/returns>
        /// <param name='userID'></param>
        /// <param name='sessionID'></param>
        /// <param name='secureSessionID'></param>
        bool LoginAgent(string userID, UUID sessionID, UUID secureSessionID);

        /// <summary>
        /// Remove session information.
        /// </summary>
        /// <returns></returns>
        /// <param name='sessionID'></param>
        bool LogoutAgent(UUID sessionID);

        /// <summary>
        /// Remove session information for all agents in the given region.
        /// </summary>
        /// <returns></returns>
        /// <param name='regionID'></param>
        bool LogoutRegionAgents(UUID regionID);

        /// <summary>
        /// Update data for an existing session.
        /// </summary>
        /// <returns></returns>
        /// <param name='sessionID'></param>
        /// <param name='regionID'></param>
        bool ReportAgent(UUID sessionID, UUID regionID);

        /// <summary>
        /// Get session information for a given session ID.
        /// </summary>
        /// <returns></returns>
        /// <param name='sessionID'></param>
        PresenceInfo GetAgent(UUID sessionID);

        /// <summary>
        /// Get session information for a collection of users.
        /// </summary>
        /// <returns>Session information for the users.</returns>
        /// <param name='userIDs'></param>
        PresenceInfo[] GetAgents(string[] userIDs);
    }
}