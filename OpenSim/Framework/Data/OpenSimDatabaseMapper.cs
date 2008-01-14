using System.Data.Common;
using libsecondlife;
using TribalMedia.Framework.Data;

namespace OpenSim.Framework.Data
{
    public abstract class OpenSimDatabaseMapper : DatabaseMapper
    {
        public OpenSimDatabaseMapper(string connectionString) : base(connectionString)
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
    }
}
