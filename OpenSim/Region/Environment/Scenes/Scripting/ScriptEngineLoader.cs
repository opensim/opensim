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
* 
*/
/* Original code: Tedd Hansen */
using System;
using System.IO;
using System.Reflection;
using OpenSim.Framework.Console;

namespace OpenSim.Region.Environment.Scenes.Scripting
{
    public class ScriptEngineLoader
    {
        private LogBase m_log;

        public ScriptEngineLoader(LogBase logger)
        {
            m_log = logger;
        }

        public ScriptEngineInterface LoadScriptEngine(string EngineName)
        {
            ScriptEngineInterface ret = null;
            try
            {
                ret =
                    LoadAndInitAssembly(
                        Path.Combine("ScriptEngines", "OpenSim.Region.ScriptEngine." + EngineName + ".dll"),
                        "OpenSim.Region.ScriptEngine." + EngineName + ".ScriptEngine");
            }
            catch (Exception e)
            {
                m_log.Error("ScriptEngine",
                            "Error loading assembly \"" + EngineName + "\": " + e.Message + ", " +
                            e.StackTrace.ToString());
            }
            return ret;
        }

        /// <summary>
        /// Does actual loading and initialization of script Assembly
        /// </summary>
        /// <param name="FreeAppDomain">AppDomain to load script into</param>
        /// <param name="FileName">FileName of script assembly (.dll)</param>
        /// <returns></returns>
        private ScriptEngineInterface LoadAndInitAssembly(string FileName, string NameSpace)
        {
            //Common.SendToDebug("Loading ScriptEngine Assembly " + FileName);
            // Load .Net Assembly (.dll)
            // Initialize and return it

            // TODO: Add error handling

            Assembly a;
            //try
            //{


            // Load to default appdomain (temporary)
            a = Assembly.LoadFrom(FileName);
            // Load to specified appdomain
            // TODO: Insert security
            //a = FreeAppDomain.Load(FileName);
            //}
            //catch (Exception e)
            //{
            //    m_log.Error("ScriptEngine", "Error loading assembly \"" + FileName + "\": " + e.ToString());
            //}


            //Console.WriteLine("Loading: " + FileName);
            //foreach (Type _t in a.GetTypes())
            //{
            //    Console.WriteLine("Type: " + _t.ToString());
            //}

            Type t;
            //try
            //{
            t = a.GetType(NameSpace, true);
            //}
            //catch (Exception e)
            //{
            //    m_log.Error("ScriptEngine", "Error initializing type \"" + NameSpace + "\" from \"" + FileName + "\": " + e.ToString());
            //}

            ScriptEngineInterface ret;
            //try
            //{
            ret = (ScriptEngineInterface) Activator.CreateInstance(t);
            //}
            //catch (Exception e)
            //{
            //    m_log.Error("ScriptEngine", "Error initializing type \"" + NameSpace + "\" from \"" + FileName + "\": " + e.ToString());
            //}

            return ret;
        }
    }
}
