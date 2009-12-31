using System;
using System.Collections.Generic;
using System.Reflection;

using log4net;
using Nini.Config;
using OpenMetaverse;

using OpenSim.Framework;
using OpenSim.Server.Base;
using OpenSim.Services.Interfaces;

namespace OpenSim.Services.LLLoginService
{
    public class LLLoginService : ILoginService
    {
        private IUserAccountService m_UserAccountService;
        private IAuthenticationService m_AuthenticationService;
        private IInventoryService m_InventoryService;
        private IGridService m_GridService;
        private IPresenceService m_PresenceService;

        public LLLoginService(IConfigSource config)
        {
            IConfig serverConfig = config.Configs["LoginService"];
            if (serverConfig == null)
                throw new Exception(String.Format("No section LoginService in config file"));

            string accountService = serverConfig.GetString("UserAccountService", String.Empty);
            string authService = serverConfig.GetString("AuthenticationService", String.Empty);
            string invService = serverConfig.GetString("InventoryService", String.Empty);
            string gridService = serverConfig.GetString("GridService", String.Empty);
            string presenceService = serverConfig.GetString("PresenceService", String.Empty);

            // These 3 are required; the other 2 aren't
            if (accountService == string.Empty || authService == string.Empty ||
                invService == string.Empty)
                throw new Exception("LoginService is missing service specifications");

            Object[] args = new Object[] { config };
            m_UserAccountService = ServerUtils.LoadPlugin<IUserAccountService>(accountService, args);
            m_AuthenticationService = ServerUtils.LoadPlugin<IAuthenticationService>(authService, args);
            m_InventoryService = ServerUtils.LoadPlugin<IInventoryService>(invService, args);
            if (gridService != string.Empty)
                m_GridService = ServerUtils.LoadPlugin<IGridService>(gridService, args);
            if (presenceService != string.Empty)
                m_PresenceService = ServerUtils.LoadPlugin<IPresenceService>(presenceService, args);

        }

        public LoginResponse Login(string firstName, string lastName, string passwd, string startLocation)
        {
            // Get the account and check that it exists
            UserAccount account = m_UserAccountService.GetUserAccount(UUID.Zero, firstName, lastName);
            if (account == null)
                return LLFailedLoginResponse.UserProblem;

            // Authenticate this user
            string token = m_AuthenticationService.Authenticate(account.PrincipalID, passwd, 30);
            UUID session = UUID.Zero;
            if ((token == string.Empty) || (token != string.Empty && !UUID.TryParse(token, out session)))
                return LLFailedLoginResponse.UserProblem;

            // Get the user's inventory
            List<InventoryFolderBase> inventorySkel = m_InventoryService.GetInventorySkeleton(account.PrincipalID);
            if ((inventorySkel == null) || (inventorySkel != null && inventorySkel.Count == 0))
                return LLFailedLoginResponse.InventoryProblem;

            // lots of things missing... need to do the simulation service

            return null;
        }
    }
}
