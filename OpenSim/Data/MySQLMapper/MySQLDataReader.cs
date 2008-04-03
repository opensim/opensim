using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using OpenSim.Data.Base;

namespace OpenSim.Data.MySQLMapper
{
    public class MySQLDataReader : OpenSimDataReader
    {
        public MySQLDataReader(IDataReader source) : base(source)
        {
        }
    }
}
