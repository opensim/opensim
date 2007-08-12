using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using Microsoft.CSharp;
using System.CodeDom.Compiler;

namespace OpenSim.Region.ScriptEngine.DotNetEngine.Compiler.LSL
{
    public class Compiler
    {
        private LSL2CS.Converter.LSL2CSConverter LSL_Converter = new LSL2CS.Converter.LSL2CSConverter();
        private CSharpCodeProvider codeProvider = new CSharpCodeProvider();
        //private ICodeCompiler icc = codeProvider.CreateCompiler();
        public string Compile(string LSOFileName)
        {


            // Output assembly name
            string OutFile = Path.GetFileNameWithoutExtension(LSOFileName) + ".dll";

            // TODO: Add error handling
            string CS_Code = LSL_Converter.Convert(File.ReadAllText(LSOFileName));

            // Do actual compile
            System.CodeDom.Compiler.CompilerParameters parameters = new CompilerParameters();
            parameters.IncludeDebugInformation = true;
            parameters.ReferencedAssemblies.Add("OpenSim.Region.Environment");
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
