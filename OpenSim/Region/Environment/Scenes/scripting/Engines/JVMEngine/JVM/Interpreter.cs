/*
* Copyright (c) Contributors, http://www.openmetaverse.org/
* See CONTRIBUTORS.TXT for a full list of copyright holders.
*
* Redistribution and use in source and binary forms, with or without
* modification, are permitted provided that the following conditions are met:
*     * Redistributions of source code must retain the above copyright
*       notice, this list of conditions and the following disclaimer.
*     * Redistributions in binary form must reproduce the above copyright
*       notice, this list of conditions and the following disclaimer in the
*       documentation and/or other materials provided with the distribution.
*     * Neither the name of the OpenSim Project nor the
*       names of its contributors may be used to endorse or promote products
*       derived from this software without specific prior written permission.
*
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS AND ANY
* EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
* WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
* DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
* DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
* (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
* LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
* ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
* (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
* SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
* 
*/
using System;
using System.Collections.Generic;
using System.Text;
using OpenSim.Region.Scripting.EmbeddedJVM.Types;
using OpenSim.Region.Scripting.EmbeddedJVM.Types.PrimitiveTypes;

namespace OpenSim.Region.Scripting.EmbeddedJVM
{
    partial class Thread
    {
        private partial class Interpreter
        {
            private Thread m_thread;

            public Interpreter(Thread parentThread)
            {
                m_thread = parentThread;
            }

            public bool Excute()
            {
                bool run = true;
                byte currentOpCode = GlobalMemory.MethodArea.MethodBuffer[this.m_thread.PC++];
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
                        if (this.m_thread.stack.StackFrames.Count > 1)
                        {
                            Console.WriteLine("returning int from function");
                            int retPC1 = this.m_thread.m_currentFrame.ReturnPC;
                            BaseType bas1 = this.m_thread.m_currentFrame.OpStack.Pop();
                            this.m_thread.stack.StackFrames.Pop();
                            this.m_thread.m_currentFrame = this.m_thread.stack.StackFrames.Peek();
                            this.m_thread.PC = retPC1;
                            if (bas1 is Int)
                            {
                                this.m_thread.m_currentFrame.OpStack.Push((Int)bas1);
                            }
                        }
                        else
                        {
                          //  Console.WriteLine("No parent function so ending program");
                            this.m_thread.stack.StackFrames.Pop();
                            run = false;
                        }
                        handled = true;
                    }
                    if (currentOpCode == 174)
                    {
                        if (this.m_thread.stack.StackFrames.Count > 1)
                        {
                            Console.WriteLine("returning float from function");
                            int retPC1 = this.m_thread.m_currentFrame.ReturnPC;
                            BaseType bas1 = this.m_thread.m_currentFrame.OpStack.Pop();
                            this.m_thread.stack.StackFrames.Pop();
                            this.m_thread.m_currentFrame = this.m_thread.stack.StackFrames.Peek();
                            this.m_thread.PC = retPC1;
                            if (bas1 is Float)
                            {
                                this.m_thread.m_currentFrame.OpStack.Push((Float)bas1);
                            }
                        }
                        else
                        {
                           // Console.WriteLine("No parent function so ending program");
                            this.m_thread.stack.StackFrames.Pop();
                            run = false;
                        }
                        handled = true;
                    }
                    if (currentOpCode == 177)
                    {
                        if (this.m_thread.stack.StackFrames.Count > 1)
                        {
                            Console.WriteLine("returning from function");
                            int retPC = this.m_thread.m_currentFrame.ReturnPC;
                            this.m_thread.stack.StackFrames.Pop();
                            this.m_thread.m_currentFrame = this.m_thread.stack.StackFrames.Peek();
                            this.m_thread.PC = retPC;
                        }
                        else
                        {
                           // Console.WriteLine("No parent function so ending program");
                            this.m_thread.stack.StackFrames.Pop();
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
