/*
 * Copyright (c) Contributors, http://opensimulator.org/
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
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using OpenSim.Region.Environment.Scenes;
using OpenSim.Region.ExtensionsScriptModule.Engines.JVMEngine.Types;
using OpenSim.Region.ExtensionsScriptModule.Engines.JVMEngine.Types.PrimitiveTypes;

namespace OpenSim.Region.ExtensionsScriptModule.Engines.JVMEngine.JVM
{
    public partial class Thread
    {
        // Is this smart?
        public static MainMemory GlobalMemory;
        public static Scene World;
        private int PC = 0;
        private Stack stack;
        private Interpreter m_Interpreter;
        public ClassRecord currentClass;
        public ClassInstance currentInstance;
        private StackFrame m_currentFrame;
        public int excutionCounter = 0;
        public bool running = false;

        public ScriptInfo scriptInfo;

        public Thread()
        {
            m_Interpreter = new Interpreter(this);
            stack = new Stack();
        }

        public void SetPC(int methodpointer)
        {
            //Console.WriteLine("Thread PC has been set to " + methodpointer);
            PC = methodpointer;
        }

        public void StartMethod(ClassRecord rec, string methName)
        {
            m_currentFrame = new StackFrame();
            stack.StackFrames.Push(m_currentFrame);
            currentClass = rec;
            currentClass.StartMethod(this, methName);
        }

        public void StartMethod(string methName)
        {
            m_currentFrame = new StackFrame();
            stack.StackFrames.Push(m_currentFrame);
            currentClass.StartMethod(this, methName);
        }

        public void JumpToStaticVoidMethod(string methName, int returnPC)
        {
            m_currentFrame = new StackFrame();
            m_currentFrame.ReturnPC = returnPC;
            stack.StackFrames.Push(m_currentFrame);
            currentClass.StartMethod(this, methName);
        }

        public void JumpToStaticParamMethod(string methName, string param, int returnPC)
        {
            if (param == "I")
            {
                BaseType bs1 = m_currentFrame.OpStack.Pop();
                m_currentFrame = new StackFrame();
                m_currentFrame.ReturnPC = returnPC;
                stack.StackFrames.Push(m_currentFrame);
                m_currentFrame.LocalVariables[0] = ((Int) bs1);
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
            return m_Interpreter.Excute();
        }
    }
}