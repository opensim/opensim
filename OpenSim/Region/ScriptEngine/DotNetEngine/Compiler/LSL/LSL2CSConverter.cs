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
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace OpenSim.Region.ScriptEngine.DotNetEngine.Compiler.LSL
{
    public class LSL2CSConverter
    {
        // Uses regex to convert LSL code to C# code.

        //private Regex rnw = new Regex(@"[a-zA-Z0-9_\-]", RegexOptions.Compiled);
        private Dictionary<string, string> dataTypes = new Dictionary<string, string>();
        private Dictionary<string, string> quotes = new Dictionary<string, string>();
        // c Style
        private Regex cstylecomments = new Regex(@"/\*(.|[\r\n])*?\*/", RegexOptions.Compiled | RegexOptions.Multiline);
        // c# one liners
        private Regex nonCommentFwsl = new Regex("\"[a-zA-Z0-9.,:/\\n ]+//[^\"+]+([\\\\\\\"+]+)?(\\s+)?[\"+](\\s+)?(;)?", RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.Singleline);
        private Regex conelinecomments = new Regex(@"[^:].?([\/]{2}[^\n]*)|([\n]{1,}[\/]{2}[^\n]*)", RegexOptions.Compiled | RegexOptions.Multiline);
        // ([^\"])((?:[a-zA-Z])\.[a-zA-Z].?)([^\"])

        // value we're looking for: (?:[a-zA-Z])\.[a-zA-Z]
        public LSL2CSConverter()
        {
            // Only the types we need to convert
            dataTypes.Add("void", "void");
            dataTypes.Add("integer", "LSL_Types.LSLInteger");
            dataTypes.Add("float", "LSL_Types.LSLFloat");
            dataTypes.Add("string", "LSL_Types.LSLString");
            dataTypes.Add("key", "LSL_Types.LSLString");
            dataTypes.Add("vector", "LSL_Types.Vector3");
            dataTypes.Add("rotation", "LSL_Types.Quaternion");
            dataTypes.Add("list", "LSL_Types.list");
            dataTypes.Add("null", "null");
            dataTypes.Add("Int32", "LSL_Types.LSLInteger");
            dataTypes.Add("int", "LSL_Types.LSLInteger");
        }

        public string Convert(string Script)
        {
            quotes.Clear();
            string Return = String.Empty;
            Script = " \r\n" + Script;

            //
            // Prepare script for processing
            //

            // Clean up linebreaks
            Script = Regex.Replace(Script, @"\r\n", "\n");
            Script = Regex.Replace(Script, @"\n", "\r\n");

            // QUOTE REPLACEMENT
            // temporarily replace quotes so we can work our magic on the script without
            //  always considering if we are inside our outside quotes's
            // TODO: Does this work on half-quotes in strings? ;)
            string _Script = String.Empty;
            string C;
            bool in_quote = false;
            bool quote_replaced = false;
            string quote_replacement_string = "Q_U_O_T_E_REPLACEMENT_";
            string quote = String.Empty;
            bool last_was_escape = false;
            int quote_replaced_count = 0;

            string removefwnoncomments = nonCommentFwsl.Replace(Script, "\"\";");

            string removecomments = conelinecomments.Replace(removefwnoncomments, "");
            removecomments = cstylecomments.Replace(removecomments, "");
            string[] localscript = removecomments.Split('"');
            string checkscript = String.Empty;
            bool flip = true;

            for (int p = 0; p < localscript.Length; p++)
            {
                //if (localscript[p].Length >= 1)
                //{
                    if (!localscript[p].EndsWith(@"\"))
                    {
                        flip = !flip;
                        //System.Console.WriteLine("Flip:" + flip.ToString() + " - " + localscript[p] + " ! " + localscript[p].EndsWith(@"\").ToString());
                    }
                //}
                //else
                //{
                //    flip = !flip;
                //    System.Console.WriteLine("Flip:" + flip.ToString() + " - " + localscript[p]);
                //}
                if (!flip)
                    checkscript += localscript[p];
            }

            //System.Console.WriteLine("SCRIPT:" + checkscript);

            // checks for alpha.alpha way of referring to objects in C#
            // ignores alpha.x alpha.y, alpha.z for refering to vector components
            Match SecurityM;


            SecurityM = Regex.Match(checkscript, @"([a-zA-Z])\.(?:[a-wA-Z]|[a-zA-Z][a-zA-Z])", RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.Singleline);
            if (SecurityM.Success)
               throw new Exception("CS0103: 'The . symbol cannot be used in LSL except in float values or vector components'.  Detected around: " + SecurityM.Captures[0].Value);

            SecurityM = Regex.Match(checkscript, @"typeof\s", RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.Singleline);
            if (SecurityM.Success)
                throw new Exception("CS0103: 'The object.typeof method isn't allowed in LSL'");

            SecurityM = Regex.Match(checkscript, @"GetType\(", RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.Singleline);
            if (SecurityM.Success)
                throw new Exception("CS0103: 'The object.GetType method isn't allowed in LSL'");

            for (int p = 0; p < Script.Length; p++)
            {
                C = Script.Substring(p, 1);
                while (true)
                {
                    // found " and last was not \ so this is not an escaped \"
                    if (C == "\"" && last_was_escape == false)
                    {
                        // Toggle inside/outside quote
                        in_quote = !in_quote;
                        if (in_quote)
                        {
                            quote_replaced_count++;
                        }
                        else
                        {
                            if (quote == String.Empty)
                            {
                                // We didn't replace quote, probably because of empty string?
                                _Script += quote_replacement_string +
                                           quote_replaced_count.ToString().PadLeft(5, "0".ToCharArray()[0]);
                            }
                            // We just left a quote
                            quotes.Add(
                                quote_replacement_string +
                                quote_replaced_count.ToString().PadLeft(5, "0".ToCharArray()[0]), quote);
                            quote = String.Empty;
                        }
                        break;
                    }

                    if (!in_quote)
                    {
                        // We are not inside a quote
                        quote_replaced = false;
                    }
                    else
                    {
                        // We are inside a quote
                        if (!quote_replaced)
                        {
                            // Replace quote
                            _Script += quote_replacement_string +
                                       quote_replaced_count.ToString().PadLeft(5, "0".ToCharArray()[0]);
                            quote_replaced = true;
                        }
                        quote += C;
                        break;
                    }
                    _Script += C;
                    break;
                }
                last_was_escape = false;
                if (C == @"\")
                {
                    last_was_escape = true;
                }
            }
            Script = _Script;
            //
            // END OF QUOTE REPLACEMENT
            //

            //
            // PROCESS STATES
            // Remove state definitions and add state names to start of each event within state
            //
            int ilevel = 0;
            int lastlevel = 0;
            string ret = String.Empty;
            string cache = String.Empty;
            bool in_state = false;
            string current_statename = String.Empty;
            for (int p = 0; p < Script.Length; p++)
            {
                C = Script.Substring(p, 1);
                while (true)
                {
                    // inc / dec level
                    if (C == @"{")
                        ilevel++;
                    if (C == @"}")
                        ilevel--;
                    if (ilevel < 0)
                        ilevel = 0;
                    cache += C;

                    // if level == 0, add to return
                    if (ilevel == 1 && lastlevel == 0)
                    {
                        // 0 => 1: Get last
                        Match m =
                          //Regex.Match(cache, @"(?![a-zA-Z_]+)\s*([a-zA-Z_]+)[^a-zA-Z_\(\)]*{",
                            Regex.Match(cache, @"(?![a-zA-Z_]+)\s*(state\s+)?(?<statename>[a-zA-Z_][a-zA-Z_0-9]*)[^a-zA-Z_0-9\(\)]*{",

                                        RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.Singleline);

                        in_state = false;
                        if (m.Success)
                        {
                            // Go back to level 0, this is not a state
                            in_state = true;
                            current_statename = m.Groups["statename"].Captures[0].Value;
                            //Console.WriteLine("Current statename: " + current_statename);
                            cache =
                                              //@"(?<s1>(?![a-zA-Z_]+)\s*)" + @"([a-zA-Z_]+)(?<s2>[^a-zA-Z_\(\)]*){",
                                Regex.Replace(cache,
                                              @"(?<s1>(?![a-zA-Z_]+)\s*)" + @"(state\s+)?([a-zA-Z_][a-zA-Z_0-9]*)(?<s2>[^a-zA-Z_0-9\(\)]*){",
                                              "${s1}${s2}",
                                              RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.Singleline | RegexOptions.IgnoreCase);
                        }
                        ret += cache;
                        cache = String.Empty;
                    }
                    if (ilevel == 0 && lastlevel == 1)
                    {
                        // 1 => 0: Remove last }
                        if (in_state == true)
                        {
                            cache = cache.Remove(cache.Length - 1, 1);
                            //cache = Regex.Replace(cache, "}$", String.Empty, RegexOptions.Multiline | RegexOptions.Singleline);

                            //Replace function names
                            // void dataserver(key query_id, string data) {
                            //cache = Regex.Replace(cache, @"([^a-zA-Z_]\s*)((?!if|switch|for)[a-zA-Z_]+\s*\([^\)]*\)[^{]*{)", "$1" + "<STATE>" + "$2", RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.Singleline);
                            //Console.WriteLine("Replacing using statename: " + current_statename);
                            cache =
                                Regex.Replace(cache,
                                              @"^(\s*)((?!(if|switch|for|while)[^a-zA-Z0-9_])[a-zA-Z0-9_]*\s*\([^\)]*\)[^;]*\{)",
                                              @"$1public " + current_statename + "_event_$2",
                                              RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.Singleline | RegexOptions.IgnoreCase);
                        }

                        ret += cache;
                        cache = String.Empty;
                        in_state = true;
                        current_statename = String.Empty;
                    }

                    break;
                }
                lastlevel = ilevel;
            }
            ret += cache;
            cache = String.Empty;

            Script = ret;
            ret = String.Empty;

            foreach (string key in dataTypes.Keys)
            {
                string val;
                dataTypes.TryGetValue(key, out val);

                // Replace CAST - (integer) with (int)
                Script =
                    Regex.Replace(Script, @"\(" + key + @"\)", @"(" + val + ")",
                                  RegexOptions.Compiled | RegexOptions.Multiline);
                // Replace return types and function variables - integer a() and f(integer a, integer a)
                Script =
                    Regex.Replace(Script, @"(^|;|}|[\(,])(\s*)" + key + @"(\s+)", @"$1$2" + val + "$3",
                                  RegexOptions.Compiled | RegexOptions.Multiline);
                Script =
                    Regex.Replace(Script, @"(^|;|}|[\(,])(\s*)" + key + @"(\s*)[,]", @"$1$2" + val + "$3,",
                                  RegexOptions.Compiled | RegexOptions.Multiline);
            }

            // Add "void" in front of functions that needs it
            Script =
                Regex.Replace(Script,
                              @"^(\s*public\s+)?((?!(if|switch|for)[^a-zA-Z0-9_])[a-zA-Z0-9_]*\s*\([^\)]*\)[^;]*\{)",
                              @"$1void $2", RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.Singleline);

            // Replace <x,y,z> and <x,y,z,r>
            Script =
                Regex.Replace(Script, @"<([^,>;]*,[^,>;]*,[^,>;]*,[^,>;]*)>", @"new LSL_Types.Quaternion($1)",
                              RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.Singleline);
            Script =
                Regex.Replace(Script, @"<([^,>;)]*,[^,>;]*,[^,>;]*)>", @"new LSL_Types.Vector3($1)",
                              RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.Singleline);

            // Replace List []'s
            Script =
                Regex.Replace(Script, @"\[([^\]]*)\]", @"new LSL_Types.list($1)",
                              RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.Singleline);

            // Replace (string) to .ToString() //
            Script =
                Regex.Replace(Script, @"\(string\)\s*([a-zA-Z0-9_.]+(\s*\([^\)]*\))?)", @"$1.ToString()",
                              RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.Singleline);
            Script =
                Regex.Replace(Script, @"\((float|int)\)\s*([a-zA-Z0-9_.]+(\s*\([^\)]*\))?)", @"$1.Parse($2)",
                              RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.Singleline);

            // Replace "state STATENAME" with "state("statename")"
            Script =
                    Regex.Replace(Script, @"(state)\s+([^;\n\r]+)(;[\r\n\s])", "$1(\"$2\")$3",
                  RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.Singleline | RegexOptions.IgnoreCase);

            // REPLACE BACK QUOTES
            foreach (string key in quotes.Keys)
            {
                string val;
                quotes.TryGetValue(key, out val);
                Script = Script.Replace(key, "\"" + val + "\"");
            }

            //System.Console.WriteLine(Script);
            Return = String.Empty;// +
                     //"using OpenSim.Region.ScriptEngine.Common; using System.Collections.Generic;";

            //Return += String.Empty +
            //          "namespace SecondLife { ";
            //Return += String.Empty +
            //          //"[Serializable] " +
            //          "public class Script : OpenSim.Region.ScriptEngine.Common.LSL_BaseClass { ";
            //Return += @"public Script() { } ";
            Return += Script;
            //Return += "} }\r\n";

            quotes.Clear();

            return Return;
        }
    }
}
