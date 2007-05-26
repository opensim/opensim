using System;
using System.Collections.Generic;
using System.Text;
using OpenSim.Scripting.EmbeddedJVM.Types;
using OpenSim.Scripting.EmbeddedJVM.Types.PrimitiveTypes;
using OpenSim.Framework;
using OpenSim.Framework.Interfaces;

namespace OpenSim.Scripting.EmbeddedJVM
{
    public partial class Thread
    {
        public static MainMemory GlobalMemory;
        public static IScriptAPI OpenSimScriptAPI;
        private int PC = 0;
        private Stack stack;
        private Interpreter mInterpreter;
        public ClassRecord currentClass;
        public ClassInstance currentInstance;
        private StackFrame currentFrame;
        public int excutionCounter = 0;
        public bool running = false;
        public uint EntityId = 0;

        public Thread()
        {
            this.mInterpreter = new Interpreter(this);
            this.stack = new Stack();
        }

        public void SetPC(int methodpointer)
        {
            //Console.WriteLine("Thread PC has been set to " + methodpointer);
            PC = methodpointer;
        }

        public void StartMethod(ClassRecord rec, string methName)
        {
            currentFrame = new StackFrame();
            this.stack.StackFrames.Push(currentFrame);
            this.currentClass = rec;
            currentClass.StartMethod(this, methName);
        }

        public void StartMethod( string methName)
        {
            currentFrame = new StackFrame();
            this.stack.StackFrames.Push(currentFrame);
            currentClass.StartMethod(this, methName);
        }

        public void JumpToStaticVoidMethod(string methName, int returnPC)
        {
            currentFrame = new StackFrame();
            currentFrame.ReturnPC = returnPC;
            this.stack.StackFrames.Push(currentFrame);
            currentClass.StartMethod(this, methName);
        }

        public void JumpToStaticParamMethod(string methName, string param, int returnPC)
        {
            if (param == "I")
            {
                BaseType bs1 = currentFrame.OpStack.Pop();
                currentFrame = new StackFrame();
                currentFrame.ReturnPC = returnPC;
                this.stack.StackFrames.Push(currentFrame);
                currentFrame.LocalVariables[0] = ((Int)bs1);
                currentClass.StartMethod(this, methName);
            }
            if (param == "F")
            {

            }
        }

        public void JumpToClassStaticVoidMethod(string className, string methName, int returnPC)
        {

        }

        public bool Excute()
        {
            excutionCounter++;
            return this.mInterpreter.Excute();
        }
    }
}
