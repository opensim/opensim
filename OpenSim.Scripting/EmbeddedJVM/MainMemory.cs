using System;
using System.Collections.Generic;
using System.Text;

namespace OpenSim.Scripting.EmbeddedJVM
{
    public class MainMemory
    {
        public Heap HeapArea;
        public MethodMemory MethodArea;
 
        public MainMemory()
        {
            MethodArea = new MethodMemory();
            HeapArea = new Heap();
        }
    }
}
