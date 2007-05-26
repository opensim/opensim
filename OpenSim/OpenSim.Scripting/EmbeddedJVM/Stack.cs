using System;
using System.Collections.Generic;
using System.Text;

namespace OpenSim.Scripting.EmbeddedJVM
{
    public class Stack
    {
        public Stack<StackFrame> StackFrames = new Stack<StackFrame>();

        public Stack()
        {
        }
    }
}
