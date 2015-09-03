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
using System.Collections;
using System.Collections.Generic;
using System.Data;
using OpenMetaverse;
using OpenSim.Framework;
using MySql.Data.MySqlClient;

namespace OpenSim.Data.MySQL
{
    public class MySqlUserAccountData : MySQLGenericTableHandler<UserAccountData>, IUserAccountData
    {
        public MySqlUserAccountData(string connectionString, string realm)
                : base(connectionString, realm, "UserAccount")
        {
        }

        public UserAccountData[] GetUsers(UUID scopeID, string query)
        {
            string[] words = query.Split(new char[] {' '});

            bool valid = false;

            for (int i = 0 ; i < words.Length ; i++)
            {
                if (words[i].Length > 2)
                    valid = true;
//                if (words[i].Length < 3)
//                {
//                    if (i != words.Length - 1)
//                        Array.Copy(words, i + 1, words, i, words.Length - i - 1);
//                    Array.Resize(ref words, words.Length - 1);
//                }
            }

            if ((!valid) || words.Length == 0)
                return new UserAccountData[0];

            if (words.Length > 2)
                return new UserAccountData[0];

            using (MySqlCommand cmd = new MySqlCommand())
            {
                if (words.Length == 1)
                {
                    cmd.CommandText = String.Format("select * from {0} where (ScopeID=?ScopeID or ScopeID='00000000-0000-0000-0000-000000000000') and (FirstName like ?search or LastName like ?search) and active=1", m_Realm);
                    cmd.Parameters.AddWithValue("?search", "%" + words[0] + "%");
                    cmd.Parameters.AddWithValue("?ScopeID", scopeID.ToString());
                }
                else
                {
                    cmd.CommandText = String.Format("select * from {0} where (ScopeID=?ScopeID or ScopeID='00000000-0000-0000-0000-000000000000') and (FirstName like ?searchFirst and LastName like ?searchLast) and active=1", m_Realm);
                    cmd.Parameters.AddWithValue("?searchFirst", "%" + words[0] + "%");
                    cmd.Parameters.AddWithValue("?searchLast", "%" + words[1] + "%");
                    cmd.Parameters.AddWithValue("?ScopeID", scopeID.ToString());
                }

                return DoQuery(cmd);
            }
        }

        public UserAccountData[] GetUsersWhere(UUID scopeID, string where)
        {
            using (MySqlCommand cmd = new MySqlCommand())
            {
                if (scopeID != UUID.Zero)
                {
                    where = "(ScopeID=?ScopeID or ScopeID='00000000-0000-0000-0000-000000000000') and (" + where + ")";
                    cmd.Parameters.AddWithValue("?ScopeID", scopeID.ToString());
                }

                cmd.CommandText = String.Format("select * from {0} where " + where, m_Realm);

                return DoQuery(cmd);
            }
        }
    }
}
