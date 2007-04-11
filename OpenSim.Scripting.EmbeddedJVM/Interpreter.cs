using System;
using System.Collections.Generic;
using System.Text;
using OpenSim.Scripting.EmbeddedJVM.Types;
using OpenSim.Scripting.EmbeddedJVM.Types.PrimitiveTypes;

namespace OpenSim.Scripting.EmbeddedJVM
{
    partial class Thread
    {
        private partial class Interpreter
        {
            private Thread _mThread;

            public Interpreter(Thread parentThread)
            {
                _mThread = parentThread;
            }

            public bool Excute()
            {
                bool run = true;
                byte currentOpCode = GlobalMemory.MethodArea.MethodBuffer[this._mThread.PC++];
               // Console.WriteLine("opCode is: " + currentOpCode);
                bool handled = false;

                handled = this.IsLogicOpCode(currentOpCode);
                if (!handled)
                {
                    handled = this.IsMethodOpCode(currentOpCode);
                }
                if (!handled)
                {
                    if (currentOpCode == 172)
                    {
                        if (this._mThread.stack.StackFrames.Count > 1)
                        {
                            Console.WriteLine("returning int from function");
                            int retPC1 = this._mThread.currentFrame.ReturnPC;
                            BaseType bas1 = this._mThread.currentFrame.OpStack.Pop();
                            this._mThread.stack.StackFrames.Pop();
                            this._mThread.currentFrame = this._mThread.stack.StackFrames.Peek();
                            this._mThread.PC = retPC1;
                            if (bas1 is Int)
                            {
                                this._mThread.currentFrame.OpStack.Push((Int)bas1);
                            }
                        }
                        else
                        {
                          //  Console.WriteLine("No parent function so ending program");
                            run = false;
                        }
                        handled = true;
                    }
                    if (currentOpCode == 174)
                    {
                        if (this._mThread.stack.StackFrames.Count > 1)
                        {
                            Console.WriteLine("returning float from function");
                            int retPC1 = this._mThread.currentFrame.ReturnPC;
                            BaseType bas1 = this._mThread.currentFrame.OpStack.Pop();
                            this._mThread.stack.StackFrames.Pop();
                            this._mThread.currentFrame = this._mThread.stack.StackFrames.Peek();
                            this._mThread.PC = retPC1;
                            if (bas1 is Float)
                            {
                                this._mThread.currentFrame.OpStack.Push((Float)bas1);
                            }
                        }
                        else
                        {
                           // Console.WriteLine("No parent function so ending program");
                            run = false;
                        }
                        handled = true;
                    }
                    if (currentOpCode == 177)
                    {
                        if (this._mThread.stack.StackFrames.Count > 1)
                        {
                            Console.WriteLine("returning from function");
                            int retPC = this._mThread.currentFrame.ReturnPC;
                            this._mThread.stack.StackFrames.Pop();
                            this._mThread.currentFrame = this._mThread.stack.StackFrames.Peek();
                            this._mThread.PC = retPC;
                        }
                        else
                        {
                           // Console.WriteLine("No parent function so ending program");
                            run = false;
                        }
                        handled = true;
                    }
                }
                if (!handled)
                {
                    Console.WriteLine("opcode " + currentOpCode + " not been handled ");
                }
                return run;

            }
        }
    }
}
