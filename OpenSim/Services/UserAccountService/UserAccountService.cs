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
using System.Reflection;
using Nini.Config;
using OpenSim.Data;
using OpenSim.Services.Interfaces;
using System.Collections.Generic;
using OpenMetaverse;

namespace OpenSim.Services.UserAccountService
{
    public class UserAccountService : UserAccountServiceBase, IUserAccountService
    {
        public UserAccountService(IConfigSource config) : base(config)
        {
        }

        public UserAccount GetUserAccount(UUID scopeID, string firstName,
                string lastName)
        {
            UserAccountData[] d = m_Database.Get(
                    new string[] {"ScopeID", "FirstName", "LastName"},
                    new string[] {scopeID.ToString(), firstName, lastName});

            if (d.Length < 1)
                return null;

            UserAccount u = new UserAccount();
            u.FirstName = d[0].FirstName;
            u.LastName = d[0].LastName;
            u.PrincipalID = d[0].PrincipalID;
            u.ScopeID = d[0].ScopeID;
            u.Email = d[0].Data["Email"].ToString();
            u.Created = Convert.ToInt32(d[0].Data["Created"].ToString());

            return null;
        }

        public UserAccount GetUserAccount(UUID scopeID, string email)
        {
            return null;
        }
        
        public UserAccount GetUserAccount(UUID scopeID, UUID userID)
        {
            return null;
        }

        public bool SetUserAccount(UserAccount data)
        {
            return false;
        }

        public bool CreateUserAccount(UserAccount data)
        {
            return false;
        }

        public List<UserAccount> GetUserAccounts(UUID scopeID, string query)
        {
            return null;
        }
    }
}
