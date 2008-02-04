using System.Data;
using System.Data.Common;
using libsecondlife;
using MySql.Data.MySqlClient;

using TribalMedia.Framework.Data;

namespace OpenSim.Framework.Data
{
    public abstract class OpenSimDatabaseConnector : BaseDatabaseConnector
    {
        public OpenSimDatabaseConnector(string connectionString) : base(connectionString)
        {
        }

        public override object ConvertToDbType(object value)
        {
            if (value is LLUUID)
            {
                return ((LLUUID) value).UUID.ToString();
            }

            return base.ConvertToDbType(value);
        }

        public override BaseDataReader CreateReader(IDataReader reader)
        {
            return new OpenSimDataReader(reader);
        }
    }


    public class MySQLDatabaseMapper : OpenSimDatabaseConnector
    {
        public MySQLDatabaseMapper(string connectionString)
            : base(connectionString)
        {
        }

        public override DbConnection GetNewConnection()
        {
            MySqlConnection connection = new MySqlConnection(m_connectionString);
            return connection;
        }

        public override string CreateParamName(string fieldName)
        {
            return "?" + fieldName;
        }
    }
}

