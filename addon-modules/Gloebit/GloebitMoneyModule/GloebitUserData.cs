/*
 * GloebitUserData.cs is part of OpenSim-MoneyModule-Gloebit
 * Copyright (C) 2015 Gloebit LLC
 *
 * OpenSim-MoneyModule-Gloebit is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Lesser General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * OpenSim-MoneyModule-Gloebit is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with OpenSim-MoneyModule-Gloebit.  If not, see <https://www.gnu.org/licenses/>.
 */

using System;
using Nini.Config;
using OpenSim.Data.MySQL;
using OpenSim.Data.PGSQL;
using OpenSim.Data.SQLite;

namespace Gloebit.GloebitMoneyModule
{
    class GloebitUserData {

        private static IGloebitUserData m_impl;

        public static void Initialise(string storageProvider, string connectionString) {
            switch(storageProvider) {
                case "OpenSim.Data.SQLite.dll":
                    m_impl = new SQLiteImpl(connectionString);
                    break;
                case "OpenSim.Data.MySQL.dll":
                    m_impl = new MySQLImpl(connectionString);
                    break;
                case "OpenSim.Data.PGSQL.dll":
                    m_impl = new PGSQLImpl(connectionString);
                    break;
                default:
                    break;
            }
        }

        public static IGloebitUserData Instance {
            get { return m_impl; }
        }

        public interface IGloebitUserData {
            GloebitUser[] Get(string field, string key);

            GloebitUser[] Get(string[] fields, string[] keys);

            bool Store(GloebitUser user);
        }

        private class SQLiteImpl : SQLiteGenericTableHandler<GloebitUser>, IGloebitUserData {
            public SQLiteImpl(string connectionString)
                : base(connectionString, "GloebitUsers", "GloebitUsersSQLite")
            {
            }
        }

        private class MySQLImpl : MySQLGenericTableHandler<GloebitUser>, IGloebitUserData {
            public MySQLImpl(string connectionString)
                : base(connectionString, "GloebitUsers", "GloebitUsersMySQL")
            {
            }
        }

        private class PGSQLImpl : PGSQLGenericTableHandler<GloebitUser>, IGloebitUserData {
            public PGSQLImpl(string connectionString)
                : base(connectionString, "GloebitUsers", "GloebitUsersPGSQL")
            {
            }
        }
    }
}
