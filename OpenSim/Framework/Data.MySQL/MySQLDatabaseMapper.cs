using System.Data.Common;
using MySql.Data.MySqlClient;

namespace OpenSim.Framework.Data.MySQL
{
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
