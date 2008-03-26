using System;
using System.Collections.Generic;
using OpenSim.Framework;
using OpenSim.Framework.Data;
using OpenSim.Framework.Data.Base;
using OpenSim.Framework.Data.MySQLMapper;

namespace OpenSim.Framework.Data.MapperFactory
{
    public class DataMapperFactory
    {
        public enum MAPPER_TYPE {
            MYSQL,
        };

        public DataMapperFactory() {
            
        }

        static public BaseDatabaseConnector GetDataBaseMapper(MAPPER_TYPE type, string connectionString)
        {
            switch (type) {
                case MAPPER_TYPE.MYSQL:
                    return new MySQLDatabaseMapper(connectionString);
                default:
                    return null;
            }            
        }
    }
}
