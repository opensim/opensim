using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using Microsoft.CSharp;
using System.CodeDom.Compiler;
using System.Reflection;

namespace OpenSim.Region.ScriptEngine.DotNetEngine.Compiler.LSL
{
    
    public class Compiler
    {
        private LSL2CSConverter LSL_Converter = new LSL2CSConverter();
        private CSharpCodeProvider codeProvider = new CSharpCodeProvider();
        //private ICodeCompiler icc = codeProvider.CreateCompiler();
        public string Compile(string LSOFileName)
        {


            // Output assembly name
            string OutFile = Path.Combine("ScriptEngines", Path.GetFileNameWithoutExtension(LSOFileName) + ".dll");
            //string OutFile = Path.Combine("ScriptEngines", "SecondLife.Script.dll");

            Common.SendToDebug("Reading source code into memory");
            // TODO: Add error handling
            string CS_Code;
            switch (System.IO.Path.GetExtension(LSOFileName).ToLower())
            {
                case ".txt":
                case ".lsl":
                    Common.SendToDebug("Source code is LSL, converting to CS");
                    CS_Code = LSL_Converter.Convert(File.ReadAllText(LSOFileName));
                    break;
                case ".cs":
                    Common.SendToDebug("Source code is CS");
                    CS_Code = File.ReadAllText(LSOFileName);
                    break;
                default:
                    throw new Exception("Unknown script type.");
            }

            Common.SendToDebug("Compiling");

            // DEBUG - write source to disk
            try
            {
                File.WriteAllText(Path.Combine("ScriptEngines", "debug_" + Path.GetFileNameWithoutExtension(LSOFileName) + ".cs"), CS_Code);
            }
            catch { }

            // Do actual compile
            System.CodeDom.Compiler.CompilerParameters parameters = new CompilerParameters();
            parameters.IncludeDebugInformation = true;
            // Add all available assemblies
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                //Console.WriteLine("Adding assembly: " + asm.Location);
                //parameters.ReferencedAssemblies.Add(asm.Location);
            }

            string rootPath = Path.GetDirectoryName(this.GetType().Assembly.Location);
            Console.WriteLine("Assembly location: " + rootPath);
            parameters.ReferencedAssemblies.Add(Path.Combine(rootPath, "OpenSim.Region.ScriptEngine.Common.dll"));
            parameters.ReferencedAssemblies.Add(Path.Combine(rootPath, "OpenSim.Region.ScriptEngine.DotNetEngine.dll"));
            
            //parameters.ReferencedAssemblies.Add("OpenSim.Region.Environment");
            parameters.GenerateExecutable = false;
            parameters.OutputAssembly = OutFile;
            CompilerResults results = codeProvider.CompileAssemblyFromSource(parameters, CS_Code);

            // Go through errors
            // TODO: Return errors to user somehow
            if (results.Errors.Count > 0)
            {
                foreach (CompilerError CompErr in results.Errors)
                {
                    Console.WriteLine("Line number " + CompErr.Line +
                        ", Error Number: " + CompErr.ErrorNumber +
                        ", '" + CompErr.ErrorText + ";");
                }
            }


            return OutFile;
        }

    }
}
