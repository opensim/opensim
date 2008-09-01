using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Reflection;
using System.Text;

namespace OpenSim.Data.MSSQL
{
    public class MSSQLMigration : Migration
    {
        public MSSQLMigration(DbConnection conn, Assembly assem, string type) : base(conn, assem, type)
        {
        }

        public MSSQLMigration(DbConnection conn, Assembly assem, string subtype, string type) : base(conn, assem, subtype, type)
        {
        }

        protected override int FindVersion(DbConnection conn, string type)
        {
            int version = 0;
            using (DbCommand cmd = conn.CreateCommand())
            {
                try
                {
                    cmd.CommandText = "select top 1 version from migrations where name = '" + type + "' order by version desc"; //Must be 
                    using (IDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            version = Convert.ToInt32(reader["version"]);
                        }
                        reader.Close();
                    }
                }
                catch
                {
                    // Something went wrong, so we're version 0
                }
            }
            return version;
        }
    }
}
