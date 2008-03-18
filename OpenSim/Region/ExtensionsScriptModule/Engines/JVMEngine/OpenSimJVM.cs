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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using OpenSim.Framework;
using OpenSim.Region.Environment.Scenes;
using OpenSim.Region.ExtensionsScriptModule.JVMEngine.JVM;

namespace OpenSim.Region.ExtensionsScriptModule.JVMEngine
{
    public class JVMScript : IScript
    {
        private List<Thread> _threads = new List<Thread>();
        private BlockingQueue<CompileInfo> CompileScripts = new BlockingQueue<CompileInfo>();
        private MainMemory _mainMemory;

        private ScriptInfo scriptInfo;

        public void Initialise(ScriptInfo info)
        {
            scriptInfo = info;

            _mainMemory = new MainMemory();
            Thread.GlobalMemory = _mainMemory;
            Thread.World = info.world;
            CompileScript();

            scriptInfo.events.OnFrame += new EventManager.OnFrameDelegate(events_OnFrame);
            scriptInfo.events.OnNewPresence += new EventManager.OnNewPresenceDelegate(events_OnNewPresence);
        }

        private void events_OnNewPresence(ScenePresence presence)
        {
            for (int i = 0; i < _threads.Count; i++)
            {
                if (!_threads[i].running)
                {
                    _threads[i].StartMethod("OnNewPresence");
                    bool run = true;
                    while (run)
                    {
                        run = _threads[i].Excute();
                    }
                }
            }
        }

        private void events_OnFrame()
        {
            for (int i = 0; i < _threads.Count; i++)
            {
                if (!_threads[i].running)
                {
                    _threads[i].StartMethod("OnFrame");
                    bool run = true;
                    while (run)
                    {
                        run = _threads[i].Excute();
                    }
                }
            }
        }

        public string Name
        {
            get { return "JVM Scripting Engine"; }
        }

        public void LoadScript(string script)
        {
            Console.WriteLine("OpenSimJVM - loading new script: " + script);
            CompileInfo comp = new CompileInfo();
            comp.script = script;
            comp.scriptName = script;
            CompileScripts.Enqueue(comp);
        }

        public void CompileScript()
        {
            CompileInfo comp = CompileScripts.Dequeue();
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
                ProcessStartInfo psi = new ProcessStartInfo("javac.exe", "*.java");
                // psi.RedirectStandardOutput = true;
                psi.WindowStyle = ProcessWindowStyle.Hidden;
                psi.UseShellExecute = false;

                Process javacomp;
                javacomp = Process.Start(psi);
                javacomp.WaitForExit();


                //now load in class file
                ClassRecord class1 = new ClassRecord();
                class1.LoadClassFromFile(scriptName + ".class");
                class1.PrintToConsole();
                //Console.WriteLine();
                _mainMemory.MethodArea.Classes.Add(class1);
                class1.AddMethodsToMemory(_mainMemory.MethodArea);

                Thread newThread = new Thread();
                _threads.Add(newThread);
                newThread.currentClass = class1;
                newThread.scriptInfo = scriptInfo;

                //now delete the created files
                File.Delete(scriptName + ".java");
                File.Delete(scriptName + ".class");
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
