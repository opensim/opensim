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
using OpenSim.Framework;
using OpenSim.Framework.Interfaces;
using OpenSim.Region.Environment.Scenes;
using OpenSim.Region.Scripting;

namespace OpenSim.Region.Scripting.EmbeddedJVM
{
    public partial class Thread
    {
        // Is this smart?
        public static MainMemory GlobalMemory;
        public static Scene World;
        private int PC = 0;
        private Stack stack;
        private Interpreter mInterpreter;
        public ClassRecord currentClass;
        public ClassInstance currentInstance;
        private StackFrame currentFrame;
        public int excutionCounter = 0;
        public bool running = false;

        public ScriptInfo scriptInfo;

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
