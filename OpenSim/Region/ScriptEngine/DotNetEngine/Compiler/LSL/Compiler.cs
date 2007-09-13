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
        private static UInt64 scriptCompileCounter = 0;
        //private ICodeCompiler icc = codeProvider.CreateCompiler();
        public string CompileFromFile(string LSOFileName)
        {
            switch (System.IO.Path.GetExtension(LSOFileName).ToLower())
            {
                case ".txt":
                case ".lsl":
                    Common.SendToDebug("Source code is LSL, converting to CS");
                    return CompileFromLSLText(File.ReadAllText(LSOFileName));
                case ".cs":
                    Common.SendToDebug("Source code is CS");
                    return CompileFromCSText(File.ReadAllText(LSOFileName));
                default:
                    throw new Exception("Unknown script type.");
            }
        }
        /// <summary>
        /// Converts script from LSL to CS and calls CompileFromCSText
        /// </summary>
        /// <param name="Script">LSL script</param>
        /// <returns>Filename to .dll assembly</returns>
        public string CompileFromLSLText(string Script)
        {
            return CompileFromCSText(LSL_Converter.Convert(Script));
        }
        /// <summary>
        /// Compile CS script to .Net assembly (.dll)
        /// </summary>
        /// <param name="Script">CS script</param>
        /// <returns>Filename to .dll assembly</returns>
        public string CompileFromCSText(string Script)
        {


            // Output assembly name
            scriptCompileCounter++;
            string OutFile = Path.Combine("ScriptEngines", "Script_" + scriptCompileCounter + ".dll");
            try
            {
                System.IO.File.Delete(OutFile);
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception attempting to delete old compiled script: " + e.ToString());
            }
            //string OutFile = Path.Combine("ScriptEngines", "SecondLife.Script.dll");

            // DEBUG - write source to disk
            try
            {
                File.WriteAllText(Path.Combine("ScriptEngines", "debug_" + Path.GetFileNameWithoutExtension(OutFile) + ".cs"), Script);
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

            string rootPath = Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory);
            string rootPathSE = Path.GetDirectoryName(this.GetType().Assembly.Location);
            //Console.WriteLine("Assembly location: " + rootPath);
            parameters.ReferencedAssemblies.Add(Path.Combine(rootPath, "OpenSim.Region.ScriptEngine.Common.dll"));
            parameters.ReferencedAssemblies.Add(Path.Combine(rootPathSE, "OpenSim.Region.ScriptEngine.DotNetEngine.dll"));
            
            //parameters.ReferencedAssemblies.Add("OpenSim.Region.Environment");
            parameters.GenerateExecutable = false;
            parameters.OutputAssembly = OutFile;
            parameters.IncludeDebugInformation = false;
            CompilerResults results = codeProvider.CompileAssemblyFromSource(parameters, Script);

            // Go through errors
            // TODO: Return errors to user somehow
            if (results.Errors.Count > 0)
            {

                string errtext = "";
                foreach (CompilerError CompErr in results.Errors)
                {
                    errtext += "Line number " + (CompErr.Line - 1) +
                        ", Error Number: " + CompErr.ErrorNumber +
                        ", '" + CompErr.ErrorText + "'\r\n";
                }
                throw new Exception(errtext);
            }


            return OutFile;
        }

    }
}
