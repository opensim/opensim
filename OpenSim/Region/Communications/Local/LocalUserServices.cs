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
using OpenSim.Framework;
using OpenSim.Framework.Communications;
using log4net;
using System.Reflection;

namespace OpenSim.Region.Communications.Local
{
    public class LocalUserServices : UserManagerBase
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private readonly uint m_defaultHomeX;
        private readonly uint m_defaultHomeY;

        /// <summary>
        /// User services used when OpenSim is running in standalone mode.
        /// </summary>
        /// <param name="defaultHomeLocX"></param>
        /// <param name="defaultHomeLocY"></param>
        /// <param name="commsManager"></param>
        public LocalUserServices(
            uint defaultHomeLocX, uint defaultHomeLocY, CommunicationsManager commsManager)
            : base(commsManager)
        {
            m_defaultHomeX = defaultHomeLocX;
            m_defaultHomeY = defaultHomeLocY;
        }

        public override UserProfileData SetupMasterUser(string firstName, string lastName)
        {
            return SetupMasterUser(firstName, lastName, String.Empty);
        }

        public override UserProfileData SetupMasterUser(string firstName, string lastName, string password)
        {
            UserProfileData profile = GetUserProfile(firstName, lastName);
            if (profile != null)
            {
                return profile;
            }

            m_log.Debug("Unknown Master User. Sandbox Mode: Creating Account");
            AddUser(firstName, lastName, password, "", m_defaultHomeX, m_defaultHomeY);
            return GetUserProfile(firstName, lastName);
        }

        public override UserProfileData SetupMasterUser(UUID uuid)
        {
            UserProfileData data = GetUserProfile(uuid);
            if (data == null)
            {
                throw new Exception("[LOCAL USER SERVICES]: Unknown master user UUID. Possible reason: UserServer is not running.");
            }
            return data;
        }

        public override bool AuthenticateUserByPassword(UUID userID, string password)
        {
            UserProfileData userProfile = GetUserProfile(userID);

            if (null == userProfile)
                return false;
      
            string md5PasswordHash = Util.Md5Hash(Util.Md5Hash(password) + ":" + userProfile.PasswordSalt);
    
            if (md5PasswordHash == userProfile.PasswordHash)
                return true;
            else
                return false;
        }
    }
}