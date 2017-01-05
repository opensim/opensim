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
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.CSharp;
using OpenSim.Region.ScriptEngine.Shared.CodeTools;
using System.CodeDom.Compiler;

namespace OpenSim.Tools.LSL.Compiler
{
    class Program
    {
//        Commented out because generated warning since m_positionMap could never be anything other than null
//        private static Dictionary<KeyValuePair<int, int>, KeyValuePair<int, int>> m_positionMap;
        private static CSharpCodeProvider CScodeProvider = new CSharpCodeProvider();

        static void Main(string[] args)
        {
             string source = null;

             if (args.Length == 0)
             {
                 Console.WriteLine("No input file specified");
                 Environment.Exit(1);
             }

             if (!File.Exists(args[0]))
             {
                 Console.WriteLine("Input file does not exist");
                 Environment.Exit(1);
             }

             try
             {
                 ICodeConverter cvt = (ICodeConverter) new CSCodeGenerator();
                 source = cvt.Convert(File.ReadAllText(args[0]));
             }
             catch(Exception e)
             {
                 Console.WriteLine("Conversion failed:\n"+e.Message);
                 Environment.Exit(1);
             }

             source = CreateCSCompilerScript(source);

             try
             {
                 Console.WriteLine(CompileFromDotNetText(source,"a.out"));
             }
             catch(Exception e)
             {
                 Console.WriteLine("Conversion failed: "+e.Message);
                 Environment.Exit(1);
             }

             Environment.Exit(0);
        }

        private static string CreateCSCompilerScript(string compileScript)
        {
            compileScript = String.Empty +
                "using OpenSim.Region.ScriptEngine.Shared; using System.Collections.Generic;\r\n" +
                String.Empty + "namespace SecondLife { " +
                String.Empty + "public class Script : OpenSim.Region.ScriptEngine.Shared.ScriptBase.ScriptBaseClass { \r\n" +
                @"public Script() { } " +
                compileScript +
                "} }\r\n";
            return compileScript;
        }

        private static string CompileFromDotNetText(string Script, string asset)
        {

            string OutFile = asset;
            string disp    ="OK";

            try
            {
                File.Delete(OutFile);
            }
            catch (Exception e) // NOTLEGIT - Should be just FileIOException
            {
                throw new Exception("Unable to delete old existing "+
                        "script-file before writing new. Compile aborted: " +
                        e.ToString());
            }

            // Do actual compile
            CompilerParameters parameters = new CompilerParameters();

            parameters.IncludeDebugInformation = true;

            string rootPath =
                Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory);

            parameters.ReferencedAssemblies.Add(Path.Combine(rootPath,
                    "OpenSim.Region.ScriptEngine.Shared.dll"));
            parameters.ReferencedAssemblies.Add(Path.Combine(rootPath,
                    "OpenSim.Region.ScriptEngine.Shared.Api.Runtime.dll"));

            parameters.GenerateExecutable = false;
            parameters.OutputAssembly = OutFile;
            parameters.IncludeDebugInformation = true;
            parameters.WarningLevel = 1;
            parameters.TreatWarningsAsErrors = false;

            CompilerResults results = CScodeProvider.CompileAssemblyFromSource(parameters, Script);

            if (results.Errors.Count > 0)
            {
                string errtext = String.Empty;
                foreach (CompilerError CompErr in results.Errors)
                {
                    string severity = CompErr.IsWarning ? "Warning" : "Error";

                    KeyValuePair<int, int> lslPos;

                    lslPos = FindErrorPosition(CompErr.Line, CompErr.Column);

                    string text = CompErr.ErrorText;

                    text = ReplaceTypes(CompErr.ErrorText);

                    // The Second Life viewer's script editor begins
                    // countingn lines and columns at 0, so we subtract 1.
                    errtext += String.Format("Line ({0},{1}): {4} {2}: {3}\n",
                                             lslPos.Key - 1, lslPos.Value - 1,
                                             CompErr.ErrorNumber, text, severity);
                }

                disp = "Completed with errors";

                if (!File.Exists(OutFile))
                {
                    throw new Exception(errtext);
                }
            }

            if (!File.Exists(OutFile))
            {
                string errtext = String.Empty;
                errtext += "No compile error. But not able to locate compiled file.";
                throw new Exception(errtext);
            }

            // Because windows likes to perform exclusive locks, we simply
            // write out a textual representation of the file here
            //
            // Read the binary file into a buffer
            //
            FileInfo fi = new FileInfo(OutFile);

            if (fi == null)
            {
                string errtext = String.Empty;
                errtext += "No compile error. But not able to stat file.";
                throw new Exception(errtext);
            }

            Byte[] data = new Byte[fi.Length];

            try
            {
                FileStream fs = File.Open(OutFile, FileMode.Open, FileAccess.Read);
                fs.Read(data, 0, data.Length);
                fs.Close();
            }
            catch (Exception)
            {
                string errtext = String.Empty;
                errtext += "No compile error. But not able to open file.";
                throw new Exception(errtext);
            }

            // Convert to base64
            //
            string filetext = System.Convert.ToBase64String(data);
            Byte[] buf = Encoding.ASCII.GetBytes(filetext);
            FileStream sfs = File.Create(OutFile + ".text");
            sfs.Write(buf, 0, buf.Length);
            sfs.Close();

            string posmap = String.Empty;
//            if (m_positionMap != null)
//            {
//                foreach (KeyValuePair<KeyValuePair<int, int>, KeyValuePair<int, int>> kvp in m_positionMap)
//                {
//                    KeyValuePair<int, int> k = kvp.Key;
//                    KeyValuePair<int, int> v = kvp.Value;
//                    posmap += String.Format("{0},{1},{2},{3}\n",
//                            k.Key, k.Value, v.Key, v.Value);
//                }
//            }

            buf = Encoding.ASCII.GetBytes(posmap);

            FileStream mfs = File.Create(OutFile + ".map");
            mfs.Write(buf, 0, buf.Length);
            mfs.Close();

            return disp;
        }

        private static string ReplaceTypes(string message)
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

        private static KeyValuePair<int, int> FindErrorPosition(int line, int col)
        {
            //return FindErrorPosition(line, col, m_positionMap);
            return FindErrorPosition(line, col, null);
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
    }
}
