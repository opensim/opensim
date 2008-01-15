using System.Data;
using TribalMedia.Framework.Data;

namespace OpenSim.Framework.Data
{
    public abstract class OpenSimTableMapper<TRowMapper, TPrimaryKey> : BaseTableMapper<TRowMapper, TPrimaryKey>
    {
        public OpenSimTableMapper(BaseDatabaseConnector database, string tableName) : base(database, tableName)
        {
        }

        protected override DataReader CreateReader(IDataReader reader)
        {
            return new OpenSimDataReader(reader);
        }
    }
}
