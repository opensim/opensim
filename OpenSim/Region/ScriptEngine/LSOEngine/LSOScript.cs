using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using OpenSim.Region.ScriptEngine.LSOEngine.LSO;

namespace OpenSim.Region.ScriptEngine.LSOEngine
{
    /// <summary>
    /// This class encapsulated an LSO file and contains execution-specific data
    /// </summary>
    public class LSOScript
    {
        private byte[] LSOCode = new byte[1024 * 16];              // Contains the LSO-file
        //private System.IO.MemoryStream LSOCode = new MemoryStream(1024 * 16);

        public void Execute(LSO_Enums.Event_Mask_Values Event, params object[] param)
        {
            
        }
    }
}
