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
using OpenSim.Region.Environment.Scripting;
using OpenSim.Region.Environment.Scenes;

namespace OpenSim.Scripting.EmbeddedJVM
{
    public class JVMScript : IScript
    {
        private List<Thread> _threads = new List<Thread>();
        private BlockingQueue<CompileInfo> CompileScripts = new BlockingQueue<CompileInfo>();
        private MainMemory _mainMemory;

        ScriptInfo scriptInfo;

        public void Initialise(ScriptInfo info)
        {
            scriptInfo = info;

            _mainMemory = new MainMemory();
            Thread.GlobalMemory = this._mainMemory;
            Thread.World = info.world;
            CompileScript();

            scriptInfo.events.OnFrame += new EventManager.OnFrameDelegate(events_OnFrame);
            scriptInfo.events.OnNewPresence += new EventManager.OnNewPresenceDelegate(events_OnNewPresence);
        }

        void events_OnNewPresence(ScenePresence presence)
        {
            for (int i = 0; i < this._threads.Count; i++)
            {
                if (!this._threads[i].running)
                {
                    this._threads[i].StartMethod("OnNewPresence");
                    bool run = true;
                    while (run)
                    {
                        run = this._threads[i].Excute();
                    }
                }
            }
        }

        void events_OnFrame()
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

        public string getName()
        {
            return "JVM Scripting Engine";
        }

        public void LoadScript(string script)
        {
            Console.WriteLine("OpenSimJVM - loading new script: " + script);
            CompileInfo comp = new CompileInfo();
            comp.script = script;
            comp.scriptName = script;
            this.CompileScripts.Enqueue(comp);
        }

        public void CompileScript()
        {
            CompileInfo comp = this.CompileScripts.Dequeue();
            string script = comp.script;
            string scriptName = comp.scriptName;
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
                newThread.currentClass = class1;
                newThread.scriptInfo = scriptInfo;

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

        private class CompileInfo
        {
            public string script;
            public string scriptName;

            public CompileInfo()
            {

            }
        }
    }
}
