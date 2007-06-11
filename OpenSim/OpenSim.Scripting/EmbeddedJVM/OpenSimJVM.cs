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
using System.IO;
using System.Threading;
using OpenSim.Framework;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Utilities;

namespace OpenSim.Scripting.EmbeddedJVM
{
    public class OpenSimJVM : IScriptEngine
    {
        private List<Thread> _threads = new List<Thread>();
        private BlockingQueue<CompileInfo> CompileScripts = new BlockingQueue<CompileInfo>();
        private MainMemory _mainMemory;
        private System.Threading.Thread compileThread;

        public OpenSimJVM()
        {

        }

        public bool Init(IScriptAPI api)
        {
            Console.WriteLine("Creating OpenSim JVM scripting engine");
            _mainMemory = new MainMemory();
            Thread.GlobalMemory = this._mainMemory;
            Thread.OpenSimScriptAPI = api;
            compileThread = new System.Threading.Thread(new ThreadStart(CompileScript));
            compileThread.IsBackground = true;
            compileThread.Start();
            return true;
        }

        public string GetName()
        {
            return "OpenSimJVM";
        }

        public void LoadScript(string script, string scriptName, uint entityID)
        {
            Console.WriteLine("OpenSimJVM - loading new script: " + scriptName);
            CompileInfo comp = new CompileInfo();
            comp.entityId = entityID;
            comp.script = script;
            comp.scriptName = scriptName;
            this.CompileScripts.Enqueue(comp);
        }

        public void CompileScript()
        {
            while (true)
            {
                CompileInfo comp = this.CompileScripts.Dequeue();
                string script = comp.script;
                string scriptName = comp.scriptName;
                uint entityID = comp.entityId;
                try
                {
                    //need to compile the script into a java class file

                    //first save it to a java source file
                    TextWriter tw = new StreamWriter(scriptName + ".java");
                    tw.WriteLine(script);
                    tw.Close();

                    //now compile
                    System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo("javac.exe", "*.java");
                   // psi.RedirectStandardOutput = true;
                    psi.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
                    psi.UseShellExecute = false;

                    System.Diagnostics.Process javacomp;
                    javacomp = System.Diagnostics.Process.Start(psi);
                    javacomp.WaitForExit();

                    
                    //now load in class file
                    ClassRecord class1 = new ClassRecord();
                    class1.LoadClassFromFile(scriptName + ".class");
                    class1.PrintToConsole();
                    //Console.WriteLine();
                    this._mainMemory.MethodArea.Classes.Add(class1);
                    class1.AddMethodsToMemory(this._mainMemory.MethodArea);

                    Thread newThread = new Thread();
                    this._threads.Add(newThread);
                    newThread.EntityId = entityID;
                    newThread.currentClass = class1;

                    //now delete the created files
                    System.IO.File.Delete(scriptName + ".java");
                    System.IO.File.Delete(scriptName + ".class");
                    //this.OnFrame();
                }
                catch (Exception e)
                {
                    Console.WriteLine("exception");
                    Console.WriteLine(e.StackTrace);
                    Console.WriteLine(e.Message);
                }
            }
        }

        public void OnFrame()
        {
            for (int i = 0; i < this._threads.Count; i++)
            {
                if (!this._threads[i].running)
                {
                    this._threads[i].StartMethod("OnFrame");
                    bool run = true;
                    while (run)
                    {
                        run = this._threads[i].Excute();
                    }
                }
            }
        }

        private class CompileInfo
        {
            public string script;
            public string scriptName;
            public uint entityId;

            public CompileInfo()
            {

            }
        }
    }
}
