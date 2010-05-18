using System;
using System.Collections.Generic;
using System.Text;
using OpenMetaverse;

namespace OpenSim.Data
{

    public static class DBGuid
    {
        /// <summary>This function converts a value returned from the database in one of the
        /// supported formats into a UUID.  This function is not actually DBMS-specific right
        /// now
        /// 
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public static UUID FromDB(object id)
        {
            if( (id == null) || (id == DBNull.Value))
                return UUID.Zero;

            if (id.GetType() == typeof(Guid))
                return new UUID((Guid)id);

            if (id.GetType() == typeof(byte[]))
            {
                if (((byte[])id).Length == 0)
                    return UUID.Zero;
                else if (((byte[])id).Length == 16)
                    return new UUID((byte[])id, 0);
            }
            else if (id.GetType() == typeof(string))
            {
                if (((string)id).Length == 0)
                    return UUID.Zero;
                else if (((string)id).Length == 36)
                    return new UUID((string)id);
            }

            throw new Exception("Failed to convert db value to UUID: " + id.ToString());
        }
    }
}
