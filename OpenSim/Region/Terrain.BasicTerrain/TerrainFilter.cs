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
using System.CodeDom.Compiler;
using System.Collections.Generic;
using libTerrain;
using Microsoft.CSharp;
using Microsoft.JScript;

namespace OpenSim.Region.Terrain
{
    public interface ITerrainFilter
    {
        void Filter(Channel heightmap, string[] args);
        string Register();
        string Help();
    }

    public class TestFilter : ITerrainFilter
    {
        public void Filter(Channel heightmap, string[] args)
        {
            Console.WriteLine("Hello world");
        }

        public string Register()
        {
            return "demofilter";
        }

        public string Help()
        {
            return "demofilter - Does nothing";
        }
    }

    public class FilterHost
    {
        public Dictionary<string, ITerrainFilter> filters = new Dictionary<string, ITerrainFilter>();

        private void LoadFilter(ICodeCompiler compiler, string filename)
        {
            CompilerParameters compilerParams = new CompilerParameters();
            CompilerResults compilerResults;
            compilerParams.GenerateExecutable = false;
            compilerParams.GenerateInMemory = true;
            compilerParams.IncludeDebugInformation = false;
            compilerParams.ReferencedAssemblies.Add("libTerrain-BSD.dll");
            compilerParams.ReferencedAssemblies.Add("OpenSim.Terrain.BasicTerrain.dll");
            compilerParams.ReferencedAssemblies.Add("System.dll");

            compilerResults = compiler.CompileAssemblyFromFile(compilerParams, filename);

            if (compilerResults.Errors.Count > 0)
            {
                Console.WriteLine("Compile errors:");
                foreach (CompilerError error in compilerResults.Errors)
                {
                    Console.WriteLine(error.Line.ToString() + ": " + error.ErrorText.ToString());
                }
            }
            else
            {
                foreach (Type pluginType in compilerResults.CompiledAssembly.GetExportedTypes())
                {
                    Type testInterface = pluginType.GetInterface("ITerrainFilter",true);

                    if (testInterface != null)
                    {
                        ITerrainFilter filter = (ITerrainFilter)compilerResults.CompiledAssembly.CreateInstance(pluginType.ToString());

                        string filterName = filter.Register();
                        Console.WriteLine("Plugin: " + filterName + " loaded.");

                        if (!filters.ContainsKey(filterName))
                        {
                            filters.Add(filterName, filter);
                        }
                        else
                        {
                            filters[filterName] = filter;
                        }
                    }
                }
            }

        }

        public void LoadFilterCSharp(string filename)
        {
            CSharpCodeProvider compiler = new CSharpCodeProvider();
            LoadFilter(compiler.CreateCompiler(), filename);
        }

        public void LoadFilterJScript(string filename)
        {
            JScriptCodeProvider compiler = new JScriptCodeProvider();
            LoadFilter(compiler.CreateCompiler(), filename);
        }
    }
}
