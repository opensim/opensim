using System;
using System.Collections.Generic;
using System.Text;
using OpenSim.Scripting.EmbeddedJVM.Types;

namespace OpenSim.Scripting.EmbeddedJVM
{
    public class StackFrame
    {
        public BaseType[] LocalVariables;
        public Stack<BaseType> OpStack = new Stack<BaseType>();

        public int ReturnPC = 0;
        public ClassRecord CallingClass = null;

        public StackFrame()
        {
            LocalVariables = new BaseType[20];
        }

    }
}
