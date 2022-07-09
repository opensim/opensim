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
using System.Collections.Generic;
using System.Reflection;
using log4net;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Data;
using OpenSim.Framework;
using OpenSim.Services.Base;
using OpenSim.Services.Interfaces;

namespace OpenSim.Services.UserAccountService
{
    public class UserAliasService : ServiceBase, IUserAliasService
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        protected IUserAliasData m_Database = null;

        public UserAliasService(IConfigSource config) 
            : base(config)
        {
            string dllName = String.Empty;
            string connString = String.Empty;
            string realm = "UserAlias";

            IConfig dbConfig = config.Configs["DatabaseService"];
            if (dbConfig != null)
            {
                dllName = dbConfig.GetString("StorageProvider", String.Empty);
                connString = dbConfig.GetString("ConnectionString", String.Empty);
            }

            IConfig userConfig = config.Configs["UserAliasService"];
            if (userConfig == null)
            {
                throw new Exception("No UserAliasService configuration");
            }

            dllName = userConfig.GetString("StorageProvider", dllName);
            if (dllName.Length == 0)
            {
                throw new Exception("No StorageProvider configured");
            }

            connString = userConfig.GetString("ConnectionString", connString);
            realm = userConfig.GetString("Realm", realm);

            m_Database = LoadPlugin<IUserAliasData>(dllName, new Object[] { connString, realm });

            if (m_Database == null)
            {
                throw new Exception("Could not find a storage interface in the given module");
            }

            // Console commands

            // In case there are several instances of this class in the same process,
            // the console commands are only registered for the root instance
            if (MainConsole.Instance != null)
            {
                MainConsole.Instance.Commands.AddCommand("Aliases", false,
                    "create alias",
                    "create alias [<userId> [<aliasid> [<description>]]]",
                    "Create a new user alias", HandleCreateAlias);

                MainConsole.Instance.Commands.AddCommand("Aliases", false,
                    "show alias",
                    "show alias <userId>",
                    "Show Aliases user ids defined for the specified user account", HandleShowAliases);

                MainConsole.Instance.Commands.AddCommand("Aliases", false,
                        "delete alias",
                        "delete alias [<aliasid>]",
                        "delete an existing user alias by aliasId", HandleDeleteAlias);
            }
        }


        #region Console commands

        /// <summary>
        /// Handle the create user command from the console.
        /// </summary>
        /// <param name="cmdparams">string array with parameters: userid, aliasid, description </param>
        protected void HandleCreateAlias(string module, string[] cmdparams)
        {
            string rawUserId = (cmdparams.Length < 3 ? MainConsole.Instance.Prompt("UserID", "") : cmdparams[2]);
            string rawAliasId = (cmdparams.Length < 4 ? MainConsole.Instance.Prompt("AliasID", "") : cmdparams[3]);
            string description = (cmdparams.Length < 5 ? MainConsole.Instance.Prompt("Description", "") : cmdparams[4]);

            if (UUID.TryParse(rawUserId, out UUID UserID) == false)
                throw new Exception(string.Format("ID {0} is not a valid UUID", rawUserId));

            if (UUID.TryParse(rawAliasId, out UUID AliasID) == false)
                throw new Exception(string.Format("ID {0} is not a valid UUID", rawAliasId));

            var alias = CreateAlias(AliasID, UserID, description);
            if (alias != null)
            { 
                MainConsole.Instance.Output(
                    "Alias Created - UserID: {0}, AliasID: {1}, Description: {2}",
                    alias.UserID, alias.AliasID, alias.Description);
            }
        }

        protected void HandleShowAliases(string module, string[] cmdparams)
        {
            string rawUserId = (cmdparams.Length < 3 ? MainConsole.Instance.Prompt("UserID", "") : cmdparams[2]);

            if (UUID.TryParse(rawUserId, out UUID UserID) == false)
                throw new Exception(string.Format("ID {0} is not a valid UUID", rawUserId));

            var aliases = GetUserAliases(UserID);

            if (aliases == null)
            {
                MainConsole.Instance.Output("No aliases for user {0}", rawUserId);
                return;
            }

            foreach (var alias in aliases)
            {
                MainConsole.Instance.Output(
                    "UserID: {0}, AliasID: {1}, Description: {2}", 
                    alias.UserID, alias.AliasID, alias.Description);
            }
        }

        private void HandleDeleteAlias(string module, string[] cmdparams)
        {
            string rawAliasId = (cmdparams.Length < 3 ? MainConsole.Instance.Prompt("AliasID", "") : cmdparams[2]);

            if (UUID.TryParse(rawAliasId, out UUID AliasID) == false)
                throw new Exception(string.Format("ID {0} is not a valid UUID", rawAliasId));

            if (DeleteAlias(AliasID) == true)
            {
                MainConsole.Instance.Output("Alias for ID {0} deleted", rawAliasId);
            }
            else
            {
                MainConsole.Instance.Output("No alias with ID {0} found", rawAliasId);
            }
        }

        #endregion

        /// <summary>
        /// Given a userID, return a list of any aliases (UUIDs) defined for this user
        /// </summary>
        /// <param name="userID"></param>
        /// <returns>List<UserAlias>() - A list of aliases or null if none are defined</UUID></returns>
        public List<UserAlias> GetUserAliases(UUID userID)
        {
 //           m_log.DebugFormat("[USER ALIAS SERVICE] Retrieving aliases for user by userid {0}", userID);

            var aliases = m_Database.GetUserAliases(userID);

            if ((aliases == null) || (aliases.Count == 0))
                return null;
            
            var userAliases = new List<UserAlias>();
            foreach (var alias in aliases)
            {
                var userAlias = new UserAlias
                {
                    AliasID = alias.AliasID,
                    UserID = alias.UserID,
                    Description = alias.Description
                };

                userAliases.Add(userAlias);
            }

            return userAliases;
        }

        public UserAlias GetUserForAlias(UUID aliasID)
        {
//            m_log.DebugFormat("[USER ALIAS SERVICE]: Retrieving userID for alias by aliasId ", aliasID);

            var alias = m_Database.GetUserForAlias(aliasID);

            if (alias == null)
            {
                return null;
            }
            else
            {
                var userAlias = new UserAlias
                {
                    AliasID = alias.AliasID,
                    UserID = alias.UserID,
                    Description = alias.Description
                };

                return userAlias;
            }
        }

        public UserAlias CreateAlias(UUID AliasID, UUID UserID, string Description)
        {
            var aliasData = new UserAliasData
            {
                AliasID = AliasID,
                UserID = UserID,
                Description = Description
            };

            if (m_Database.Store(aliasData) == true)
            {
                return new UserAlias(AliasID, UserID, Description); 
            }

            return null;
        }

        public bool DeleteAlias(UUID aliasID)
        {
            return m_Database.Delete("AliasID", aliasID.ToString());
            throw new NotImplementedException();
        }
    }
}
