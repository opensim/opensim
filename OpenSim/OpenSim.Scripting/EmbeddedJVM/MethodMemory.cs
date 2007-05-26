using System;
using System.Collections.Generic;
using System.Text;

namespace OpenSim.Scripting.EmbeddedJVM
{
    public class MethodMemory
    {
        public byte[] MethodBuffer;
        public List<ClassRecord> Classes = new List<ClassRecord>();
        public int NextMethodPC = 0;
        public int Methodcount = 0;

        public MethodMemory()
        {
            MethodBuffer = new byte[20000];
        }
    }
}
