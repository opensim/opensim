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
 *     * Neither the name of the OpenSimulator Project nor the
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
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using log4net;
using OpenSim.ScriptEngine.Shared;
using ScriptAssemblies;

namespace OpenSim.ScriptEngine.Components.DotNetEngine.Compilers
{
    public abstract class CILCompiler
    {
        internal static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private string ScriptEnginesPath = "ScriptEngines";
        private string Name { get { return "SECS.DotNetEngine.CILCompiler"; } }
        private string m_scriptAssemblyName;
        internal string ScriptAssemblyName { get { return m_scriptAssemblyName; } set { m_scriptAssemblyName = value; } }

        // Default inherit
        protected string ScriptInheritFrom = typeof(ScriptAssemblies.ScriptBase).Name;
        private readonly System.Security.Cryptography.MD5CryptoServiceProvider MD5Sum = new System.Security.Cryptography.MD5CryptoServiceProvider();
        protected CodeDomProvider CompileProvider;

        //private string[] AppDomainAssemblies = new string[] { "OpenSim.Region.ScriptEngine.Shared.dll", "OpenSim.Region.ScriptEngine.Shared.Script.dll", "OpenSim.Region.ScriptEngine.Shared.Api.Runtime.dll" };
        private readonly string[] AppDomainAssemblies = new string[] { 
            Assembly.GetAssembly(typeof(Int32)).Location,
            "OpenSim.ScriptEngine.Shared.dll", 
            "OpenSim.ScriptEngine.Shared.Script.dll", 
            Path.Combine("ScriptEngines", "OpenSim.Region.ScriptEngine.Shared.dll")
        };

        public abstract string PreProcessScript(ref string script);

        public CILCompiler()
        {
        }

        private static readonly Regex FileNameFixer = new Regex(@"[^a-zA-Z0-9\.\-]", RegexOptions.Compiled | RegexOptions.Singleline);
        public string Compile(ScriptMetaData data, ref string _script)
        {
            // Add "using", "inherit", default constructor, etc around script.
            string script = PreProcessScript(ref _script);

            // Get filename based on content
            string md5Sum = System.Convert.ToBase64String(
                  MD5Sum.ComputeHash(
                    System.Text.Encoding.ASCII.GetBytes(script)
                  ));
            // Unique name for this assembly
            ScriptAssemblyName = "SECS_Script_" + FileNameFixer.Replace(md5Sum, "_");

            string OutFile = Path.Combine(ScriptEnginesPath, ScriptAssemblyName + ".dll");

            // Make sure target dir exist
            if (!Directory.Exists(ScriptEnginesPath))
                try { Directory.CreateDirectory(ScriptEnginesPath); }
                catch { }

            // Already exist? No point in recompiling
            if (File.Exists(OutFile))
                return OutFile;

            //
            // Dump source code
            //
            string dumpFile = OutFile + ".txt";
            try
            {
                if (File.Exists(dumpFile))
                    File.Delete(dumpFile);
                File.WriteAllText(dumpFile, script);
            }
            catch (Exception e)
            {
                m_log.DebugFormat("[{0}] Exception trying to dump script source code to file \"{1}\": {2}", Name, dumpFile, e.ToString());
            }

            //
            // COMPILE
            //

            CompilerParameters parameters = new CompilerParameters();
            parameters.IncludeDebugInformation = true;
            //string rootPath = Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory);

            foreach (string file in AppDomainAssemblies)
            {
                parameters.ReferencedAssemblies.Add(file);
                m_log.DebugFormat("[{0}] Adding reference for compile: \"{1}\".", Name, file);
            }
            //lock (commandProvider)
            //{
            //    foreach (string key in commandProvider.Keys)
            //    {
            //        IScriptCommandProvider cp = commandProvider[key];
            //            string
            //        file = cp.GetType().Assembly.Location;
            //        parameters.ReferencedAssemblies.Add(file);
            //        m_log.DebugFormat("[{0}] Loading command provider assembly \"{1}\" into AppDomain: \"{2}\".", Name,
            //                          key, file);
            //    }
            //}

            parameters.GenerateExecutable = false;
            parameters.OutputAssembly = OutFile;
            parameters.IncludeDebugInformation = true;
            //parameters.WarningLevel = 1; // Should be 4?
            parameters.TreatWarningsAsErrors = false;

            // Do compile
            CompilerResults results = CompileProvider.CompileAssemblyFromSource(parameters, script);


            //
            // WARNINGS AND ERRORS
            //
            //TODO
            int display = 5;
            if (results.Errors.Count > 0)
            {
                string errtext = String.Empty;
                foreach (CompilerError CompErr in results.Errors)
                {
                    // Show 5 errors max
                    //
                    if (display <= 0)
                        break;
                    display--;

                    string severity = "Error";
                    if (CompErr.IsWarning)
                        severity = "Warning";

                    //TODO: Implement
                    KeyValuePair<int, int> lslPos = new KeyValuePair<int, int>();

                    //lslPos = "NOT IMPLEMENTED";// FindErrorPosition(CompErr.Line, CompErr.Column);

                    string text = CompErr.ErrorText;

                    // The Second Life viewer's script editor begins
                    // countingn lines and columns at 0, so we subtract 1.
                    errtext += String.Format("Line ({0},{1}): {4} {2}: {3}\n",
                            lslPos.Key - 1, lslPos.Value - 1,
                            CompErr.ErrorNumber, text, severity);
                }

                if (!File.Exists(OutFile))
                {
                    throw new Exception(errtext);
                }
            }

            // TODO: Process errors
            return OutFile;
        }

    }
}
