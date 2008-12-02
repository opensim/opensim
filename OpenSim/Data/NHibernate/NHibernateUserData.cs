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
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using OpenMetaverse;
using log4net;
using NHibernate;
using NHibernate.Criterion;
using OpenSim.Framework;
using Environment=NHibernate.Cfg.Environment;

namespace OpenSim.Data.NHibernate
{
    /// <summary>
    /// A User storage interface for the DB4o database system
    /// </summary>
    public class NHibernateUserData : UserDataBase
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private NHibernateManager manager;

        public override void Initialise()
        {
            m_log.Info("[NHibernateUserData]: " + Name + " cannot be default-initialized!");
            throw new PluginNotInitialisedException (Name);
        }

        public override void Initialise(string connect)
        {
            m_log.InfoFormat("[NHIBERNATE] Initializing NHibernateUserData");
            manager = new NHibernateManager(connect, "UserStore");
        }

        private bool ExistsUser(UUID uuid)
        {
            UserProfileData user = null;

            m_log.InfoFormat("[NHIBERNATE] ExistsUser; {0}", uuid);
            user = (UserProfileData)manager.Load(typeof(UserProfileData), uuid);
            
            if (user == null)
            {
                m_log.InfoFormat("[NHIBERNATE] User with given UUID does not exist {0} ", uuid);
                return false;
            }

            return true;

        }

        override public UserProfileData GetUserByUUID(UUID uuid)
        {
            UserProfileData user;
            m_log.InfoFormat("[NHIBERNATE] GetUserByUUID: {0} ", uuid);
            
            user = (UserProfileData)manager.Load(typeof(UserProfileData), uuid);
            if (user != null)    
            {
                UserAgentData agent = GetAgentByUUID(uuid);
                if (agent != null)
                {
                    user.CurrentAgent = agent;
                }
            }

            return user;
        }

        override public void AddNewUserProfile(UserProfileData profile)
        {
            if (!ExistsUser(profile.ID))
            {
                m_log.InfoFormat("[NHIBERNATE] AddNewUserProfile {0}", profile.ID);
                manager.Save(profile);
                SetAgentData(profile.ID, profile.CurrentAgent);

            }
            else
            {
                m_log.ErrorFormat("[NHIBERNATE] Attempted to add User {0} {1} that already exists, updating instead", profile.FirstName, profile.SurName);
                UpdateUserProfile(profile);
            }
        }

        private void SetAgentData(UUID uuid, UserAgentData agent)
        {
            UserAgentData old = (UserAgentData)manager.Load(typeof(UserAgentData), uuid);
            if (old != null)
            {
                m_log.InfoFormat("[NHIBERNATE] SetAgentData deleting old: {0} ",uuid);
                manager.Delete(old);
            }
            if (agent != null)
            {
                m_log.InfoFormat("[NHIBERNATE] SetAgentData: {0} ", agent.ProfileID);
                manager.Save(agent);
            }

        }
        override public bool UpdateUserProfile(UserProfileData profile)
        {
            if (ExistsUser(profile.ID))
            {
                manager.Update(profile);
                SetAgentData(profile.ID, profile.CurrentAgent);
                return true;
            }
            else
            {
                m_log.ErrorFormat("[NHIBERNATE] Attempted to update User {0} {1} that doesn't exist, updating instead", profile.FirstName, profile.SurName);
                AddNewUserProfile(profile);
                return true;
            }
        }

        override public void AddNewUserAgent(UserAgentData agent)
        {
            UserAgentData old = (UserAgentData)manager.Load(typeof(UserAgentData), agent.ProfileID);
            if (old != null)
            {
                manager.Delete(old);
            }
            
            manager.Save(agent);
            
        }

        public void UpdateUserAgent(UserAgentData agent)
        {
            m_log.InfoFormat("[NHIBERNATE] UpdateUserAgent: {0} ", agent.ProfileID);
            manager.Update(agent);
        }

        override public UserAgentData GetAgentByUUID(UUID uuid)
        {
            m_log.InfoFormat("[NHIBERNATE] GetAgentByUUID: {0} ", uuid);
            return (UserAgentData)manager.Load(typeof(UserAgentData), uuid);
        }

        override public UserProfileData GetUserByName(string fname, string lname)
        {
            m_log.InfoFormat("[NHIBERNATE] GetUserByName: {0} {1} ", fname, lname);
            ICriteria criteria = manager.GetSession().CreateCriteria(typeof(UserProfileData));
            criteria.Add(Expression.Eq("FirstName", fname));
            criteria.Add(Expression.Eq("SurName", lname));
            foreach (UserProfileData profile in criteria.List())
            {
                profile.CurrentAgent = GetAgentByUUID(profile.ID);
                return profile;
            }
            return null;
        }

        override public UserAgentData GetAgentByName(string fname, string lname)
        {
            return GetUserByName(fname, lname).CurrentAgent;
        }

        override public UserAgentData GetAgentByName(string name)
        {
            return GetAgentByName(name.Split(' ')[0], name.Split(' ')[1]);
        }

        override public List<AvatarPickerAvatar> GeneratePickerResults(UUID queryID, string query)
        {
            List<AvatarPickerAvatar> results = new List<AvatarPickerAvatar>();
            string[] querysplit;
            querysplit = query.Split(' ');

            if (querysplit.Length == 2)
            {
                ICriteria criteria = manager.GetSession().CreateCriteria(typeof(UserProfileData));
                criteria.Add(Expression.Like("FirstName", querysplit[0]));
                criteria.Add(Expression.Like("SurName", querysplit[1]));
                foreach (UserProfileData profile in criteria.List())
                {
                    AvatarPickerAvatar user = new AvatarPickerAvatar();
                    user.AvatarID = profile.ID;
                    user.firstName = profile.FirstName;
                    user.lastName = profile.SurName;
                    results.Add(user);
                }
            }
            return results;
        }

        // TODO: actually implement these
        public override void StoreWebLoginKey(UUID agentID, UUID webLoginKey) { return; }
        public override void AddNewUserFriend(UUID friendlistowner, UUID friend, uint perms) { return; }
        public override void RemoveUserFriend(UUID friendlistowner, UUID friend) { return; }
        public override void UpdateUserFriendPerms(UUID friendlistowner, UUID friend, uint perms) { return; }
        public override List<FriendListItem> GetUserFriendList(UUID friendlistowner) { return new List<FriendListItem>(); }
        public override Dictionary<UUID, FriendRegionInfo> GetFriendRegionInfos (List<UUID> uuids) { return new Dictionary<UUID, FriendRegionInfo>(); }
        public override bool MoneyTransferRequest(UUID from, UUID to, uint amount) { return true; }
        public override bool InventoryTransferRequest(UUID from, UUID to, UUID inventory) { return true; }

        /// Appearance
        /// TODO: stubs for now to get us to a compiling state gently
        public override AvatarAppearance GetUserAppearance(UUID user)
        {
            return (AvatarAppearance)manager.Load(typeof(AvatarAppearance), user);
        }

        private bool ExistsAppearance(UUID uuid)
        {
            AvatarAppearance appearance = (AvatarAppearance)manager.Load(typeof(AvatarAppearance), uuid);
            if (appearance == null)
            {
                return false;
            }

            return true;
        }


        public override void UpdateUserAppearance(UUID user, AvatarAppearance appearance)
        {
            if (appearance == null)
                return;

            appearance.Owner = user;

            bool exists = ExistsAppearance(user);
            if (exists)
            {
                manager.Update(appearance);
            }
            else
            {
                manager.Save(appearance);
            }
        }

        public override void ResetAttachments(UUID userID)
        {
        }

        public override void LogoutUsers(UUID regionID)
        {
        }

        public override string Name {
            get { return "NHibernate"; }
        }

        public override string Version {
            get { return "0.1"; }
        }

        public override void Dispose()
        {

        }
    }
}
