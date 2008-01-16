using System.Data;
using libsecondlife;
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
}
