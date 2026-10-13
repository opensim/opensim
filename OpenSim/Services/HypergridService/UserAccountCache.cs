using log4net;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace OpenSim.Services.HypergridService
{
    public class UserAccountCache : IUserAccountService
    {
        private const double CACHE_EXPIRATION_SECONDS = 300; // 5 minutes

//        private static readonly ILog m_log =
//                LogManager.GetLogger(
//                MethodBase.GetCurrentMethod().DeclaringType);

        private ExpiringCacheOS<UUID, UserAccount> m_UUIDCache;

        private IUserAccountService m_UserAccountService;

        private static UserAccountCache m_Singleton;

        public static UserAccountCache CreateUserAccountCache(IUserAccountService u)
        {
            m_Singleton ??= new UserAccountCache(u);

            return m_Singleton;
        }

        private UserAccountCache(IUserAccountService u)
        {
            m_UUIDCache = new ExpiringCacheOS<UUID, UserAccount>(5000);
            m_UserAccountService = u;
        }

        public void Cache(UUID userID, UserAccount account)
        {
            // Cache even null accounts
            m_UUIDCache.AddOrUpdate(userID, account, CACHE_EXPIRATION_SECONDS);

            //m_log.DebugFormat("[USER CACHE]: cached user {0}", userID);
        }

        public UserAccount Get(UUID userID, out bool inCache)
        {
            UserAccount account = null;
            inCache = false;
            if (m_UUIDCache.TryGetValue(userID, out account))
            {
                //m_log.DebugFormat("[USER CACHE]: Account {0} {1} found in cache", account.FirstName, account.LastName);
                inCache = true;
                return account;
            }

            return null;
        }

        public UserAccount GetUser(string id)
        {
            UUID uuid = UUID.Zero;
            if(!UUID.TryParse(id, out UUID userID))
                return null;

            if (m_UUIDCache.TryGetValue(userID, out UserAccount account))
            {
                //m_log.DebugFormat("[USER CACHE]: Account {0} {1} found in cache", account.FirstName, account.LastName);
                return account;
            }

            account = m_UserAccountService.GetUserAccount(UUID.Zero, uuid);
            Cache(uuid, account);

            return account;
        }

        #region IUserAccountService
        public UserAccount GetUserAccount(UUID scopeID, UUID userID)
        {
            return GetUser(userID.ToString());
        }

        public UserAccount GetUserAccount(UUID scopeID, string FirstName, string LastName)
        {
            return null;
        }

        public UserAccount GetUserAccount(UUID scopeID, string Email)
        {
            return null;
        }

        public List<UserAccount> GetUserAccountsWhere(UUID scopeID, string query)
        {
            return null;
        }

        public List<UserAccount> GetUserAccounts(UUID scopeID, string query)
        {
            return null;
        }

        public List<UserAccount> GetUserAccounts(UUID scopeID, List<string> IDs)
        {
            return null;
        }

        public void InvalidateCache(UUID userID)
        {
            m_UUIDCache.Remove(userID);
        }

        public bool StoreUserAccount(UserAccount data)
        {
            return false;
        }
        #endregion

    }

}
