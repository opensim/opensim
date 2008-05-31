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
using libsecondlife;
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

        public override void Initialise(string connect)
        {
            char[] split = {';'};
            string[] parts = connect.Split(split, 3);
            if (parts.Length != 3)
            {
                // TODO: make this a real exception type
                throw new Exception("Malformed Inventory connection string '" + connect + "'");
            }

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

            HbmSerializer.Default.Validate = true;
            using (MemoryStream stream =
                   HbmSerializer.Default.Serialize(Assembly.GetExecutingAssembly()))
                cfg.AddInputStream(stream);

            // new SchemaExport(cfg).Create(true, true);

            factory  = cfg.BuildSessionFactory();
            InitDB();
        }

        private void InitDB()
        {
            string regex = @"no such table: UserProfiles";
            Regex RE = new Regex(regex, RegexOptions.Multiline);
            try
            {
                using (ISession session = factory.OpenSession())
                {
                    session.Load(typeof(UserProfileData), LLUUID.Zero);
                }
            }
            catch (ObjectNotFoundException)
            {
                // yes, we know it's not there, but that's ok
            }
            catch (ADOException e)
            {
                Match m = RE.Match(e.ToString());
                if (m.Success)
                {
                    // We don't have this table, so create it.
                    new SchemaExport(cfg).Create(true, true);
                }
            }
        }

        private bool ExistsUser(LLUUID uuid)
        {
            UserProfileData user = null;
            try
            {
                using (ISession session = factory.OpenSession())
                {
                    user = session.Load(typeof(UserProfileData), uuid) as UserProfileData;
                }
                // BUG: CATCHALL IS BAD.
            }
            catch (Exception) {}

            return (user != null);
        }

        override public UserProfileData GetUserByUUID(LLUUID uuid)
        {
            UserProfileData user;
            // TODO: I'm sure I'll have to do something silly here
            using (ISession session = factory.OpenSession())
            {
                user = session.Load(typeof(UserProfileData), uuid) as UserProfileData;
                user.CurrentAgent = GetAgentByUUID(uuid);
            }
            return user;
        }

        override public void AddNewUserProfile(UserProfileData profile)
        {
            if (!ExistsUser(profile.ID))
            {
                using (ISession session = factory.OpenSession())
                {
                    using (ITransaction transaction = session.BeginTransaction())
                    {
                        session.Save(profile);
                        SetAgentData(profile.ID, profile.CurrentAgent, session);
                        // TODO: save agent
                        transaction.Commit();
                    }
                }
            }
            else
            {
                m_log.ErrorFormat("Attempted to add User {0} {1} that already exists, updating instead", profile.FirstName, profile.SurName);
                UpdateUserProfile(profile);
            }
        }

        private static void SetAgentData(LLUUID uuid, UserAgentData agent, ISession session)
        {
            if (agent == null)
            {
                // TODO: got to figure out how to do a delete right
            }
            else
            {
                UserAgentData old = session.Load(typeof(UserAgentData), uuid) as UserAgentData;
                if (old == null)
                {
                    session.Save(agent);
                }
                else
                {
                    session.Update(agent);
                }
            }

        }
        override public bool UpdateUserProfile(UserProfileData profile)
        {
            if (ExistsUser(profile.ID))
            {
                using (ISession session = factory.OpenSession())
                {
                    using (ITransaction transaction = session.BeginTransaction())
                    {
                        session.Update(profile);
                        SetAgentData(profile.ID, profile.CurrentAgent, session);
                        transaction.Commit();
                        return true;
                    }
                }
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
            using (ISession session = factory.OpenSession())
            {
                using (ITransaction transaction = session.BeginTransaction())
                {
                    session.Save(agent);
                    transaction.Commit();
                }
            }
        }

        public void UpdateUserAgent(UserAgentData agent)
        {
            using (ISession session = factory.OpenSession())
            {
                using (ITransaction transaction = session.BeginTransaction())
                {
                    session.Update(agent);
                    transaction.Commit();
                }
            }
        }

        override public UserAgentData GetAgentByUUID(LLUUID uuid)
        {
            try
            {
                using (ISession session = factory.OpenSession())
                {
                    return session.Load(typeof(UserAgentData), uuid) as UserAgentData;
                }
            }
            catch
            {
                return null;
            }
        }

        override public UserProfileData GetUserByName(string fname, string lname)
        {
            using (ISession session = factory.OpenSession())
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
        }

        override public UserAgentData GetAgentByName(string fname, string lname)
        {
            return GetUserByName(fname, lname).CurrentAgent;
        }

        override public UserAgentData GetAgentByName(string name)
        {
            return GetAgentByName(name.Split(' ')[0], name.Split(' ')[1]);
        }

        override public List<AvatarPickerAvatar> GeneratePickerResults(LLUUID queryID, string query)
        {
            List<AvatarPickerAvatar> results = new List<AvatarPickerAvatar>();
            string[] querysplit;
            querysplit = query.Split(' ');

            if (querysplit.Length == 2)
            {
                using (ISession session = factory.OpenSession())
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
            }
            return results;
        }

        // TODO: actually implement these
        public override void UpdateUserCurrentRegion(LLUUID avatarid, LLUUID regionuuid, ulong regionhandle) { return; }
        public override void StoreWebLoginKey(LLUUID agentID, LLUUID webLoginKey) {return;}
        public override void AddNewUserFriend(LLUUID friendlistowner, LLUUID friend, uint perms) {return;}
        public override void RemoveUserFriend(LLUUID friendlistowner, LLUUID friend) {return;}
        public override void UpdateUserFriendPerms(LLUUID friendlistowner, LLUUID friend, uint perms) {return;}
        public override List<FriendListItem> GetUserFriendList(LLUUID friendlistowner) {return new List<FriendListItem>();}
        public override bool MoneyTransferRequest(LLUUID from, LLUUID to, uint amount) {return true;}
        public override bool InventoryTransferRequest(LLUUID from, LLUUID to, LLUUID inventory) {return true;}

        /// Appearance
        /// TODO: stubs for now to get us to a compiling state gently
        public override AvatarAppearance GetUserAppearance(LLUUID user)
        {
            AvatarAppearance appearance;
            // TODO: I'm sure I'll have to do something silly here
            using (ISession session = factory.OpenSession())
            {
                appearance = session.Load(typeof(AvatarAppearance), user) as AvatarAppearance;
            }
            return appearance;
        }

        private bool ExistsAppearance(LLUUID uuid)
        {
            AvatarAppearance appearance;
            using (ISession session = factory.OpenSession())
            {
                appearance = session.Load(typeof(AvatarAppearance), uuid) as AvatarAppearance;
            }
            return (appearance == null) ? false : true;
        }


        public override void UpdateUserAppearance(LLUUID user, AvatarAppearance appearance)
        {
            bool exists = ExistsAppearance(user);
            using (ISession session = factory.OpenSession())
            {
                using (ITransaction transaction = session.BeginTransaction())
                {
                    if (exists)
                    {
                        session.Update(appearance);
                    }
                    else
                    {
                        session.Save(appearance);
                    }
                    transaction.Commit();
                }
            }
        }

        override public void AddAttachment(LLUUID user, LLUUID item)
        {
            return;
        }

        override public void RemoveAttachment(LLUUID user, LLUUID item)
        {
            return;
        }

        override public List<LLUUID> GetAttachments(LLUUID user)
        {
            return new List<LLUUID>();
        }

        public override string Name {
            get { return "NHibernate"; }
        }

        public override string Version {
            get { return "0.1"; }
        }

        public void Dispose()
        {

        }
    }
}
