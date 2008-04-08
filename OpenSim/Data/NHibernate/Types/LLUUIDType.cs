using System;
using System.Data;
using NHibernate.SqlTypes;
using NHibernate.UserTypes;
using libsecondlife;

namespace OpenSim.Data.NHibernate
{
    [Serializable]
    public class LLUUIDString : IUserType 
    {
        public object Assemble(object cached, object owner)
        {
            return cached;
        }

        bool IUserType.Equals(object uuid1, object uuid2)
        {
            return uuid1.Equals(uuid2);
        }

        public object DeepCopy(object uuid)
        {
            return uuid;
        }

        public object Disassemble(object uuid)
        {
            return uuid;
        }

        public int GetHashCode(object uuid)
        {
            return (uuid == null) ? 0 : uuid.GetHashCode();
        }

        public bool IsMutable
        {
            get { return false; }
        }

        public object NullSafeGet(System.Data.IDataReader rs, string[] names, object owner)
        {
            object uuid = null; 

            int ord = rs.GetOrdinal(names[0]);
            if (!rs.IsDBNull(ord))
            {
                string first = (string)rs.GetString(ord);
                uuid = new LLUUID(first);
            }

            return uuid;
        }

        public void NullSafeSet(System.Data.IDbCommand cmd, object obj, int index)
        {
            LLUUID UUID = (LLUUID)obj;
            ((IDataParameter)cmd.Parameters[index]).Value = UUID.ToString();
        }

        public object Replace(object original, object target, object owner)
        {
            return original;
        }

        public Type ReturnedType
        {
            get { return typeof(LLUUID); }
        }

        public SqlType[] SqlTypes
        {
            // I think we're up to 36
            get { return new SqlType [] { SqlTypeFactory.GetString(36) }; }
        }
    }
}
