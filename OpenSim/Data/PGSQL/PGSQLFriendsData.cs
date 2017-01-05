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
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ''AS IS'' AND ANY
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
using System.Reflection;
using System.Text;
using Npgsql;

namespace OpenSim.Data.PGSQL
{
    public class PGSQLFriendsData : PGSQLGenericTableHandler<FriendsData>, IFriendsData
    {
        public PGSQLFriendsData(string connectionString, string realm)
            : base(connectionString, realm, "FriendsStore")
        {
            using (NpgsqlConnection conn = new NpgsqlConnection(m_ConnectionString))
            {
                conn.Open();
                Migration m = new Migration(conn, GetType().Assembly, "FriendsStore");
                m.Update();
            }
        }


        public override bool Delete(string principalID, string friend)
        {
            UUID princUUID = UUID.Zero;

            bool ret = UUID.TryParse(principalID, out princUUID);

            if (ret)
                return Delete(princUUID, friend);
            else
                return false;
        }

        public bool Delete(UUID principalID, string friend)
        {
            using (NpgsqlConnection conn = new NpgsqlConnection(m_ConnectionString))
            using (NpgsqlCommand cmd = new NpgsqlCommand())
            {
                cmd.CommandText = String.Format("delete from {0} where \"PrincipalID\" = :PrincipalID and \"Friend\" = :Friend", m_Realm);
                cmd.Parameters.Add(m_database.CreateParameter("PrincipalID", principalID.ToString()));
                cmd.Parameters.Add(m_database.CreateParameter("Friend", friend));
                cmd.Connection = conn;
                conn.Open();
                cmd.ExecuteNonQuery();

                return true;
            }
        }

        public FriendsData[] GetFriends(string principalID)
        {
            UUID princUUID = UUID.Zero;

            bool ret = UUID.TryParse(principalID, out princUUID);

            if (ret)
               return GetFriends(princUUID);
            else
                return new FriendsData[0];
        }

        public FriendsData[] GetFriends(UUID principalID)
        {
            using (NpgsqlConnection conn = new NpgsqlConnection(m_ConnectionString))
            using (NpgsqlCommand cmd = new NpgsqlCommand())
            {

                cmd.CommandText = String.Format("select a.*,case when b.\"Flags\" is null then '-1' else b.\"Flags\" end as \"TheirFlags\" from {0} as a " +
                                                " left join {0} as b on a.\"PrincipalID\" = b.\"Friend\" and a.\"Friend\" = b.\"PrincipalID\" " +
                                                " where a.\"PrincipalID\" = :PrincipalID", m_Realm);
                cmd.Parameters.Add(m_database.CreateParameter("PrincipalID", principalID.ToString()));
                cmd.Connection = conn;
                conn.Open();
                return DoQuery(cmd);
            }
        }

        public FriendsData[] GetFriends(Guid principalID)
        {
            return GetFriends(principalID);
        }

    }
}
