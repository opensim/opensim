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
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS AS IS AND ANY
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
using System.IO;
using System.Reflection;
using Microsoft.CSharp;

namespace OpenSim.Region.ScriptEngine.DotNetEngine.Compiler.LSL
{
    public class Compiler
    {

        // * Uses "LSL2Converter" to convert LSL to C# if necessary.
        // * Compiles C#-code into an assembly
        // * Returns assembly name ready for AppDomain load.
        //
        // Assembly is compiled using LSL_BaseClass as base. Look at debug C# code file created when LSL script is compiled for full details.
        //

        private LSL2CSConverter LSL_Converter = new LSL2CSConverter();
        private CSharpCodeProvider codeProvider = new CSharpCodeProvider();
        private static UInt64 scriptCompileCounter = 0;

        private static int instanceID = new Random().Next(0, int.MaxValue);
                           // Implemented due to peer preassure --- will cause garbage in ScriptEngines folder ;)

        //private ICodeCompiler icc = codeProvider.CreateCompiler();
        public string CompileFromFile(string LSOFileName)
        {
            switch (Path.GetExtension(LSOFileName).ToLower())
            {
                case ".txt":
                case ".lsl":
                    Common.ScriptEngineBase.Common.SendToDebug("Source code is LSL, converting to CS");
                    return CompileFromLSLText(File.ReadAllText(LSOFileName));
                case ".cs":
                    Common.ScriptEngineBase.Common.SendToDebug("Source code is CS");
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
            if (Script.Substring(0, 4).ToLower() == "//c#")
            {
                return CompileFromCSText(Script);
            }
            else
            {
                return CompileFromCSText(LSL_Converter.Convert(Script));
            }
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
            string OutFile =
                Path.Combine("ScriptEngines",
                             "DotNetScript_" + instanceID.ToString() + "_" + scriptCompileCounter.ToString() + ".dll");
            try
            {
                File.Delete(OutFile);
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception attempting to delete old compiled script: " + e.ToString());
            }
            //string OutFile = Path.Combine("ScriptEngines", "SecondLife.Script.dll");

            // DEBUG - write source to disk
            try
            {
                File.WriteAllText(
                    Path.Combine("ScriptEngines", "debug_" + Path.GetFileNameWithoutExtension(OutFile) + ".cs"), Script);
            }
            catch
            {
            }

            // Do actual compile
            CompilerParameters parameters = new CompilerParameters();
            parameters.IncludeDebugInformation = true;
            // Add all available assemblies
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                //Console.WriteLine("Adding assembly: " + asm.Location);
                //parameters.ReferencedAssemblies.Add(asm.Location);
            }

            string rootPath = Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory);
            string rootPathSE = Path.GetDirectoryName(GetType().Assembly.Location);
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
                string errtext = String.Empty;
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