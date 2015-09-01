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
using OpenMetaverse;

namespace OpenSim.Framework
{
    /// <summary>
    /// Information about a users session
    /// </summary>
    public class UserAgentData
    {
        /// <summary>
        /// The UUID of the users avatar (not the agent!)
        /// </summary>
        private UUID UUID;

        /// <summary>
        /// The session ID for the user (also the agent ID)
        /// </summary>
        private UUID sessionID;

        /// <summary>
        /// The "secure" session ID for the user
        /// </summary>
        /// <remarks>Not very secure. Dont rely on it for anything more than Linden Lab does.</remarks>
        private UUID secureSessionID;

        /// <summary>
        /// The IP address of the user
        /// </summary>
        private string agentIP = String.Empty;

        /// <summary>
        /// The port of the user
        /// </summary>
        private uint agentPort;

        /// <summary>
        /// Is the user online?
        /// </summary>
        private bool agentOnline;

        /// <summary>
        /// A unix timestamp from when the user logged in
        /// </summary>
        private int loginTime;

        /// <summary>
        /// When this agent expired and logged out, 0 if still online
        /// </summary>
        private int logoutTime;

        /// <summary>
        /// Region ID the user is logged into
        /// </summary>
        private UUID regionID;

        /// <summary>
        /// Region handle of the current region the user is in
        /// </summary>
        private ulong regionHandle;

        /// <summary>
        /// The position of the user within the region
        /// </summary>
        private Vector3 currentPos;

        /// <summary>
        /// Current direction the user is looking at
        /// </summary>
        private Vector3 currentLookAt = Vector3.Zero;

        /// <summary>
        /// The region the user logged into initially
        /// </summary>
        private UUID originRegionID;

        public virtual UUID ProfileID
        {
            get { return UUID; }
            set { UUID = value; }
        }

        public virtual UUID SessionID
        {
            get { return sessionID; }
            set { sessionID = value; }
        }

        public virtual UUID SecureSessionID
        {
            get { return secureSessionID; }
            set { secureSessionID = value; }
        }

        public virtual string AgentIP
        {
            get { return agentIP; }
            set { agentIP = value; }
        }

        public virtual uint AgentPort
        {
            get { return agentPort; }
            set { agentPort = value; }
        }

        public virtual bool AgentOnline
        {
            get { return agentOnline; }
            set { agentOnline = value; }
        }

        public virtual int LoginTime
        {
            get { return loginTime; }
            set { loginTime = value; }
        }

        public virtual int LogoutTime
        {
            get { return logoutTime; }
            set { logoutTime = value; }
        }

        public virtual UUID Region
        {
            get { return regionID; }
            set { regionID = value; }
        }

        public virtual ulong Handle
        {
            get { return regionHandle; }
            set { regionHandle = value; }
        }

        public virtual Vector3 Position
        {
            get { return currentPos; }
            set { currentPos = value; }
        }

/* 2008-08-28-tyre: Not really useful
        public virtual float PositionX
        {
            get { return currentPos.X; }
            set { currentPos.X = value; }
        }

        public virtual float PositionY
        {
            get { return currentPos.Y; }
            set { currentPos.Y = value; }
        }

        public virtual float PositionZ
        {
            get { return currentPos.Z; }
            set { currentPos.Z = value; }
        }
*/

        public virtual Vector3 LookAt
        {
            get { return currentLookAt; }
            set { currentLookAt = value; }
        }

        public virtual UUID InitialRegion
        {
            get { return originRegionID; }
            set { originRegionID = value; }
        }

    }
}
