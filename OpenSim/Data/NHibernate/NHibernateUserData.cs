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
using NHibernate.Cfg;
using NHibernate.Expression;
using NHibernate.Mapping.Attributes;
using NHibernate.Tool.hbm2ddl;
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

        private Configuration cfg;
        private ISessionFactory factory;
        private ISession session;

        public override void Initialise()
        {
            m_log.Info("[NHibernateUserData]: " + Name + " cannot be default-initialized!");
            throw new PluginNotInitialisedException (Name);
        }

        public override void Initialise(string connect)
        {
            char[] split = {';'};
            string[] parts = connect.Split(split, 3);
            if (parts.Length != 3)
            {
                // TODO: make this a real exception type
                throw new Exception("Malformed Inventory connection string '" + connect + "'");
            }
            string dialect = parts[0];

            // This is stubbing for now, it will become dynamic later and support different db backends
            cfg = new Configuration();
            cfg.SetProperty(Environment.ConnectionProvider,
                            "NHibernate.Connection.DriverConnectionProvider");
            cfg.SetProperty(Environment.Dialect,
                            "NHibernate.Dialect." + parts[0]);
            cfg.SetProperty(Environment.ConnectionDriver,
                            "NHibernate.Driver." + parts[1]);
            cfg.SetProperty(Environment.ConnectionString, parts[2]);
            cfg.AddAssembly("OpenSim.Data.NHibernate");

            factory  = cfg.BuildSessionFactory();
            session = factory.OpenSession();

            // This actually does the roll forward assembly stuff
            Assembly assem = GetType().Assembly;
            Migration m = new Migration((System.Data.Common.DbConnection)factory.ConnectionProvider.GetConnection(), assem, dialect, "UserStore");
            m.Update();
        }

        private bool ExistsUser(UUID uuid)
        {
            UserProfileData user = null;
            try
            {
                user = session.Load(typeof(UserProfileData), uuid) as UserProfileData;
            }
            catch (ObjectNotFoundException)
            {
                user = null;
            }

            return (user != null);
        }

        override public UserProfileData GetUserByUUID(UUID uuid)
        {
            UserProfileData user;
            // TODO: I'm sure I'll have to do something silly here
            try
            {
                user = session.Load(typeof(UserProfileData), uuid) as UserProfileData;
                user.CurrentAgent = GetAgentByUUID(uuid);
            }
            catch (ObjectNotFoundException)
            {
                user = null;
            }

            return user;
        }

        override public void AddNewUserProfile(UserProfileData profile)
        {
            if (!ExistsUser(profile.ID))
            {
                session.Save(profile);
                SetAgentData(profile.ID, profile.CurrentAgent);
            }
            else
            {
                m_log.ErrorFormat("Attempted to add User {0} {1} that already exists, updating instead", profile.FirstName, profile.SurName);
                UpdateUserProfile(profile);
            }
        }

        private void SetAgentData(UUID uuid, UserAgentData agent)
        {
            if (agent == null)
            {
                // TODO: got to figure out how to do a delete right
            }
            else
            {
                try
                {
                    UserAgentData old = session.Load(typeof(UserAgentData), uuid) as UserAgentData;
                    session.Delete(old);
                }
                catch (ObjectNotFoundException)
                {
                }
                session.Save(agent);
            }

        }
        override public bool UpdateUserProfile(UserProfileData profile)
        {
            if (ExistsUser(profile.ID))
            {
                session.Update(profile);
                SetAgentData(profile.ID, profile.CurrentAgent);
                return true;
            }
            else
            {
                m_log.ErrorFormat("Attempted to update User {0} {1} that doesn't exist, updating instead", profile.FirstName, profile.SurName);
                AddNewUserProfile(profile);
                return true;
            }
        }

        override public void AddNewUserAgent(UserAgentData agent)
        {
            try
            {
                UserAgentData old = session.Load(typeof(UserAgentData), agent.ProfileID) as UserAgentData;
                session.Delete(old);
            }
            catch (ObjectNotFoundException)
            {
            }
            session.Save(agent);
        }

        public void UpdateUserAgent(UserAgentData agent)
        {
            session.Update(agent);
        }

        override public UserAgentData GetAgentByUUID(UUID uuid)
        {
            try
            {
                return session.Load(typeof(UserAgentData), uuid) as UserAgentData;
            }
            catch
            {
                return null;
            }
        }

        override public UserProfileData GetUserByName(string fname, string lname)
        {
            ICriteria criteria = session.CreateCriteria(typeof(UserProfileData));
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
                ICriteria criteria = session.CreateCriteria(typeof(UserProfileData));
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
        public override void UpdateUserCurrentRegion(UUID avatarid, UUID regionuuid, ulong regionhandle) { return; }
        public override void StoreWebLoginKey(UUID agentID, UUID webLoginKey) { return; }
        public override void AddNewUserFriend(UUID friendlistowner, UUID friend, uint perms) { return; }
        public override void RemoveUserFriend(UUID friendlistowner, UUID friend) { return; }
        public override void UpdateUserFriendPerms(UUID friendlistowner, UUID friend, uint perms) { return; }
        public override List<FriendListItem> GetUserFriendList(UUID friendlistowner) { return new List<FriendListItem>(); }
        public override bool MoneyTransferRequest(UUID from, UUID to, uint amount) { return true; }
        public override bool InventoryTransferRequest(UUID from, UUID to, UUID inventory) { return true; }

        /// Appearance
        /// TODO: stubs for now to get us to a compiling state gently
        public override AvatarAppearance GetUserAppearance(UUID user)
        {
            AvatarAppearance appearance;
            // TODO: I'm sure I'll have to do something silly here
            try {
                appearance = session.Load(typeof(AvatarAppearance), user) as AvatarAppearance;
            } catch (ObjectNotFoundException) {
                appearance = null;
            }
            return appearance;
        }

        private bool ExistsAppearance(UUID uuid)
        {
            AvatarAppearance appearance;
            try {
                appearance = session.Load(typeof(AvatarAppearance), uuid) as AvatarAppearance;
            } catch (ObjectNotFoundException) {
                appearance = null;
            }

            return (appearance == null) ? false : true;
        }


        public override void UpdateUserAppearance(UUID user, AvatarAppearance appearance)
        {
            if (appearance == null)
                return;

            appearance.Owner = user;

            bool exists = ExistsAppearance(user);
            if (exists)
            {
                session.Update(appearance);
            }
            else
            {
                session.Save(appearance);
            }
        }

        public override void ResetAttachments(UUID userID)
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
