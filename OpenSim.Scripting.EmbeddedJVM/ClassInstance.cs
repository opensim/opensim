using System;
using System.Collections.Generic;
using System.Text;
using OpenSim.Scripting.EmbeddedJVM.Types;

namespace OpenSim.Scripting.EmbeddedJVM
{
    public class ClassInstance : Object
    {
        public int size;
        public Dictionary<string, BaseType> Fields = new Dictionary<string, BaseType>();

        public ClassInstance()
        {

        }
    }
}
