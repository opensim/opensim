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
using System.Globalization;
using System.Reflection;
using System.IO;
using System.Text;
using Microsoft.CSharp;
//using Microsoft.JScript;
using Microsoft.VisualBasic;
using log4net;

using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.ScriptEngine.Interfaces;
using OpenMetaverse;

namespace OpenSim.Region.ScriptEngine.Shared.CodeTools
{
    public class Compiler : ICompiler
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        // * Uses "LSL2Converter" to convert LSL to C# if necessary.
        // * Compiles C#-code into an assembly
        // * Returns assembly name ready for AppDomain load.
        //
        // Assembly is compiled using LSL_BaseClass as base. Look at debug C# code file created when LSL script is compiled for full details.
        //

        internal enum enumCompileType
        {
            lsl = 0,
            cs = 1,
            vb = 2
        }

        /// <summary>
        /// This contains number of lines WE use for header when compiling script. User will get error in line x-LinesToRemoveOnError when error occurs.
        /// </summary>
        public int LinesToRemoveOnError = 3;
        private enumCompileType DefaultCompileLanguage;
        private bool WriteScriptSourceToDebugFile;
        private bool CompileWithDebugInformation;
        private Dictionary<string, bool> AllowedCompilers = new Dictionary<string, bool>(StringComparer.CurrentCultureIgnoreCase);
        private Dictionary<string, enumCompileType> LanguageMapping = new Dictionary<string, enumCompileType>(StringComparer.CurrentCultureIgnoreCase);
        private bool m_insertCoopTerminationCalls;

        private string FilePrefix;
        private string ScriptEnginesPath = null;
        // mapping between LSL and C# line/column numbers
        private ICodeConverter LSL_Converter;

        private List<string> m_warnings = new List<string>();

        // private object m_syncy = new object();

//        private static CSharpCodeProvider CScodeProvider = new CSharpCodeProvider();
//        private static VBCodeProvider VBcodeProvider = new VBCodeProvider();

        // private static int instanceID = new Random().Next(0, int.MaxValue);                 // Unique number to use on our compiled files
        private static UInt64 scriptCompileCounter = 0;                                     // And a counter

        public IScriptEngine m_scriptEngine;
        private Dictionary<string, Dictionary<KeyValuePair<int, int>, KeyValuePair<int, int>>> m_lineMaps =
            new Dictionary<string, Dictionary<KeyValuePair<int, int>, KeyValuePair<int, int>>>();

        public bool in_startup = true;

        public Compiler(IScriptEngine scriptEngine)
        {
            m_scriptEngine = scriptEngine;
            ScriptEnginesPath = scriptEngine.ScriptEnginePath;
            ReadConfig();
        }

        public void ReadConfig()
        {
            // Get some config
            WriteScriptSourceToDebugFile = m_scriptEngine.Config.GetBoolean("WriteScriptSourceToDebugFile", false);
            CompileWithDebugInformation = m_scriptEngine.Config.GetBoolean("CompileWithDebugInformation", true);
            bool DeleteScriptsOnStartup = m_scriptEngine.Config.GetBoolean("DeleteScriptsOnStartup", true);
            m_insertCoopTerminationCalls = m_scriptEngine.Config.GetString("ScriptStopStrategy", "abort") == "co-op";

            // Get file prefix from scriptengine name and make it file system safe:
            FilePrefix = "CommonCompiler";
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                FilePrefix = FilePrefix.Replace(c, '_');
            }

            if (in_startup)
            {
                in_startup = false;
                CheckOrCreateScriptsDirectory();

                // First time we start? Delete old files
                if (DeleteScriptsOnStartup)
                    DeleteOldFiles();
            }

            // Map name and enum type of our supported languages
            LanguageMapping.Add(enumCompileType.cs.ToString(), enumCompileType.cs);
            LanguageMapping.Add(enumCompileType.vb.ToString(), enumCompileType.vb);
            LanguageMapping.Add(enumCompileType.lsl.ToString(), enumCompileType.lsl);

            // Allowed compilers
            string allowComp = m_scriptEngine.Config.GetString("AllowedCompilers", "lsl");
            AllowedCompilers.Clear();

#if DEBUG
            m_log.Debug("[Compiler]: Allowed languages: " + allowComp);
#endif


            foreach (string strl in allowComp.Split(','))
            {
                string strlan = strl.Trim(" \t".ToCharArray()).ToLower();
                if (!LanguageMapping.ContainsKey(strlan))
                {
                    m_log.Error("[Compiler]: Config error. Compiler is unable to recognize language type \"" + strlan + "\" specified in \"AllowedCompilers\".");
                }
                else
                {
#if DEBUG
                    //m_log.Debug("[Compiler]: Config OK. Compiler recognized language type \"" + strlan + "\" specified in \"AllowedCompilers\".");
#endif
                }
                AllowedCompilers.Add(strlan, true);
            }
            if (AllowedCompilers.Count == 0)
                m_log.Error("[Compiler]: Config error. Compiler could not recognize any language in \"AllowedCompilers\". Scripts will not be executed!");

            // Default language
            string defaultCompileLanguage = m_scriptEngine.Config.GetString("DefaultCompileLanguage", "lsl").ToLower();

            // Is this language recognized at all?
            if (!LanguageMapping.ContainsKey(defaultCompileLanguage))
            {
                m_log.Error("[Compiler]: " +
                                            "Config error. Default language \"" + defaultCompileLanguage + "\" specified in \"DefaultCompileLanguage\" is not recognized as a valid language. Changing default to: \"lsl\".");
                defaultCompileLanguage = "lsl";
            }

            // Is this language in allow-list?
            if (!AllowedCompilers.ContainsKey(defaultCompileLanguage))
            {
                m_log.Error("[Compiler]: " +
                            "Config error. Default language \"" + defaultCompileLanguage + "\"specified in \"DefaultCompileLanguage\" is not in list of \"AllowedCompilers\". Scripts may not be executed!");
            }
            else
            {
#if DEBUG
                //                m_log.Debug("[Compiler]: " +
                //                                            "Config OK. Default language \"" + defaultCompileLanguage + "\" specified in \"DefaultCompileLanguage\" is recognized as a valid language.");
#endif
                // LANGUAGE IS IN ALLOW-LIST
                DefaultCompileLanguage = LanguageMapping[defaultCompileLanguage];
            }

            // We now have an allow-list, a mapping list, and a default language
        }

        /// <summary>
        /// Create the directory where compiled scripts are stored if it does not already exist.
        /// </summary>
        private void CheckOrCreateScriptsDirectory()
        {
            if (!Directory.Exists(ScriptEnginesPath))
            {
                try
                {
                    Directory.CreateDirectory(ScriptEnginesPath);
                }
                catch (Exception ex)
                {
                    m_log.Error("[Compiler]: Exception trying to create ScriptEngine directory \"" + ScriptEnginesPath + "\": " + ex.ToString());
                }
            }

            if (!Directory.Exists(Path.Combine(ScriptEnginesPath,
                    m_scriptEngine.World.RegionInfo.RegionID.ToString())))
            {
                try
                {
                    Directory.CreateDirectory(Path.Combine(ScriptEnginesPath,
                        m_scriptEngine.World.RegionInfo.RegionID.ToString()));
                }
                catch (Exception ex)
                {
                    m_log.Error("[Compiler]: Exception trying to create ScriptEngine directory \"" + Path.Combine(ScriptEnginesPath,
                                            m_scriptEngine.World.RegionInfo.RegionID.ToString()) + "\": " + ex.ToString());
                }
            }
        }

        /// <summary>
        /// Delete old script files
        /// </summary>
        private void DeleteOldFiles()
        {
            foreach (string file in Directory.GetFiles(Path.Combine(ScriptEnginesPath,
                     m_scriptEngine.World.RegionInfo.RegionID.ToString()), FilePrefix + "_compiled*"))
            {
                try
                {
                    File.Delete(file);
                }
                catch (Exception ex)
                {
                    m_log.Error("[Compiler]: Exception trying delete old script file \"" + file + "\": " + ex.ToString());
                }
            }
            foreach (string file in Directory.GetFiles(Path.Combine(ScriptEnginesPath,
                    m_scriptEngine.World.RegionInfo.RegionID.ToString()), FilePrefix + "_source*"))
            {
                try
                {
                    File.Delete(file);
                }
                catch (Exception ex)
                {
                    m_log.Error("[Compiler]: Exception trying delete old script file \"" + file + "\": " + ex.ToString());
                }
            }
        }

        ////private ICodeCompiler icc = codeProvider.CreateCompiler();
        //public string CompileFromFile(string LSOFileName)
        //{
        //    switch (Path.GetExtension(LSOFileName).ToLower())
        //    {
        //        case ".txt":
        //        case ".lsl":
        //            Common.ScriptEngineBase.Shared.SendToDebug("Source code is LSL, converting to CS");
        //            return CompileFromLSLText(File.ReadAllText(LSOFileName));
        //        case ".cs":
        //            Common.ScriptEngineBase.Shared.SendToDebug("Source code is CS");
        //            return CompileFromCSText(File.ReadAllText(LSOFileName));
        //        default:
        //            throw new Exception("Unknown script type.");
        //    }
        //}

        public string GetCompilerOutput(string assetID)
        {
            return Path.Combine(ScriptEnginesPath, Path.Combine(
                    m_scriptEngine.World.RegionInfo.RegionID.ToString(),
                    FilePrefix + "_compiled_" + assetID + ".dll"));
        }

        public string GetCompilerOutput(UUID assetID)
        {
            return GetCompilerOutput(assetID.ToString());
        }

        public void PerformScriptCompile(
            string source, string asset, UUID ownerUUID,
            out string assembly, out Dictionary<KeyValuePair<int, int>, KeyValuePair<int, int>> linemap)
        {
            PerformScriptCompile(source, asset, ownerUUID, false, out assembly, out linemap);
        }

        public void PerformScriptCompile(
            string source, string asset, UUID ownerUUID, bool alwaysRecompile,
            out string assembly, out Dictionary<KeyValuePair<int, int>, KeyValuePair<int, int>> linemap)
        {
//            m_log.DebugFormat("[Compiler]: Checking script for asset {0} in {1}\n{2}", asset, m_scriptEngine.World.Name, source);

            IScriptModuleComms comms = m_scriptEngine.World.RequestModuleInterface<IScriptModuleComms>();

            linemap = null;
            m_warnings.Clear();

            assembly = GetCompilerOutput(asset);

//            m_log.DebugFormat("[Compiler]: Retrieved assembly {0} for asset {1} in {2}", assembly, asset, m_scriptEngine.World.Name);

            CheckOrCreateScriptsDirectory();

            // Don't recompile if we're not forced to and we already have it
            // Performing 3 file exists tests for every script can still be slow
            if (!alwaysRecompile && File.Exists(assembly) && File.Exists(assembly + ".text") && File.Exists(assembly + ".map"))
            {
//                m_log.DebugFormat("[Compiler]: Found existing assembly {0} for asset {1} in {2}", assembly, asset, m_scriptEngine.World.Name);

                // If we have already read this linemap file, then it will be in our dictionary.
                // Don't build another copy of the dictionary (saves memory) and certainly
                // don't keep reading the same file from disk multiple times.
                if (!m_lineMaps.ContainsKey(assembly))
                    m_lineMaps[assembly] = ReadMapFile(assembly + ".map");
                linemap = m_lineMaps[assembly];
                return;
            }

//            m_log.DebugFormat("[Compiler]: Compiling assembly {0} for asset {1} in {2}", assembly, asset, m_scriptEngine.World.Name);

            if (source == String.Empty)
                throw new Exception("Cannot find script assembly and no script text present");

            enumCompileType language = DefaultCompileLanguage;

            if (source.StartsWith("//c#", true, CultureInfo.InvariantCulture))
                language = enumCompileType.cs;
            if (source.StartsWith("//vb", true, CultureInfo.InvariantCulture))
            {
                language = enumCompileType.vb;
                // We need to remove //vb, it won't compile with that

                source = source.Substring(4, source.Length - 4);
            }
            if (source.StartsWith("//lsl", true, CultureInfo.InvariantCulture))
                language = enumCompileType.lsl;

//            m_log.DebugFormat("[Compiler]: Compile language is {0}", language);

            if (!AllowedCompilers.ContainsKey(language.ToString()))
            {
                // Not allowed to compile to this language!
                string errtext = String.Empty;
                errtext += "The compiler for language \"" + language.ToString() + "\" is not in list of allowed compilers. Script will not be executed!";
                throw new Exception(errtext);
            }

            if (m_scriptEngine.World.Permissions.CanCompileScript(ownerUUID, (int)language) == false)
            {
                // Not allowed to compile to this language!
                string errtext = String.Empty;
                errtext += ownerUUID + " is not in list of allowed users for this scripting language. Script will not be executed!";
                throw new Exception(errtext);
            }

            string compileScript = string.Empty;

            if (language == enumCompileType.lsl)
            {
                // Its LSL, convert it to C#

                StringBuilder sb = new StringBuilder(16394);

                LSL_Converter = (ICodeConverter)new CSCodeGenerator(comms, m_insertCoopTerminationCalls);
                AddCSScriptHeader(
                        m_scriptEngine.ScriptClassName,
                        m_scriptEngine.ScriptBaseClassName,
                        m_scriptEngine.ScriptBaseClassParameters,
                        sb);

                LSL_Converter.Convert(source,sb);
                AddCSScriptTail(sb);
                compileScript = sb.ToString();
                // copy converter warnings into our warnings.
                foreach (string warning in LSL_Converter.GetWarnings())
                {
                    AddWarning(warning);
                }

                linemap = ((CSCodeGenerator)LSL_Converter).PositionMap;
                // Write the linemap to a file and save it in our dictionary for next time.
                m_lineMaps[assembly] = linemap;
                WriteMapFile(assembly + ".map", linemap);
                LSL_Converter.Clear();
            }
            else
            {
                switch (language)
                {
                    case enumCompileType.cs:
                        compileScript = CreateCSCompilerScript(
                            compileScript,
                            m_scriptEngine.ScriptClassName,
                            m_scriptEngine.ScriptBaseClassName,
                            m_scriptEngine.ScriptBaseClassParameters);
                        break;
                    case enumCompileType.vb:
                        compileScript = CreateVBCompilerScript(
                            compileScript, m_scriptEngine.ScriptClassName, m_scriptEngine.ScriptBaseClassName);
                        break;
                }
            }

            assembly = CompileFromDotNetText(compileScript, language, asset, assembly);
        }

        public string[] GetWarnings()
        {
            return m_warnings.ToArray();
        }

        private void AddWarning(string warning)
        {
            if (!m_warnings.Contains(warning))
            {
                m_warnings.Add(warning);
            }
        }

//        private static string CreateJSCompilerScript(string compileScript)
//        {
//            compileScript = String.Empty +
//                "import OpenSim.Region.ScriptEngine.Shared; import System.Collections.Generic;\r\n" +
//                "package SecondLife {\r\n" +
//                "class Script extends OpenSim.Region.ScriptEngine.Shared.ScriptBase.ScriptBaseClass { \r\n" +
//                compileScript +
//                "} }\r\n";
//            return compileScript;
//        }

        public static void AddCSScriptHeader(string className, string baseClassName, ParameterInfo[] constructorParameters, StringBuilder sb)
        {
            sb.Append(string.Format(
@"using OpenSim.Region.ScriptEngine.Shared;
using System.Collections.Generic;

namespace SecondLife
{{
    public class {0} : {1}
    {{
        public {0}({2}) : base({3}) {{}}
",
                className,
                baseClassName,
                constructorParameters != null
                    ? string.Join(", ", Array.ConvertAll<ParameterInfo, string>(constructorParameters, pi => pi.ToString()))
                    : "",
                constructorParameters != null
                    ? string.Join(", ", Array.ConvertAll<ParameterInfo, string>(constructorParameters, pi => pi.Name))
                    : ""
               ));
        }

        public static void AddCSScriptTail(StringBuilder sb)
        {
            sb.Append(string.Format("    }}\n}}\n"));
        }

        public static string CreateCSCompilerScript(
            string compileScript, string className, string baseClassName, ParameterInfo[] constructorParameters)
        {
            compileScript = string.Format(
@"using OpenSim.Region.ScriptEngine.Shared;
using System.Collections.Generic;

namespace SecondLife
{{
    public class {0} : {1}
    {{
        public {0}({2}) : base({3}) {{}}
{4}
    }}
}}",
                className,
                baseClassName,
                constructorParameters != null
                    ? string.Join(", ", Array.ConvertAll<ParameterInfo, string>(constructorParameters, pi => pi.ToString()))
                    : "",
                constructorParameters != null
                    ? string.Join(", ", Array.ConvertAll<ParameterInfo, string>(constructorParameters, pi => pi.Name))
                    : "",
                compileScript);

            return compileScript;
        }

        public static string CreateVBCompilerScript(string compileScript, string className, string baseClassName)
        {
            compileScript = String.Empty +
                "Imports OpenSim.Region.ScriptEngine.Shared: Imports System.Collections.Generic: " +
                String.Empty + "NameSpace SecondLife:" +
                String.Empty + "Public Class " + className + ": Inherits " + baseClassName +
                "\r\nPublic Sub New()\r\nEnd Sub: " +
                compileScript +
                ":End Class :End Namespace\r\n";

            return compileScript;
        }

        /// <summary>
        /// Compile .NET script to .Net assembly (.dll)
        /// </summary>
        /// <param name="Script">CS script</param>
        /// <returns>Filename to .dll assembly</returns>
        internal string CompileFromDotNetText(string Script, enumCompileType lang, string asset, string assembly)
        {
//            m_log.DebugFormat("[Compiler]: Compiling to assembly\n{0}", Script);

            string ext = "." + lang.ToString();

            // Output assembly name
            scriptCompileCounter++;
            try
            {
                if (File.Exists(assembly))
                {
                    File.SetAttributes(assembly, FileAttributes.Normal);
                    File.Delete(assembly);
                }
            }
            catch (Exception e) // NOTLEGIT - Should be just FileIOException
            {
                throw new Exception("Unable to delete old existing " +
                        "script-file before writing new. Compile aborted: " +
                        e.ToString());
            }

            // DEBUG - write source to disk
            if (WriteScriptSourceToDebugFile)
            {
                string srcFileName = FilePrefix + "_source_" +
                        Path.GetFileNameWithoutExtension(assembly) + ext;
                try
                {
                    File.WriteAllText(Path.Combine(Path.Combine(
                        ScriptEnginesPath,
                        m_scriptEngine.World.RegionInfo.RegionID.ToString()),
                        srcFileName), Script);
                }
                catch (Exception ex) //NOTLEGIT - Should be just FileIOException
                {
                    m_log.Error("[Compiler]: Exception while " +
                                "trying to write script source to file \"" +
                                srcFileName + "\": " + ex.ToString());
                }
            }

            // Do actual compile
            CompilerParameters parameters = new CompilerParameters();

            string rootPath = AppDomain.CurrentDomain.BaseDirectory;

            parameters.ReferencedAssemblies.Add(Path.Combine(rootPath,
                    "OpenSim.Region.ScriptEngine.Shared.dll"));
            parameters.ReferencedAssemblies.Add(Path.Combine(rootPath,
                    "OpenSim.Region.ScriptEngine.Shared.Api.Runtime.dll"));
            parameters.ReferencedAssemblies.Add(Path.Combine(rootPath,
                    "OpenMetaverseTypes.dll"));

            if (m_scriptEngine.ScriptReferencedAssemblies != null)
                Array.ForEach<string>(
                    m_scriptEngine.ScriptReferencedAssemblies,
                    a => parameters.ReferencedAssemblies.Add(Path.Combine(rootPath, a)));

            parameters.GenerateExecutable = false;
            parameters.OutputAssembly = assembly;
            parameters.IncludeDebugInformation = CompileWithDebugInformation;
            //parameters.WarningLevel = 1; // Should be 4?
            parameters.TreatWarningsAsErrors = false;
            parameters.GenerateInMemory = false;

            CompilerResults results;

            CodeDomProvider provider;
            switch (lang)
            {
                case enumCompileType.vb:
//                    results = VBcodeProvider.CompileAssemblyFromSource(
//                            parameters, Script);
                    provider = CodeDomProvider.CreateProvider("VisualBasic");
                    break;
                case enumCompileType.cs:
                case enumCompileType.lsl:
                    provider = CodeDomProvider.CreateProvider("CSharp");
                    break;
                default:
                    throw new Exception("Compiler is not able to recongnize " +
                                        "language type \"" + lang.ToString() + "\"");
            }

            if(provider == null)
                    throw new Exception("Compiler failed to load ");


                    bool complete = false;
                    bool retried = false;

                    do
                    {
//                        lock (CScodeProvider)
//                        {
//                            results = CScodeProvider.CompileAssemblyFromSource(
//                                parameters, Script);
//                        }

                        results = provider.CompileAssemblyFromSource(
                                parameters, Script);
                        // Deal with an occasional segv in the compiler.
                        // Rarely, if ever, occurs twice in succession.
                        // Line # == 0 and no file name are indications that
                        // this is a native stack trace rather than a normal
                        // error log.
                        if (results.Errors.Count > 0)
                        {
                            if (!retried && string.IsNullOrEmpty(results.Errors[0].FileName) &&
                                results.Errors[0].Line == 0)
                            {
                                // System.Console.WriteLine("retrying failed compilation");
                                retried = true;
                            }
                            else
                            {
                                complete = true;
                            }
                        }
                        else
                        {
                            complete = true;
                        }
                    } while (!complete);
//                    break;
//                default:
//                    throw new Exception("Compiler is not able to recongnize " +
//                                        "language type \"" + lang.ToString() + "\"");
//            }

//            foreach (Type type in results.CompiledAssembly.GetTypes())
//            {
//                foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
//                {
//                    m_log.DebugFormat("[COMPILER]: {0}.{1}", type.FullName, method.Name);
//                }
//            }

            //
            // WARNINGS AND ERRORS
            //
            bool hadErrors = false;
            string errtext = String.Empty;
            if (results.Errors.Count > 0)
            {
                foreach (CompilerError CompErr in results.Errors)
                {
                    string severity = CompErr.IsWarning ? "Warning" : "Error";

                    KeyValuePair<int, int> errorPos;

                    // Show 5 errors max, but check entire list for errors

                    if (severity == "Error")
                    {
                        // C# scripts will not have a linemap since theres no line translation involved.
                        if (!m_lineMaps.ContainsKey(assembly))
                            errorPos = new KeyValuePair<int, int>(CompErr.Line, CompErr.Column);
                        else
                            errorPos = FindErrorPosition(CompErr.Line, CompErr.Column, m_lineMaps[assembly]);

                        string text = CompErr.ErrorText;

                        // Use LSL type names
                        if (lang == enumCompileType.lsl)
                            text = ReplaceTypes(CompErr.ErrorText);

                        // The Second Life viewer's script editor begins
                        // countingn lines and columns at 0, so we subtract 1.
                        errtext += String.Format("({0},{1}): {4} {2}: {3}\n",
                                errorPos.Key - 1, errorPos.Value - 1,
                                CompErr.ErrorNumber, text, severity);
                        hadErrors = true;
                    }
                }
            }

            provider.Dispose();

            if (hadErrors)
            {
                throw new Exception(errtext);
            }

            //  On today's highly asynchronous systems, the result of
            //  the compile may not be immediately apparent. Wait a
            //  reasonable amount of time before giving up on it.

            if (!File.Exists(assembly))
            {
                for (int i = 0; i < 20 && !File.Exists(assembly); i++)
                {
                    System.Threading.Thread.Sleep(250);
                }
                // One final chance...
                if (!File.Exists(assembly))
                {
                    errtext = String.Empty;
                    errtext += "No compile error. But not able to locate compiled file.";
                    throw new Exception(errtext);
                }
            }

            //            m_log.DebugFormat("[Compiler] Compiled new assembly "+
            //                    "for {0}", asset);

            // Because windows likes to perform exclusive locks, we simply
            // write out a textual representation of the file here
            //
            // Read the binary file into a buffer
            //
            FileInfo fi = new FileInfo(assembly);

            if (fi == null)
            {
                errtext = String.Empty;
                errtext += "No compile error. But not able to stat file.";
                throw new Exception(errtext);
            }

            Byte[] data = new Byte[fi.Length];

            try
            {
                using (FileStream fs = File.Open(assembly, FileMode.Open, FileAccess.Read))
                    fs.Read(data, 0, data.Length);
            }
            catch (Exception)
            {
                errtext = String.Empty;
                errtext += "No compile error. But not able to open file.";
                throw new Exception(errtext);
            }

            // Convert to base64
            //
            string filetext = System.Convert.ToBase64String(data);

            Byte[] buf = Encoding.ASCII.GetBytes(filetext);

            using (FileStream sfs = File.Create(assembly + ".text"))
                sfs.Write(buf, 0, buf.Length);

            return assembly;
        }

        private class kvpSorter : IComparer<KeyValuePair<KeyValuePair<int, int>, KeyValuePair<int, int>>>
        {
            public int Compare(KeyValuePair<KeyValuePair<int, int>, KeyValuePair<int, int>> a,
                               KeyValuePair<KeyValuePair<int, int>, KeyValuePair<int, int>> b)
            {
                int kc = a.Key.Key.CompareTo(b.Key.Key);
                return (kc != 0) ? kc : a.Key.Value.CompareTo(b.Key.Value);
            }
        }

        public static KeyValuePair<int, int> FindErrorPosition(int line,
                int col, Dictionary<KeyValuePair<int, int>,
                KeyValuePair<int, int>> positionMap)
        {
            if (positionMap == null || positionMap.Count == 0)
                return new KeyValuePair<int, int>(line, col);

            KeyValuePair<int, int> ret = new KeyValuePair<int, int>();

            if (positionMap.TryGetValue(new KeyValuePair<int, int>(line, col),
                    out ret))
                return ret;

            var sorted = new List<KeyValuePair<KeyValuePair<int, int>, KeyValuePair<int, int>>>(positionMap);

            sorted.Sort(new kvpSorter());

            int l = 1;
            int c = 1;
            int pl = 1;

            foreach (KeyValuePair<KeyValuePair<int, int>, KeyValuePair<int, int>> posmap in sorted)
            {
                //m_log.DebugFormat("[Compiler]: Scanning line map {0},{1} --> {2},{3}", posmap.Key.Key, posmap.Key.Value, posmap.Value.Key, posmap.Value.Value);
                int nl = posmap.Value.Key + line - posmap.Key.Key;      // New, translated LSL line and column.
                int nc = posmap.Value.Value + col - posmap.Key.Value;
                // Keep going until we find the first point passed line,col.
                if (posmap.Key.Key > line)
                {
                  //m_log.DebugFormat("[Compiler]: Line is larger than requested {0},{1}, returning {2},{3}", line, col, l, c);
                  if (pl < line)
                  {
                    //m_log.DebugFormat("[Compiler]: Previous line ({0}) is less than requested line ({1}), setting column to 1.", pl, line);
                    c = 1;
                  }
                  break;
                }
                if (posmap.Key.Key == line && posmap.Key.Value > col)
                {
                  // Never move l,c backwards.
                  if (nl > l || (nl == l && nc > c))
                  {
                    //m_log.DebugFormat("[Compiler]: Using offset relative to this: {0} + {1} - {2}, {3} + {4} - {5} = {6}, {7}",
                    //    posmap.Value.Key, line, posmap.Key.Key, posmap.Value.Value, col, posmap.Key.Value, nl, nc);
                    l = nl;
                    c = nc;
                  }
                  //m_log.DebugFormat("[Compiler]: Column is larger than requested {0},{1}, returning {2},{3}", line, col, l, c);
                  break;
                }
                pl = posmap.Key.Key;
                l = posmap.Value.Key;
                c = posmap.Value.Value;
            }
            return new KeyValuePair<int, int>(l, c);
        }

        string ReplaceTypes(string message)
        {
            message = message.Replace(
                    "OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLString",
                    "string");

            message = message.Replace(
                    "OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLInteger",
                    "integer");

            message = message.Replace(
                    "OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLFloat",
                    "float");

            message = message.Replace(
                    "OpenSim.Region.ScriptEngine.Shared.LSL_Types.list",
                    "list");

            return message;
        }

        private static void WriteMapFile(string filename, Dictionary<KeyValuePair<int, int>, KeyValuePair<int, int>> linemap)
        {
            StringBuilder mapbuilder = new StringBuilder(1024);

            foreach (KeyValuePair<KeyValuePair<int, int>, KeyValuePair<int, int>> kvp in linemap)
            {
                KeyValuePair<int, int> k = kvp.Key;
                KeyValuePair<int, int> v = kvp.Value;
                mapbuilder.Append(String.Format("{0},{1},{2},{3}\n", k.Key, k.Value, v.Key, v.Value));
            }

            Byte[] mapbytes = Encoding.ASCII.GetBytes(mapbuilder.ToString());

            using (FileStream mfs = File.Create(filename))
                mfs.Write(mapbytes, 0, mapbytes.Length);
        }

        private static Dictionary<KeyValuePair<int, int>, KeyValuePair<int, int>> ReadMapFile(string filename)
        {
            Dictionary<KeyValuePair<int, int>, KeyValuePair<int, int>> linemap;
            try
            {
                using (StreamReader r = File.OpenText(filename))
                {
                    linemap = new Dictionary<KeyValuePair<int, int>, KeyValuePair<int, int>>();

                    string line;
                    while ((line = r.ReadLine()) != null)
                    {
                        String[] parts = line.Split(new Char[] { ',' });
                        int kk = System.Convert.ToInt32(parts[0]);
                        int kv = System.Convert.ToInt32(parts[1]);
                        int vk = System.Convert.ToInt32(parts[2]);
                        int vv = System.Convert.ToInt32(parts[3]);

                        KeyValuePair<int, int> k = new KeyValuePair<int, int>(kk, kv);
                        KeyValuePair<int, int> v = new KeyValuePair<int, int>(vk, vv);

                        linemap[k] = v;
                    }
                }
            }
            catch
            {
                linemap = new Dictionary<KeyValuePair<int, int>, KeyValuePair<int, int>>();
            }

            return linemap;
        }
    }
}