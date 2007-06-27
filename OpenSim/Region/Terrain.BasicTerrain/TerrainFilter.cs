using System;
using System.Collections.Generic;
using System.Text;

using System.CodeDom.Compiler;
using System.CodeDom;
using Microsoft.CSharp;
using Microsoft.JScript;

using libTerrain;

namespace OpenSim.Terrain
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
