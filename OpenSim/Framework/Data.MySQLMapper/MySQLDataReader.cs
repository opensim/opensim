using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using OpenSim.Framework.Data.Base;

namespace OpenSim.Framework.Data.MySQLMapper
{
    public class MySQLDataReader : OpenSimDataReader
    {
        public MySQLDataReader(IDataReader source) : base(source)
        {
        }
    }
}
