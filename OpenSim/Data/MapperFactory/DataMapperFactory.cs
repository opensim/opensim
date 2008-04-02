using System;
using OpenSim.Data.Base;
using OpenSim.Data.MSSQLMapper;
using OpenSim.Data.MySQLMapper;

namespace OpenSim.Data.MapperFactory
{
    public class DataMapperFactory
    {
        public enum MAPPER_TYPE {
            MySQL,
            MSSQL,
        };

        static public BaseDatabaseConnector GetDataBaseMapper(MAPPER_TYPE type, string connectionString)
        {
            switch (type) {
                case MAPPER_TYPE.MySQL:
                    return new MySQLDatabaseMapper(connectionString);
                case MAPPER_TYPE.MSSQL:
                    return new MSSQLDatabaseMapper(connectionString);
                default:
                    throw new ArgumentException("Unknown Database Mapper type [" + type + "].");
            }            
        }
    }
}
